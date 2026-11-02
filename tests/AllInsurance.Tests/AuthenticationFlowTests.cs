using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using AllInsurance.Infrastructure;
using AllInsurance.Infrastructure.Persistence;
using AllInsurance.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AllInsurance.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task FirstRunSeedsRolesCreatesAdministratorAndAuthenticates()
    {
        await using var environment = new TestEnvironment();
        Assert.Equal(StartupState.AdministratorSetupRequired, environment.StartupState);

        var result = await environment.CreateAdministratorAsync();
        Assert.Equal(StartupState.LoginRequired, environment.Initializer.Initialize());
        Assert.Equal(39, result.RecoveryCode.Length);
        Assert.True(environment.RecoveryCodeGenerator.Verify(result.RecoveryCode, await environment.GetActiveDigestAsync(result.UserId)));

        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            Assert.Equal(2, await db.Roles.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await db.UserRoles.CountAsync());
            var recovery = await db.RecoveryCodes.SingleAsync();
            Assert.NotEqual(result.RecoveryCode, recovery.CodeDigest);
            Assert.Equal(64, recovery.CodeDigest.Length);
        }

        var signIn = await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        Assert.Equal(SignInStatus.Authenticated, signIn.Status);
        Assert.True(environment.Session.IsAuthenticated);
        Assert.Contains(Permission.ManageUsers, environment.Session.Current!.Permissions);
    }

    [Fact]
    public async Task InvalidUsernameAndPasswordReturnTheSameGenericFailure()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();

        var unknownUser = await environment.AuthenticationService.SignInAsync("missing.user", "wrong password");
        var wrongPassword = await environment.AuthenticationService.SignInAsync("first.admin", "wrong password");

        Assert.Equal(SignInStatus.Failed, unknownUser.Status);
        Assert.Equal(SignInStatus.Failed, wrongPassword.Status);
        Assert.Equal(unknownUser.Message, wrongPassword.Message);
        Assert.Equal(AuthenticationService.GenericSignInFailure, unknownUser.Message);
    }

    [Fact]
    public void SessionCombinesPermissionsAcrossAllAssignedRoles()
    {
        var session = new SessionService();

        session.EstablishSession(Guid.NewGuid(), "multi.role", "Multi Role", ["Employee", "Administrator"]);

        Assert.Contains("EMPLOYEE", session.Current!.Roles);
        Assert.Contains("ADMINISTRATOR", session.Current.Roles);
        Assert.Contains(Permission.ManageUsers, session.Current.Permissions);
        Assert.Contains(Permission.ReadSecurityEvents, session.Current.Permissions);
    }

    [Fact]
    public async Task FailedAttemptsPersistLockoutAndAllowSignInAfterExpiry()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await environment.AuthenticationService.SignInAsync("first.admin", "wrong password");
            Assert.Equal(SignInStatus.Failed, failed.Status);
        }

        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            var account = await db.Users.SingleAsync();
            Assert.Equal(0, account.FailedLoginCount);
            Assert.Equal(environment.Clock.UtcNow.AddMinutes(15), account.LockoutUntilUtc);
        }

        var locked = await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        Assert.Equal(SignInStatus.Failed, locked.Status);
        environment.Clock.Advance(TimeSpan.FromMinutes(16));
        var unlocked = await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        Assert.Equal(SignInStatus.Authenticated, unlocked.Status);
    }

    [Fact]
    public async Task LockoutThresholdAndDurationCanBeConfigured()
    {
        await using var environment = new TestEnvironment(new AuthenticationPolicyOptions
        {
            MaximumFailedAttempts = 2,
            LockoutDurationMinutes = 3
        });
        await environment.CreateAdministratorAsync();

        await environment.AuthenticationService.SignInAsync("first.admin", "wrong password");
        await environment.AuthenticationService.SignInAsync("first.admin", "wrong password");

        await using var db = await environment.ContextFactory.CreateDbContextAsync();
        Assert.Equal(environment.Clock.UtcNow.AddMinutes(3), (await db.Users.SingleAsync()).LockoutUntilUtc);
    }

    [Fact]
    public async Task DeactivatedUsersCannotSignIn()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var employeeId = await environment.UserAdministrationService.CreateUserAsync(
            "employee.one",
            "Employee One",
            "Temporary passphrase 2026!",
            ["Employee"]);
        await environment.UserAdministrationService.DeactivateUserAsync(employeeId);
        environment.Session.EndSession();

        var signIn = await environment.AuthenticationService.SignInAsync(
            "employee.one",
            "Temporary passphrase 2026!");

        Assert.Equal(SignInStatus.Failed, signIn.Status);
        Assert.Equal(AuthenticationService.GenericSignInFailure, signIn.Message);
    }

    [Fact]
    public async Task AdministrativeServiceRejectsAStaleSessionAfterAccountDeactivation()
    {
        await using var environment = new TestEnvironment();
        var administrator = await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");

        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Id == administrator.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            environment.UserAdministrationService.CreateUserAsync(
                "employee.one",
                "Employee One",
                "Temporary passphrase 2026!",
                ["Employee"]));
        Assert.False(environment.Session.IsAuthenticated);
    }

    [Fact]
    public async Task RecoveryRotatesCodeAtomicallyAndRejectsReuse()
    {
        await using var environment = new TestEnvironment();
        var setup = await environment.CreateAdministratorAsync();

        var invalid = await environment.RecoveryService.ResetPasswordAsync(
            "first.admin",
            "0000-0000-0000-0000-0000-0000-0000-0000",
            "A new secure passphrase 2026!",
            "A new secure passphrase 2026!");
        Assert.False(invalid.Succeeded);

        var recovered = await environment.RecoveryService.ResetPasswordAsync(
            "FIRST.ADMIN",
            setup.RecoveryCode,
            "A new secure passphrase 2026!",
            "A new secure passphrase 2026!");
        Assert.True(recovered.Succeeded);
        Assert.NotNull(recovered.ReplacementCode);
        Assert.False(environment.RecoveryCodeGenerator.Verify(
            setup.RecoveryCode,
            await environment.GetActiveDigestAsync(setup.UserId)));
        Assert.True(environment.RecoveryCodeGenerator.Verify(
            recovered.ReplacementCode,
            await environment.GetActiveDigestAsync(setup.UserId)));

        var reused = await environment.RecoveryService.ResetPasswordAsync(
            "first.admin",
            setup.RecoveryCode,
            "Another new passphrase 2026!",
            "Another new passphrase 2026!");
        Assert.False(reused.Succeeded);
        Assert.NotEqual(recovered.ReplacementCode, await environment.GetActiveCodeDigestPlaintextIsAbsentAsync(setup.UserId));

        var signIn = await environment.AuthenticationService.SignInAsync("first.admin", "A new secure passphrase 2026!");
        Assert.Equal(SignInStatus.Authenticated, signIn.Status);

        await using var db = await environment.ContextFactory.CreateDbContextAsync();
        var events = await db.SecurityEvents.OrderBy(entry => entry.CreatedAtUtc).ToListAsync();
        Assert.Contains(events, entry => entry.EventType == "RECOVERY_FAILURE");
        Assert.Contains(events, entry => entry.EventType == "PASSWORD_RESET" && entry.Outcome == "SUCCESS");
        Assert.All(events, entry => Assert.DoesNotContain(setup.RecoveryCode, entry.Details ?? string.Empty));
    }

    [Fact]
    public async Task ExistingDatabaseWithoutAdministratorRequiresRepairAndCannotReenterSetup()
    {
        await using var environment = new TestEnvironment();
        Assert.Equal(StartupState.AdministratorSetupRequired, environment.StartupState);
        Assert.Equal(StartupState.RepairRequired, environment.Initializer.Initialize());
        await Assert.ThrowsAsync<InvalidOperationException>(() => environment.CreateAdministratorAsync());

        await using var db = await environment.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Contains(await db.SecurityEvents.ToListAsync(), entry =>
            entry.EventType == "ADMINISTRATOR_REPAIR_REQUIRED");
    }

    [Fact]
    public async Task ExistingDatabaseWithPendingMigrationGetsASqliteBackup()
    {
        await using var environment = new TestEnvironment(precreateDatabase: true);

        Assert.Equal(StartupState.RepairRequired, environment.StartupState);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(environment.DatabasePath)!, "*.backup.db"));
    }

    [Fact]
    public async Task ConcurrentStartupIsSerializedAndRoleSeedsRemainIdempotent()
    {
        await using var environment = new TestEnvironment();

        var startupResults = await Task.WhenAll(
            Task.Run(() => environment.Initializer.Initialize()),
            Task.Run(() => environment.Initializer.Initialize()));

        Assert.All(startupResults, result => Assert.Equal(StartupState.RepairRequired, result));
        await using var db = await environment.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Single(await db.SecurityEvents
            .Where(entry => entry.EventType == "ADMINISTRATOR_REPAIR_REQUIRED")
            .ToListAsync());
    }

    [Fact]
    public async Task SqliteEnforcesActiveRecoveryCodeUniquenessAndForeignKeys()
    {
        await using var environment = new TestEnvironment();
        var administrator = await environment.CreateAdministratorAsync();

        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            db.RecoveryCodes.Add(new RecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = administrator.UserId,
                CodeDigest = new string('A', 64),
                IssuedAtUtc = environment.Clock.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = Guid.NewGuid(),
                RoleId = Guid.NewGuid(),
                AssignedAtUtc = environment.Clock.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task TemporaryAccountMustChangePasswordAndAcknowledgeRecoveryCodeBeforeWorkspace()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var employeeId = await environment.UserAdministrationService.CreateUserAsync(
            "employee.one",
            "Employee One",
            "Temporary passphrase 2026!",
            ["Employee"]);
        await environment.UserAdministrationService.ResetEmployeePasswordAsync(
            employeeId,
            "Reset temporary passphrase 2026!");

        environment.Session.EndSession();
        Assert.Equal(SignInStatus.Failed,
            (await environment.AuthenticationService.SignInAsync(
                "employee.one",
                "Temporary passphrase 2026!")).Status);
        var signIn = await environment.AuthenticationService.SignInAsync("employee.one", "Reset temporary passphrase 2026!");
        Assert.Equal(SignInStatus.PasswordChangeRequired, signIn.Status);
        Assert.True(environment.Session.RequiresPasswordChange);
        Assert.False(environment.Session.IsAuthenticated);

        var recoveryCode = await environment.AuthenticationService.CompleteRequiredPasswordChangeAsync(
            "Employee's new passphrase 2026!",
            "Employee's new passphrase 2026!");
        Assert.NotNull(recoveryCode);
        Assert.False(environment.Session.IsAuthenticated);
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            Assert.True((await db.Users.SingleAsync(user => user.Id == employeeId)).MustChangePassword);
        }

        await environment.AuthenticationService.AcknowledgeRequiredPasswordChangeAsync();
        Assert.True(environment.Session.IsAuthenticated);
        Assert.False(environment.Session.RequiresPasswordChange);
        Assert.DoesNotContain(Permission.ManageUsers, environment.Session.Current!.Permissions);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            environment.UserAdministrationService.CreateUserAsync(
                "employee.two",
                "Employee Two",
                "Temporary passphrase 2026!",
                ["Employee"]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            environment.UserAdministrationService.AssignRoleAsync(employeeId, "Administrator"));
        Assert.Contains("EMPLOYEE", environment.Session.Current!.Roles);
        Assert.DoesNotContain("ADMINISTRATOR", environment.Session.Current.Roles);

        environment.Session.EndSession();
        Assert.Equal(SignInStatus.Authenticated,
            (await environment.AuthenticationService.SignInAsync("employee.one", "Employee's new passphrase 2026!")).Status);
    }

    [Fact]
    public async Task LastAdministratorCannotBeDeactivatedOrDemoted()
    {
        await using var environment = new TestEnvironment();
        var administrator = await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            environment.UserAdministrationService.DeactivateUserAsync(administrator.UserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            environment.UserAdministrationService.RevokeRoleAsync(administrator.UserId, "Administrator"));

        await using var db = await environment.ContextFactory.CreateDbContextAsync();
        var account = await db.Users.SingleAsync();
        Assert.True(account.IsActive);
        Assert.Contains(await db.UserRoles.ToListAsync(), membership => membership.UserId == administrator.UserId);
    }
}

internal sealed class TestEnvironment : IAsyncDisposable
{
    private readonly string _directoryPath;

    public TestEnvironment(AuthenticationPolicyOptions? policy = null, bool precreateDatabase = false)
    {
        _directoryPath = Path.Combine(Path.GetTempPath(), "AllInsurance.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directoryPath);
        DatabasePath = Path.Combine(_directoryPath, "test.db");
        if (precreateDatabase)
        {
            using var connection = new SqliteConnection($"Data Source={DatabasePath}");
            connection.Open();
        }

        var options = new DbContextOptionsBuilder<AllInsuranceDbContext>()
            .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                ForeignKeys = true
            }.ToString())
            .Options;
        var rawFactory = new TestDbContextFactory(options);
        ContextFactory = new ApplicationDbContextFactory(rawFactory);
        Initializer = new DatabaseInitializer(rawFactory, DatabasePath);
        Clock = new TestClock();
        PasswordHasher = new IdentityPasswordHasher();
        RecoveryCodeGenerator = new CryptographicRecoveryCodeGenerator();
        Session = new SessionService();
        Policy = policy ?? new AuthenticationPolicyOptions();
        StartupState = Initializer.Initialize();
        SetupService = new AdministratorSetupService(ContextFactory, PasswordHasher, RecoveryCodeGenerator, Clock);
        AuthenticationService = new AuthenticationService(
            ContextFactory,
            PasswordHasher,
            RecoveryCodeGenerator,
            Clock,
            Session,
            Policy);
        RecoveryService = new RecoveryService(
            ContextFactory,
            PasswordHasher,
            RecoveryCodeGenerator,
            Clock,
            Policy);
        UserAdministrationService = new AllInsurance.Application.Users.UserAdministrationService(
            ContextFactory,
            PasswordHasher,
            Clock,
            Session);
    }

    public string DatabasePath { get; }
    public ApplicationDbContextFactory ContextFactory { get; }
    public DatabaseInitializer Initializer { get; }
    public TestClock Clock { get; }
    public IdentityPasswordHasher PasswordHasher { get; }
    public CryptographicRecoveryCodeGenerator RecoveryCodeGenerator { get; }
    public SessionService Session { get; }
    public AuthenticationPolicyOptions Policy { get; }
    public StartupState StartupState { get; }
    public AdministratorSetupService SetupService { get; }
    public AuthenticationService AuthenticationService { get; }
    public RecoveryService RecoveryService { get; }
    public AllInsurance.Application.Users.UserAdministrationService UserAdministrationService { get; }

    public Task<AdministratorSetupResult> CreateAdministratorAsync() =>
        SetupService.CreateAdministratorAsync(
            "first.admin",
            "First Administrator",
            "A strong passphrase 2026!",
            "A strong passphrase 2026!");

    public async Task<string> GetActiveDigestAsync(Guid userId)
    {
        await using var db = await ContextFactory.CreateDbContextAsync();
        return await db.RecoveryCodes
            .Where(code => code.UserId == userId && code.UsedAtUtc == null && code.RevokedAtUtc == null)
            .Select(code => code.CodeDigest)
            .SingleAsync();
    }

    public async Task<string> GetActiveCodeDigestPlaintextIsAbsentAsync(Guid userId)
    {
        await using var db = await ContextFactory.CreateDbContextAsync();
        var activeCode = await db.RecoveryCodes
            .SingleAsync(code => code.UserId == userId && code.UsedAtUtc == null && code.RevokedAtUtc == null);
        Assert.Equal(64, activeCode.CodeDigest.Length);
        return activeCode.CodeDigest;
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed class TestDbContextFactory(DbContextOptions<AllInsuranceDbContext> options)
    : IDbContextFactory<AllInsuranceDbContext>
{
    public AllInsuranceDbContext CreateDbContext() => new(options);

    public Task<AllInsuranceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
}

internal sealed class TestClock : IClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

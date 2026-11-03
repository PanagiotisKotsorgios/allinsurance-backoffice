using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Authentication;

public sealed class AuthenticationService(
    IApplicationDbContextFactory contextFactory,
    IPasswordHasher passwordHasher,
    IRecoveryCodeGenerator recoveryCodeGenerator,
    IClock clock,
    ISessionManager session,
    AuthenticationPolicyOptions policy) : IAuthenticationService
{
    public const string GenericSignInFailure = "Η σύνδεση απέτυχε. Ελέγξτε τα στοιχεία σας ή δοκιμάστε ξανά αργότερα.";

    public async Task<SignInResult> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim().ToUpperInvariant();
        var now = clock.UtcNow;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);

        if (user is null)
        {
            AddEvent(db, null, "LOGIN_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SignInResult(SignInStatus.Failed, GenericSignInFailure);
        }

        if (user.LockoutUntilUtc > now)
        {
            AddEvent(db, user.Id, "LOGIN_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SignInResult(SignInStatus.Failed, GenericSignInFailure);
        }

        var passwordMatches = passwordHasher.Verify(user.PasswordHash, password);
        if (!user.IsActive || !passwordMatches)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= policy.MaximumFailedAttempts)
            {
                user.LockoutUntilUtc = now.Add(policy.GetLockoutDuration());
                user.FailedLoginCount = 0;
                AddEvent(db, user.Id, "ACCOUNT_LOCKED", "FAILURE", now);
            }

            user.UpdatedAtUtc = now;
            AddEvent(db, user.Id, "LOGIN_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SignInResult(SignInStatus.Failed, GenericSignInFailure);
        }

        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        AddEvent(db, user.Id, "LOGIN_SUCCESS", "SUCCESS", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var roles = user.UserRoles.Select(membership => membership.Role.Name).ToArray();
        if (user.MustChangePassword)
        {
            session.BeginPasswordChange(user.Id);
            return new SignInResult(SignInStatus.PasswordChangeRequired, string.Empty);
        }

        session.EstablishSession(user.Id, user.Username, user.DisplayName, roles);
        return new SignInResult(SignInStatus.Authenticated, string.Empty);
    }

    public async Task<string?> CompleteRequiredPasswordChangeAsync(
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(newPassword, confirmation);
        var userId = session.PendingPasswordChangeUserId
            ?? throw new UnauthorizedAccessException("Δεν υπάρχει εκκρεμής αλλαγή κωδικού πρόσβασης.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.MustChangePassword)
        {
            session.EndSession();
            throw new UnauthorizedAccessException("Η αλλαγή κωδικού πρόσβασης δεν είναι πλέον έγκυρη.");
        }

        var now = clock.UtcNow;
        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.UpdatedAtUtc = now;

        var activeCodes = await db.RecoveryCodes
            .Where(code => code.UserId == user.Id && code.UsedAtUtc == null && code.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var activeCode in activeCodes)
        {
            activeCode.RevokedAtUtc = now;
        }

        var generatedCode = recoveryCodeGenerator.Create();
        db.RecoveryCodes.Add(new RecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeDigest = generatedCode.Digest,
            IssuedAtUtc = now
        });
        AddEvent(db, user.Id, "PASSWORD_CHANGED", "SUCCESS", now);
        AddEvent(db, user.Id, "RECOVERY_CODE_ISSUED", "SUCCESS", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return generatedCode.Code;
    }

    public async Task AcknowledgeRequiredPasswordChangeAsync(CancellationToken cancellationToken = default)
    {
        var userId = session.PendingPasswordChangeUserId
            ?? throw new UnauthorizedAccessException("Δεν υπάρχει εκκρεμής αλλαγή κωδικού πρόσβασης.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.MustChangePassword)
        {
            session.EndSession();
            throw new UnauthorizedAccessException("Η αλλαγή κωδικού πρόσβασης δεν είναι πλέον έγκυρη.");
        }

        user.MustChangePassword = false;
        user.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        session.CompletePasswordChange(
            user.Id,
            user.Username,
            user.DisplayName,
            user.UserRoles.Select(membership => membership.Role.Name));
    }

    public async Task<string> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        var current = session.Current
            ?? throw new UnauthorizedAccessException("Sign in is required.");
        ValidatePassword(newPassword, confirmation);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == current.UserId && candidate.IsActive,
            cancellationToken);
        if (user is null || !passwordHasher.Verify(user.PasswordHash, currentPassword))
        {
            throw new InvalidOperationException("Ο τρέχων κωδικός πρόσβασης είναι λανθασμένος.");
        }

        var now = clock.UtcNow;
        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.UpdatedAtUtc = now;
        AddEvent(db, user.Id, "PASSWORD_CHANGED", "SUCCESS", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await IssueOwnRecoveryCodeAsync(current.UserId, cancellationToken);
    }

    public async Task<string> IssueOwnRecoveryCodeAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var current = session.Current
            ?? throw new UnauthorizedAccessException("Sign in is required.");
        if (userId.HasValue && userId.Value != current.UserId)
        {
            throw new UnauthorizedAccessException("Recovery codes can only be issued for the signed-in user.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == current.UserId && candidate.IsActive,
            cancellationToken)
            ?? throw new UnauthorizedAccessException("The signed-in account is no longer active.");
        var now = clock.UtcNow;
        var activeCodes = await db.RecoveryCodes
            .Where(code => code.UserId == user.Id && code.UsedAtUtc == null && code.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var activeCode in activeCodes)
        {
            activeCode.RevokedAtUtc = now;
        }

        var generatedCode = recoveryCodeGenerator.Create();
        db.RecoveryCodes.Add(new RecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeDigest = generatedCode.Digest,
            IssuedAtUtc = now
        });
        AddEvent(db, user.Id, "RECOVERY_CODE_ISSUED", "SUCCESS", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return generatedCode.Code;
    }

    private static void AddEvent(
        IApplicationDbContext db,
        Guid? userId,
        string eventType,
        string outcome,
        DateTime createdAtUtc)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            Outcome = outcome,
            CreatedAtUtc = createdAtUtc
        });
    }

    private static void ValidatePassword(string password, string confirmation)
    {
        var policyError = PasswordPolicy.Validate(password);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError, nameof(password));
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new ArgumentException("Οι κωδικοί πρόσβασης δεν ταιριάζουν.", nameof(confirmation));
        }
    }
}

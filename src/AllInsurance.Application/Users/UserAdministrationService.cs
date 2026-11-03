using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Users;

public sealed class UserAdministrationService(
    IApplicationDbContextFactory contextFactory,
    IPasswordHasher passwordHasher,
    IClock clock,
    ISessionManager session) : IUserAdministrationService
{
    public async Task<IReadOnlyList<UserAccountSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.ManageUsers, cancellationToken);
        var users = await db.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(membership => membership.Role)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
        return users.Select(user => new UserAccountSummary(
            user.Id,
            user.Username,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword,
            user.UserRoles.Select(membership => membership.Role.Name).OrderBy(name => name).ToArray()))
            .ToArray();
    }

    public async Task<Guid> CreateUserAsync(
        string username,
        string displayName,
        string temporaryPassword,
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var cleanUsername = username.Trim();
        var cleanDisplayName = displayName.Trim();
        var normalizedUsername = cleanUsername.ToUpperInvariant();
        ValidateIdentity(cleanUsername, cleanDisplayName);
        ValidatePassword(temporaryPassword);
        var requestedRoles = roleNames
            .Select(name => name.Trim().ToUpperInvariant())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedRoles.Length == 0)
        {
            throw new ArgumentException("Πρέπει να οριστεί τουλάχιστον ένας ρόλος.", nameof(roleNames));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.ManageUsers, cancellationToken);
        if (await db.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Αυτό το όνομα χρήστη χρησιμοποιείται ήδη.");
        }

        var roles = await db.Roles
            .Where(role => requestedRoles.Contains(role.NormalizedName))
            .ToListAsync(cancellationToken);
        if (roles.Count != requestedRoles.Length)
        {
            throw new ArgumentException("Ένας ή περισσότεροι ρόλοι δεν υπάρχουν.", nameof(roleNames));
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = cleanUsername,
            NormalizedUsername = normalizedUsername,
            DisplayName = cleanDisplayName,
            PasswordHash = passwordHasher.Hash(temporaryPassword),
            MustChangePassword = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Users.Add(user);
        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAtUtc = now,
                AssignedByUserId = session.Current!.UserId
            });
        }

        AddEvent(db, user.Id, "USER_CREATED", "SUCCESS", now, $"actor_user_id={session.Current!.UserId:D}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user.Id;
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.ManageUsers, cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Ο χρήστης δεν υπάρχει.");
        if (!user.IsActive)
        {
            return;
        }

        if (HasAdministratorRole(user) && await CountOtherActiveAdministratorsAsync(db, user.Id, cancellationToken) == 0)
        {
            throw new InvalidOperationException("Ο τελευταίος ενεργός διαχειριστής δεν μπορεί να απενεργοποιηθεί.");
        }

        user.IsActive = false;
        user.UpdatedAtUtc = clock.UtcNow;
        AddEvent(
            db,
            user.Id,
            "USER_DEACTIVATED",
            "SUCCESS",
            clock.UtcNow,
            $"actor_user_id={session.Current!.UserId:D}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (session.Current?.UserId == user.Id)
        {
            session.EndSession();
        }
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedRoleName = roleName.Trim().ToUpperInvariant();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.AssignRoles, cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Ο χρήστης δεν υπάρχει.");
        var role = await db.Roles.SingleOrDefaultAsync(
            candidate => candidate.NormalizedName == normalizedRoleName,
            cancellationToken)
            ?? throw new InvalidOperationException("Ο ρόλος δεν υπάρχει.");

        if (user.UserRoles.Any(membership => membership.RoleId == role.Id))
        {
            return;
        }

        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAtUtc = clock.UtcNow,
            AssignedByUserId = session.Current!.UserId
        });
        AddEvent(
            db,
            user.Id,
            "ROLE_CHANGED",
            "SUCCESS",
            clock.UtcNow,
            $"actor_user_id={session.Current!.UserId:D}; role={role.NormalizedName}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RefreshCurrentSessionRolesAsync(user.Id, cancellationToken);
    }

    public async Task RevokeRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedRoleName = roleName.Trim().ToUpperInvariant();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.AssignRoles, cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Ο χρήστης δεν υπάρχει.");
        var membership = user.UserRoles.SingleOrDefault(candidate =>
            candidate.Role.NormalizedName == normalizedRoleName);
        if (membership is null)
        {
            return;
        }

        if (user.IsActive &&
            membership.Role.NormalizedName == "ADMINISTRATOR" &&
            await CountOtherActiveAdministratorsAsync(db, user.Id, cancellationToken) == 0)
        {
            throw new InvalidOperationException("Ο τελευταίος ενεργός διαχειριστής δεν μπορεί να χάσει τον ρόλο διαχειριστή.");
        }

        db.UserRoles.Remove(membership);
        AddEvent(
            db,
            user.Id,
            "ROLE_CHANGED",
            "SUCCESS",
            clock.UtcNow,
            $"actor_user_id={session.Current!.UserId:D}; role={membership.Role.NormalizedName}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RefreshCurrentSessionRolesAsync(user.Id, cancellationToken);
    }

    public async Task ResetEmployeePasswordAsync(
        Guid userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(temporaryPassword);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.ResetCredentials, cancellationToken);
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Ο χρήστης δεν υπάρχει.");
        if (HasAdministratorRole(user))
        {
            throw new InvalidOperationException("Ο κωδικός διαχειριστή δεν μπορεί να επαναφερθεί από την επαναφορά υπαλλήλου.");
        }

        var now = clock.UtcNow;
        user.PasswordHash = passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.UpdatedAtUtc = now;
        var codes = await db.RecoveryCodes
            .Where(code => code.UserId == user.Id && code.UsedAtUtc == null && code.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var code in codes)
        {
            code.RevokedAtUtc = now;
        }

        AddEvent(db, user.Id, "PASSWORD_RESET", "SUCCESS", now, $"actor_user_id={session.Current!.UserId:D}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityEventSummary>> GetSecurityEventsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Το πλήθος συμβάντων πρέπει να είναι από 1 έως 500.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsurePermissionAsync(db, Permission.ReadSecurityEvents, cancellationToken);
        return await db.SecurityEvents
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(limit)
            .Select(entry => new SecurityEventSummary(
                entry.CreatedAtUtc,
                entry.EventType,
                entry.Outcome,
                entry.User == null ? null : entry.User.Username,
                entry.Details))
            .ToListAsync(cancellationToken);
    }

    private async Task RefreshCurrentSessionRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (session.Current?.UserId != userId)
        {
            return;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var roleNames = await db.UserRoles
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.Role.Name)
            .ToListAsync(cancellationToken);
        session.RefreshRoles(userId, roleNames);
    }

    private async Task<int> CountOtherActiveAdministratorsAsync(
        IApplicationDbContext db,
        Guid excludedUserId,
        CancellationToken cancellationToken) =>
        await db.Users.CountAsync(
            user => user.Id != excludedUserId &&
                    user.IsActive &&
                    user.UserRoles.Any(membership => membership.Role.NormalizedName == "ADMINISTRATOR"),
            cancellationToken);

    private static bool HasAdministratorRole(User user) =>
        user.UserRoles.Any(membership => membership.Role.NormalizedName == "ADMINISTRATOR");

    private async Task EnsurePermissionAsync(
        IApplicationDbContext db,
        string permission,
        CancellationToken cancellationToken)
    {
        var current = session.Current
            ?? throw new UnauthorizedAccessException("Δεν έχετε δικαίωμα για αυτή την ενέργεια.");
        var user = await db.Users
            .Include(candidate => candidate.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == current.UserId && candidate.IsActive, cancellationToken);
        if (user is null)
        {
            session.EndSession();
            throw new UnauthorizedAccessException("Δεν έχετε δικαίωμα για αυτή την ενέργεια.");
        }

        session.RefreshRoles(user.Id, user.UserRoles.Select(membership => membership.Role.Name));
        if (session.Current is null || !session.Current.Permissions.Contains(permission))
        {
            throw new UnauthorizedAccessException("Δεν έχετε δικαίωμα για αυτή την ενέργεια.");
        }
    }

    private static void ValidateIdentity(string username, string displayName)
    {
        if (username.Length is < 3 or > 100)
        {
            throw new ArgumentException("Το όνομα χρήστη πρέπει να έχει από 3 έως 100 χαρακτήρες.", nameof(username));
        }

        if (displayName.Length is < 1 or > 200)
        {
            throw new ArgumentException("Το ονοματεπώνυμο πρέπει να έχει από 1 έως 200 χαρακτήρες.", nameof(displayName));
        }
    }

    private static void ValidatePassword(string password)
    {
        var policyError = PasswordPolicy.Validate(password);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError, nameof(password));
        }
    }

    private static void AddEvent(
        IApplicationDbContext db,
        Guid? userId,
        string eventType,
        string outcome,
        DateTime createdAtUtc,
        string? details = null)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            Outcome = outcome,
            CreatedAtUtc = createdAtUtc,
            Details = details
        });
    }
}

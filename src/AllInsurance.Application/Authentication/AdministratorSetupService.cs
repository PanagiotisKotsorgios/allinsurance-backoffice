using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Authentication;

public sealed class AdministratorSetupService(
    IApplicationDbContextFactory contextFactory,
    IPasswordHasher passwordHasher,
    IRecoveryCodeGenerator recoveryCodeGenerator,
    IClock clock) : IAdministratorSetupService
{
    private static readonly Guid AdministratorRoleId = Guid.Parse("a34f3189-90c9-4cec-9f7d-1a8cd4d19001");

    public async Task<AdministratorSetupResult> CreateAdministratorAsync(
        string username,
        string displayName,
        string password,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        var cleanUsername = username.Trim();
        var cleanDisplayName = displayName.Trim();
        var normalizedUsername = NormalizeUsername(cleanUsername);
        ValidateIdentity(cleanUsername, cleanDisplayName);
        ValidatePassword(password, confirmation);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var firstRunMarker = await db.AppMetadata
            .AnyAsync(entry => entry.Key == "first_run_pending" && entry.Value == "true", cancellationToken);
        if (!firstRunMarker)
        {
            throw new InvalidOperationException("Η δημιουργία διαχειριστή δεν είναι διαθέσιμη σε αυτή την εγκατάσταση.");
        }

        if (await db.Users.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Υπάρχουν ήδη λογαριασμοί χρηστών σε αυτή την εγκατάσταση.");
        }

        if (!await db.Roles.AnyAsync(role => role.Id == AdministratorRoleId, cancellationToken))
        {
            throw new InvalidOperationException("Λείπει ο ρόλος διαχειριστή. Απαιτείται επιδιόρθωση της βάσης δεδομένων.");
        }

        if (await db.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Αυτό το όνομα χρήστη χρησιμοποιείται ήδη.");
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = cleanUsername,
            NormalizedUsername = normalizedUsername,
            DisplayName = cleanDisplayName,
            PasswordHash = passwordHasher.Hash(password),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var generatedCode = recoveryCodeGenerator.Create();

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = AdministratorRoleId,
            AssignedAtUtc = now
        });
        db.RecoveryCodes.Add(new RecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeDigest = generatedCode.Digest,
            IssuedAtUtc = now
        });
        db.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EventType = "ADMIN_CREATED",
            Outcome = "SUCCESS",
            CreatedAtUtc = now
        });

        var marker = await db.AppMetadata.SingleAsync(entry => entry.Key == "first_run_pending", cancellationToken);
        marker.Value = "false";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdministratorSetupResult(user.Id, generatedCode.Code);
    }

    private static string NormalizeUsername(string username) => username.ToUpperInvariant();

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

using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Authentication;

public sealed class RecoveryService(
    IApplicationDbContextFactory contextFactory,
    IPasswordHasher passwordHasher,
    IRecoveryCodeGenerator recoveryCodeGenerator,
    IClock clock,
    AuthenticationPolicyOptions policy) : IRecoveryService
{
    private const string GenericFailure = "Password reset failed. Check your details or try again later.";

    public async Task<RecoveryResult> ResetPasswordAsync(
        string username,
        string recoveryCode,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(newPassword, confirmation);
        var normalizedUsername = username.Trim().ToUpperInvariant();
        var now = clock.UtcNow;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);

        if (user is null)
        {
            AddEvent(db, null, "RECOVERY_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RecoveryResult(false, GenericFailure, null);
        }

        if (!user.IsActive || user.LockoutUntilUtc > now)
        {
            AddEvent(db, user.Id, "RECOVERY_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RecoveryResult(false, GenericFailure, null);
        }

        var activeCodes = await db.RecoveryCodes
            .Where(code => code.UserId == user.Id && code.UsedAtUtc == null && code.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        var matchingCode = activeCodes.FirstOrDefault(code => recoveryCodeGenerator.Verify(recoveryCode, code.CodeDigest));
        if (matchingCode is null)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= policy.MaximumFailedAttempts)
            {
                user.LockoutUntilUtc = now.Add(policy.GetLockoutDuration());
                user.FailedLoginCount = 0;
                AddEvent(db, user.Id, "ACCOUNT_LOCKED", "FAILURE", now);
            }

            user.UpdatedAtUtc = now;
            AddEvent(db, user.Id, "RECOVERY_FAILURE", "FAILURE", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RecoveryResult(false, GenericFailure, null);
        }

        var generatedCode = recoveryCodeGenerator.Create();
        matchingCode.UsedAtUtc = now;
        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.UpdatedAtUtc = now;
        db.RecoveryCodes.Add(new RecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeDigest = generatedCode.Digest,
            IssuedAtUtc = now
        });
        AddEvent(db, user.Id, "PASSWORD_RESET", "SUCCESS", now);
        AddEvent(db, user.Id, "RECOVERY_CODE_ISSUED", "SUCCESS", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RecoveryResult(true, string.Empty, generatedCode.Code);
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
            throw new ArgumentException("The password entries do not match.", nameof(confirmation));
        }
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
}

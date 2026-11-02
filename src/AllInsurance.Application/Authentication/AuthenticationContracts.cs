namespace AllInsurance.Application.Authentication;

public enum SignInStatus
{
    Failed,
    Authenticated,
    PasswordChangeRequired
}

public sealed record SignInResult(SignInStatus Status, string Message);

public sealed record RecoveryResult(bool Succeeded, string Message, string? ReplacementCode);

public sealed record AdministratorSetupResult(Guid UserId, string RecoveryCode);

public sealed record SecurityEventSummary(
    DateTime CreatedAtUtc,
    string EventType,
    string Outcome,
    string? Username,
    string? Details);

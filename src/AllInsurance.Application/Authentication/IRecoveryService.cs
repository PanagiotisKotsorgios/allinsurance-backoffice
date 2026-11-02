namespace AllInsurance.Application.Authentication;

public interface IRecoveryService
{
    Task<RecoveryResult> ResetPasswordAsync(
        string username,
        string recoveryCode,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default);
}

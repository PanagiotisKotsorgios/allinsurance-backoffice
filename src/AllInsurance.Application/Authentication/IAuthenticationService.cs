namespace AllInsurance.Application.Authentication;

public interface IAuthenticationService
{
    Task<SignInResult> SignInAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<string?> CompleteRequiredPasswordChangeAsync(
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default);
    Task AcknowledgeRequiredPasswordChangeAsync(CancellationToken cancellationToken = default);
    Task<string> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default);
    Task<string> IssueOwnRecoveryCodeAsync(Guid? userId = null, CancellationToken cancellationToken = default);
}

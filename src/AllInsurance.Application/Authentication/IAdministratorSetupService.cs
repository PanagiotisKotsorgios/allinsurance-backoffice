namespace AllInsurance.Application.Authentication;

public interface IAdministratorSetupService
{
    Task<AdministratorSetupResult> CreateAdministratorAsync(
        string username,
        string displayName,
        string password,
        string confirmation,
        CancellationToken cancellationToken = default);
}

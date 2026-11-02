namespace AllInsurance.Application.Abstractions;

public enum StartupState
{
    AdministratorSetupRequired,
    LoginRequired,
    RepairRequired
}

public interface IStartupInitializer
{
    StartupState Initialize(CancellationToken cancellationToken = default);
}

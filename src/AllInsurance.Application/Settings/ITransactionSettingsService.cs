namespace AllInsurance.Application.Settings;

public interface ITransactionSettingsService
{
    Task<string> GetCurrencyAsync(CancellationToken cancellationToken = default);
    Task SetCurrencyAsync(string currency, CancellationToken cancellationToken = default);
}

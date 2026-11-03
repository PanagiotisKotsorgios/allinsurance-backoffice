using AllInsurance.Domain.Entities;
using AllInsurance.Application.Settings;

namespace AllInsurance.Application.Customers;

public sealed record CustomerListItem(
    Guid Id, string FullName, string? TaxNumber, string? PrimaryMobile,
    string? PrimaryEmail, bool IsActive, decimal Balance, DateTime CreatedAtUtc,
    string? GroupName, string? PartnerName, bool BelongsToOffice,
    string LastName, string FirstName, string? CompanyName, string CustomerKind,
    string? City)
{
    public string Code => "C-" + Id.ToString("N")[..8].ToUpperInvariant();
    public string StatusLabel => IsActive ? "Ενεργός" : "Ανενεργός";
    public string Currency { get; init; } = "EUR";
    public string BalanceDisplay => $"{Balance:N2} {TransactionSettingsService.Symbol(Currency)}";
    public string CreatedDisplay => CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy");
    public string SearchText { get; init; } = "";
}

public sealed record CustomerStatistics(
    int Total, int Active, int Inactive, int Debtors, decimal TotalReceivable,
    decimal TotalPayable, int Referrals, int Drivers);

public sealed record CustomerRelatedRecord(
    Guid CustomerId, string CustomerName, string Category, string Title,
    string? Value, bool IsHistorical, DateTime CreatedAtUtc);

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItem>> SearchAsync(string? query = null, bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> SaveAsync(Customer customer, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDetail> AddDetailAsync(Guid customerId, string category, string title, string? value,
        string? notes, DateTime? effectiveFrom, DateTime? effectiveUntil, bool isHistorical,
        CancellationToken cancellationToken = default);
    Task UpdateDetailAsync(Guid customerId, Guid detailId, string title, string? value,
        string? notes, DateTime? effectiveFrom, DateTime? effectiveUntil, bool isHistorical,
        CancellationToken cancellationToken = default);
    Task DeleteDetailAsync(Guid customerId, Guid detailId, CancellationToken cancellationToken = default);
    Task<CustomerDetail> AddFileAsync(Guid customerId, string sourcePath, CancellationToken cancellationToken = default);
    string GetFilePath(CustomerDetail file);
    Task<CustomerTransaction> AddTransactionAsync(Guid customerId, string type, decimal amount, string? description,
        string? paymentMethod = null, string? reference = null,
        CancellationToken cancellationToken = default);
    Task<CustomerStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerRelatedRecord>> GetRelatedRecordsAsync(string category,
        string? query = null, CancellationToken cancellationToken = default);
}

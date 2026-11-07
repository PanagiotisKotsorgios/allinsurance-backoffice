using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Filters;

public interface IFilterOptionService
{
    Task<IReadOnlyList<FilterOption>> ListAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ChoicesAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ScopedChoicesAsync(string category, string? company, string? branch,
        CancellationToken cancellationToken = default);
    Task SaveAsync(Guid? id, string category, string value, CancellationToken cancellationToken = default);
    Task SaveScopedAsync(Guid? id, string category, string value, string? company, string? branch,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ImportAsync(string category, IEnumerable<string> values, CancellationToken cancellationToken = default);
    Task<int> ImportScopedAsync(string category, IEnumerable<string> values, string? company,
        string? branch, CancellationToken cancellationToken = default);
}

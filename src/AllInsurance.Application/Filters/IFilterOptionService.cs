using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Filters;

public interface IFilterOptionService
{
    Task<IReadOnlyList<FilterOption>> ListAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ChoicesAsync(string category, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid? id, string category, string value, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ImportAsync(string category, IEnumerable<string> values, CancellationToken cancellationToken = default);
}

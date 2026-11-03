using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Business;

public interface IBusinessRecordService
{
    Task<IReadOnlyList<BusinessRecord>> SearchAsync(string sectionKey, string? query = null,
        CancellationToken cancellationToken = default);
    Task<BusinessRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> SaveAsync(BusinessRecord record, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

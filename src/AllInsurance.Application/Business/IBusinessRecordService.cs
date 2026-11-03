using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Business;

public interface IBusinessRecordService
{
    Task<IReadOnlyList<BusinessRecord>> SearchAsync(string sectionKey, string? query = null,
        CancellationToken cancellationToken = default);
    Task<BusinessRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessRecordActivity>> GetActivitiesAsync(Guid id,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessRecordAttachment>> GetAttachmentsAsync(Guid id,
        CancellationToken cancellationToken = default);
    Task<BusinessRecordAttachment> AddAttachmentAsync(Guid id, string sourcePath,
        CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(Guid id, Guid attachmentId,
        CancellationToken cancellationToken = default);
    string GetAttachmentPath(BusinessRecordAttachment attachment);
    Task<Guid> SaveAsync(BusinessRecord record, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

namespace AllInsurance.Domain.Entities;

public sealed class BusinessRecordAttachment
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public BusinessRecord Record { get; set; } = null!;
    public required string FileName { get; set; }
    public required string RelativePath { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public required string UploadedBy { get; set; }
}

namespace AllInsurance.Domain.Entities;

public sealed class BusinessRecord
{
    public Guid Id { get; set; }
    public required string SectionKey { get; set; }
    public required string Title { get; set; }
    public string? Reference { get; set; }
    public string? RelatedParty { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? DocumentType { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime? EventDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

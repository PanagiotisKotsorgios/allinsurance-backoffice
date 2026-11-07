namespace AllInsurance.Domain.Entities;

public sealed class OfficeOperation
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "expense";
    public DateTime OccurredOn { get; set; } = DateTime.Today;
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? CompanyId { get; set; }
    public decimal? GrossPremium { get; set; }
    public decimal? NetPremium { get; set; }
    public decimal? OfficeCommission { get; set; }
    public decimal? PartnerCommission { get; set; }
    public decimal? OverCommission { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

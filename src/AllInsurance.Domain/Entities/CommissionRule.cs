namespace AllInsurance.Domain.Entities;

public sealed class CommissionRule
{
    public Guid Id { get; set; }
    public string Insurer { get; set; } = "";
    public string? Branch { get; set; }
    public string? VehicleUse { get; set; }
    public string? Coverage { get; set; }
    public string? Package { get; set; }
    public string? PartnerCategory { get; set; }
    public Guid? PartnerId { get; set; }
    public decimal OfficePercent { get; set; }
    public decimal PartnerPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

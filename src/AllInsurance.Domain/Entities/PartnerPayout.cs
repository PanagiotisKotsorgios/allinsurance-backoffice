namespace AllInsurance.Domain.Entities;

public sealed class PartnerPayout
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public BusinessRecord Partner { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string PaymentMethod { get; set; } = "Τραπεζική μεταφορά";
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

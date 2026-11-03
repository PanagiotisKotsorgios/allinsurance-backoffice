namespace AllInsurance.Domain.Entities;

public sealed class CustomerDetail
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Category { get; set; }
    public required string Title { get; set; }
    public string? Value { get; set; }
    public string? Notes { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public bool IsHistorical { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

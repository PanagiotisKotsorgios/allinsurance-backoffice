namespace AllInsurance.Domain.Entities;

public sealed class CustomerTransaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Type { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

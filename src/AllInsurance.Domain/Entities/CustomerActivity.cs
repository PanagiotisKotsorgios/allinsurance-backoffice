namespace AllInsurance.Domain.Entities;

public sealed class CustomerActivity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Action { get; set; }
    public required string Description { get; set; }
    public required string ActorName { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

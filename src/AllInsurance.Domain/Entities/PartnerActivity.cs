namespace AllInsurance.Domain.Entities;

public sealed class PartnerActivity
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public BusinessRecord Partner { get; set; } = null!;
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}

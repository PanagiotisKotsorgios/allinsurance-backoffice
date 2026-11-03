namespace AllInsurance.Domain.Entities;

public sealed class BusinessRecordActivity
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public BusinessRecord Record { get; set; } = null!;
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}

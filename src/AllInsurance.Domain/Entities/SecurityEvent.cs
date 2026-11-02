namespace AllInsurance.Domain.Entities;

public sealed class SecurityEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string EventType { get; set; }
    public required string Outcome { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Details { get; set; }
    public User? User { get; set; }
}

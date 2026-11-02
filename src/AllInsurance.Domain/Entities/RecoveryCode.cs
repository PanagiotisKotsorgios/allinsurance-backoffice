namespace AllInsurance.Domain.Entities;

public sealed class RecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string CodeDigest { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public User User { get; set; } = null!;
}

namespace AllInsurance.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; } = new List<UserRole>();
}

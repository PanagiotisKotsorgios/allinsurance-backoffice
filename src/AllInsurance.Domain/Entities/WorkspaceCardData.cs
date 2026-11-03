using System.Text.Json;

namespace AllInsurance.Domain.Entities;

/// <summary>Details and stable links for the workspace sections that share the record card.</summary>
public sealed class WorkspaceCardData
{
    public Guid? CompanyId { get; set; }
    public Guid? AgencyId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? ClaimId { get; set; }
    public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.Ordinal);

    public static WorkspaceCardData Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new WorkspaceCardData();
        try
        {
            var data = JsonSerializer.Deserialize<WorkspaceCardData>(json) ?? new WorkspaceCardData();
            data.Fields ??= new(StringComparer.Ordinal);
            return data;
        }
        catch (JsonException) { return new WorkspaceCardData(); }
    }

    public string Write() => JsonSerializer.Serialize(this);
}

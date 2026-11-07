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
    public List<OrganizationContact> Contacts { get; set; } = [];
    public List<OrganizationSettlement> Settlements { get; set; } = [];

    public static WorkspaceCardData Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new WorkspaceCardData();
        try
        {
            var data = JsonSerializer.Deserialize<WorkspaceCardData>(json) ?? new WorkspaceCardData();
            data.Fields ??= new(StringComparer.Ordinal);
            data.Contacts ??= [];
            data.Settlements ??= [];
            return data;
        }
        catch (JsonException) { return new WorkspaceCardData(); }
    }

    public string Write() => JsonSerializer.Serialize(this);
}

public sealed class OrganizationContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string Email { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Phone { get; set; } = "";
}

public sealed class OrganizationSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string Currency { get; set; } = "EUR";
}

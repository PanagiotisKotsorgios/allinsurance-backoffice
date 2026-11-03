using System.Text.Json;

namespace AllInsurance.Domain.Entities;

public sealed class PolicyCardData
{
    public string? ApplicationNumber { get; set; }
    public string? NoticeNumber { get; set; }
    public string? PolicyNumber { get; set; }
    public string? Insurer { get; set; }
    public string? Agency { get; set; }
    public string? Branch { get; set; }
    public string? Product { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? GrossPremium { get; set; }
    public decimal? NetPremium { get; set; }
    public string? CoverageNotes { get; set; }
    public Guid? PartnerId { get; set; }
    public decimal? PartnerCommissionPercent { get; set; }
}

public sealed class VehicleCardData
{
    public string? Registration { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Vin { get; set; }
    public int? ManufactureYear { get; set; }
    public DateTime? FirstRegistrationDate { get; set; }
    public string? Use { get; set; }
    public string? Fuel { get; set; }
    public int? DisplacementCc { get; set; }
    public decimal? PowerKw { get; set; }
    public DateTime? AcquisitionDate { get; set; }
    public string? Color { get; set; }
    public string? Notes { get; set; }
}

public static class BusinessRecordDetails
{
    public static T Read<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try { return JsonSerializer.Deserialize<T>(json) ?? new T(); }
        catch (JsonException) { return new T(); }
    }

    public static string Write<T>(T details) => JsonSerializer.Serialize(details);
}

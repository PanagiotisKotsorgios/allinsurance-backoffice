using System.Text.Json;

namespace AllInsurance.Domain.Entities;

public sealed class PolicyCardData
{
    public string? ApplicationNumber { get; set; }
    public string? NoticeNumber { get; set; }
    public string? PolicyNumber { get; set; }
    public string? Insurer { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Agency { get; set; }
    public Guid? AgencyId { get; set; }
    public string? Branch { get; set; }
    public string? Product { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? GrossPremium { get; set; }
    public decimal? NetPremium { get; set; }
    public string? CoverageNotes { get; set; }
    public Guid? PartnerId { get; set; }
    public decimal? PartnerCommissionPercent { get; set; }
    public decimal? OfficeCommissionPercent { get; set; }
    public Guid? CommissionRuleId { get; set; }
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
    public string? TaxClass { get; set; }
    public decimal? HorsePower { get; set; }
    public DateTime? TransferDate { get; set; }
    public int? Seats { get; set; }
    public string? FuelColor { get; set; }
    public string? RegistrationPermit { get; set; }
    public string? SpecialLicence { get; set; }
    public string? DiscountDocuments { get; set; }
    public decimal? RoadTax { get; set; }
    public string? ReportForms { get; set; }
    public bool IsConvertible { get; set; }
    public int? Doors { get; set; }
    public bool HasSunroof { get; set; }
    public bool OverThreeAndHalfTons { get; set; }
    public bool HasRearDualWheels { get; set; }
    public decimal? Tonnage { get; set; }
    public decimal? WheelbaseCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? TrackWidthCm { get; set; }
    public bool HasAlarm { get; set; }
    public bool HasImmobilizer { get; set; }
    public decimal? EstimatedMarketValue { get; set; }
    public bool IsGuarded { get; set; }
    public bool HasEmissionsCard { get; set; }
    public decimal? Co2GramsPerKm { get; set; }
    public string? KteoImagePath { get; set; }
    public string? KteoStatus { get; set; }
    public DateTime? NextKteoDate { get; set; }
    public List<VehiclePerson> People { get; set; } = [];
}

public sealed class VehiclePerson
{
    public Guid CustomerId { get; set; }
    public string Role { get; set; } = "Ιδιοκτήτης";
    public decimal OwnershipPercent { get; set; }
    public bool IsPrimaryOwner { get; set; }
}

public sealed class ClaimCardData
{
    public Guid? VehicleId { get; set; }
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

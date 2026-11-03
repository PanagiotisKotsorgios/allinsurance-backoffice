using System.Text.Json;

namespace AllInsurance.Domain.Entities;

public sealed class PartnerProfileData
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? PersonType { get; set; }
    public string? BusinessType { get; set; }
    public string? ProfessionalCapacity { get; set; }
    public string? IdentityNumber { get; set; }
    public string? CooperationManager { get; set; }
    public bool IsPotential { get; set; }
    public bool IsSuspended { get; set; }
    public bool HasEnded { get; set; }
    public bool HasActiveContract { get; set; }
    public string? Category { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? FatherName { get; set; }
    public string? MotherName { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? PassportNumber { get; set; }
    public string? Education { get; set; }
    public string? RegistryNumber { get; set; }
    public DateTime? RegistryExpiry { get; set; }
    public string? Chamber { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? SecondaryPhone { get; set; }
    public string? Mobile { get; set; }
    public string? Website { get; set; }
    public string? WorkAddress { get; set; }
    public string? WorkCity { get; set; }
    public string? WorkPostalCode { get; set; }
    public string? WorkPhone { get; set; }
    public string? WorkEmail { get; set; }
    public string? ContractNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? DefaultCommissionPercent { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }

    public static PartnerProfileData Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PartnerProfileData();
        try { return JsonSerializer.Deserialize<PartnerProfileData>(json) ?? new PartnerProfileData(); }
        catch (JsonException) { return new PartnerProfileData(); }
    }

    public string Write() => JsonSerializer.Serialize(this);
}

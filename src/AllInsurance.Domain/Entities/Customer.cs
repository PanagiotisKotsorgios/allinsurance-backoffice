namespace AllInsurance.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public required string LastName { get; set; }
    public required string FirstName { get; set; }
    public string? CompanyName { get; set; }
    public string CustomerKind { get; set; } = "Φυσικό πρόσωπο";
    public string? PrimaryAddress { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? PrimaryMobile { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? Occupation { get; set; }
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? BirthPlace { get; set; }
    public string? Nationality { get; set; }
    public string? Gender { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? FatherName { get; set; }
    public string? MotherName { get; set; }
    public string? MaritalStatus { get; set; }
    public string? SpouseName { get; set; }
    public string? HomePhone { get; set; }
    public string? SecondMobile { get; set; }
    public string? InsuranceRegistry { get; set; }
    public string? LegalForm { get; set; }
    public string? BusinessActivityCode { get; set; }
    public string? Employer { get; set; }
    public string? Source { get; set; }
    public string? Tags { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PassportNumber { get; set; }
    public string? Zone { get; set; }
    public bool HasDrivingLicense { get; set; }
    public bool IsNewDriver { get; set; }
    public DateTime? NameDay { get; set; }
    public bool HasPersonalInsurance { get; set; }
    public string? GroupName { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsReferral { get; set; }
    public Guid? ReferredByCustomerId { get; set; }
    public string? ReferredByPartnerName { get; set; }
    public Customer? ReferredByCustomer { get; set; }
    public string? PartnerName { get; set; }
    public bool BelongsToOffice { get; set; } = true;
    public bool IsDriver { get; set; }
    public DateTime? LicenseStartDate { get; set; }
    public DateTime? LicenseEndDate { get; set; }
    public string? LicenseCategory { get; set; }
    public string? LicenseNumber { get; set; }
    public string? HLDiploma { get; set; }
    public bool HasDiscountDocument { get; set; }
    public string? DiscountDocumentDescription { get; set; }
    public string? IdentityType { get; set; }
    public string? IdentityNumber { get; set; }
    public string? SecondPhone { get; set; }
    public string? ThirdPhone { get; set; }
    public string? SecondEmail { get; set; }
    public string? ThirdEmail { get; set; }
    public string? BusinessEmail { get; set; }
    public string? BusinessPhone { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public ICollection<CustomerDetail> Details { get; } = new List<CustomerDetail>();
    public ICollection<CustomerTransaction> Transactions { get; } = new List<CustomerTransaction>();
    public ICollection<BusinessRecord> BusinessRecords { get; } = new List<BusinessRecord>();
    public ICollection<CustomerActivity> Activities { get; } = new List<CustomerActivity>();
}

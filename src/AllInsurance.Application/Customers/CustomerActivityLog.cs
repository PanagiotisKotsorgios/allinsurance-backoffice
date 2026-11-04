using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Customers;

internal static class CustomerActivityLog
{
    public static string CategoryLabel(string category) => category switch
    {
        "vehicles" => "οχήματα", "policies" => "ασφαλιστήρια",
        "claims" => "ζημιές", "boats" => "σκάφη", "phones" => "τηλέφωνα",
        "addresses" => "διευθύνσεις", "emails" => "email", "persons" => "πρόσωπα",
        "files" => "αρχεία", _ => "άλλα στοιχεία"
    };

    public static string FieldLabel(string name) => name switch
    {
        nameof(Customer.LastName) => "επώνυμο",
        nameof(Customer.FirstName) => "όνομα",
        nameof(Customer.TaxNumber) => "ΑΦΜ",
        nameof(Customer.City) => "πόλη",
        nameof(Customer.PrimaryAddress) => "διεύθυνση",
        nameof(Customer.PrimaryMobile) => "κινητό",
        nameof(Customer.PrimaryEmail) => "email",
        nameof(Customer.OpeningBalance) => "υπόλοιπο έναρξης",
        nameof(Customer.IsActive) => "κατάσταση",
        nameof(Customer.BelongsToOffice) => "ιδιοκτησία",
        nameof(Customer.PartnerName) => "συνεργάτης",
        nameof(Customer.IsReferral) => "σύσταση",
        nameof(Customer.ReferredByCustomerId) => "πελάτης σύστασης",
        nameof(Customer.ReferredByPartnerName) => "συνεργάτης σύστασης",
        nameof(Customer.LicenseNumber) => "αριθμός διπλώματος",
        nameof(Customer.LicenseCategory) => "κατηγορία διπλώματος",
        nameof(Customer.BirthDate) => "ημερομηνία γέννησης",
        nameof(Customer.IdentityNumber) => "αριθμός ταυτότητας",
        nameof(Customer.Notes) => "σημειώσεις",
        nameof(Customer.CustomerKind) => "τύπος πελάτη",
        nameof(Customer.Region) => "περιοχή",
        nameof(Customer.PostalCode) => "ταχυδρομικός κώδικας",
        nameof(Customer.Gender) => "φύλο",
        nameof(Customer.SocialSecurityNumber) => "ΑΜΚΑ",
        nameof(Customer.MaritalStatus) => "οικογενειακή κατάσταση",
        nameof(Customer.SpouseName) => "σύζυγος / σύντροφος",
        nameof(Customer.HomePhone) => "τηλέφωνο",
        nameof(Customer.SecondMobile) => "δεύτερο κινητό",
        nameof(Customer.ThirdMobile) => "τρίτο κινητό",
        nameof(Customer.InsuranceRegistry) => "ασφαλιστικό μητρώο",
        nameof(Customer.LegalForm) => "νομική μορφή",
        nameof(Customer.BusinessActivityCode) => "ΚΑΔ",
        nameof(Customer.Employer) => "εργοδότης",
        nameof(Customer.Source) => "πηγή",
        nameof(Customer.Tags) => "ετικέτες",
        nameof(Customer.PhotoUrl) => "φωτογραφία",
        nameof(Customer.PassportNumber) => "διαβατήριο",
        nameof(Customer.Zone) => "ζώνη",
        nameof(Customer.Occupation) => "επάγγελμα",
        nameof(Customer.TaxOffice) => "ΔΟΥ",
        nameof(Customer.BirthPlace) => "τόπος γέννησης",
        nameof(Customer.Nationality) => "ιθαγένεια",
        nameof(Customer.FatherName) => "πατρώνυμο",
        nameof(Customer.MotherName) => "μητρώνυμο",
        nameof(Customer.HasDrivingLicense) => "έχει δίπλωμα",
        nameof(Customer.IsNewDriver) => "νέος οδηγός",
        nameof(Customer.NameDay) => "ονομαστική εορτή",
        nameof(Customer.HasPersonalInsurance) => "προσωπικά ασφαλιστήρια",
        nameof(Customer.GroupName) => "ομαδοποίηση",
        nameof(Customer.IsDriver) => "είναι οδηγός",
        nameof(Customer.LicenseStartDate) => "έναρξη διπλώματος",
        nameof(Customer.LicenseEndDate) => "λήξη διπλώματος",
        nameof(Customer.HLDiploma) => "HL Diploma",
        nameof(Customer.HasDiscountDocument) => "δικαιολογητικό έκπτωσης",
        nameof(Customer.DiscountDocumentDescription) => "περιγραφή έκπτωσης",
        nameof(Customer.IdentityType) => "τύπος ταυτότητας",
        nameof(Customer.SecondPhone) => "δεύτερο τηλέφωνο",
        nameof(Customer.ThirdPhone) => "τρίτο τηλέφωνο",
        nameof(Customer.SecondEmail) => "δεύτερο email",
        nameof(Customer.ThirdEmail) => "τρίτο email",
        nameof(Customer.BusinessEmail) => "email επιχείρησης",
        nameof(Customer.BusinessPhone) => "τηλέφωνο επιχείρησης",
        nameof(Customer.CompanyName) => "επωνυμία",
        _ => "άλλο στοιχείο"
    };

    public static string FormatValue(object? value) => value switch
    {
        null => "—",
        string text when string.IsNullOrWhiteSpace(text) => "—",
        bool flag => flag ? "Ναι" : "Όχι",
        DateTime date => date.ToString("dd/MM/yyyy"),
        decimal amount => amount.ToString("N2"),
        _ => value.ToString()?.Length > 120 ? value.ToString()![..120] + "…" : value.ToString() ?? "—"
    };

    public static void Add(IApplicationDbContext db, Guid customerId, string action,
        string description, string actor)
    {
        db.CustomerActivities.Add(new CustomerActivity
        {
            Id = Guid.NewGuid(), CustomerId = customerId, Action = action,
            Description = description.Length > 4000 ? description[..4000] : description,
            ActorName = actor, OccurredAtUtc = DateTime.UtcNow
        });
    }
}

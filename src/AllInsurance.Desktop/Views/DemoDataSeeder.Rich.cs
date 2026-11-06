using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Partners;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace AllInsurance.Desktop.Views;

internal static partial class DemoDataSeeder
{
    private static readonly string RichMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "demo-data-v2.done");

    private static async Task SeedRichFixturesOnceAsync(IServiceProvider services)
    {
        if (File.Exists(RichMarkerPath)) return;
        var customers = services.GetRequiredService<ICustomerService>();
        var records = services.GetRequiredService<IBusinessRecordService>();
        var partners = services.GetRequiredService<IPartnerService>();
        var today = DateTime.Today;
        const string demoEmail = "linked-demo@example.invalid";
        var customerRows = await customers.SearchAsync();
        var sample = customerRows.FirstOrDefault(item => item.PrimaryEmail == demoEmail);
        var usedCustomerTaxes = customerRows.Select(item => item.TaxNumber)
            .Where(item => item is not null).ToHashSet();
        var customerTax = Enumerable.Range(900000004, 100)
            .Select(item => item.ToString("D9")).First(item => !usedCustomerTaxes.Contains(item));
        var customerId = sample?.Id ?? await customers.SaveAsync(new Customer
        {
            LastName = "ΔΕΙΓΜΑ", FirstName = "Διασύνδεσης", TaxNumber = customerTax,
            CustomerKind = "Φυσικό πρόσωπο", City = "Αθήνα", Region = "Αττική",
            PrimaryAddress = "Οδός Δοκιμής 1", PostalCode = "10000",
            PrimaryMobile = "6900000004", PrimaryEmail = demoEmail,
            Occupation = "Ιδιωτικός υπάλληλος", TaxOffice = "ΔΟΥ Αθηνών",
            BirthDate = new DateTime(1990, 4, 12), BirthPlace = "Αθήνα",
            Nationality = "Ελληνική", Gender = "Άνδρας", FatherName = "Δοκιμή",
            MotherName = "Παράδειγμα", NameDay = new DateTime(2000, 4, 12),
            IsDriver = true, HasDrivingLicense = true, LicenseCategory = "Β",
            LicenseNumber = "DEMO-LIC-001", LicenseStartDate = today.AddYears(-5),
            LicenseEndDate = today.AddYears(5), Notes = "ΔΟΚΙΜΑΣΤΙΚΑ ΔΕΔΟΜΕΝΑ — όχι πραγματικό πρόσωπο."
        });

        async Task<Guid> EnsureRecord(string section, string reference, Func<BusinessRecord> create)
        {
            var saved = (await records.SearchAsync(section)).FirstOrDefault(item => item.Reference == reference);
            return saved?.Id ?? await records.SaveAsync(create());
        }

        var usedPartnerTaxes = (await records.SearchAsync("partners"))
            .Select(item => PartnerProfileData.Read(item.DetailsJson).TaxNumber).ToHashSet();
        var partnerTax = Enumerable.Range(900000105, 100)
            .Select(item => item.ToString("D9")).First(item => !usedPartnerTaxes.Contains(item));
        var partnerId = await EnsureRecord("partners", "DEMO-V2-PARTNER", () => new BusinessRecord
        {
            SectionKey = "partners", Title = "ΔΕΙΓΜΑ ΣΥΝΕΡΓΑΤΗ", Reference = "DEMO-V2-PARTNER",
            Status = "Ενεργός", RelatedParty = "Αθήνα", EventDate = today.AddYears(-2),
            Notes = "Δοκιμαστική καρτέλα συνεργάτη.",
            DetailsJson = new PartnerProfileData
            {
                FirstName = "Συνεργάτη", LastName = "ΔΕΙΓΜΑ", TaxNumber = partnerTax,
                TaxOffice = "ΔΟΥ Αθηνών", PersonType = "Φυσικό πρόσωπο",
                ProfessionalCapacity = "Ασφαλιστικός διαμεσολαβητής", Category = "Α",
                Email = "partner-demo@example.invalid", Phone = "2100000005",
                Mobile = "6900000005", City = "Αθήνα", PostalCode = "10000",
                Address = "Οδός Δοκιμής 2", FatherName = "Παράδειγμα",
                MotherName = "Δοκιμή", BirthDate = new DateTime(1985, 2, 10),
                BirthPlace = "Αθήνα", Nationality = "Ελληνική", Gender = "Άνδρας",
                NameDay = new DateTime(2000, 2, 10), Education = "Ανώτερη εκπαίδευση",
                ContractNumber = "DEMO-CONTRACT-01", StartDate = today.AddYears(-2),
                HasActiveContract = true, CreditLimit = 5000m,
                DefaultCommissionPercent = 7m, PaymentMethod = "Τραπεζική μεταφορά",
                BankAccounts = [new PartnerBankAccount
                { BankName = "Δοκιμαστική τράπεζα", Iban = "DEMO-IBAN-001" }],
                Notes = "ΔΟΚΙΜΑΣΤΙΚΑ ΔΕΔΟΜΕΝΑ — χωρίς πραγματική τραπεζική σύνδεση."
            }.Write()
        });

        var demoCustomer = await customers.GetAsync(customerId);
        if (demoCustomer is not null && demoCustomer.PartnerName != "ΔΕΙΓΜΑ ΣΥΝΕΡΓΑΤΗ")
        {
            demoCustomer.PartnerName = "ΔΕΙΓΜΑ ΣΥΝΕΡΓΑΤΗ";
            demoCustomer.BelongsToOffice = false;
            await customers.SaveAsync(demoCustomer);
        }

        var companyId = await EnsureRecord("companies", "DEMO-V2-COMPANY", () => new BusinessRecord
        {
            SectionKey = "companies", Title = "ΔΕΙΓΜΑ ΑΣΦΑΛΙΣΤΙΚΗ",
            Reference = "DEMO-V2-COMPANY", Status = "Ενεργό",
            Notes = "Δοκιμαστική εταιρεία — δεν χρησιμοποιείται για πραγματικές εργασίες.",
            DetailsJson = new WorkspaceCardData
            {
                Fields = new Dictionary<string, string?>
                {
                    ["companyType"] = "Ασφαλιστική εταιρεία", ["origin"] = "Εσωτερικό",
                    ["taxNumber"] = "900000006", ["taxOffice"] = "ΔΟΥ Αθηνών",
                    ["phone"] = "2100000006", ["email"] = "company-demo@example.invalid",
                    ["city"] = "Αθήνα", ["address"] = "Οδός Δοκιμής 3",
                    ["creditLimit"] = "10000", ["payoutMethod"] = "Τραπεζική μεταφορά"
                },
                Contacts = [new OrganizationContact { Name = "ΔΕΙΓΜΑ Επαφής",
                    Position = "Υπεύθυνος συνεργασίας", Phone = "2100000016",
                    Email = "contact-demo@example.invalid" }]
            }.Write()
        });
        var agencyId = await EnsureRecord("agencies", "DEMO-V2-AGENCY", () => new BusinessRecord
        {
            SectionKey = "agencies", Title = "ΔΕΙΓΜΑ ΠΡΑΚΤΟΡΕΙΟ",
            Reference = "DEMO-V2-AGENCY", Status = "Ενεργό",
            Notes = "Δοκιμαστικό πρακτορείο.",
            DetailsJson = new WorkspaceCardData
            {
                CompanyId = companyId,
                Fields = new Dictionary<string, string?>
                {
                    ["agencyType"] = "Πρακτορείο", ["origin"] = "Εσωτερικό",
                    ["taxNumber"] = "900000007", ["phone"] = "2100000007",
                    ["email"] = "agency-demo@example.invalid", ["city"] = "Αθήνα",
                    ["address"] = "Οδός Δοκιμής 4", ["creditLimit"] = "3000"
                },
                Contacts = [new OrganizationContact { Name = "ΔΕΙΓΜΑ Επικοινωνίας",
                    Position = "Εξυπηρέτηση", Phone = "2100000017",
                    Email = "agency-contact-demo@example.invalid" }]
            }.Write()
        });
        var vehicleId = await EnsureRecord("vehicles", "ΜΕΗ1234", () => new BusinessRecord
        {
            SectionKey = "vehicles", Title = "ΔΕΙΓΜΑ FIAT 500", Reference = "ΜΕΗ1234",
            CustomerId = customerId, Status = "Ενεργό", EventDate = today.AddYears(-6),
            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
            {
                Registration = "ΜΕΗ1234", Make = "FIAT", Model = "500",
                Vin = "DEMO000000000001", ManufactureYear = today.Year - 7,
                FirstRegistrationDate = today.AddYears(-6), Use = "ΕΙΧ",
                Fuel = "Βενζίνη", Color = "Λευκό", Seats = 4, Doors = 3,
                DisplacementCc = 1200, PowerKw = 51, NextKteoDate = today.AddMonths(8),
                KteoStatus = "Ενεργό", Notes = "Δοκιμαστικό όχημα.",
                People = [new VehiclePerson { CustomerId = customerId,
                    Role = "Ιδιοκτήτης και οδηγός", OwnershipPercent = 100, IsPrimaryOwner = true }]
            })
        });
        var policyId = await EnsureRecord("policies", "DEMO-V2-POLICY", () => new BusinessRecord
        {
            SectionKey = "policies", Title = "DEMO-V2-POLICY", Reference = "DEMO-V2-POLICY",
            DocumentType = "Ασφαλιστήριο", CustomerId = customerId,
            RelatedParty = "ΔΕΙΓΜΑ ΑΣΦΑΛΙΣΤΙΚΗ", Status = "Ενεργό",
            EventDate = today.AddMonths(-2),
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                PolicyNumber = "DEMO-V2-POLICY", Insurer = "ΔΕΙΓΜΑ ΑΣΦΑΛΙΣΤΙΚΗ",
                CompanyId = companyId, Agency = "ΔΕΙΓΜΑ ΠΡΑΚΤΟΡΕΙΟ", AgencyId = agencyId,
                PartnerId = partnerId, VehicleId = vehicleId, Branch = "Αυτοκίνητο",
                Product = "Δοκιμαστικό πακέτο", StartDate = today.AddMonths(-2),
                EndDate = today.AddDays(24), IssueDate = today.AddMonths(-2),
                RenewalDate = today.AddDays(24), CoverageDurationValue = 3,
                CoverageDurationUnit = "Μήνες", NetPremium = 95m, GrossPremium = 120m,
                OfficeCommissionPercent = 15m, PartnerCommissionPercent = 7m,
                PreferredPaymentMethod = "Μετρητά", PaymentRecipient = "Στο γραφείο",
                InsuredValue = 8000m,
                Coverages = [new PolicyCoverage { Name = "Αστική ευθύνη",
                    SumInsured = 1000000m, Premium = 60m },
                    new PolicyCoverage { Name = "Οδική βοήθεια", Premium = 15m }],
                InsuredObjects = [new PolicyInsuredObject { Kind = "Όχημα",
                    Description = "FIAT 500 · ΜΕΗ1234", InsuredValue = 8000m }],
                Endorsements = [new PolicyEndorsement { Number = "DEMO-ADD-01",
                    EffectiveDate = today.AddMonths(-1), Notes = "Δοκιμαστική πρόσθετη πράξη" }],
                GreenCards = [new PolicyGreenCard { Number = "DEMO-GREEN-01",
                    StartDate = today.AddMonths(-2), EndDate = today.AddDays(24),
                    Countries = "Ελλάδα" }],
                Installments = [new PolicyInstallment { Number = 1,
                    DueDate = today.AddMonths(-2), Amount = 120m }]
            })
        });
        var claimId = await EnsureRecord("claims", "DEMO-V2-CLAIM", () => new BusinessRecord
        {
            SectionKey = "claims", Title = "DEMO-V2-CLAIM", Reference = "DEMO-V2-CLAIM",
            CustomerId = customerId, Status = "Σε εξέλιξη", EventDate = today.AddDays(-7),
            DetailsJson = new WorkspaceCardData
            {
                PolicyId = policyId, VehicleId = vehicleId, CompanyId = companyId,
                AgencyId = agencyId, PartnerId = partnerId,
                Fields = new Dictionary<string, string?>
                {
                    ["claimType"] = "Ζημιά οχήματος", ["eventTime"] = "12:30",
                    ["incidentCity"] = "Αθήνα", ["incidentLocation"] = "Δοκιμαστική τοποθεσία",
                    ["reserve"] = "500", ["settlement"] = "0", ["paid"] = "0",
                    ["notes"] = "ΔΟΚΙΜΑΣΤΙΚΗ ΖΗΜΙΑ"
                }
            }.Write()
        });
        foreach (var section in new[] { "updates", "reports", "bridges" })
            await EnsureRecord(section, "DEMO-V2-" + section.ToUpperInvariant(), () => new BusinessRecord
            {
                SectionKey = section, Title = "ΔΕΙΓΜΑ · " + section,
                Reference = "DEMO-V2-" + section.ToUpperInvariant(), Status = "Ενεργό",
                CustomerId = customerId,
                DetailsJson = new WorkspaceCardData { CompanyId = companyId,
                    Fields = new Dictionary<string, string?> { ["notes"] = "Δοκιμαστική εγγραφή." } }.Write()
            });

        var loadedCustomer = await customers.GetAsync(customerId);
        if (loadedCustomer is not null && !loadedCustomer.Transactions.Any(item => item.Reference == "DEMO-V2-RECEIPT"))
            await customers.AddTransactionAsync(customerId, "receipt", 30m,
                "Δοκιμαστική είσπραξη ασφαλιστηρίου", "Μετρητά", "DEMO-V2-RECEIPT", policyId);
        if (loadedCustomer is not null && !loadedCustomer.Transactions.Any(item => item.Reference == "DEMO-V2-PAYMENT"))
            await customers.AddTransactionAsync(customerId, "payment", 5m,
                "Δοκιμαστική επιστροφή", "Μετρητά", "DEMO-V2-PAYMENT", policyId);
        if (!(await partners.GetSnapshotAsync(partnerId)).Payouts.Any(item => item.Reference == "DEMO-V2-PAYOUT"))
            await partners.AddPayoutAsync(partnerId, 3m, "Τραπεζική μεταφορά",
                "DEMO-V2-PAYOUT", "Δοκιμαστική απόδοση προμήθειας");

        var attachmentSource = Path.Combine(Path.GetDirectoryName(RichMarkerPath)!, "demo-v2.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentSource)!);
        File.WriteAllText(attachmentSource,
            "ΔΟΚΙΜΑΣΤΙΚΟ ΑΡΧΕΙΟ ALLINSURANCE\nΔεν περιέχει πραγματικά προσωπικά ή οικονομικά δεδομένα.\n");
        foreach (var id in new[] { partnerId, companyId, agencyId, vehicleId, policyId, claimId })
            if (!(await records.GetAttachmentsAsync(id)).Any(item => item.FileName == "demo-v2.txt"))
                await records.AddAttachmentAsync(id, attachmentSource);
        if (loadedCustomer is not null && !loadedCustomer.Details.Any(item =>
                item.Category == "files" && item.Title == "demo-v2.txt"))
            await customers.AddFileAsync(customerId, attachmentSource);

        File.WriteAllText(RichMarkerPath, DateTime.UtcNow.ToString("O"));
    }
}

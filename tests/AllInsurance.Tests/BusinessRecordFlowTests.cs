using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class BusinessRecordFlowTests
{
    [Fact]
    public async Task PolicyRelationsCollectionsPaymentsAndPdfArePersisted()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        { LastName = "Policy", FirstName = "Test", TaxNumber = "100000093" });
        var companyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Test insurer" });
        var otherCompanyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Other insurer" });
        var agencyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "agencies", Title = "Test agency",
            DetailsJson = new WorkspaceCardData { CompanyId = companyId }.Write() });
        var invalid = new BusinessRecord
        { SectionKey = "policies", Title = "BAD-1", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { AgencyId = agencyId, CompanyId = otherCompanyId }) };
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(invalid));
        var policyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "policies", Title = "POL-2026", CustomerId = customerId,
            DocumentType = "Ασφαλιστήριο",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                CompanyId = companyId, AgencyId = agencyId, HolderCustomerId = customerId,
                PolicyNumber = "POL-2026", NetPremium = 80, GrossPremium = 100,
                CoverageDurationMonths = 12, StartDate = new DateTime(2026, 11, 3),
                EndDate = new DateTime(2027, 11, 3), PayInInstallments = true,
                Coverages = [new PolicyCoverage { Name = "Αστική ευθύνη", SumInsured = 10000 }],
                InsuredObjects = [new PolicyInsuredObject { Description = "Όχημα" }],
                Installments = [new PolicyInstallment { Number = 1, Amount = 100 }]
            }) });
        var saved = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(companyId, saved.CompanyId);
        Assert.Equal(agencyId, saved.AgencyId);
        Assert.Single(saved.Coverages);
        Assert.Single(saved.InsuredObjects);
        Assert.Single(saved.Installments);
        await customers.AddTransactionAsync(customerId, "receipt", 40m, "Policy payment",
            "Μετρητά", "PAY-1", policyId);
        var directPolicy = (await records.GetAsync(policyId))!;
        var directData = BusinessRecordDetails.Read<PolicyCardData>(directPolicy.DetailsJson);
        directData.PaymentRecipient = "Στην ασφαλιστική";
        directPolicy.DetailsJson = BusinessRecordDetails.Write(directData);
        await records.SaveAsync(directPolicy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => customers.AddTransactionAsync(
            customerId, "receipt", 10m, "Wrong office receipt", "Μετρητά", null, policyId));
        Assert.Contains(await records.GetActivitiesAsync(policyId), activity =>
            activity.Description.Contains("PAY-1"));
        var source = Path.Combine(Path.GetTempPath(), $"policy-test-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(source, "test attachment");
        try
        {
            var attachment = await records.AddAttachmentAsync(policyId, source);
            Assert.True(File.Exists(records.GetAttachmentPath(attachment)));
            Assert.Single(await records.GetAttachmentsAsync(policyId));
            await records.DeleteAttachmentAsync(policyId, attachment.Id);
            Assert.Empty(await records.GetAttachmentsAsync(policyId));
        }
        finally { File.Delete(source); }
        var renewalId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "policies", Title = "RENEW-1", CustomerId = customerId,
            DocumentType = "Αίτηση",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { ApplicationNumber = "RENEW-1", PreviousPolicyId = policyId }) });
        Assert.Equal(policyId, BusinessRecordDetails.Read<PolicyCardData>(
            (await records.GetAsync(renewalId))!.DetailsJson).PreviousPolicyId);
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        { SectionKey = "policies", Title = "RENEW-2", CustomerId = customerId,
            DocumentType = "Αίτηση", DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { ApplicationNumber = "RENEW-2", PreviousPolicyId = policyId }) }));
        var toCancel = (await records.GetAsync(policyId))!;
        toCancel.Status = "Ακυρωμένο";
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(toCancel));
        var cancellation = BusinessRecordDetails.Read<PolicyCardData>(toCancel.DetailsJson);
        cancellation.CancellationDate = new DateTime(2026, 11, 5);
        cancellation.CancellationReason = "Αίτημα πελάτη";
        toCancel.DetailsJson = BusinessRecordDetails.Write(cancellation);
        await records.SaveAsync(toCancel);
        Assert.Contains(await records.GetActivitiesAsync(policyId), activity =>
            activity.Action == "Ακύρωση ασφαλιστηρίου" &&
            activity.Description.Contains("Αίτημα πελάτη"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(policyId));
    }

    [Fact]
    public async Task CompanyAgencyAndClaimLinksSurviveRenamesAndHaveHistory()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        { LastName = "Linked", FirstName = "Customer", TaxNumber = "100000091" });
        var companyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Insurer A", DetailsJson = new WorkspaceCardData().Write() });
        var agencyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "agencies", Title = "Agency A",
            DetailsJson = new WorkspaceCardData { CompanyId = companyId }.Write() });
        var vehicleId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "vehicles", Title = "TEST-456", CustomerId = customerId });
        var policyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "policies", Title = "POL-456", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { Insurer = "Insurer A", Agency = "Agency A", VehicleId = vehicleId }) });
        var otherCompanyId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Insurer Other" });
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        { SectionKey = "claims", Title = "CLM-WRONG", CustomerId = customerId,
            DetailsJson = new WorkspaceCardData
            { CompanyId = otherCompanyId, PolicyId = policyId, VehicleId = vehicleId }.Write() }));
        var claimId = await records.SaveAsync(new BusinessRecord
        { SectionKey = "claims", Title = "CLM-456", CustomerId = customerId,
            DetailsJson = new WorkspaceCardData
            { CompanyId = companyId, AgencyId = agencyId, PolicyId = policyId,
                VehicleId = vehicleId }.Write() });
        Assert.Single(await records.GetActivitiesAsync(claimId));
        var claim = (await records.GetAsync(claimId))!;
        claim.Status = "Σε εξέλιξη";
        await records.SaveAsync(claim);
        Assert.Equal(2, (await records.GetActivitiesAsync(claimId)).Count);
        var company = (await records.GetAsync(companyId))!;
        company.Title = "Insurer B";
        await records.SaveAsync(company);
        var agency = (await records.GetAsync(agencyId))!;
        agency.Title = "Agency B";
        await records.SaveAsync(agency);
        var policy = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal("Insurer B", policy.Insurer);
        Assert.Equal("Agency B", policy.Agency);
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(companyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(agencyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(policyId));
    }

    [Fact]
    public async Task VehiclePeopleClaimsAndTechnicalDataStayLinked()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var owner = await customers.SaveAsync(new Customer
        { LastName = "Owner", FirstName = "One", TaxNumber = "100000061" });
        var coOwner = await customers.SaveAsync(new Customer
        { LastName = "Owner", FirstName = "Two", TaxNumber = "100000062" });
        var driver = await customers.SaveAsync(new Customer
        { LastName = "Driver", FirstName = "Three", TaxNumber = "100000063" });
        var vehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "TEST-123", CustomerId = owner,
            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
            {
                Registration = "TEST-123", DisplacementCc = 1600, PowerKw = 85,
                HasAlarm = true, Co2GramsPerKm = 123, NextKteoDate = new DateTime(2027, 5, 1),
                People =
                [
                    new VehiclePerson { CustomerId = owner, Role = "Ιδιοκτήτης",
                        OwnershipPercent = 60, IsPrimaryOwner = true },
                    new VehiclePerson { CustomerId = coOwner, Role = "Συνιδιοκτήτης",
                        OwnershipPercent = 40 },
                    new VehiclePerson { CustomerId = driver, Role = "Οδηγός" }
                ]
            })
        });
        var vehicle = BusinessRecordDetails.Read<VehicleCardData>((await records.GetAsync(vehicleId))!.DetailsJson);
        Assert.Equal(3, vehicle.People.Count);
        Assert.Equal(1600, vehicle.DisplacementCc);
        Assert.True(vehicle.HasAlarm);
        Assert.Equal(123m, vehicle.Co2GramsPerKm);
        await Assert.ThrowsAsync<InvalidOperationException>(() => customers.DeleteAsync(coOwner));
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "claims", Title = "BAD-CLAIM", CustomerId = coOwner,
            DetailsJson = BusinessRecordDetails.Write(new ClaimCardData { VehicleId = vehicleId })
        }));
        var claimId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "claims", Title = "CLM-1", CustomerId = owner,
            DetailsJson = BusinessRecordDetails.Write(new ClaimCardData { VehicleId = vehicleId })
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(vehicleId));
        await records.DeleteAsync(claimId);
        await records.DeleteAsync(vehicleId);
    }

    [Fact]
    public async Task SharedDashboardRecordsPersistAndLinkToCustomer()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => records.SearchAsync("vehicles"));
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Οδηγός", FirstName = "Δοκιμή", TaxNumber = "100000006"
        });
        var id = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "ΙΧ 1234", Reference = "ABC-1234",
            CustomerId = customerId, Status = "Ενεργό"
        });
        var item = Assert.Single(await records.SearchAsync("vehicles", "ABC-1234"));
        Assert.Equal(id, item.Id);
        Assert.Equal(customerId, item.CustomerId);
        Assert.Equal(customerId, (await customers.GetAsync(customerId))!.BusinessRecords.Single().CustomerId);
        Assert.Empty(await records.SearchAsync("policies"));
        item.Notes = "Νέα σημείωση";
        await records.SaveAsync(item);
        Assert.Equal("Νέα σημείωση", (await records.GetAsync(id))!.Notes);
        await records.DeleteAsync(id);
        Assert.Empty(await records.SearchAsync("vehicles"));
    }

    [Fact]
    public async Task InvalidSectionAndUnknownCustomerAreRejected()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "missing", Title = "Έλεγχος"
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "claims", Title = "Έλεγχος", CustomerId = Guid.NewGuid()
        }));
    }

    [Fact]
    public async Task ApplicationConversionAndLinkedPaymentAreAudited()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Policy", FirstName = "Owner", TaxNumber = "100000021"
        });
        var otherId = await customers.SaveAsync(new Customer
        {
            LastName = "Other", FirstName = "Owner", TaxNumber = "100000022"
        });
        var otherVehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "OTHER-1", CustomerId = otherId
        });
        await Assert.ThrowsAsync<ArgumentException>(() => records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "APP-INVALID", CustomerId = customerId,
            DocumentType = "Αίτηση",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                ApplicationNumber = "APP-INVALID", VehicleId = otherVehicleId
            })
        }));
        var vehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "ABC-1234", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
            {
                Registration = "ABC-1234", Make = "Test"
            })
        });
        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "APP-1", Reference = "APP-1",
            DocumentType = "Αίτηση", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                ApplicationNumber = "APP-1", VehicleId = vehicleId
            })
        });
        await Assert.ThrowsAsync<ArgumentException>(() => customers.AddTransactionAsync(
            otherId, "payment", 15m, "Wrong owner", "Μετρητά", null, policyId));
        await Assert.ThrowsAsync<ArgumentException>(() => customers.AddTransactionAsync(
            customerId, "payment", 15m, "Application only", "Μετρητά", null, policyId));
        var application = (await records.GetAsync(policyId))!;
        application.DocumentType = "Ασφαλιστήριο";
        application.Title = application.Reference = "POL-1";
        application.DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
        {
            ApplicationNumber = "APP-1", PolicyNumber = "POL-1", VehicleId = vehicleId
        });
        await records.SaveAsync(application);
        await customers.AddTransactionAsync(customerId, "payment", 15m, "Premium",
            "Τραπεζική μεταφορά", "TX-42", policyId);

        var loaded = await customers.GetAsync(customerId);
        Assert.Equal(policyId, Assert.Single(loaded!.Transactions).BusinessRecordId);
        Assert.Equal("Ασφαλιστήριο", (await records.GetAsync(policyId))!.DocumentType);
        var history = await customers.GetActivitiesAsync(customerId);
        Assert.Contains(history, item => item.Description.Contains("Εκδόθηκε το ασφαλιστήριο"));
        Assert.Contains(history, item => item.Action == "Πληρωμή" && item.Description.Contains("POL-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(policyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => records.DeleteAsync(vehicleId));
    }
}

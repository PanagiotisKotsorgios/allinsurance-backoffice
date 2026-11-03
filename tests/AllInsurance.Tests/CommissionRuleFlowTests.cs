using AllInsurance.Application.Business;
using AllInsurance.Application.Commissions;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Partners;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class CommissionRuleFlowTests
{
    [Fact]
    public async Task SpecificRuleIsSnapshottedOnInsuranceIssueAndKeepsPartnerLink()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        var rules = new CommissionRuleService(environment.ContextFactory, environment.Session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", OfficePercent = 10 }));
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        var partners = new PartnerService(environment.ContextFactory, environment.Session);
        await rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", OfficePercent = 15, PartnerPercent = 5 });
        var specificId = await rules.SaveAsync(new CommissionRule
        {
            Insurer = "Test Insurance", Branch = "Αυτοκίνητο", VehicleUse = "Ιδιωτική",
            Package = "Plus", OfficePercent = 18, PartnerPercent = 8
        });
        var selected = await rules.ResolveAsync(new CommissionMatch("Test Insurance", "Αυτοκίνητο",
            "Ιδιωτική", null, "Plus"));
        Assert.Equal(specificId, selected!.Id);
        var partnerId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "TEST PARTNER",
            DetailsJson = new PartnerProfileData { TaxNumber = "123456789" }.Write()
        });
        var customerId = await customers.SaveAsync(new Customer
        {
            LastName = "Rule", FirstName = "Customer", TaxNumber = "100000081",
            PartnerName = "TEST PARTNER", BelongsToOffice = false
        });
        var vehicleId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "vehicles", Title = "RULE-123", CustomerId = customerId,
            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
            { Registration = "RULE-123", Use = "Ιδιωτική" })
        });
        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "APP-81", CustomerId = customerId,
            DocumentType = "Αίτηση",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                ApplicationNumber = "APP-81", VehicleId = vehicleId,
                Insurer = "Test Insurance", Branch = "Αυτοκίνητο", Product = "Plus",
                GrossPremium = 1000
            })
        });
        var application = (await records.GetAsync(policyId))!;
        Assert.Null(BusinessRecordDetails.Read<PolicyCardData>(application.DetailsJson).OfficeCommissionPercent);
        application.DocumentType = "Ασφαλιστήριο";
        application.Title = "POL-81";
        var details = BusinessRecordDetails.Read<PolicyCardData>(application.DetailsJson);
        details.PolicyNumber = "POL-81";
        application.DetailsJson = BusinessRecordDetails.Write(details);
        await records.SaveAsync(application);
        var issued = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(specificId, issued.CommissionRuleId);
        Assert.Equal(18m, issued.OfficeCommissionPercent);
        Assert.Equal(partnerId, issued.PartnerId);
        Assert.Equal(8m, issued.PartnerCommissionPercent);
        Assert.Equal(80m, (await partners.GetSnapshotAsync(partnerId)).Earned);
        var rule = (await rules.ListAsync()).Single(item => item.Id == specificId);
        rule.OfficePercent = 25;
        await rules.SaveAsync(rule);
        Assert.Equal(18m, BusinessRecordDetails.Read<PolicyCardData>(
            (await records.GetAsync(policyId))!.DetailsJson).OfficeCommissionPercent);
    }
}

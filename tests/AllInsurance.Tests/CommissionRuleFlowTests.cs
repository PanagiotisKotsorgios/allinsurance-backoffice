using AllInsurance.Application.Business;
using AllInsurance.Application.Commissions;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Partners;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class CommissionRuleFlowTests
{
    [Fact]
    public async Task PartnerOverrideWinsOverCategoryAndOldPolicyChangesOnlyAfterExplicitApply()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var rules = new CommissionRuleService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var partnerId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "partners", Title = "PARTNER A",
            DetailsJson = new PartnerProfileData { TaxNumber = "123456789", Category = "Α" }.Write()
        });
        var generalId = await rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", OfficePercent = 15, PartnerPercent = 5 });
        var categoryId = await rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", PartnerCategory = "Α", OfficePercent = 20, PartnerPercent = 8 });
        var personalId = await rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", PartnerId = partnerId, OfficePercent = 25, PartnerPercent = 10 });
        Assert.Equal(personalId, (await rules.ResolveAsync(new CommissionMatch(
            "Test Insurance", null, null, null, null, partnerId, "Α")))?.Id);
        Assert.Equal(categoryId, (await rules.ResolveAsync(new CommissionMatch(
            "Test Insurance", null, null, null, null, null, "Α")))?.Id);
        Assert.Equal(generalId, (await rules.ResolveAsync(new CommissionMatch(
            "Test Insurance", null, null, null, null, null, "Β")))?.Id);

        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "POL-OLD", DocumentType = "Ασφαλιστήριο",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                PolicyNumber = "POL-OLD", Insurer = "Test Insurance", PartnerId = partnerId,
                NetPremium = 1000, StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31)
            })
        });
        var saved = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(25m, saved.OfficeCommissionPercent);
        Assert.Equal(10m, saved.PartnerCommissionPercent);

        var personal = (await rules.ListAsync()).Single(item => item.Id == personalId);
        personal.OfficePercent = 30;
        personal.PartnerPercent = 12;
        await rules.SaveAsync(personal);
        var oldRecord = (await records.GetAsync(policyId))!;
        await records.SaveAsync(oldRecord);
        saved = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(25m, saved.OfficeCommissionPercent);
        Assert.Equal(10m, saved.PartnerCommissionPercent);

        var recalculation = new CommissionRecalculationService(environment.ContextFactory,
            environment.Session);
        var preview = (await recalculation.PreviewAsync(new CommissionRecalculationFilter(
            PartnerId: partnerId))).Single(item => item.PolicyId == policyId);
        Assert.Equal(250m, preview.OldOfficeAmount);
        Assert.Equal(300m, preview.NewOfficeAmount);
        Assert.Equal(30m, preview.NewOfficePercent);
        Assert.Equal(1, await recalculation.ApplyAsync([preview]));
        saved = BusinessRecordDetails.Read<PolicyCardData>((await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(30m, saved.OfficeCommissionPercent);
        Assert.Equal(12m, saved.PartnerCommissionPercent);
        Assert.Contains(await records.GetActivitiesAsync(policyId), item =>
            item.Action == "Επανυπολογισμός προμήθειας");
        await Assert.ThrowsAsync<InvalidOperationException>(() => recalculation.ApplyAsync([preview]));
    }

    [Fact]
    public async Task AgencyRuleWithCoverageOverridesCompanyRuleOnIssuedPolicy()
    {
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var rules = new CommissionRuleService(environment.ContextFactory, environment.Session);
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var customers = new CustomerService(environment.ContextFactory, environment.Session);
        await rules.SaveAsync(new CommissionRule
        { Insurer = "Test Insurance", OfficePercent = 12, PartnerPercent = 4 });
        var agencyRuleId = await rules.SaveAsync(new CommissionRule
        {
            Insurer = "Test Agency", Coverage = CommissionCoverageSelection.Write(
                ["Οδική βοήθεια", "Θραύση κρυστάλλων"]),
            OfficePercent = 16, PartnerPercent = 6
        });
        await records.SaveAsync(new BusinessRecord
        { SectionKey = "companies", Title = "Test Insurance" });
        await records.SaveAsync(new BusinessRecord
        { SectionKey = "agencies", Title = "Test Agency" });
        var customerId = await customers.SaveAsync(new Customer
        { LastName = "Agency", FirstName = "Customer", TaxNumber = "100000082" });
        var policyId = await records.SaveAsync(new BusinessRecord
        {
            SectionKey = "policies", Title = "POL-82", CustomerId = customerId,
            DocumentType = "Ασφαλιστήριο",
            DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            {
                PolicyNumber = "POL-82", Insurer = "Test Insurance", Agency = "Test Agency",
                GrossPremium = 500, NetPremium = 400,
                Coverages = [new PolicyCoverage { Name = "Οδική βοήθεια" }]
            })
        });
        var policy = BusinessRecordDetails.Read<PolicyCardData>(
            (await records.GetAsync(policyId))!.DetailsJson);
        Assert.Equal(agencyRuleId, policy.CommissionRuleId);
        Assert.Equal(16m, policy.OfficeCommissionPercent);
        Assert.Equal(2, CommissionCoverageSelection.Read((await rules.ListAsync())
            .Single(rule => rule.Id == agencyRuleId).Coverage).Length);
        Assert.Equal(agencyRuleId, (await rules.ResolveAsync(new CommissionMatch(
            "Test Agency", null, null, "Θραύση κρυστάλλων", null)))?.Id);
        Assert.Null(await rules.ResolveAsync(new CommissionMatch(
            "Test Agency", null, null, "Νομική προστασία", null)));
    }

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
                GrossPremium = 1000, NetPremium = 800
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
        Assert.Equal(64m, (await partners.GetSnapshotAsync(partnerId)).Earned);
        var rule = (await rules.ListAsync()).Single(item => item.Id == specificId);
        rule.OfficePercent = 25;
        await rules.SaveAsync(rule);
        Assert.Equal(18m, BusinessRecordDetails.Read<PolicyCardData>(
            (await records.GetAsync(policyId))!.DetailsJson).OfficeCommissionPercent);
    }
}

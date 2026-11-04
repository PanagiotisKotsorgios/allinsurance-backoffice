using AllInsurance.Application.Business;
using AllInsurance.Domain.Entities;
using Xunit;

namespace AllInsurance.Tests;

public sealed class RenewalPlanningTests
{
    [Fact]
    public void Renewal_candidates_exclude_already_renewed_and_cancelled_documents()
    {
        var first = Policy("Ασφαλιστήριο", "Ενεργό", DateTime.Today.AddDays(20));
        var renewed = Policy("Ασφαλιστήριο", "Ενεργό", DateTime.Today.AddMonths(12));
        renewed.DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
        {
            EndDate = DateTime.Today.AddMonths(12), PreviousPolicyId = first.Id
        });
        var second = Policy("Ασφαλιστήριο", "Ενεργό", DateTime.Today.AddDays(40));
        var cancelled = Policy("Ασφαλιστήριο", "Ακυρωμένο", DateTime.Today.AddDays(10));
        var application = Policy("Αίτηση", "Σε εξέλιξη", DateTime.Today.AddDays(5));

        var candidates = RenewalPlanning.Candidates([first, renewed, second, cancelled, application]);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(second.Id, candidates[0].Record.Id);
        Assert.Equal(renewed.Id, candidates[1].Record.Id);
    }

    [Fact]
    public async Task New_installation_has_insurance_catalog_without_duplicate_seeding()
    {
        await using var environment = new TestEnvironment();
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            Assert.Contains(db.FilterOptions, item => item.Category == "Κλάδοι" && item.Value == "Αυτοκίνητο");
            Assert.Contains(db.FilterOptions, item => item.Category == "Καλύψεις" && item.Value == "Οδική βοήθεια");
            Assert.Equal(10, db.BusinessRecords.Count(item => item.SectionKey == "companies"));
            Assert.Equal(5, db.BusinessRecords.Count(item => item.SectionKey == "agencies"));
        }
        environment.Initializer.Initialize();
        await using var reopened = await environment.ContextFactory.CreateDbContextAsync();
        Assert.Equal(10, reopened.BusinessRecords.Count(item => item.SectionKey == "companies"));
        Assert.Equal(5, reopened.BusinessRecords.Count(item => item.SectionKey == "agencies"));
    }

    private static BusinessRecord Policy(string type, string status, DateTime end) => new()
    {
        Id = Guid.NewGuid(), SectionKey = "policies", Title = Guid.NewGuid().ToString("N"),
        DocumentType = type, Status = status,
        DetailsJson = BusinessRecordDetails.Write(new PolicyCardData { EndDate = end })
    };
}

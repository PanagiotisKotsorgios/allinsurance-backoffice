using AllInsurance.Application.Business;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Tests;

public sealed class OrganizationBalanceTests
{
    [Fact]
    public void PolicyAndSettlementProduceDueOrCreditBalance()
    {
        var company = new BusinessRecord { Id = Guid.NewGuid(), SectionKey = "companies", Title = "Δοκιμαστική" };
        var policy = new BusinessRecord { SectionKey = "policies", Title = "Συμβόλαιο",
            DocumentType = "Ασφαλιστήριο", DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { CompanyId = company.Id, GrossPremium = 100m, OfficeCommissionPercent = 20m }) };
        var details = new WorkspaceCardData();
        Assert.Equal(80m, OrganizationBalance.Calculate(company, details, [policy]));
        details.Settlements.Add(new OrganizationSettlement { Amount = 100m });
        Assert.Equal(-20m, OrganizationBalance.Calculate(company, details, [policy]));
    }

    [Fact]
    public void AgencyBalanceUsesItsLinkedPoliciesOnly()
    {
        var agency = new BusinessRecord { Id = Guid.NewGuid(), SectionKey = "agencies", Title = "Πρακτορείο" };
        var linked = new BusinessRecord { SectionKey = "policies", Title = "Συμβόλαιο Α",
            DocumentType = "Ασφαλιστήριο", DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { AgencyId = agency.Id, GrossPremium = 200m, OfficeCommissionPercent = 25m }) };
        var other = new BusinessRecord { SectionKey = "policies", Title = "Συμβόλαιο Β",
            DocumentType = "Ασφαλιστήριο", DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { AgencyId = Guid.NewGuid(), GrossPremium = 500m }) };
        Assert.Equal(150m, OrganizationBalance.Calculate(agency, new WorkspaceCardData(), [linked, other]));
    }

    [Fact]
    public void AgencyRoutedPolicyIsNotAlsoDueDirectlyToCompany()
    {
        var company = new BusinessRecord { Id = Guid.NewGuid(), SectionKey = "companies", Title = "Ασφαλιστική" };
        var agency = new BusinessRecord { Id = Guid.NewGuid(), SectionKey = "agencies", Title = "Πρακτορείο" };
        var policy = new BusinessRecord { SectionKey = "policies", Title = "Συμβόλαιο",
            DocumentType = "Ασφαλιστήριο", DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
            { CompanyId = company.Id, AgencyId = agency.Id, GrossPremium = 100m,
                OfficeCommissionPercent = 10m }) };
        Assert.Equal(0m, OrganizationBalance.Calculate(company, new WorkspaceCardData(), [policy]));
        Assert.Equal(90m, OrganizationBalance.Calculate(agency, new WorkspaceCardData(), [policy]));
    }
}

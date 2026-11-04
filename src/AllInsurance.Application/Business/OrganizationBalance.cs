using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Business;

public static class OrganizationBalance
{
    public static decimal Calculate(BusinessRecord organization, WorkspaceCardData data,
        IEnumerable<BusinessRecord> policies)
    {
        var isAgency = organization.SectionKey == "agencies";
        var due = policies.Where(policy => policy.SectionKey == "policies" &&
                (policy.DocumentType is null or "Ασφαλιστήριο") &&
                policy.Status is not ("Ακυρωμένο" or "Ακυρωμένη"))
            .Select(policy => BusinessRecordDetails.Read<PolicyCardData>(policy.DetailsJson))
            .Where(policy => isAgency
                ? policy.AgencyId == organization.Id || policy.AgencyId is null &&
                    string.Equals(policy.Agency, organization.Title, StringComparison.CurrentCultureIgnoreCase)
                : policy.AgencyId is null && string.IsNullOrWhiteSpace(policy.Agency) &&
                    (policy.CompanyId == organization.Id || policy.CompanyId is null &&
                    string.Equals(policy.Insurer, organization.Title, StringComparison.CurrentCultureIgnoreCase)))
            .Sum(policy => decimal.Round((policy.GrossPremium ?? 0m) *
                (100m - (policy.OfficeCommissionPercent ?? 0m)) / 100m, 2));
        return due - data.Settlements.Sum(settlement => settlement.Amount);
    }
}

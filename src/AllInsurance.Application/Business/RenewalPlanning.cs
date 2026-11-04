using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Business;

public static class RenewalPlanning
{
    public static IReadOnlyList<(BusinessRecord Record, PolicyCardData Data)> Candidates(
        IEnumerable<BusinessRecord> records)
    {
        var policies = records.Where(record => record.SectionKey == "policies")
            .Select(record => (Record: record,
                Data: BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson))).ToArray();
        var renewed = policies.Select(item => item.Data.PreviousPolicyId)
            .OfType<Guid>().ToHashSet();
        return policies.Where(item => item.Data.EndDate.HasValue &&
                !renewed.Contains(item.Record.Id) &&
                (item.Record.DocumentType is null or "Ασφαλιστήριο") &&
                item.Record.Status is not ("Ακυρωμένο" or "Ανανεωμένο"))
            .OrderBy(item => item.Data.EndDate).ToArray();
    }
}

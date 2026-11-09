using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Commissions;

public sealed record CommissionRecalculationFilter(
    string? Insurer = null, Guid? PartnerId = null, string? Branch = null,
    string? VehicleUse = null, string? Coverage = null, DateTime? StartFrom = null,
    DateTime? StartUntil = null, DateTime? EndFrom = null, DateTime? EndUntil = null);

public sealed record CommissionRecalculationPreview(
    Guid PolicyId, DateTime UpdatedAtUtc, string PolicyNumber, string Insurer,
    string? Branch, string? PartnerName, DateTime? StartDate, DateTime? EndDate,
    decimal NetPremium, decimal? OldOfficePercent, decimal? OldPartnerPercent,
    decimal NewOfficePercent, decimal NewPartnerPercent, Guid RuleId, DateTime RuleUpdatedAtUtc)
{
    public decimal OldOfficeAmount => Math.Round(NetPremium * (OldOfficePercent ?? 0) / 100m, 2);
    public decimal OldPartnerAmount => Math.Round(NetPremium * (OldPartnerPercent ?? 0) / 100m, 2);
    public decimal NewOfficeAmount => Math.Round(NetPremium * NewOfficePercent / 100m, 2);
    public decimal NewPartnerAmount => Math.Round(NetPremium * NewPartnerPercent / 100m, 2);
}

public interface ICommissionRecalculationService
{
    Task<IReadOnlyList<CommissionRecalculationPreview>> PreviewAsync(
        CommissionRecalculationFilter filter, CancellationToken cancellationToken = default);
    Task<int> ApplyAsync(IReadOnlyCollection<CommissionRecalculationPreview> selected,
        CancellationToken cancellationToken = default);
}

public sealed class CommissionRecalculationService(
    IApplicationDbContextFactory factory, ISessionService session)
    : ICommissionRecalculationService
{
    public async Task<IReadOnlyList<CommissionRecalculationPreview>> PreviewAsync(
        CommissionRecalculationFilter filter, CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var policies = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "policies")
            .ToListAsync(cancellationToken);
        var partners = (await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "partners")
            .ToListAsync(cancellationToken)).ToDictionary(item => item.Id);
        var vehicles = (await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "vehicles")
            .ToListAsync(cancellationToken)).ToDictionary(item => item.Id);
        var activeRules = await db.CommissionRules.AsNoTracking()
            .Where(item => item.IsActive).ToListAsync(cancellationToken);
        var result = new List<CommissionRecalculationPreview>();
        foreach (var policy in policies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (policy.DocumentType is not (null or "Ασφαλιστήριο")) continue;
            var details = BusinessRecordDetails.Read<PolicyCardData>(policy.DetailsJson);
            var organization = details.Agency ?? details.Insurer;
            if (string.IsNullOrWhiteSpace(organization) ||
                !Matches(filter.Insurer, organization, details.Insurer) ||
                filter.PartnerId is Guid partnerFilter && details.PartnerId != partnerFilter ||
                !Matches(filter.Branch, details.Branch) ||
                !Within(details.StartDate, filter.StartFrom, filter.StartUntil) ||
                !Within(details.EndDate, filter.EndFrom, filter.EndUntil)) continue;
            var vehicleUse = details.VehicleId is Guid vehicleId && vehicles.TryGetValue(vehicleId, out var vehicle)
                ? BusinessRecordDetails.Read<VehicleCardData>(vehicle.DetailsJson).Use : null;
            if (!Matches(filter.VehicleUse, vehicleUse)) continue;
            var coverage = string.Join(" | ", details.Coverages.Select(item => item.Name)
                .Append(details.CoverageNotes).Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(filter.Coverage) &&
                !coverage.Contains(filter.Coverage, StringComparison.CurrentCultureIgnoreCase)) continue;
            var partner = details.PartnerId is Guid partnerId && partners.TryGetValue(partnerId, out var found)
                ? found : null;
            var match = new CommissionMatch(organization, details.Branch, vehicleUse,
                coverage, details.Product, details.PartnerId,
                partner is null ? null : PartnerProfileData.Read(partner.DetailsJson).Category);
            var rule = CommissionRuleService.ResolveFrom(activeRules, match);
            if (rule is null && !string.IsNullOrWhiteSpace(details.Agency))
                rule = CommissionRuleService.ResolveFrom(activeRules,
                    match with { Insurer = details.Insurer ?? "" });
            if (rule is null) continue;
            result.Add(new CommissionRecalculationPreview(policy.Id, policy.UpdatedAtUtc,
                details.PolicyNumber ?? policy.Title, organization, details.Branch,
                partner?.Title, details.StartDate, details.EndDate, details.NetPremium ?? 0m,
                details.OfficeCommissionPercent, details.PartnerCommissionPercent,
                rule.OfficePercent, details.PartnerId is null ? 0m : rule.PartnerPercent,
                rule.Id, rule.UpdatedAtUtc));
        }
        return result.OrderBy(item => item.StartDate).ThenBy(item => item.PolicyNumber).ToArray();
    }

    public async Task<int> ApplyAsync(IReadOnlyCollection<CommissionRecalculationPreview> selected,
        CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        if (selected.Count == 0) return 0;
        if (selected.Select(item => item.PolicyId).Distinct().Count() != selected.Count)
            throw new ArgumentException("Το ίδιο ασφαλιστήριο επιλέχθηκε περισσότερες από μία φορές.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var preview in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = await db.BusinessRecords.SingleOrDefaultAsync(item =>
                item.Id == preview.PolicyId && item.SectionKey == "policies", cancellationToken)
                ?? throw new InvalidOperationException("Το ασφαλιστήριο δεν υπάρχει πλέον.");
            var rule = await db.CommissionRules.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == preview.RuleId && item.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("Ο κανόνας προμήθειας άλλαξε. Ανανεώστε την προεπισκόπηση.");
            var details = BusinessRecordDetails.Read<PolicyCardData>(policy.DetailsJson);
            if (policy.UpdatedAtUtc != preview.UpdatedAtUtc ||
                details.OfficeCommissionPercent != preview.OldOfficePercent ||
                details.PartnerCommissionPercent != preview.OldPartnerPercent ||
                rule.OfficePercent != preview.NewOfficePercent ||
                rule.UpdatedAtUtc != preview.RuleUpdatedAtUtc ||
                (details.PartnerId is not null && rule.PartnerPercent != preview.NewPartnerPercent))
                throw new InvalidOperationException("Τα δεδομένα ή ο κανόνας άλλαξαν. Ανανεώστε την προεπισκόπηση.");
            details.OfficeCommissionPercent = preview.NewOfficePercent;
            details.PartnerCommissionPercent = details.PartnerId is null ? null : preview.NewPartnerPercent;
            details.CommissionRuleId = rule.Id;
            policy.DetailsJson = BusinessRecordDetails.Write(details);
            policy.UpdatedAtUtc = DateTime.UtcNow;
            db.BusinessRecordActivities.Add(new BusinessRecordActivity
            {
                Id = Guid.NewGuid(), RecordId = policy.Id, Action = "Επανυπολογισμός προμήθειας",
                Description = $"Προμήθεια έδρας {preview.OldOfficePercent:N2}% → {preview.NewOfficePercent:N2}%, " +
                    $"συνεργάτη {preview.OldPartnerPercent:N2}% → {preview.NewPartnerPercent:N2}%.",
                ActorName = session.Current!.DisplayName, OccurredAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return selected.Count;
    }

    private void RequireAdministrator()
    {
        if (!session.IsAuthenticated ||
            !session.Current!.Permissions.Contains(Permission.ManageUsers))
            throw new UnauthorizedAccessException("Απαιτείται διαχειριστής για επεξεργασία προμηθειών.");
    }

    private static bool Matches(string? expected, params string?[] actual) =>
        string.IsNullOrWhiteSpace(expected) || actual.Any(value =>
            string.Equals(expected.Trim(), value?.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private static bool Within(DateTime? value, DateTime? from, DateTime? until) =>
        (from is null && until is null) ||
        value is DateTime date && (from is null || date.Date >= from.Value.Date) &&
        (until is null || date.Date <= until.Value.Date);
}

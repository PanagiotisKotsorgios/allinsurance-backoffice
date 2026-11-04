using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Commissions;

public sealed record CommissionMatch(string Insurer, string? Branch, string? VehicleUse,
    string? Coverage, string? Package);

public interface ICommissionRuleService
{
    Task<IReadOnlyList<CommissionRule>> ListAsync(CancellationToken cancellationToken = default);
    Task<CommissionRule?> ResolveAsync(CommissionMatch match, CancellationToken cancellationToken = default);
    Task<Guid> SaveAsync(CommissionRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class CommissionRuleService(IApplicationDbContextFactory factory, ISessionService session)
    : ICommissionRuleService
{
    public async Task<IReadOnlyList<CommissionRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.CommissionRules.AsNoTracking().OrderBy(item => item.Insurer)
            .ThenBy(item => item.Branch).ThenBy(item => item.VehicleUse)
            .ToListAsync(cancellationToken);
    }

    public async Task<CommissionRule?> ResolveAsync(CommissionMatch match,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (string.IsNullOrWhiteSpace(match.Insurer)) return null;
        var rules = await ListAsync(cancellationToken);
        return rules.Where(rule => rule.IsActive && Equal(rule.Insurer, match.Insurer) &&
                OptionalMatch(rule.Branch, match.Branch) &&
                OptionalMatch(rule.VehicleUse, match.VehicleUse) &&
                OptionalCoverage(rule.Coverage, match.Coverage) &&
                OptionalMatch(rule.Package, match.Package))
            .OrderByDescending(rule => new[] { rule.Branch, rule.VehicleUse,
                rule.Coverage, rule.Package }.Count(value => !string.IsNullOrWhiteSpace(value)))
            .ThenByDescending(rule => rule.UpdatedAtUtc).FirstOrDefault();
    }

    public async Task<Guid> SaveAsync(CommissionRule rule, CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        rule.Insurer = Clean(rule.Insurer, 200) ??
            throw new ArgumentException("Επιλέξτε εταιρεία ή πρακτορείο.");
        rule.Branch = Clean(rule.Branch, 120);
        rule.VehicleUse = Clean(rule.VehicleUse, 120);
        rule.Coverage = CommissionCoverageSelection.Write(
            CommissionCoverageSelection.Read(Clean(rule.Coverage, 4000)));
        rule.Package = Clean(rule.Package, 200);
        if (rule.OfficePercent is < 0 or > 100 || rule.PartnerPercent is < 0 or > 100 ||
            rule.PartnerPercent > rule.OfficePercent)
            throw new ArgumentException("Τα ποσοστά πρέπει να είναι από 0 έως 100 και η προμήθεια συνεργάτη να μην υπερβαίνει την προμήθεια γραφείου.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var candidates = await db.CommissionRules.AsNoTracking()
            .Where(item => item.Id != rule.Id).ToListAsync(cancellationToken);
        if (candidates.Any(item => Equal(item.Insurer, rule.Insurer) &&
            Equal(item.Branch, rule.Branch) && Equal(item.VehicleUse, rule.VehicleUse) &&
            Equal(item.Coverage, rule.Coverage) && Equal(item.Package, rule.Package)))
            throw new InvalidOperationException("Υπάρχει ήδη κανόνας με αυτά τα κριτήρια.");
        var now = DateTime.UtcNow;
        if (rule.Id == Guid.Empty)
        {
            rule.Id = Guid.NewGuid(); rule.CreatedAtUtc = now; rule.UpdatedAtUtc = now;
            db.CommissionRules.Add(rule);
        }
        else
        {
            var existing = await db.CommissionRules.SingleOrDefaultAsync(item => item.Id == rule.Id,
                cancellationToken) ?? throw new InvalidOperationException("Ο κανόνας δεν υπάρχει.");
            existing.Insurer = rule.Insurer;
            existing.Branch = rule.Branch;
            existing.VehicleUse = rule.VehicleUse;
            existing.Coverage = rule.Coverage;
            existing.Package = rule.Package;
            existing.OfficePercent = rule.OfficePercent;
            existing.PartnerPercent = rule.PartnerPercent;
            existing.IsActive = rule.IsActive;
            existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rule = await db.CommissionRules.SingleOrDefaultAsync(item => item.Id == id,
            cancellationToken) ?? throw new InvalidOperationException("Ο κανόνας δεν υπάρχει.");
        db.CommissionRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void RequireSession()
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
    }

    private void RequireAdministrator()
    {
        RequireSession();
        if (!session.Current!.Permissions.Contains(Permission.ManageUsers))
            throw new UnauthorizedAccessException("Η παραμετροποίηση προμηθειών απαιτεί διαχειριστή.");
    }

    private static bool Equal(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.CurrentCultureIgnoreCase);
    private static bool OptionalMatch(string? ruleValue, string? actual) =>
        string.IsNullOrWhiteSpace(ruleValue) || Equal(ruleValue, actual);
    private static bool OptionalCoverage(string? ruleValue, string? actual) =>
        CommissionCoverageSelection.Read(ruleValue) is not { Length: > 0 } selected ||
        selected.Any(coverage => actual?.Contains(coverage,
            StringComparison.CurrentCultureIgnoreCase) == true);
    private static string? Clean(string? text, int max)
    {
        var value = text?.Trim();
        if (value?.Length > max) throw new ArgumentException($"Το κείμενο πρέπει να έχει έως {max} χαρακτήρες.");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

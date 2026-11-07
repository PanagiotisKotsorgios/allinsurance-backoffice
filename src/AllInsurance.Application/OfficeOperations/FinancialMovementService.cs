using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.OfficeOperations;

public sealed record FinancialMovement(
    Guid Id, string Direction, DateTime OccurredOn, string PartyType, string Party,
    string? Policy, decimal Amount, string Currency, string Method, string? Reference,
    string? Description, string Source, Guid? PartyId = null, Guid? PolicyId = null);

/// <summary>Read-only ledger over the application's existing financial sources.</summary>
public sealed class FinancialMovementService(IApplicationDbContextFactory factory, ISessionService session)
{
    public async Task<IReadOnlyList<FinancialMovement>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var customerTransactions = await db.CustomerTransactions.AsNoTracking()
            .Include(item => item.Customer).Include(item => item.BusinessRecord)
            .Where(item => item.Type == "receipt" || item.Type == "payment")
            .ToListAsync(cancellationToken);
        var partnerTransactions = await db.PartnerPayouts.AsNoTracking()
            .Include(item => item.Partner).ToListAsync(cancellationToken);
        var officeTransactions = await db.OfficeOperations.AsNoTracking()
            .Where(item => item.Kind == "income" || item.Kind == "expense")
            .ToListAsync(cancellationToken);
        var organizations = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "companies" || item.SectionKey == "agencies")
            .ToListAsync(cancellationToken);
        var policies = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "policies")
            .Select(item => new { item.Id, item.Title }).ToListAsync(cancellationToken);
        var policyNames = policies.ToDictionary(item => item.Id, item => item.Title);
        var result = new List<FinancialMovement>();

        foreach (var item in customerTransactions)
            result.Add(new FinancialMovement(item.Id, item.Type, item.OccurredAtUtc,
                "Πελάτης", $"{item.Customer.LastName} {item.Customer.FirstName}".Trim(),
                item.BusinessRecord?.Title ?? PolicyName(item.BusinessRecordId), item.Amount,
                item.Currency, item.PaymentMethod, item.Reference, item.Description,
                "Πελάτες", item.CustomerId, item.BusinessRecordId));

        foreach (var item in partnerTransactions)
            result.Add(new FinancialMovement(item.Id, item.Kind, item.OccurredAtUtc,
                "Συνεργάτης", item.Partner.Title, PolicyName(item.PolicyId), item.Amount,
                item.Currency, item.PaymentMethod, item.Reference, item.Description,
                "Συνεργάτες", item.PartnerId, item.PolicyId));

        foreach (var item in officeTransactions)
            result.Add(new FinancialMovement(item.Id,
                item.Kind == "income" ? "receipt" : "payment", item.OccurredOn,
                "Γραφείο", item.Category ?? "Γενική κίνηση", null, item.Amount, "EUR",
                item.PaymentMethod ?? "—", item.Reference, item.Description,
                "Έσοδα – έξοδα γραφείου"));

        foreach (var organization in organizations)
        {
            var data = WorkspaceCardData.Read(organization.DetailsJson);
            foreach (var settlement in data.Settlements)
                result.Add(new FinancialMovement(settlement.Id, "payment", settlement.Date,
                    organization.SectionKey == "companies" ? "Ασφαλιστική" : "Πρακτορείο",
                    organization.Title, null, settlement.Amount, "EUR", "—", null,
                    settlement.Note, "Αποδόσεις", organization.Id));
        }
        return result.OrderByDescending(item => item.OccurredOn).ToArray();

        string? PolicyName(Guid? id) => id is Guid policyId && policyNames.TryGetValue(policyId, out var name)
            ? name : null;
    }
}

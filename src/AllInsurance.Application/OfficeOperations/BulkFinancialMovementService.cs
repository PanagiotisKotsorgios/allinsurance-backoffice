using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Settings;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.OfficeOperations;

public sealed record BulkMovementItem(Guid TargetId, decimal Amount);
public sealed record BulkFinancialPostResult(string Reference, IReadOnlyList<Guid> MovementIds);

/// <summary>Atomically posts one financial movement to each selected party.</summary>
public sealed class BulkFinancialMovementService(IApplicationDbContextFactory factory, ISessionService session)
{
    public async Task<BulkFinancialPostResult> PostAsync(string section, string direction,
        IReadOnlyList<BulkMovementItem> items, string method, string? reference,
        string? description, string expectedCurrency,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
        if (section is not ("customers" or "policies" or "partners" or "companies" or "agencies") ||
            direction is not ("receipt" or "payment"))
            throw new ArgumentException("Μη έγκυρο είδος μαζικής κίνησης.");
        if (items.Count == 0 || items.Any(item => item.TargetId == Guid.Empty ||
                item.Amount <= 0 || item.Amount > 999_999_999m) ||
            items.Select(item => item.TargetId).Distinct().Count() != items.Count)
            throw new ArgumentException("Κάθε επιλεγμένη καρτέλα χρειάζεται ένα θετικό ποσό.");
        method = method.Trim();
        reference = reference?.Trim();
        description = description?.Trim();
        if (method.Length is < 1 or > 80 || reference?.Length > 100 || description?.Length > 500)
            throw new ArgumentException("Ελέγξτε τον τρόπο πληρωμής, την αναφορά και την αιτιολογία.");
        if (section == "customers" && direction == "receipt" && string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Στη μαζική είσπραξη συμπληρώστε κοινή αιτιολογία για τις επιλεγμένες καρτέλες.");
        var batchReference = string.IsNullOrWhiteSpace(reference)
            ? "ΜΑΖ-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant() : reference;
        var ids = items.Select(item => item.TargetId).ToArray();
        var movementIds = new List<Guid>(items.Count);
        var occurredAt = DateTime.UtcNow;
        var actor = session.Current!.DisplayName;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var currency = await db.AppMetadata.AsNoTracking()
            .Where(item => item.Key == TransactionSettingsService.CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
        if (currency != expectedCurrency)
            throw new InvalidOperationException("Το νόμισμα άλλαξε. Ανοίξτε ξανά τη μαζική καταχώριση.");
        if (section == "policies")
        {
            var policies = await db.BusinessRecords.AsNoTracking()
                .Where(item => ids.Contains(item.Id) && item.SectionKey == "policies")
                .Select(item => new { item.Id, item.CustomerId, item.Title })
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            if (policies.Count != ids.Length || policies.Values.Any(item => item.CustomerId is null))
                throw new InvalidOperationException("Κάποιο επιλεγμένο ασφαλιστήριο δεν υπάρχει ή δεν έχει συνδεδεμένο πελάτη.");
            foreach (var item in items)
            {
                var policy = policies[item.TargetId];
                var customerId = policy.CustomerId!.Value;
                var movementId = Guid.NewGuid();
                movementIds.Add(movementId);
                db.CustomerTransactions.Add(new CustomerTransaction
                {
                    Id = movementId, CustomerId = customerId, BusinessRecordId = policy.Id,
                    Type = direction, Amount = item.Amount, PaymentMethod = method,
                    Currency = currency, Reference = batchReference, Description = description,
                    OccurredAtUtc = occurredAt, CreatedByUserId = session.Current.UserId
                });
                var action = direction == "receipt" ? "Μαζική είσπραξη" : "Μαζική πληρωμή";
                var detail = $"{item.Amount:N2} {currency} · {method} · {policy.Title} · {batchReference}";
                db.CustomerActivities.Add(new CustomerActivity
                {
                    Id = Guid.NewGuid(), CustomerId = customerId, Action = action,
                    Description = detail, ActorName = actor, OccurredAtUtc = occurredAt
                });
                db.BusinessRecordActivities.Add(new BusinessRecordActivity
                {
                    Id = Guid.NewGuid(), RecordId = policy.Id, Action = action,
                    Description = detail, ActorName = actor, OccurredAtUtc = occurredAt
                });
            }
        }
        else if (section == "customers")
        {
            var customers = await db.Customers.AsNoTracking().Where(item => ids.Contains(item.Id))
                .Select(item => item.Id).ToListAsync(cancellationToken);
            if (customers.Count != ids.Length) throw new InvalidOperationException("Κάποιος επιλεγμένος πελάτης δεν υπάρχει πλέον.");
            foreach (var item in items)
            {
                var movementId = Guid.NewGuid();
                movementIds.Add(movementId);
                db.CustomerTransactions.Add(new CustomerTransaction
                {
                    Id = movementId, CustomerId = item.TargetId, Type = direction,
                    Amount = item.Amount, PaymentMethod = method, Currency = currency,
                    Reference = batchReference, Description = description,
                    OccurredAtUtc = occurredAt, CreatedByUserId = session.Current.UserId
                });
                db.CustomerActivities.Add(new CustomerActivity
                {
                    Id = Guid.NewGuid(), CustomerId = item.TargetId,
                    Action = direction == "receipt" ? "Μαζική είσπραξη" : "Μαζική πληρωμή",
                    Description = $"{item.Amount:N2} {currency} · {method} · {batchReference}" +
                        (string.IsNullOrWhiteSpace(description) ? "" : $" · {description}"),
                    ActorName = actor, OccurredAtUtc = occurredAt
                });
            }
        }
        else if (section == "partners")
        {
            var partners = await db.BusinessRecords.AsNoTracking()
                .Where(item => ids.Contains(item.Id) && item.SectionKey == "partners")
                .Select(item => item.Id).ToListAsync(cancellationToken);
            if (partners.Count != ids.Length) throw new InvalidOperationException("Κάποιος επιλεγμένος συνεργάτης δεν υπάρχει πλέον.");
            foreach (var item in items)
            {
                var movementId = Guid.NewGuid();
                movementIds.Add(movementId);
                db.PartnerPayouts.Add(new PartnerPayout
                {
                    Id = movementId, PartnerId = item.TargetId, Kind = direction,
                    Amount = item.Amount, Currency = currency, PaymentMethod = method,
                    Reference = batchReference,
                    Description = description, OccurredAtUtc = occurredAt,
                    CreatedByUserId = session.Current.UserId
                });
                db.PartnerActivities.Add(new PartnerActivity
                {
                    Id = Guid.NewGuid(), PartnerId = item.TargetId,
                    Action = direction == "receipt" ? "Μαζική είσπραξη" : "Μαζική πληρωμή",
                    Description = $"{item.Amount:N2} € · {method} · {batchReference}" +
                        (string.IsNullOrWhiteSpace(description) ? "" : $" · {description}"),
                    ActorName = actor, OccurredAtUtc = occurredAt
                });
            }
        }
        else
        {
            var organizations = await db.BusinessRecords
                .Where(item => ids.Contains(item.Id) && item.SectionKey == section)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            if (organizations.Count != ids.Length) throw new InvalidOperationException("Κάποιος επιλεγμένος φορέας δεν υπάρχει πλέον.");
            foreach (var item in items)
            {
                var movementId = Guid.NewGuid();
                movementIds.Add(movementId);
                var record = organizations[item.TargetId];
                var data = WorkspaceCardData.Read(record.DetailsJson);
                // Existing settlement convention: positive = paid to the organization,
                // negative = received back. The balance calculation uses the signed value.
                data.Settlements.Add(new OrganizationSettlement
                {
                    Id = movementId, Date = occurredAt.ToLocalTime(),
                    Amount = direction == "payment" ? item.Amount : -item.Amount,
                    PaymentMethod = method, Reference = batchReference, Currency = currency,
                    Note = description ?? ""
                });
                record.DetailsJson = data.Write();
                record.UpdatedAtUtc = occurredAt;
                db.BusinessRecordActivities.Add(new BusinessRecordActivity
                {
                    Id = Guid.NewGuid(), RecordId = record.Id,
                    Action = direction == "receipt" ? "Μαζική είσπραξη" : "Μαζική πληρωμή",
                    Description = $"{item.Amount:N2} € · {method} · {batchReference}",
                    ActorName = actor, OccurredAtUtc = occurredAt
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BulkFinancialPostResult(batchReference, movementIds);
    }
}

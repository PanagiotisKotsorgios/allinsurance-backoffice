using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Business;

public sealed class BusinessRecordService(IApplicationDbContextFactory factory, ISessionService session)
    : IBusinessRecordService
{
    private static readonly HashSet<string> Sections =
    [
        "partners", "vehicles", "companies", "agencies", "policies",
        "claims", "updates", "reports", "bridges"
    ];

    public async Task<IReadOnlyList<BusinessRecord>> SearchAsync(string sectionKey, string? query = null,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        ValidateSection(sectionKey);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = db.BusinessRecords.AsNoTracking().Include(item => item.Customer)
            .Where(item => item.SectionKey == sectionKey);
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            records = records.Where(item => item.Title.Contains(term) ||
                (item.Reference != null && item.Reference.Contains(term)) ||
                (item.RelatedParty != null && item.RelatedParty.Contains(term)) ||
                (item.Status != null && item.Status.Contains(term)));
        return await records.OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<BusinessRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessRecords.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<Guid> SaveAsync(BusinessRecord record, CancellationToken cancellationToken = default)
    {
        RequireSession();
        ValidateSection(record.SectionKey);
        record.Title = Normalize(record.Title, 200) ??
            throw new ArgumentException("Ο τίτλος είναι υποχρεωτικός.");
        record.Reference = Normalize(record.Reference, 100);
        record.RelatedParty = Normalize(record.RelatedParty, 200);
        record.Status = Normalize(record.Status, 100);
        record.Notes = Normalize(record.Notes, 2000);
        record.DocumentType = Normalize(record.DocumentType, 40);
        record.DetailsJson = Normalize(record.DetailsJson, 12000);
        if (record.SectionKey == "policies" && record.DocumentType is not null &&
            record.DocumentType is not ("Αίτηση" or "Ειδοποιητήριο" or "Ασφαλιστήριο"))
            throw new ArgumentException("Μη έγκυρος τύπος εγγράφου ασφάλισης.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (record.CustomerId is Guid customerId &&
            !await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
            throw new ArgumentException("Ο συνδεδεμένος πελάτης δεν υπάρχει.");
        if (record.SectionKey == "policies")
        {
            var vehicleId = BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson).VehicleId;
            if (vehicleId is Guid id && !await db.BusinessRecords.AnyAsync(item =>
                item.Id == id && item.SectionKey == "vehicles" && item.CustomerId == record.CustomerId,
                cancellationToken))
                throw new ArgumentException("Το όχημα πρέπει να ανήκει στον ίδιο πελάτη.");
        }
        var now = DateTime.UtcNow;
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
            record.CreatedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.CreatedByUserId = session.Current!.UserId;
            db.BusinessRecords.Add(record);
            if (record.CustomerId is Guid activityCustomerId)
                CustomerActivityLog.Add(db, activityCustomerId, "Προσθήκη " +
                    (record.SectionKey == "policies" ? record.DocumentType ?? "ασφαλιστηρίου" :
                     record.SectionKey == "vehicles" ? "οχήματος" : "εγγραφής"),
                    $"Καταχωρίστηκε {record.Title}.", session.Current!.DisplayName);
        }
        else
        {
            var existing = await db.BusinessRecords.SingleOrDefaultAsync(item => item.Id == record.Id,
                cancellationToken) ?? throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
            if (existing.SectionKey != record.SectionKey)
                throw new ArgumentException("Η εγγραφή ανήκει σε άλλη ενότητα.");
            if (existing.UpdatedAtUtc != record.UpdatedAtUtc)
                throw new InvalidOperationException("Η εγγραφή έχει αλλάξει. Ανοίξτε την ξανά.");
            var created = existing.CreatedAtUtc;
            var creator = existing.CreatedByUserId;
            var previousType = existing.DocumentType;
            var previousCustomerId = existing.CustomerId;
            var changes = new List<string>();
            if (existing.Title != record.Title) changes.Add($"Αριθμός: {existing.Title} → {record.Title}");
            if (existing.Status != record.Status)
                changes.Add($"Κατάσταση: {existing.Status ?? "—"} → {record.Status ?? "—"}");
            if (existing.DocumentType != record.DocumentType)
                changes.Add($"Τύπος: {existing.DocumentType ?? "—"} → {record.DocumentType ?? "—"}");
            if (existing.DetailsJson != record.DetailsJson) changes.Add("Ενημερώθηκαν τα αναλυτικά στοιχεία.");
            if (existing.Notes != record.Notes) changes.Add("Ενημερώθηκαν οι σημειώσεις.");
            db.Entry(existing).CurrentValues.SetValues(record);
            if (previousCustomerId is Guid previousOwnerId && previousCustomerId != record.CustomerId)
                CustomerActivityLog.Add(db, previousOwnerId, "Αλλαγή σύνδεσης εγγραφής",
                    $"Η εγγραφή {record.Title} αποσυνδέθηκε από τον πελάτη.", session.Current!.DisplayName);
            if (record.CustomerId is Guid activityCustomerId)
                CustomerActivityLog.Add(db, activityCustomerId, "Επεξεργασία εγγραφής",
                    (previousType is "Αίτηση" or "Ειδοποιητήριο") &&
                    record.DocumentType == "Ασφαλιστήριο"
                        ? $"Η αίτηση {record.Title} μετατράπηκε σε ασφαλιστήριο."
                        : $"Ενημερώθηκε η εγγραφή {record.Title}. " +
                            string.Join(" ", changes),
                    session.Current!.DisplayName);
            existing.CreatedAtUtc = created;
            existing.CreatedByUserId = creator;
            existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return record.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.BusinessRecords.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
        if (record.SectionKey == "policies" &&
            await db.CustomerTransactions.AnyAsync(item => item.BusinessRecordId == id, cancellationToken))
            throw new InvalidOperationException("Το ασφαλιστήριο έχει συνδεδεμένες οικονομικές κινήσεις και δεν μπορεί να διαγραφεί.");
        if (record.SectionKey == "vehicles")
        {
            var policies = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "policies" && item.DetailsJson != null)
                .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
            if (policies.Any(json => BusinessRecordDetails.Read<PolicyCardData>(json).VehicleId == id))
                throw new InvalidOperationException("Το όχημα συνδέεται με αίτηση ή ασφαλιστήριο και δεν μπορεί να διαγραφεί.");
        }
        if (record.CustomerId is Guid customerId)
            CustomerActivityLog.Add(db, customerId, "Διαγραφή εγγραφής",
                $"Διαγράφηκε η εγγραφή {record.Title}.", session.Current!.DisplayName);
        db.BusinessRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void RequireSession()
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
    }

    private static void ValidateSection(string sectionKey)
    {
        if (!Sections.Contains(sectionKey)) throw new ArgumentException("Μη έγκυρη ενότητα.");
    }

    private static string? Normalize(string? value, int maximumLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > maximumLength)
            throw new ArgumentException($"Το πεδίο δεν μπορεί να υπερβαίνει τους {maximumLength} χαρακτήρες.");
        return text;
    }
}

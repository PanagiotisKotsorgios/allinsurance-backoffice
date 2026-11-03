using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
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
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (record.CustomerId is Guid customerId &&
            !await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
            throw new ArgumentException("Ο συνδεδεμένος πελάτης δεν υπάρχει.");
        var now = DateTime.UtcNow;
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
            record.CreatedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.CreatedByUserId = session.Current!.UserId;
            db.BusinessRecords.Add(record);
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
            db.Entry(existing).CurrentValues.SetValues(record);
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

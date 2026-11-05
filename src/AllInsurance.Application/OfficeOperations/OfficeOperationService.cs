using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.OfficeOperations;

public sealed class OfficeOperationService(IApplicationDbContextFactory factory, ISessionService session)
{
    public async Task<IReadOnlyList<OfficeOperation>> ListAsync(string kind,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        ValidateKind(kind);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.OfficeOperations.AsNoTracking().Where(item => item.Kind == kind)
            .OrderByDescending(item => item.OccurredOn).ThenByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<OfficeOperation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.OfficeOperations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<Guid> SaveAsync(OfficeOperation input,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        ValidateKind(input.Kind);
        input.Description = input.Description.Trim();
        input.Category = input.Category?.Trim();
        input.Reference = input.Reference?.Trim();
        input.PaymentMethod = input.PaymentMethod?.Trim();
        input.Notes = input.Notes?.Trim();
        input.OccurredOn = input.OccurredOn.Date;
        if (input.Description.Length is 0 or > 300 || input.Category?.Length > 100 ||
            input.Reference?.Length > 100 || input.PaymentMethod?.Length > 80 ||
            input.Notes?.Length > 2000)
            throw new ArgumentException("Ελέγξτε την περιγραφή και το μήκος των πεδίων.");
        if (input.OccurredOn.Year is < 1900 or > 2100)
            throw new ArgumentException("Συμπληρώστε έγκυρη ημερομηνία.");
        if (input.Kind == "overcommission")
        {
            if (input.PartnerId is null || input.CompanyId is null)
                throw new ArgumentException("Επιλέξτε συνεργάτη και ασφαλιστική εταιρεία.");
            if (input.GrossPremium is null or < 0 || input.NetPremium is null or < 0 ||
                input.OfficeCommission is null or < 0 || input.OverCommission is null or <= 0)
                throw new ArgumentException("Συμπληρώστε μη αρνητικά ασφάλιστρα, προμήθεια έδρας και θετική υπερπρομήθεια.");
            input.Amount = input.OverCommission.Value;
        }
        else if (input.Amount <= 0 || input.Amount > 999999999m)
            throw new ArgumentException("Το ποσό πρέπει να είναι μεγαλύτερο από μηδέν.");

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (input.Kind == "overcommission")
        {
            if (!await db.BusinessRecords.AnyAsync(item => item.Id == input.PartnerId &&
                item.SectionKey == "partners", cancellationToken) ||
                !await db.BusinessRecords.AnyAsync(item => item.Id == input.CompanyId &&
                item.SectionKey == "companies", cancellationToken))
                throw new ArgumentException("Ο συνεργάτης ή η ασφαλιστική εταιρεία δεν υπάρχει πλέον.");
        }
        if (input.Id == Guid.Empty)
        {
            input.Id = Guid.NewGuid();
            input.CreatedAtUtc = DateTime.UtcNow;
            input.CreatedByUserId = session.Current!.UserId;
            input.UpdatedAtUtc = input.CreatedAtUtc;
            db.OfficeOperations.Add(input);
        }
        else
        {
            var saved = await db.OfficeOperations.SingleOrDefaultAsync(item => item.Id == input.Id,
                cancellationToken) ?? throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
            if (saved.Kind != input.Kind)
                throw new ArgumentException("Δεν επιτρέπεται αλλαγή τύπου εγγραφής.");
            saved.OccurredOn = input.OccurredOn;
            saved.Description = input.Description;
            saved.Category = input.Category;
            saved.Amount = input.Amount;
            saved.PaymentMethod = input.PaymentMethod;
            saved.Reference = input.Reference;
            saved.Notes = input.Notes;
            saved.PartnerId = input.PartnerId;
            saved.CompanyId = input.CompanyId;
            saved.GrossPremium = input.GrossPremium;
            saved.NetPremium = input.NetPremium;
            saved.OfficeCommission = input.OfficeCommission;
            saved.OverCommission = input.OverCommission;
            saved.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return input.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var saved = await db.OfficeOperations.SingleOrDefaultAsync(item => item.Id == id,
            cancellationToken) ?? throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
        db.OfficeOperations.Remove(saved);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void RequireSession()
    {
        if (!session.IsAuthenticated)
            throw new UnauthorizedAccessException("Απαιτείται σύνδεση στην εφαρμογή.");
    }

    private static void ValidateKind(string kind)
    {
        if (kind is not ("income" or "expense" or "overcommission"))
            throw new ArgumentException("Μη έγκυρος τύπος οικονομικής εγγραφής.");
    }
}

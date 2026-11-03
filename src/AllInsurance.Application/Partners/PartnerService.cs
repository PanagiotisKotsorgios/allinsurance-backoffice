using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Partners;

public sealed record PartnerProductionItem(Guid PolicyId, string Number, string CustomerName,
    DateTime? StartDate, decimal GrossPremium, decimal CommissionPercent, decimal Commission);

public sealed record PartnerSnapshot(IReadOnlyList<PartnerProductionItem> Production,
    IReadOnlyList<PartnerPayout> Payouts, decimal Earned, decimal Paid,
    IReadOnlyList<PartnerActivity> Activities)
{
    public decimal Due => Earned - Paid;
}

public interface IPartnerService
{
    Task<PartnerSnapshot> GetSnapshotAsync(Guid partnerId, CancellationToken cancellationToken = default);
    Task<Guid> AddPayoutAsync(Guid partnerId, decimal amount, string method,
        string? reference, string? description, CancellationToken cancellationToken = default);
    Task<Guid> AddMovementAsync(Guid partnerId, string kind, decimal amount, string method,
        string? reference, string? description, CancellationToken cancellationToken = default);
}

public sealed class PartnerService(IApplicationDbContextFactory factory, ISessionService session)
    : IPartnerService
{
    public async Task<PartnerSnapshot> GetSnapshotAsync(Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var partner = await db.BusinessRecords.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == partnerId && item.SectionKey == "partners", cancellationToken)
            ?? throw new InvalidOperationException("Ο συνεργάτης δεν υπάρχει.");
        var profile = PartnerProfileData.Read(partner.DetailsJson);
        var policies = await db.BusinessRecords.AsNoTracking().Include(item => item.Customer)
            .Where(item => item.SectionKey == "policies" &&
                (item.DocumentType == "Ασφαλιστήριο" || item.DocumentType == null))
            .ToListAsync(cancellationToken);
        var production = policies.Select(item => (Record: item,
                Details: BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson)))
            .Where(item => item.Details.PartnerId == partnerId ||
                (item.Details.PartnerId == null && string.Equals(item.Record.Customer?.PartnerName,
                    partner.Title, StringComparison.CurrentCultureIgnoreCase)))
            .Select(item =>
            {
                var premium = item.Details.GrossPremium ?? 0m;
                var rate = item.Details.PartnerCommissionPercent ?? profile.DefaultCommissionPercent ?? 0m;
                return new PartnerProductionItem(item.Record.Id, item.Record.Title,
                    item.Record.Customer is null ? "—" :
                        $"{item.Record.Customer.LastName} {item.Record.Customer.FirstName}".Trim(),
                    item.Details.StartDate ?? item.Record.EventDate, premium, rate,
                    decimal.Round(premium * rate / 100m, 2, MidpointRounding.AwayFromZero));
            }).OrderByDescending(item => item.StartDate).ToArray();
        var payouts = await db.PartnerPayouts.AsNoTracking().Where(item => item.PartnerId == partnerId)
            .OrderByDescending(item => item.OccurredAtUtc).ToListAsync(cancellationToken);
        var activities = await db.PartnerActivities.AsNoTracking().Where(item => item.PartnerId == partnerId)
            .OrderByDescending(item => item.OccurredAtUtc).ToListAsync(cancellationToken);
        return new PartnerSnapshot(production, payouts, production.Sum(item => item.Commission),
            payouts.Sum(item => item.Kind == "receipt" ? -item.Amount : item.Amount), activities);
    }

    public async Task<Guid> AddPayoutAsync(Guid partnerId, decimal amount, string method,
        string? reference, string? description, CancellationToken cancellationToken = default)
        => await AddMovementAsync(partnerId, "payment", amount, method, reference,
            description, cancellationToken);

    public async Task<Guid> AddMovementAsync(Guid partnerId, string kind, decimal amount, string method,
        string? reference, string? description, CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (kind is not ("payment" or "receipt")) throw new ArgumentException("Μη έγκυρος τύπος κίνησης.");
        if (amount <= 0 || amount > 999_999_999m) throw new ArgumentException("Συμπληρώστε θετικό ποσό κίνησης.");
        method = method.Trim();
        if (method.Length is < 1 or > 80) throw new ArgumentException("Επιλέξτε τρόπο πληρωμής.");
        reference = Clean(reference, 100);
        description = Clean(description, 500);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.BusinessRecords.AnyAsync(item => item.Id == partnerId &&
            item.SectionKey == "partners", cancellationToken))
            throw new InvalidOperationException("Ο συνεργάτης δεν υπάρχει.");
        var payout = new PartnerPayout
        {
            Id = Guid.NewGuid(), PartnerId = partnerId, Amount = amount, Kind = kind,
            PaymentMethod = method, Reference = reference, Description = description,
            OccurredAtUtc = DateTime.UtcNow, CreatedByUserId = session.Current!.UserId
        };
        db.PartnerPayouts.Add(payout);
        PartnerActivityLog.Add(db, partnerId, kind == "payment" ? "Γενική πληρωμή" : "Γενική είσπραξη",
            $"Καταχωρίστηκε {(kind == "payment" ? "πληρωμή" : "είσπραξη")} {amount:N2} € με {method}" +
            (reference is null ? "." : $" (αναφορά {reference})."), session.Current!.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
        return payout.Id;
    }

    private void RequireSession()
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
    }

    private static string? Clean(string? value, int limit)
    {
        var result = value?.Trim();
        if (result?.Length > limit) throw new ArgumentException($"Το κείμενο πρέπει να έχει έως {limit} χαρακτήρες.");
        return string.IsNullOrEmpty(result) ? null : result;
    }
}

public static class PartnerActivityLog
{
    public static string Label(string key) => key switch
    {
        nameof(PartnerProfileData.FirstName) => "Όνομα",
        nameof(PartnerProfileData.LastName) => "Επώνυμο",
        nameof(PartnerProfileData.CompanyName) => "Επωνυμία",
        nameof(PartnerProfileData.TaxNumber) => "ΑΦΜ",
        nameof(PartnerProfileData.TaxOffice) => "ΔΟΥ",
        nameof(PartnerProfileData.PersonType) => "Τύπος προσώπου",
        nameof(PartnerProfileData.BusinessType) => "Τύπος επιχείρησης",
        nameof(PartnerProfileData.ProfessionalCapacity) => "Επαγγελματική ιδιότητα",
        nameof(PartnerProfileData.IdentityNumber) => "Αριθμός ταυτότητας",
        nameof(PartnerProfileData.GemiNumber) => "Αριθμός ΓΕΜΗ",
        nameof(PartnerProfileData.CooperationManager) => "Υπεύθυνος συνεργασίας",
        nameof(PartnerProfileData.IsPotential) => "Πιθανός συνεργάτης",
        nameof(PartnerProfileData.IsSuspended) => "Αναστολή",
        nameof(PartnerProfileData.HasEnded) => "Λήξη συνεργασίας",
        nameof(PartnerProfileData.HasActiveContract) => "Ενεργή σύμβαση",
        nameof(PartnerProfileData.Category) => "Κατηγορία συνεργάτη",
        nameof(PartnerProfileData.Email) => "Email",
        nameof(PartnerProfileData.Phone) => "Τηλέφωνο",
        nameof(PartnerProfileData.Address) => "Διεύθυνση",
        nameof(PartnerProfileData.City) => "Πόλη",
        nameof(PartnerProfileData.PostalCode) => "Ταχυδρομικός κώδικας",
        nameof(PartnerProfileData.FatherName) => "Πατρώνυμο",
        nameof(PartnerProfileData.MotherName) => "Μητρώνυμο",
        nameof(PartnerProfileData.Gender) => "Φύλο",
        nameof(PartnerProfileData.BirthDate) => "Ημερομηνία γέννησης",
        nameof(PartnerProfileData.BirthPlace) => "Τόπος γέννησης",
        nameof(PartnerProfileData.Nationality) => "Εθνικότητα",
        nameof(PartnerProfileData.PassportNumber) => "Διαβατήριο",
        nameof(PartnerProfileData.Education) => "Εκπαίδευση",
        nameof(PartnerProfileData.RegistryNumber) => "Αριθμός μητρώου",
        nameof(PartnerProfileData.RegistryExpiry) => "Λήξη μητρώου",
        nameof(PartnerProfileData.Chamber) => "Επιμελητήριο",
        nameof(PartnerProfileData.SecondaryEmail) => "Δεύτερο email",
        nameof(PartnerProfileData.SecondaryPhone) => "Δεύτερο τηλέφωνο",
        nameof(PartnerProfileData.Mobile) => "Κινητό",
        nameof(PartnerProfileData.Website) => "Ιστότοπος",
        nameof(PartnerProfileData.WorkAddress) => "Διεύθυνση εργασίας",
        nameof(PartnerProfileData.WorkCity) => "Πόλη εργασίας",
        nameof(PartnerProfileData.WorkPostalCode) => "ΤΚ εργασίας",
        nameof(PartnerProfileData.WorkPhone) => "Τηλέφωνο εργασίας",
        nameof(PartnerProfileData.WorkEmail) => "Email εργασίας",
        nameof(PartnerProfileData.ContractNumber) => "Αριθμός σύμβασης",
        nameof(PartnerProfileData.StartDate) => "Έναρξη συνεργασίας",
        nameof(PartnerProfileData.EndDate) => "Λήξη συνεργασίας",
        nameof(PartnerProfileData.CreditLimit) => "Πιστωτικό όριο",
        nameof(PartnerProfileData.DefaultCommissionPercent) => "Ποσοστό προμήθειας",
        nameof(PartnerProfileData.BankName) => "Τράπεζα",
        nameof(PartnerProfileData.Iban) => "IBAN",
        nameof(PartnerProfileData.BankAccounts) => "Τραπεζικοί λογαριασμοί",
        nameof(PartnerProfileData.PaymentMethod) => "Τρόπος πληρωμής",
        nameof(PartnerProfileData.Notes) => "Σημειώσεις",
        _ => "Στοιχείο συνεργάτη"
    };

    public static string Value(object? value) => value switch
    {
        null => "—", string text when string.IsNullOrWhiteSpace(text) => "—",
        IEnumerable<PartnerBankAccount> accounts => $"{accounts.Count()} λογαριασμοί",
        bool flag => flag ? "Ναι" : "Όχι", DateTime date => date.ToString("dd/MM/yyyy"),
        decimal amount => amount.ToString("N2"),
        _ => value.ToString()?.Length > 120 ? value.ToString()![..120] + "…" : value.ToString() ?? "—"
    };

    public static void Add(IApplicationDbContext db, Guid partnerId, string action,
        string description, string actor)
    {
        db.PartnerActivities.Add(new PartnerActivity
        {
            Id = Guid.NewGuid(), PartnerId = partnerId, Action = action,
            Description = description.Length > 4000 ? description[..4000] : description,
            ActorName = actor, OccurredAtUtc = DateTime.UtcNow
        });
    }
}

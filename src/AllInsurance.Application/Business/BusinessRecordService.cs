using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Commissions;
using AllInsurance.Application.Partners;
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
        record.DetailsJson = Normalize(record.DetailsJson, 50000);
        if (record.SectionKey == "policies" && record.DocumentType is not null &&
            record.DocumentType is not ("Αίτηση" or "Ειδοποιητήριο" or "Ασφαλιστήριο"))
            throw new ArgumentException("Μη έγκυρος τύπος εγγράφου ασφάλισης.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (record.SectionKey == "partners" && record.DetailsJson is not null)
        {
            var profile = PartnerProfileData.Read(record.DetailsJson);
            if (profile.TaxNumber is not { Length: 9 } || !profile.TaxNumber.All(char.IsDigit))
                throw new ArgumentException("Ο ΑΦΜ συνεργάτη πρέπει να έχει 9 ψηφία.");
            var otherProfiles = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "partners" && item.Id != record.Id &&
                    item.DetailsJson != null)
                .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
            if (otherProfiles.Any(json => PartnerProfileData.Read(json).TaxNumber == profile.TaxNumber))
                throw new InvalidOperationException("Υπάρχει ήδη συνεργάτης με αυτόν τον ΑΦΜ.");
        }
        if (record.CustomerId is Guid customerId &&
            !await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
            throw new ArgumentException("Ο συνδεδεμένος πελάτης δεν υπάρχει.");
        if (record.SectionKey == "vehicles" && record.DetailsJson is not null)
        {
            var vehicle = BusinessRecordDetails.Read<VehicleCardData>(record.DetailsJson);
            var people = vehicle.People ?? [];
            if (people.Count > 0)
            {
                if (people.Select(item => item.CustomerId).Distinct().Count() != people.Count ||
                    people.Count(item => item.IsPrimaryOwner) != 1 ||
                    people.Single(item => item.IsPrimaryOwner).CustomerId != record.CustomerId ||
                    people.Any(item => item.OwnershipPercent is < 0 or > 100 ||
                        item.Role is not ("Ιδιοκτήτης" or "Συνιδιοκτήτης" or "Οδηγός" or "Ιδιοκτήτης και οδηγός") ||
                        item.Role == "Οδηγός" && item.OwnershipPercent != 0) ||
                    people.Sum(item => item.OwnershipPercent) > 100)
                    throw new ArgumentException("Τα πρόσωπα και τα ποσοστά ιδιοκτησίας του οχήματος δεν είναι έγκυρα.");
                var personIds = people.Select(item => item.CustomerId).ToArray();
                var found = await db.Customers.CountAsync(item => personIds.Contains(item.Id), cancellationToken);
                if (found != personIds.Length)
                    throw new ArgumentException("Κάποιο πρόσωπο του οχήματος δεν υπάρχει στους πελάτες.");
            }
        }
        if (record.SectionKey == "claims" && record.DetailsJson is not null)
        {
            var vehicleId = BusinessRecordDetails.Read<ClaimCardData>(record.DetailsJson).VehicleId;
            if (vehicleId is Guid linkedVehicle && !await db.BusinessRecords.AnyAsync(item =>
                item.Id == linkedVehicle && item.SectionKey == "vehicles" &&
                item.CustomerId == record.CustomerId, cancellationToken))
                throw new ArgumentException("Η ζημιά πρέπει να συνδέεται με όχημα του ίδιου πελάτη.");
        }
        if (record.SectionKey == "policies")
        {
            var details = BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson);
            var vehicleId = details.VehicleId;
            if (vehicleId is Guid id && !await db.BusinessRecords.AnyAsync(item =>
                item.Id == id && item.SectionKey == "vehicles" && item.CustomerId == record.CustomerId,
                cancellationToken))
                throw new ArgumentException("Το όχημα πρέπει να ανήκει στον ίδιο πελάτη.");
            if (details.PartnerId is Guid explicitPartner &&
                !await db.BusinessRecords.AnyAsync(item => item.Id == explicitPartner &&
                    item.SectionKey == "partners", cancellationToken))
                throw new ArgumentException("Ο συνεργάτης του ασφαλιστηρίου δεν υπάρχει.");
            if (record.DocumentType is null or "Ασφαλιστήριο" &&
                details.PartnerId is null && record.CustomerId is Guid linkedCustomer)
            {
                var partnerName = await db.Customers.AsNoTracking()
                    .Where(item => item.Id == linkedCustomer).Select(item => item.PartnerName)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(partnerName))
                {
                    var partner = await db.BusinessRecords.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.SectionKey == "partners" &&
                            item.Title == partnerName, cancellationToken);
                    if (partner is not null)
                    {
                        details.PartnerId = partner.Id;
                        details.PartnerCommissionPercent =
                            PartnerProfileData.Read(partner.DetailsJson).DefaultCommissionPercent;
                        record.DetailsJson = BusinessRecordDetails.Write(details);
                    }
                }
            }
            if (record.DocumentType is null or "Ασφαλιστήριο" &&
                details.OfficeCommissionPercent is null &&
                !string.IsNullOrWhiteSpace(details.Insurer))
            {
                string? vehicleUse = null;
                if (details.VehicleId is Guid linkedVehicleId)
                {
                    var vehicleJson = await db.BusinessRecords.AsNoTracking()
                        .Where(item => item.Id == linkedVehicleId)
                        .Select(item => item.DetailsJson).SingleOrDefaultAsync(cancellationToken);
                    vehicleUse = BusinessRecordDetails.Read<VehicleCardData>(vehicleJson).Use;
                }
                var rule = await new CommissionRuleService(factory, session).ResolveAsync(
                    new CommissionMatch(details.Insurer, details.Branch, vehicleUse,
                        details.CoverageNotes, details.Product), cancellationToken);
                if (rule is not null)
                {
                    details.CommissionRuleId = rule.Id;
                    details.OfficeCommissionPercent = rule.OfficePercent;
                    if (details.PartnerId is not null)
                        details.PartnerCommissionPercent = rule.PartnerPercent;
                }
            }
            record.DetailsJson = BusinessRecordDetails.Write(details);
        }
        var now = DateTime.UtcNow;
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
            record.CreatedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.CreatedByUserId = session.Current!.UserId;
            db.BusinessRecords.Add(record);
            if (record.SectionKey == "partners")
                PartnerActivityLog.Add(db, record.Id, "Δημιουργία συνεργάτη",
                    $"Δημιουργήθηκε η καρτέλα {record.Title}.", session.Current!.DisplayName);
            if (record.SectionKey == "policies" &&
                (record.DocumentType is null or "Ασφαλιστήριο") &&
                BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson).PartnerId is Guid newPartnerId)
                PartnerActivityLog.Add(db, newPartnerId, "Νέα παραγωγή",
                    $"Καταχωρίστηκε ασφαλιστήριο {record.Title}.", session.Current!.DisplayName);
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
            if (record.SectionKey == "partners")
            {
                if (existing.Title != record.Title)
                {
                    var assigned = await db.Customers.Where(item => item.PartnerName == existing.Title ||
                            item.ReferredByPartnerName == existing.Title)
                        .ToListAsync(cancellationToken);
                    foreach (var customer in assigned)
                    {
                        if (customer.PartnerName == existing.Title) customer.PartnerName = record.Title;
                        if (customer.ReferredByPartnerName == existing.Title)
                            customer.ReferredByPartnerName = record.Title;
                        customer.UpdatedAtUtc = now;
                        CustomerActivityLog.Add(db, customer.Id, "Αλλαγή συνεργάτη",
                            $"Ο συνεργάτης μετονομάστηκε: {existing.Title} → {record.Title}.",
                            session.Current!.DisplayName);
                    }
                }
                var before = PartnerProfileData.Read(existing.DetailsJson);
                var after = PartnerProfileData.Read(record.DetailsJson);
                var changesInProfile = typeof(PartnerProfileData).GetProperties()
                    .Where(property => !Equals(property.GetValue(before), property.GetValue(after)))
                    .ToArray();
                if (existing.Title != record.Title)
                    PartnerActivityLog.Add(db, record.Id, "Επεξεργασία συνεργάτη",
                        $"Ονομασία: {existing.Title} → {record.Title}", session.Current!.DisplayName);
                if (existing.Status != record.Status)
                    PartnerActivityLog.Add(db, record.Id, "Επεξεργασία συνεργάτη",
                        $"Κατάσταση: {existing.Status ?? "—"} → {record.Status ?? "—"}",
                        session.Current!.DisplayName);
                foreach (var property in changesInProfile)
                    PartnerActivityLog.Add(db, record.Id, "Επεξεργασία συνεργάτη",
                        $"{PartnerActivityLog.Label(property.Name)}: " +
                        $"{PartnerActivityLog.Value(property.GetValue(before))} → " +
                        PartnerActivityLog.Value(property.GetValue(after)),
                        session.Current!.DisplayName);
            }
            var changes = new List<string>();
            if (existing.Title != record.Title) changes.Add($"Αριθμός: {existing.Title} → {record.Title}");
            if (existing.Status != record.Status)
                changes.Add($"Κατάσταση: {existing.Status ?? "—"} → {record.Status ?? "—"}");
            if (existing.DocumentType != record.DocumentType)
                changes.Add($"Τύπος: {existing.DocumentType ?? "—"} → {record.DocumentType ?? "—"}");
            if (existing.DetailsJson != record.DetailsJson) changes.Add("Ενημερώθηκαν τα αναλυτικά στοιχεία.");
            if (existing.Notes != record.Notes) changes.Add("Ενημερώθηκαν οι σημειώσεις.");
            if (record.SectionKey == "policies")
            {
                var before = BusinessRecordDetails.Read<PolicyCardData>(existing.DetailsJson);
                var after = BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson);
                var wasIssued = previousType is null or "Ασφαλιστήριο";
                var isIssued = record.DocumentType is null or "Ασφαλιστήριο";
                if (wasIssued && before.PartnerId is Guid oldPartner &&
                    (!isIssued || oldPartner != after.PartnerId))
                    PartnerActivityLog.Add(db, oldPartner, "Μεταβολή παραγωγής",
                        $"Το ασφαλιστήριο {existing.Title} αποσυνδέθηκε από την παραγωγή.",
                        session.Current!.DisplayName);
                if (isIssued && after.PartnerId is Guid currentPartner &&
                    (!wasIssued || before.PartnerId != currentPartner))
                    PartnerActivityLog.Add(db, currentPartner, "Νέα παραγωγή",
                        $"Το ασφαλιστήριο {record.Title} προστέθηκε στην παραγωγή.",
                        session.Current!.DisplayName);
                else if (isIssued && after.PartnerId is Guid samePartner &&
                    (existing.Title != record.Title || existing.DetailsJson != record.DetailsJson))
                    PartnerActivityLog.Add(db, samePartner, "Μεταβολή παραγωγής",
                        $"Ενημερώθηκε το ασφαλιστήριο {record.Title} και η προμήθειά του.",
                        session.Current!.DisplayName);
            }
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
        if (record.SectionKey == "partners")
        {
            if (await db.PartnerPayouts.AnyAsync(item => item.PartnerId == id, cancellationToken) ||
                await db.Customers.AnyAsync(item => item.PartnerName == record.Title ||
                    item.ReferredByPartnerName == record.Title, cancellationToken))
                throw new InvalidOperationException("Ο συνεργάτης έχει οικονομικές κινήσεις ή πελάτες. Θέστε τον ανενεργό αντί να τον διαγράψετε.");
            var policiesOfPartner = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "policies" && item.DetailsJson != null)
                .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
            if (policiesOfPartner.Any(json => BusinessRecordDetails.Read<PolicyCardData>(json).PartnerId == id))
                throw new InvalidOperationException("Ο συνεργάτης έχει συνδεδεμένα ασφαλιστήρια και δεν μπορεί να διαγραφεί.");
        }
        if (record.SectionKey == "policies" &&
            await db.CustomerTransactions.AnyAsync(item => item.BusinessRecordId == id, cancellationToken))
            throw new InvalidOperationException("Το ασφαλιστήριο έχει συνδεδεμένες οικονομικές κινήσεις και δεν μπορεί να διαγραφεί.");
        if (record.SectionKey == "vehicles")
        {
            var claims = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "claims" && item.DetailsJson != null)
                .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
            if (claims.Any(json => BusinessRecordDetails.Read<ClaimCardData>(json).VehicleId == id))
                throw new InvalidOperationException("Το όχημα έχει συνδεδεμένες ζημιές και δεν μπορεί να διαγραφεί.");
            var policies = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "policies" && item.DetailsJson != null)
                .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
            if (policies.Any(json => BusinessRecordDetails.Read<PolicyCardData>(json).VehicleId == id))
                throw new InvalidOperationException("Το όχημα συνδέεται με αίτηση ή ασφαλιστήριο και δεν μπορεί να διαγραφεί.");
        }
        if (record.SectionKey == "policies" &&
            BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson).PartnerId is Guid partnerId)
            PartnerActivityLog.Add(db, partnerId, "Διαγραφή παραγωγής",
                $"Διαγράφηκε το ασφαλιστήριο {record.Title}.", session.Current!.DisplayName);
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

using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.IO;
using AllInsurance.Application.Settings;

namespace AllInsurance.Application.Customers;

public sealed class CustomerService(IApplicationDbContextFactory factory, ISessionService session,
    string? fileRoot = null) : ICustomerService
{
    private readonly string _fileRoot = fileRoot ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "CustomerFiles");
    private static readonly HashSet<string> Categories =
    [
        "policies", "vehicles", "boats", "claims", "phones", "addresses",
        "emails", "persons", "other", "files", "contacts"
    ];

    public async Task<IReadOnlyList<CustomerListItem>> SearchAsync(string? query = null, bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var customers = db.Customers.AsNoTracking().Include(customer => customer.Transactions).AsQueryable();
        if (!includeInactive)
        {
            customers = customers.Where(customer => customer.IsActive);
        }
        var term = query?.Trim();
        var codeSearch = term?.StartsWith("C-", StringComparison.OrdinalIgnoreCase) == true;
        if (!string.IsNullOrWhiteSpace(term) && !codeSearch)
        {
            customers = customers.Where(customer =>
                customer.LastName.Contains(term) || customer.FirstName.Contains(term) ||
                (customer.CompanyName != null && customer.CompanyName.Contains(term)) ||
                (customer.TaxNumber != null && customer.TaxNumber.Contains(term)) ||
                (customer.PrimaryMobile != null && customer.PrimaryMobile.Contains(term)) ||
                (customer.PrimaryEmail != null && customer.PrimaryEmail.Contains(term)) ||
                (customer.PrimaryAddress != null && customer.PrimaryAddress.Contains(term)) ||
                (customer.City != null && customer.City.Contains(term)));
        }
        var records = await customers.OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName).ToListAsync(cancellationToken);
        var currency = await db.AppMetadata.AsNoTracking()
            .Where(item => item.Key == TransactionSettingsService.CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
        var results = records.Select(customer => new CustomerListItem(
            customer.Id, $"{customer.LastName} {customer.FirstName}", customer.TaxNumber,
            customer.PrimaryMobile, customer.PrimaryEmail, customer.IsActive,
            Balance(customer), customer.CreatedAtUtc, customer.GroupName,
            customer.PartnerName, customer.BelongsToOffice,
            customer.LastName, customer.FirstName, customer.CompanyName,
            customer.CustomerKind, customer.City)
        {
            Currency = currency,
            SearchText = string.Join(' ', new[] { customer.LastName, customer.FirstName,
                customer.CompanyName, customer.TaxNumber, customer.PrimaryMobile,
                customer.HomePhone, customer.SecondMobile, customer.ThirdMobile, customer.SecondPhone,
                customer.ThirdPhone, customer.PrimaryEmail, customer.SecondEmail,
                customer.ThirdEmail, customer.BusinessEmail, customer.BusinessPhone,
                customer.PrimaryAddress, customer.City, customer.Region,
                customer.PostalCode, customer.Occupation, customer.PartnerName,
                customer.GroupName, customer.IdentityNumber, customer.LicenseNumber,
                customer.Tags, customer.PassportNumber, customer.Zone }
                .Where(value => !string.IsNullOrWhiteSpace(value))!)
        }).ToArray();
        return codeSearch ? results.Where(item => item.Code.StartsWith(term!,
            StringComparison.OrdinalIgnoreCase)).ToArray() : results;
    }

    public async Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AsNoTracking()
            .Include(customer => customer.Details)
            .Include(customer => customer.Transactions)
            .Include(customer => customer.BusinessRecords)
            .AsSplitQuery()
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerActivity>> GetActivitiesAsync(Guid customerId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerActivities.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        RequireSession();
        Validate(customer);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (customer.TaxNumber is not null &&
            await db.Customers.AnyAsync(existing => existing.Id != customer.Id &&
                existing.TaxNumber == customer.TaxNumber, cancellationToken))
        {
            throw new InvalidOperationException("Υπάρχει ήδη πελάτης με αυτό το ΑΦΜ.");
        }
        if (customer.ReferredByCustomerId is Guid referrerId)
        {
            if (referrerId == customer.Id ||
                !await db.Customers.AnyAsync(existing => existing.Id == referrerId, cancellationToken))
            {
                throw new ArgumentException("Ο πελάτης που έκανε τη σύσταση δεν είναι έγκυρος.");
            }
        }
        if (!string.IsNullOrWhiteSpace(customer.ReferredByPartnerName) &&
            !await db.BusinessRecords.AnyAsync(item => item.SectionKey == "partners" &&
                item.Title == customer.ReferredByPartnerName, cancellationToken))
            throw new ArgumentException("Ο συνεργάτης που έκανε τη σύσταση δεν υπάρχει.");
        var now = DateTime.UtcNow;
        if (customer.Id == Guid.Empty)
        {
            customer.Id = Guid.NewGuid();
            customer.CreatedAtUtc = now;
            customer.UpdatedAtUtc = now;
            customer.CreatedByUserId = session.Current!.UserId;
            db.Customers.Add(customer);
            CustomerActivityLog.Add(db, customer.Id, "Δημιουργία πελάτη",
                "Δημιουργήθηκε η καρτέλα πελάτη.", session.Current!.DisplayName);
        }
        else
        {
            var existing = await db.Customers.SingleOrDefaultAsync(item => item.Id == customer.Id, cancellationToken)
                ?? throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
            if (existing.UpdatedAtUtc != customer.UpdatedAtUtc)
            {
                throw new InvalidOperationException("Η καρτέλα έχει αλλάξει από άλλη ενέργεια. Ανοίξτε την ξανά.");
            }
            var id = existing.Id;
            var created = existing.CreatedAtUtc;
            var creator = existing.CreatedByUserId;
            db.Customers.Entry(existing).CurrentValues.SetValues(customer);
            var changed = db.Entry(existing).Properties
                .Where(property => property.IsModified && property.Metadata.Name is not
                    (nameof(Customer.UpdatedAtUtc) or nameof(Customer.CreatedAtUtc) or
                     nameof(Customer.CreatedByUserId) or nameof(Customer.Id)))
                .ToArray();
            foreach (var property in changed)
                CustomerActivityLog.Add(db, customer.Id, "Επεξεργασία πελάτη",
                    $"{CustomerActivityLog.FieldLabel(property.Metadata.Name)}: " +
                    $"{CustomerActivityLog.FormatValue(property.OriginalValue)} → " +
                    CustomerActivityLog.FormatValue(property.CurrentValue),
                    session.Current!.DisplayName);
            existing.Id = id;
            existing.CreatedAtUtc = created;
            existing.CreatedByUserId = creator;
            existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
        if (await db.Customers.AnyAsync(item => item.ReferredByCustomerId == id, cancellationToken))
        {
            throw new InvalidOperationException("Ο πελάτης έχει ενεργές συστάσεις. Αφαιρέστε πρώτα τις συσχετίσεις.");
        }
        var vehiclePeople = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "vehicles" && item.DetailsJson != null)
            .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
        if (vehiclePeople.Any(json => (BusinessRecordDetails.Read<VehicleCardData>(json).People ?? [])
            .Any(person => person.CustomerId == id)))
            throw new InvalidOperationException("Ο πελάτης συνδέεται με όχημα ως ιδιοκτήτης ή οδηγός.");
        var policyLinks = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "policies" && item.DetailsJson != null)
            .Select(item => item.DetailsJson).ToListAsync(cancellationToken);
        if (policyLinks.Any(json => BusinessRecordDetails.Read<PolicyCardData>(json).HolderCustomerId == id))
            throw new InvalidOperationException("Ο πελάτης είναι κάτοχος ασφαλιστηρίου. Αφαιρέστε πρώτα τη σύνδεση.");
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
        var folder = Path.Combine(_fileRoot, id.ToString("N"));
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    public async Task<CustomerDetail> AddDetailAsync(Guid customerId, string category, string title, string? value,
        string? notes, DateTime? effectiveFrom, DateTime? effectiveUntil, bool isHistorical,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (!Categories.Contains(category) || string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new ArgumentException("Συμπληρώστε έγκυρη κατηγορία και τίτλο.");
        }
        if (effectiveFrom > effectiveUntil)
        {
            throw new ArgumentException("Η ημερομηνία λήξης πρέπει να είναι μετά την ημερομηνία έναρξης.");
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
        }
        var detail = new CustomerDetail
        {
            Id = Guid.NewGuid(), CustomerId = customerId, Category = category,
            Title = title.Trim(), Value = Normalize(value, 1000), Notes = Normalize(notes, 2000),
            EffectiveFrom = effectiveFrom, EffectiveUntil = effectiveUntil,
            IsHistorical = isHistorical, CreatedAtUtc = DateTime.UtcNow
        };
        db.CustomerDetails.Add(detail);
        CustomerActivityLog.Add(db, customerId, "Προσθήκη στοιχείου",
            $"Προστέθηκε στοιχείο στην κατηγορία {CustomerActivityLog.CategoryLabel(category)}: {detail.Title}.", session.Current!.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
        return detail;
    }

    public async Task DeleteDetailAsync(Guid customerId, Guid detailId, CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var detail = await db.CustomerDetails.SingleOrDefaultAsync(item => item.Id == detailId &&
            item.CustomerId == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Η σχετική καταχώριση δεν υπάρχει.");
        db.CustomerDetails.Remove(detail);
        CustomerActivityLog.Add(db, customerId, "Διαγραφή στοιχείου",
            $"Διαγράφηκε στοιχείο από την κατηγορία {CustomerActivityLog.CategoryLabel(detail.Category)}: {detail.Title}.",
            session.Current!.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
        if (detail.Category == "files")
        {
            var path = GetFilePath(detail);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public async Task<CustomerDetail> AddFileAsync(Guid customerId, string sourcePath,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var source = new FileInfo(sourcePath);
        if (!source.Exists || source.Length > 50 * 1024 * 1024)
            throw new ArgumentException("Το αρχείο δεν υπάρχει ή υπερβαίνει τα 50 MB.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
            throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
        var id = Guid.NewGuid();
        var extension = source.Extension.Length <= 12 ? source.Extension : "";
        var relative = Path.Combine(customerId.ToString("N"), id.ToString("N") + extension);
        var destination = Path.Combine(_fileRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            await using (var input = source.OpenRead())
            await using (var output = File.Create(destination))
                await input.CopyToAsync(output, cancellationToken);
            var detail = new CustomerDetail
            {
                Id = id, CustomerId = customerId, Category = "files",
                Title = Normalize(source.Name, 200)!, Value = relative,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.CustomerDetails.Add(detail);
            CustomerActivityLog.Add(db, customerId, "Επισύναψη αρχείου",
                $"Επισυνάφθηκε αρχείο: {detail.Title}.", session.Current!.DisplayName);
            await db.SaveChangesAsync(cancellationToken);
            return detail;
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    public string GetFilePath(CustomerDetail file)
    {
        RequireSession();
        if (file.Category != "files" || string.IsNullOrWhiteSpace(file.Value))
            throw new ArgumentException("Δεν είναι συνημμένο αρχείο.");
        var fullRoot = Path.GetFullPath(_fileRoot) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_fileRoot, file.Value));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Μη έγκυρη διαδρομή αρχείου.");
        return fullPath;
    }

    public async Task UpdateDetailAsync(Guid customerId, Guid detailId, string title, string? value,
        string? notes, DateTime? effectiveFrom, DateTime? effectiveUntil, bool isHistorical,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || effectiveFrom > effectiveUntil)
        {
            throw new ArgumentException("Ελέγξτε τον τίτλο και τις ημερομηνίες.");
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var detail = await db.CustomerDetails.SingleOrDefaultAsync(item => item.Id == detailId &&
            item.CustomerId == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Η σχετική καταχώριση δεν υπάρχει.");
        detail.Title = title.Trim();
        detail.Value = Normalize(value, 1000);
        detail.Notes = Normalize(notes, 2000);
        detail.EffectiveFrom = effectiveFrom;
        detail.EffectiveUntil = effectiveUntil;
        detail.IsHistorical = isHistorical;
        CustomerActivityLog.Add(db, customerId, "Επεξεργασία στοιχείου",
            $"Ενημερώθηκε στοιχείο της κατηγορίας {CustomerActivityLog.CategoryLabel(detail.Category)}: {detail.Title}.",
            session.Current!.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerTransaction> AddTransactionAsync(Guid customerId, string type, decimal amount,
        string? description, string? paymentMethod = null, string? reference = null,
        Guid? businessRecordId = null,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (type is not ("receipt" or "payment") || amount <= 0 || amount > 999999999999m)
        {
            throw new ArgumentException("Συμπληρώστε έγκυρο είδος και θετικό ποσό.");
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
        }
        string? policyTitle = null;
        if (businessRecordId is Guid policyId)
        {
            policyTitle = await db.BusinessRecords.AsNoTracking()
                .Where(record => record.Id == policyId && record.CustomerId == customerId &&
                    record.SectionKey == "policies" &&
                    (record.DocumentType == null || record.DocumentType == "Ασφαλιστήριο"))
                .Select(record => record.Title).SingleOrDefaultAsync(cancellationToken);
            if (policyTitle is null)
                throw new ArgumentException("Επιλέξτε ασφαλιστήριο του συγκεκριμένου πελάτη.");
        }
        var method = Normalize(paymentMethod, 80) ?? "Μετρητά";
        var currency = await db.AppMetadata.AsNoTracking()
            .Where(item => item.Key == TransactionSettingsService.CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
        var transaction = new CustomerTransaction
        {
            Id = Guid.NewGuid(), CustomerId = customerId, Type = type, Amount = amount,
            PaymentMethod = method, Currency = currency, Reference = Normalize(reference, 100),
            BusinessRecordId = businessRecordId,
            Description = Normalize(description, 500), OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = session.Current!.UserId
        };
        db.CustomerTransactions.Add(transaction);
        var transactionReference = string.IsNullOrWhiteSpace(transaction.Reference)
            ? string.Empty : $" · Αρ. συναλλαγής: {transaction.Reference}";
        CustomerActivityLog.Add(db, customerId,
            type == "receipt" ? "Είσπραξη" : "Πληρωμή",
            $"{(type == "receipt" ? "Είσπραξη" : "Πληρωμή")} {amount:N2} {currency} με {method}" +
            (policyTitle is null ? "." : $" για {policyTitle}.") + transactionReference,
            session.Current!.DisplayName);
        if (businessRecordId is Guid linkedPolicyId)
            db.BusinessRecordActivities.Add(new BusinessRecordActivity
            {
                Id = Guid.NewGuid(), RecordId = linkedPolicyId,
                Action = type == "receipt" ? "Είσπραξη" : "Πληρωμή προς πελάτη",
                Description = $"{amount:N2} {currency} με {method} για {policyTitle}." +
                    transactionReference,
                ActorName = session.Current!.DisplayName, OccurredAtUtc = transaction.OccurredAtUtc
            });
        await db.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task TransferCreditAsync(Guid fromCustomerId, Guid toCustomerId, decimal amount,
        Guid? targetPolicyId = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (fromCustomerId == toCustomerId || amount <= 0 || amount > 999999999999m)
            throw new ArgumentException("Επιλέξτε διαφορετικό πελάτη και έγκυρο θετικό ποσό.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var customers = await db.Customers.Include(item => item.Transactions)
            .Where(item => item.Id == fromCustomerId || item.Id == toCustomerId)
            .ToArrayAsync(cancellationToken);
        var source = customers.SingleOrDefault(item => item.Id == fromCustomerId)
            ?? throw new ArgumentException("Ο πελάτης προέλευσης δεν υπάρχει.");
        var target = customers.SingleOrDefault(item => item.Id == toCustomerId)
            ?? throw new ArgumentException("Ο πελάτης προορισμού δεν υπάρχει.");
        if (Balance(source) >= 0 || amount > -Balance(source))
            throw new ArgumentException("Το ποσό υπερβαίνει το διαθέσιμο πιστωτικό υπόλοιπο του πελάτη.");
        if (targetPolicyId is Guid policyId && !await db.BusinessRecords.AnyAsync(item =>
                item.Id == policyId && item.CustomerId == toCustomerId && item.SectionKey == "policies",
                cancellationToken))
            throw new ArgumentException("Το ασφαλιστήριο δεν ανήκει στον πελάτη προορισμού.");
        var currency = await db.AppMetadata.AsNoTracking()
            .Where(item => item.Key == TransactionSettingsService.CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
        var reference = "TR-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var description = Normalize(reason, 350);
        db.CustomerTransactions.AddRange(
            new CustomerTransaction { Id = Guid.NewGuid(), CustomerId = source.Id,
                Type = "credit_transfer_out", Amount = amount, PaymentMethod = "Εσωτερική μεταφορά",
                Currency = currency, Reference = reference, Description =
                    $"Μεταφορά πιστωτικού προς {target.LastName} {target.FirstName}. {description}",
                OccurredAtUtc = now, CreatedByUserId = session.Current!.UserId },
            new CustomerTransaction { Id = Guid.NewGuid(), CustomerId = target.Id,
                Type = "credit_transfer_in", Amount = amount, PaymentMethod = "Εσωτερική μεταφορά",
                Currency = currency, Reference = reference, BusinessRecordId = targetPolicyId,
                Description = $"Πιστωτικό από {source.LastName} {source.FirstName}. {description}",
                OccurredAtUtc = now, CreatedByUserId = session.Current!.UserId });
        CustomerActivityLog.Add(db, source.Id, "Μεταφορά πιστωτικού",
            $"{amount:N2} {currency} προς {target.LastName} {target.FirstName} · {reference}.",
            session.Current!.DisplayName);
        CustomerActivityLog.Add(db, target.Id, "Λήψη πιστωτικού",
            $"{amount:N2} {currency} από {source.LastName} {source.FirstName} · {reference}.",
            session.Current!.DisplayName);
        if (targetPolicyId is Guid linkedPolicyId)
            db.BusinessRecordActivities.Add(new BusinessRecordActivity { Id = Guid.NewGuid(),
                RecordId = linkedPolicyId, Action = "Εσωτερική μεταφορά πιστωτικού",
                Description = $"{amount:N2} {currency} από {source.LastName} {source.FirstName} · {reference}.",
                ActorName = session.Current!.DisplayName, OccurredAtUtc = now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CustomerStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var customers = await db.Customers.AsNoTracking().Include(customer => customer.Transactions)
            .ToListAsync(cancellationToken);
        var balances = customers.Select(Balance).ToArray();
        return new CustomerStatistics(
            customers.Count, customers.Count(customer => customer.IsActive),
            customers.Count(customer => !customer.IsActive), balances.Count(balance => balance > 0),
            balances.Where(balance => balance > 0).Sum(),
            -balances.Where(balance => balance < 0).Sum(),
            customers.Count(customer => customer.IsReferral),
            customers.Count(customer => customer.IsDriver));
    }

    public async Task<IReadOnlyList<CustomerRelatedRecord>> GetRelatedRecordsAsync(string category,
        string? query = null, CancellationToken cancellationToken = default)
    {
        RequireSession();
        if (!Categories.Contains(category)) throw new ArgumentException("Μη έγκυρη ενότητα.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = db.CustomerDetails.AsNoTracking()
            .Include(detail => detail.Customer)
            .Where(detail => detail.Category == category);
        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            records = records.Where(detail => detail.Title.Contains(term) ||
                (detail.Value != null && detail.Value.Contains(term)) ||
                detail.Customer.LastName.Contains(term) ||
                detail.Customer.FirstName.Contains(term));
        }
        var rows = await records.OrderByDescending(detail => detail.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(detail => new CustomerRelatedRecord(
            detail.CustomerId, $"{detail.Customer.LastName} {detail.Customer.FirstName}",
            detail.Category, detail.Title, detail.Value, detail.IsHistorical,
            detail.CreatedAtUtc)).ToArray();
    }

    public static decimal Balance(Customer customer) => customer.OpeningBalance +
        customer.Transactions.Sum(transaction =>
            transaction.Type is "payment" or "credit_transfer_out" ? transaction.Amount : -transaction.Amount);

    private void RequireSession()
    {
        if (!session.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
        }
    }

    private static void Validate(Customer customer)
    {
        customer.LastName = Normalize(customer.LastName, 150) ??
            throw new ArgumentException("Το επώνυμο είναι υποχρεωτικό.");
        customer.FirstName = Normalize(customer.FirstName, 150) ??
            throw new ArgumentException("Το όνομα είναι υποχρεωτικό.");
        customer.TaxNumber = string.IsNullOrWhiteSpace(customer.TaxNumber)
            ? null : customer.TaxNumber.Trim();
        customer.CustomerKind = Normalize(customer.CustomerKind, 40) ?? "Φυσικό πρόσωπο";
        if (customer.TaxNumber is not null &&
            (customer.TaxNumber.Length != 9 || !customer.TaxNumber.All(char.IsAsciiDigit)))
        {
            throw new ArgumentException("Το ΑΦΜ πρέπει να έχει 9 ψηφία.");
        }
        foreach (var email in new[] { customer.PrimaryEmail, customer.SecondEmail,
            customer.ThirdEmail, customer.BusinessEmail })
        {
            if (!string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email, out _))
            {
                throw new ArgumentException("Μία διεύθυνση email δεν είναι έγκυρη.");
            }
        }
        if (customer.BirthDate > DateTime.UtcNow.Date || customer.LicenseStartDate > customer.LicenseEndDate)
        {
            throw new ArgumentException("Ελέγξτε τις ημερομηνίες γέννησης και διπλώματος.");
        }
        if (customer.OpeningBalance is < -999999999999m or > 999999999999m)
        {
            throw new ArgumentException("Το αρχικό υπόλοιπο είναι εκτός ορίων.");
        }
        customer.IsReferral = customer.ReferredByCustomerId.HasValue ||
            !string.IsNullOrWhiteSpace(customer.ReferredByPartnerName) || customer.Source == "Σύσταση";
        if (!customer.IsReferral)
        {
            customer.ReferredByCustomerId = null;
            customer.ReferredByPartnerName = null;
        }
        if (customer.BelongsToOffice) customer.PartnerName = null;
        else if (string.IsNullOrWhiteSpace(customer.PartnerName))
            throw new ArgumentException("Επιλέξτε συνεργάτη ή δηλώστε ότι ο πελάτης ανήκει στο γραφείο.");
    }

    private static string? Normalize(string? value, int maximumLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > maximumLength) throw new ArgumentException($"Το πεδίο δεν μπορεί να υπερβαίνει τους {maximumLength} χαρακτήρες.");
        return text;
    }
}

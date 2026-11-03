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
        "emails", "persons", "other", "files"
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
                customer.HomePhone, customer.SecondMobile, customer.SecondPhone,
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
        var now = DateTime.UtcNow;
        if (customer.Id == Guid.Empty)
        {
            customer.Id = Guid.NewGuid();
            customer.CreatedAtUtc = now;
            customer.UpdatedAtUtc = now;
            customer.CreatedByUserId = session.Current!.UserId;
            db.Customers.Add(customer);
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
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerTransaction> AddTransactionAsync(Guid customerId, string type, decimal amount,
        string? description, string? paymentMethod = null, string? reference = null,
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
        var method = Normalize(paymentMethod, 80) ?? "Μετρητά";
        var currency = await db.AppMetadata.AsNoTracking()
            .Where(item => item.Key == TransactionSettingsService.CurrencyKey)
            .Select(item => item.Value).SingleOrDefaultAsync(cancellationToken) ?? "EUR";
        var transaction = new CustomerTransaction
        {
            Id = Guid.NewGuid(), CustomerId = customerId, Type = type, Amount = amount,
            PaymentMethod = method, Currency = currency, Reference = Normalize(reference, 100),
            Description = Normalize(description, 500), OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = session.Current!.UserId
        };
        db.CustomerTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
        return transaction;
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
            transaction.Type == "payment" ? transaction.Amount : -transaction.Amount);

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
        customer.TaxNumber = Normalize(customer.TaxNumber, 9);
        if (customer.TaxNumber is null)
            throw new ArgumentException("Το ΑΦΜ είναι υποχρεωτικό.");
        customer.CustomerKind = Normalize(customer.CustomerKind, 40) ?? "Φυσικό πρόσωπο";
        if (customer.TaxNumber.Length != 9 || !customer.TaxNumber.All(char.IsAsciiDigit))
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
        customer.IsReferral = customer.ReferredByCustomerId.HasValue || customer.IsReferral;
        if (!customer.IsReferral) customer.ReferredByCustomerId = null;
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

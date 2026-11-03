using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Filters;

public sealed class FilterOptionService(IApplicationDbContextFactory factory, ISessionService session)
    : IFilterOptionService
{
    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken cancellationToken = default)
    {
        EnsureSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var saved = await db.FilterOptions.AsNoTracking().Select(item => item.Category)
            .Distinct().ToListAsync(cancellationToken);
        return new[] { "Πόλεις", "Κλάδοι", "Καλύψεις", "Επαγγέλματα", "Καταστάσεις",
            "Φύλα", "Νομικές μορφές", "Οικογενειακές καταστάσεις", "Πηγές πελατών",
            "Τύποι ταυτότητας", "Κατηγορίες διπλώματος", "Ιθαγένειες", "ΔΟΥ",
            "Τρόποι πληρωμής" }
            .Concat(saved).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public async Task<IReadOnlyList<FilterOption>> ListAsync(string category,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        category = CleanCategory(category);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.FilterOptions.AsNoTracking().Where(item => item.Category == category)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Value).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ChoicesAsync(string category,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        category = CleanCategory(category);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var configured = await db.FilterOptions.AsNoTracking().Where(item => item.Category == category)
            .Select(item => item.Value).ToListAsync(cancellationToken);
        IEnumerable<string?> recorded = category switch
        {
            "Πόλεις" => await db.Customers.AsNoTracking().Select(item => item.City).ToListAsync(cancellationToken),
            "Συνεργάτες" => (await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "partners").Select(item => item.Title)
                .ToListAsync(cancellationToken)).Concat(await db.Customers.AsNoTracking()
                .Select(item => item.PartnerName).ToListAsync(cancellationToken)),
            "Πελάτες" => (await db.Customers.AsNoTracking()
                .Select(item => item.LastName + " " + item.FirstName).ToListAsync(cancellationToken)),
            "Επαγγέλματα" => await db.Customers.AsNoTracking().Select(item => item.Occupation)
                .ToListAsync(cancellationToken),
            _ => []
        };
        return configured.Concat(recorded.Where(value => !string.IsNullOrWhiteSpace(value))!)
            .Select(value => value!.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task SaveAsync(Guid? id, string category, string value,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        category = CleanCategory(category);
        EnsureConfigurable(category);
        value = CleanValue(value);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.FilterOptions.AnyAsync(item => item.Category == category && item.Value == value && item.Id != id,
            cancellationToken)) throw new InvalidOperationException("Η τιμή υπάρχει ήδη σε αυτή την κατηγορία.");
        if (id is Guid optionId)
        {
            var existing = await db.FilterOptions.SingleOrDefaultAsync(item => item.Id == optionId,
                cancellationToken) ?? throw new InvalidOperationException("Η τιμή δεν υπάρχει.");
            existing.Category = category;
            existing.Value = value;
        }
        else db.FilterOptions.Add(new FilterOption { Id = Guid.NewGuid(), Category = category, Value = value });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var item = await db.FilterOptions.SingleOrDefaultAsync(option => option.Id == id,
            cancellationToken) ?? throw new InvalidOperationException("Η τιμή δεν υπάρχει.");
        db.FilterOptions.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ImportAsync(string category, IEnumerable<string> values,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        category = CleanCategory(category);
        EnsureConfigurable(category);
        var incoming = values.Select(value => value.Trim()).Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (incoming.Any(value => value.Length > 160))
            throw new ArgumentException("Κάθε τιμή πρέπει να έχει έως 160 χαρακτήρες.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = (await db.FilterOptions.AsNoTracking().Where(item => item.Category == category)
            .Select(item => item.Value).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var additions = incoming.Where(value => !existing.Contains(value))
            .Select(value => new FilterOption { Id = Guid.NewGuid(), Category = category, Value = value })
            .ToArray();
        db.FilterOptions.AddRange(additions);
        await db.SaveChangesAsync(cancellationToken);
        return additions.Length;
    }

    private void EnsureSession()
    {
        if (!session.IsAuthenticated) throw new UnauthorizedAccessException("Απαιτείται σύνδεση.");
    }

    private static string CleanCategory(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.Length is < 1 or > 80) throw new ArgumentException("Επιλέξτε έγκυρη κατηγορία φίλτρου.");
        return cleaned;
    }

    private static string CleanValue(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.Length is < 1 or > 160) throw new ArgumentException("Η τιμή πρέπει να έχει 1 έως 160 χαρακτήρες.");
        return cleaned;
    }

    private static void EnsureConfigurable(string category)
    {
        if (category is "Συνεργάτες" or "Πελάτες")
            throw new ArgumentException("Οι συνεργάτες και οι πελάτες προκύπτουν από τις καταχωρήσεις τους.");
    }
}

using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Bridges;

public enum BridgeMappingKind { Branch, VehicleUse, Coverage, Partner }

public sealed record BridgeMappingEntry(Guid Id, NativeBridgeSource Source,
    BridgeMappingKind Kind, string Code, string Target, Guid? PartnerId,
    bool IsValid);

/// <summary>Editable mappings used by the two native insurance file importers.</summary>
public sealed class BridgeMappingStore(IFilterOptionService filters, IBusinessRecordService records)
{
    public static string Company(NativeBridgeSource source) => source switch
    {
        NativeBridgeSource.Ydrogeios => "Υδρόγειος",
        NativeBridgeSource.Orizon => "Ορίζων 1964",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static string Label(BridgeMappingKind kind) => kind switch
    {
        BridgeMappingKind.Branch => "Κλάδοι",
        BridgeMappingKind.VehicleUse => "Χρήσεις οχημάτων",
        BridgeMappingKind.Coverage => "Καλύψεις",
        BridgeMappingKind.Partner => "Συνεργάτες",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string Category(NativeBridgeSource source, BridgeMappingKind kind) =>
        $"bridge:{source}:{(kind == BridgeMappingKind.Partner ? "partner" : Label(kind))}";

    public async Task<IReadOnlyList<BridgeMappingEntry>> ListAsync()
    {
        var partners = (await records.SearchAsync("partners"))
            .ToDictionary(item => item.Id, item => item.Title);
        var entries = new List<BridgeMappingEntry>();
        foreach (var source in Enum.GetValues<NativeBridgeSource>())
        {
            foreach (var kind in Enum.GetValues<BridgeMappingKind>())
            {
                var saved = await filters.ListAsync(Category(source, kind));
                var choices = kind == BridgeMappingKind.Partner ? null :
                    (await filters.ScopedChoicesAsync(Label(kind), Company(source), null))
                    .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
                foreach (var option in saved)
                {
                    var parts = option.Value.Split('|', 2);
                    if (parts.Length != 2) continue;
                    if (kind == BridgeMappingKind.Partner)
                    {
                        var partnerId = Guid.TryParse(parts[1], out var id) ? id : (Guid?)null;
                        entries.Add(new BridgeMappingEntry(option.Id, source, kind, parts[0],
                            partnerId is Guid linked && partners.TryGetValue(linked, out var name)
                                ? name : partnerId is null ? "Χωρίς συνεργάτη" : "Δεν βρέθηκε",
                            partnerId, partnerId is null || partners.ContainsKey(partnerId.Value)));
                    }
                    else entries.Add(new BridgeMappingEntry(option.Id, source, kind, parts[0],
                        parts[1], null, choices!.Contains(parts[1])));
                }
            }
        }
        return entries.OrderBy(item => Company(item.Source))
            .ThenBy(item => Label(item.Kind)).ThenBy(item => item.Code).ToArray();
    }

    public async Task SaveAsync(Guid? id, NativeBridgeSource source,
        BridgeMappingKind kind, string code, string? target, Guid? partnerId,
        bool createMissingTarget)
    {
        code = code.Trim();
        if (code.Length is < 1 or > 30 || code.Contains('|') || code.Any(char.IsControl))
            throw new ArgumentException("Ο κωδικός γέφυρας πρέπει να έχει 1 έως 30 χαρακτήρες χωρίς |.");
        var category = Category(source, kind);
        var prior = await ListAsync();
        if (id is Guid mappingId && prior.All(item => item.Id != mappingId))
            throw new InvalidOperationException("Η αντιστοίχιση δεν υπάρχει πλέον.");
        if (prior.Any(item => item.Id != id && item.Source == source && item.Kind == kind &&
            item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Αυτός ο κωδικός έχει ήδη αντιστοιχιστεί για την εταιρεία και την κατηγορία.");
        string value;
        if (kind == BridgeMappingKind.Partner)
        {
            if (partnerId is Guid linked && (await records.GetAsync(linked)) is not
                { SectionKey: "partners" })
                throw new ArgumentException("Επιλέξτε καταχωρημένο συνεργάτη.");
            value = $"{code}|{partnerId}";
        }
        else
        {
            target = target?.Trim();
            if (string.IsNullOrWhiteSpace(target) || target.Length > 120 || target.Contains('|'))
                throw new ArgumentException("Επιλέξτε έγκυρη τιμή αντιστοίχισης.");
            var choices = await filters.ScopedChoicesAsync(Label(kind), Company(source), null);
            if (!choices.Contains(target, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!createMissingTarget)
                    throw new ArgumentException("Η τιμή δεν υπάρχει στα παραμετρικά. Επιλέξτε δημιουργία νέας τιμής.");
                if (!(await records.SearchAsync("companies")).Any(item =>
                    item.Title.Equals(Company(source), StringComparison.CurrentCultureIgnoreCase)))
                    await records.SaveAsync(new BusinessRecord
                    { SectionKey = "companies", Title = Company(source), Status = "Ενεργή" });
                await filters.ImportScopedAsync(Label(kind), [target], Company(source), null);
            }
            value = $"{code}|{target}";
        }
        await filters.SaveAsync(id, category, value);
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!(await ListAsync()).Any(item => item.Id == id))
            throw new InvalidOperationException("Η αντιστοίχιση δεν υπάρχει πλέον.");
        await filters.DeleteAsync(id);
    }
}

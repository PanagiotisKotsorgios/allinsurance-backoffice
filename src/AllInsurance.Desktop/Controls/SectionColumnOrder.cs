using System.IO;
using System.Text.Json;

namespace AllInsurance.Desktop.Controls;

internal static class SectionColumnOrder
{
    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "section-column-order.json");
    private static readonly string WidthsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "section-column-widths.json");
    private static readonly string VisiblePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "section-column-visibility.json");

    public static bool[] LoadVisibility(string sectionKey)
    {
        var defaults = Enumerable.Repeat(true, ColumnCount(sectionKey)).ToArray();
        try
        {
            if (!File.Exists(VisiblePath)) return defaults;
            var saved = JsonSerializer.Deserialize<Dictionary<string, bool[]>>(File.ReadAllText(VisiblePath));
            if (saved?.TryGetValue(sectionKey, out var flags) == true &&
                flags.Length == defaults.Length && flags[0])
                return flags;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return defaults;
    }

    public static void SaveVisibility(string sectionKey, bool[] flags)
    {
        if (flags.Length != ColumnCount(sectionKey) || !flags[0])
            throw new ArgumentException("Η βασική στήλη πρέπει να παραμένει ορατή.");
        Dictionary<string, bool[]> saved;
        try { saved = File.Exists(VisiblePath)
            ? JsonSerializer.Deserialize<Dictionary<string, bool[]>>(File.ReadAllText(VisiblePath)) ?? []
            : []; }
        catch (JsonException) { saved = []; }
        saved[sectionKey] = [.. flags];
        Directory.CreateDirectory(Path.GetDirectoryName(VisiblePath)!);
        File.WriteAllText(VisiblePath, JsonSerializer.Serialize(saved));
    }

    public static double[] LoadWidths(string sectionKey)
    {
        var defaults = sectionKey switch
        {
            "partners" => new double[] { 240, 140, 165, 175, 150, 160, 130 },
            "vehicles" => [180, 135, 145, 210, 140, 165, 125, 125],
            _ => [240, 140, 190, 130, 130, 240, 100]
        };
        try
        {
            if (!File.Exists(WidthsPath)) return defaults;
            var saved = JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(WidthsPath));
            if (saved?.TryGetValue(sectionKey, out var widths) == true && widths.Length == defaults.Length &&
                widths.All(value => double.IsFinite(value) && value is >= 70 and <= 520)) return widths;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return defaults;
    }

    public static void SaveWidths(string sectionKey, double[] widths)
    {
        Dictionary<string, double[]> saved;
        try { saved = File.Exists(WidthsPath)
            ? JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(WidthsPath)) ?? []
            : []; }
        catch (JsonException) { saved = []; }
        saved[sectionKey] = [.. widths];
        Directory.CreateDirectory(Path.GetDirectoryName(WidthsPath)!);
        File.WriteAllText(WidthsPath, JsonSerializer.Serialize(saved));
    }

    public static int[] Load(string sectionKey)
    {
        try
        {
            var saved = Read();
            if (saved.TryGetValue(sectionKey, out var order) && Valid(order, sectionKey)) return order;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return Enumerable.Range(0, ColumnCount(sectionKey)).ToArray();
    }

    public static void Save(string sectionKey, int[] order)
    {
        if (!Valid(order, sectionKey)) throw new ArgumentException("Η σειρά στηλών δεν είναι έγκυρη.");
        Dictionary<string, int[]> saved;
        try { saved = Read(); }
        catch (JsonException) { saved = []; }
        saved[sectionKey] = [.. order];
        Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
        File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(saved));
    }

    private static Dictionary<string, int[]> Read() => File.Exists(PathOnDisk)
        ? JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(PathOnDisk)) ?? []
        : [];

    private static int ColumnCount(string sectionKey) => sectionKey == "vehicles" ? 8 : 7;

    private static bool Valid(int[]? order, string sectionKey) =>
        order is not null && order.Length == ColumnCount(sectionKey) &&
        order.Distinct().OrderBy(value => value)
            .SequenceEqual(Enumerable.Range(0, ColumnCount(sectionKey)));
}

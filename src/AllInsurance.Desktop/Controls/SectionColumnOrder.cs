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

    public static double[] LoadWidths(string sectionKey)
    {
        var defaults = new double[] { 240, 140, 190, 130, 130, 240, 100 };
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
            if (saved.TryGetValue(sectionKey, out var order) && Valid(order)) return order;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return [0, 1, 2, 3, 4, 5, 6];
    }

    public static void Save(string sectionKey, int[] order)
    {
        if (!Valid(order)) throw new ArgumentException("Η σειρά στηλών δεν είναι έγκυρη.");
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

    private static bool Valid(int[]? order) => order is { Length: 7 } &&
        order.Distinct().OrderBy(value => value).SequenceEqual(Enumerable.Range(0, 7));
}

using Microsoft.UI.Xaml;
using System.IO;
using System.Text.Json;

namespace AllInsurance.Desktop.Controls;

public static class CustomerColumns
{
    public static readonly (string Key, string Title, int Width)[] All =
    [
        ("Code", "Κωδικός", 125), ("Kind", "Είδος", 130),
        ("LastName", "Επώνυμο", 155), ("FirstName", "Όνομα", 155),
        ("Company", "Επωνυμία", 180), ("Tax", "ΑΦΜ", 100),
        ("Email", "Email", 220), ("Mobile", "Κινητό", 145),
        ("City", "Πόλη", 140), ("Balance", "Υπόλοιπο", 100)
    ];

    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "customer-columns.json");
    private static HashSet<string> _visible = Load();
    private static string[] _order = LoadOrder();
    private static Dictionary<string, double> _widths = LoadWidths();

    public static (string Key, string Title, int Width)[] Ordered =>
        _order.Select(key => All.First(item => item.Key == key)).ToArray();

    public static void Move(string key, int offset)
    {
        var index = Array.IndexOf(_order, key);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _order.Length) return;
        (_order[index], _order[target]) = (_order[target], _order[index]);
        var path = Path.Combine(Path.GetDirectoryName(PathOnDisk)!, "customer-column-order.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(_order));
    }

    private static string[] LoadOrder()
    {
        var path = Path.Combine(Path.GetDirectoryName(PathOnDisk)!, "customer-column-order.json");
        try
        {
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
                if (saved.Length == All.Length && saved.Distinct().Count() == All.Length &&
                    saved.All(key => All.Any(column => column.Key == key))) return saved;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return All.Select(item => item.Key).ToArray();
    }

    public static bool IsVisible(string key) => _visible.Contains(key);
    public static double WidthValue(string key) => _widths.TryGetValue(key, out var width)
        ? width : All.First(item => item.Key == key).Width;

    public static GridLength Width(string key) => new(IsVisible(key) ? WidthValue(key) : 0);

    public static void SetWidth(string key, double width) => _widths[key] = Math.Clamp(width, 70, 520);

    public static void SaveWidths()
    {
        var path = Path.Combine(Path.GetDirectoryName(PathOnDisk)!, "customer-column-widths.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(_widths));
    }

    private static Dictionary<string, double> LoadWidths()
    {
        var path = Path.Combine(Path.GetDirectoryName(PathOnDisk)!, "customer-column-widths.json");
        try
        {
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path)) ?? [];
                return saved.Where(item => All.Any(column => column.Key == item.Key) &&
                    double.IsFinite(item.Value) && item.Value is >= 70 and <= 520)
                    .ToDictionary(item => item.Key, item => item.Value);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return [];
    }

    public static void Save(IEnumerable<string> visible)
    {
        var selected = visible.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0) throw new ArgumentException("Επιλέξτε τουλάχιστον μία στήλη.");
        _visible = selected;
        Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
        File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(selected));
    }

    private static HashSet<string> Load()
    {
        try
        {
            if (File.Exists(PathOnDisk))
            {
                var selected = (JsonSerializer.Deserialize<string[]>(File.ReadAllText(PathOnDisk)) ?? [])
                    .ToHashSet(StringComparer.Ordinal);
                if (selected.Overlaps(All.Select(item => item.Key))) return selected;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return All.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
    }
}


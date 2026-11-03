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

    public static bool IsVisible(string key) => _visible.Contains(key);
    public static GridLength Width(string key) => new(IsVisible(key)
        ? All.First(item => item.Key == key).Width : 0);

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


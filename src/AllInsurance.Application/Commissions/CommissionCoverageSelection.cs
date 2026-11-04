using System.Text.Json;

namespace AllInsurance.Application.Commissions;

/// <summary>Reads legacy single-cover rules and new multi-cover rules from the same column.</summary>
public static class CommissionCoverageSelection
{
    public static string[] Read(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return [];
        if (stored.TrimStart().StartsWith('['))
        {
            try
            {
                return Normalize(JsonSerializer.Deserialize<string[]>(stored) ?? []);
            }
            catch (JsonException) { return []; }
        }
        return Normalize([stored]);
    }

    public static string? Write(IEnumerable<string> selections)
    {
        var values = Normalize(selections);
        return values.Length switch
        {
            0 => null,
            1 => values[0],
            _ => JsonSerializer.Serialize(values)
        };
    }

    public static string Display(string? stored) =>
        Read(stored) is { Length: > 0 } values ? string.Join(", ", values) : "Όλες";

    private static string[] Normalize(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
}

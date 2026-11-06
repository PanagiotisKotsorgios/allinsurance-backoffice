using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class PolicyStatusColors
{
    public static SolidColorBrush DocumentTypeBackground(string? documentType) => documentType switch
    {
        "Προσφορά" => Brush(102, 124, 149),
        "Αίτηση" => StatusPalette.Brush(StatusPalette.Caution),
        "Ειδοποιητήριο" => StatusPalette.Brush(StatusPalette.Informational),
        "Πρόσθετη πράξη" => Brush(105, 151, 122),
        "Πράσινη κάρτα" => StatusPalette.Brush(StatusPalette.Positive),
        _ => Brush(76, 139, 114)
    };

    public static SolidColorBrush Background(string? documentType, string? status, DateTime? expiry)
    {
        if (Contains(documentType, "ακύρ") || Contains(status, "ακύρ") ||
            Contains(status, "άκυρ") || Contains(status, "ληγ"))
            return StatusPalette.Brush(StatusPalette.Negative);
        if (Contains(documentType, "πράσινη κάρτα"))
            return StatusPalette.Brush(StatusPalette.Positive);
        if (Contains(documentType, "πρόσθετη πράξη") || Contains(status, "πρόσθετη πράξη"))
            return Brush(105, 151, 122);
        if (Contains(documentType, "προσφορά")) return Brush(102, 124, 149);
        if (Contains(documentType, "ειδοποιητήριο"))
            return StatusPalette.Brush(StatusPalette.Informational);
        if (Contains(documentType, "αίτηση") || Contains(status, "σε εξέλιξη"))
            return StatusPalette.Brush(StatusPalette.Caution);
        if (expiry is DateTime end && end.Date < DateTime.Today)
            return StatusPalette.Brush(StatusPalette.Negative);
        if (expiry is DateTime approaching && approaching.Date <= DateTime.Today.AddDays(30))
            return StatusPalette.Brush(StatusPalette.Caution);
        return StatusPalette.Brush(StatusPalette.Positive);
    }

    private static bool Contains(string? value, string token) =>
        value?.Contains(token, StringComparison.CurrentCultureIgnoreCase) == true;
    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromArgb(255, red, green, blue));
}

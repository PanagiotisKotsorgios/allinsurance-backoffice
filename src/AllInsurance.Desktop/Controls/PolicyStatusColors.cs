using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class PolicyStatusColors
{
    public static SolidColorBrush Background(string? documentType, string? status, DateTime? expiry)
    {
        if (Contains(documentType, "ακύρ") || Contains(status, "ακύρ") ||
            Contains(status, "άκυρ") || Contains(status, "ληγ"))
            return Brush(166, 62, 62);
        if (Contains(documentType, "πράσινη κάρτα")) return Brush(44, 128, 70);
        if (Contains(documentType, "πρόσθετη πράξη") || Contains(status, "πρόσθετη πράξη"))
            return Brush(89, 153, 118);
        if (Contains(documentType, "προσφορά")) return Brush(86, 112, 145);
        if (Contains(documentType, "ειδοποιητήριο")) return Brush(65, 125, 165);
        if (Contains(documentType, "αίτηση") || Contains(status, "σε εξέλιξη"))
            return Brush(177, 126, 39);
        if (expiry is DateTime end && end.Date < DateTime.Today) return Brush(166, 62, 62);
        if (expiry is DateTime approaching && approaching.Date <= DateTime.Today.AddDays(30))
            return Brush(177, 126, 39);
        return Brush(52, 132, 79);
    }

    private static bool Contains(string? value, string token) =>
        value?.Contains(token, StringComparison.CurrentCultureIgnoreCase) == true;
    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromArgb(255, red, green, blue));
}

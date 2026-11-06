using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class StatusPalette
{
    public static readonly Color Positive = Color.FromArgb(255, 79, 128, 111);
    public static readonly Color Caution = Color.FromArgb(255, 166, 132, 77);
    public static readonly Color Negative = Color.FromArgb(255, 173, 78, 78);
    public static readonly Color Neutral = Color.FromArgb(255, 101, 111, 123);
    public static readonly Color Informational = Color.FromArgb(255, 81, 127, 164);

    public static SolidColorBrush Brush(Color color) => new(color);
}

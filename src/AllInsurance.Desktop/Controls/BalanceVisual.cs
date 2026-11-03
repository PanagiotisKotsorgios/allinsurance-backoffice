using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Controls;

internal static class BalanceVisual
{
    private static readonly SolidColorBrush DebtBrush = new(Color.FromArgb(255, 198, 40, 40));

    public static Border Create(decimal amount, string text, double fontSize = 14)
    {
        var hasBalance = amount != 0;
        var label = new TextBlock { Text = text, FontSize = fontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        if (hasBalance) label.Foreground = new SolidColorBrush(Colors.White);
        return new Border
        {
            Background = hasBalance ? DebtBrush : null,
            CornerRadius = new CornerRadius(5),
            Padding = hasBalance ? new Thickness(7, 3, 7, 3) : new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };
    }
}

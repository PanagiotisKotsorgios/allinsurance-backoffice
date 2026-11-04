using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

internal static class UiAlerts
{
    public static async Task ShowSuccessAsync(XamlRoot root, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Ολοκληρώθηκε",
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 34, 122, 76)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Child = new TextBlock { Text = "✓  " + message,
                    Foreground = new SolidColorBrush(Colors.White),
                    TextWrapping = TextWrapping.Wrap }
            },
            CloseButtonText = "Εντάξει"
        };
        await DialogButtonStyles.ShowAsync(dialog);
    }

    public static async Task ShowErrorAsync(XamlRoot root, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Προσοχή",
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 150, 50, 50)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Child = new TextBlock { Text = message, Foreground = new SolidColorBrush(Colors.White),
                    TextWrapping = TextWrapping.Wrap }
            },
            CloseButtonText = "Εντάξει"
        };
        await DialogButtonStyles.ShowAsync(dialog);
    }
}

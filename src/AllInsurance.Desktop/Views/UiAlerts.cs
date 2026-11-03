using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

internal static class UiAlerts
{
    public static async Task ShowErrorAsync(XamlRoot root, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Προσοχή",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Εντάξει"
        };
        await DialogButtonStyles.ShowAsync(dialog);
    }
}

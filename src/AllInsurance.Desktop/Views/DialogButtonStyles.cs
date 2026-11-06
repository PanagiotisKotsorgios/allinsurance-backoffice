using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

internal static class DialogButtonStyles
{
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, bool destructive = false,
        bool redClose = false)
    {
        Apply(dialog, destructive, redClose);
        return await dialog.ShowAsync();
    }

    public static void Apply(ContentDialog dialog, bool destructive = false, bool redClose = false)
    {
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        dialog.RequestedTheme = preferences.Theme;
        var dark = preferences.Theme == ElementTheme.Dark || preferences.Theme == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark;
        dialog.Background = new SolidColorBrush(dark ? Color.FromArgb(255, 43, 48, 55) :
            preferences.EyeComfortEnabled ? Color.FromArgb(255, 232, 226, 213) :
            Color.FromArgb(255, 235, 240, 245));
        dialog.Foreground = new SolidColorBrush(dark ? Color.FromArgb(255, 241, 244, 247) :
            Color.FromArgb(255, 35, 44, 53));
        dialog.PrimaryButtonStyle = (Style)resources[destructive
            ? "DialogDangerButtonStyle" : "DialogConfirmButtonStyle"];
        dialog.SecondaryButtonStyle = (Style)resources[dialog.SecondaryButtonText?.Contains("Ακύρ") == true
            ? "DialogCancelButtonStyle" : "DialogSecondaryButtonStyle"];
        dialog.CloseButtonStyle = (Style)resources[dialog.CloseButtonText == "Εντάξει"
            ? "DialogOkayButtonStyle" : redClose ? "DialogCancelButtonStyle" : "DialogCloseButtonStyle"];
        dialog.Loaded += (_, _) => ButtonMotion.AttachTree(dialog);
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AllInsurance.Desktop.Controls;

namespace AllInsurance.Desktop.Views;

internal static class DialogButtonStyles
{
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, bool destructive = false)
    {
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        dialog.PrimaryButtonStyle = (Style)resources[destructive
            ? "DialogDangerButtonStyle" : "DialogConfirmButtonStyle"];
        dialog.SecondaryButtonStyle = (Style)resources["DialogDangerButtonStyle"];
        dialog.CloseButtonStyle = (Style)resources["DialogCloseButtonStyle"];
        dialog.Loaded += (_, _) => ButtonMotion.AttachTree(dialog);
        return await dialog.ShowAsync();
    }
}

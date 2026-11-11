using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

/// <summary>Keeps the bridge catalog and mapping editor in the same navigation stack.</summary>
public sealed class BridgeWorkspacePage : Page
{
    public BridgeWorkspacePage()
    {
        var frame = new Frame();
        Content = frame;
        frame.Navigate(typeof(CompanyBridgesPage));
    }
}

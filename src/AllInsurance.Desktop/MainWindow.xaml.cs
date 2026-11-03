using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Graphics;
using System.IO;

namespace AllInsurance.Desktop;

public sealed partial class MainWindow : Window
{
    public Microsoft.UI.Xaml.XamlRoot NavigationRoot => NavigationFrame.XamlRoot;
    public MainWindow()
    {
        InitializeComponent();
        App.Navigation.Configure(NavigationFrame);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workingArea = displayArea.WorkArea;
        var width = Math.Min(820, Math.Max(360, workingArea.Width - 48));
        var height = Math.Min(720, Math.Max(400, workingArea.Height - 48));
        appWindow.MoveAndResize(new RectInt32(
            workingArea.X + (workingArea.Width - width) / 2,
            workingArea.Y + (workingArea.Height - height) / 2,
            width,
            height));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 360;
            presenter.PreferredMinimumHeight = 400;
        }
    }

    public void HideStartupProgress()
    {
        StartupProgress.IsActive = false;
        StartupProgress.Visibility = Visibility.Collapsed;
    }

    public void ApplyTheme(ElementTheme theme) => WindowRoot.RequestedTheme = theme;

    public void MaximizeWorkspace()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        if (AppWindow.GetFromWindowId(windowId).Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }
}

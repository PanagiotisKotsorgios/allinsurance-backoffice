using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AllInsurance.Desktop.Views;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AllInsurance.Desktop.Controls;

internal static class TableExportActions
{
    public static void Attach(Panel host, FrameworkElement owner, Func<TableExportData> getData)
    {
        var buttonContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center };
        buttonContent.Children.Add(new Image { Source = new SvgImageSource(new Uri("ms-appx:///Assets/Icons/action-download.svg")),
            Width = 17, Height = 17, VerticalAlignment = VerticalAlignment.Center });
        buttonContent.Children.Add(new TextBlock { Text = "Εξαγωγή δεδομένων ▴", VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = buttonContent, MinHeight = 30,
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 88, 125, 161)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(10, 3, 10, 3), VerticalAlignment = VerticalAlignment.Center };
        var flyout = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top };
        foreach (var (label, extension) in new[] { ("Excel (.xlsx)", ".xlsx"), ("CSV (.csv)", ".csv") })
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += async (_, _) =>
            {
                try
                {
                    var data = getData();
                    var picker = new FileSavePicker { SuggestedFileName = SafeName(data.Title) + "-" + DateTime.Now.ToString("yyyyMMdd") };
                    picker.FileTypeChoices.Add(label, [extension]);
                    InitializeWithWindow.Initialize(picker, App.MainWindowHandle());
                    var file = await picker.PickSaveFileAsync();
                    if (file is null) return;
                    var columns = Enumerable.Range(0, data.Headers.Length).ToArray();
                    if (extension == ".xlsx") TableDataExport.Excel(file.Path, data, columns);
                    else TableDataExport.Csv(file.Path, data, columns);
                }
                catch (Exception error) { await UiAlerts.ShowErrorAsync(owner.XamlRoot, "Αποτυχία εξαγωγής: " + error.Message); }
            };
            flyout.Items.Add(item);
        }
        flyout.Items.Add(new MenuFlyoutSeparator());
        var print = new MenuFlyoutItem { Text = "Εκτύπωση…" };
        print.Click += async (_, _) => await PrintAsync(owner, getData);
        flyout.Items.Add(print);
        button.Flyout = flyout;
        host.Children.Insert(0, button);
    }

    private static async Task PrintAsync(FrameworkElement owner, Func<TableExportData> getData)
    {
        try
        {
            var data = getData();
            var checkboxes = data.Headers.Select(header => new CheckBox
            {
                Content = header, IsChecked = true, Margin = new Thickness(0, 2, 0, 2)
            }).ToArray();
            var orientation = new ComboBox { Header = "Προσανατολισμός", Width = 230,
                ItemsSource = new[] { "Οριζόντια", "Κάθετα" }, SelectedIndex = data.Headers.Length > 5 ? 0 : 1 };
            var all = new Button { Content = "Επιλογή όλων" };
            var none = new Button { Content = "Αποεπιλογή όλων" };
            all.Click += (_, _) => { foreach (var box in checkboxes) box.IsChecked = true; };
            none.Click += (_, _) => { foreach (var box in checkboxes) box.IsChecked = false; };
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            tools.Children.Add(all); tools.Children.Add(none);
            var fields = new StackPanel { Spacing = 2 };
            foreach (var box in checkboxes) fields.Children.Add(box);
            var content = new StackPanel { Spacing = 10, Width = 420 };
            content.Children.Add(orientation); content.Children.Add(tools);
            content.Children.Add(new ScrollViewer { Content = fields, MaxHeight = 340 });
            var dialog = new ContentDialog { XamlRoot = owner.XamlRoot, Title = "Εκτύπωση πίνακα",
                Content = content, PrimaryButtonText = "Εκτύπωση", CloseButtonText = "Ακύρωση" };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var selected = checkboxes.Select((box, index) => (box, index))
                .Where(item => item.box.IsChecked == true).Select(item => item.index).ToArray();
            if (selected.Length == 0)
            { await UiAlerts.ShowErrorAsync(owner.XamlRoot, "Επιλέξτε τουλάχιστον μία στήλη."); return; }
            TableDataExport.Print(data, selected, orientation.SelectedIndex == 0);
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(owner.XamlRoot, "Αποτυχία εκτύπωσης: " + error.Message); }
    }

    private static string SafeName(string value) => string.Concat(value.Select(c =>
        Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

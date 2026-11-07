using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Runtime.InteropServices;
using Windows.System;

namespace AllInsurance.Desktop.Views;

public sealed partial class ShortcutsPage : Page
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    private readonly WorkspacePreferences _preferences;
    private ShortcutRow[] _rows = [];

    public ShortcutsPage()
    {
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Συντομεύσεις",
            ["Ενέργεια", "Συνδυασμός πλήκτρων", "Κατάσταση"],
            (ShortcutsList.ItemsSource as IEnumerable<ShortcutRow> ?? [])
                .Select(row => new[] { row.Action, row.Keys, row.State }).ToArray()));
        TableRowStriping.Attach(ShortcutsList);
        _preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        EnabledChoice.IsChecked = _preferences.ShortcutsEnabled;
        Refresh();
    }

    private void Refresh()
    {
        _rows = ShortcutCatalog.All.Select(spec => new ShortcutRow(spec,
            _preferences.GetShortcut(spec.Id))).ToArray();
        ApplyFilter();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter()
    {
        if (ShortcutsList is null || SearchInput is null || StatusText is null) return;
        var term = SearchInput.Text.Trim();
        var filtered = _rows.Where(row => term.Length == 0 ||
            (row.Action + row.Keys).Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        ShortcutsList.ItemsSource = filtered;
        StatusText.Text = $"{filtered.Length} ενέργειες · {_rows.Count(row => row.Binding is not null)} συντομεύσεις";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _preferences.SetShortcutsEnabled(EnabledChoice.IsChecked == true);
            StatusText.Text = "Η ρύθμιση αποθηκεύτηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Add_Click(object sender, RoutedEventArgs e) => await CaptureAsync(null);
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is not ShortcutRow row)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε ενέργεια από τον πίνακα."); return; }
        await CaptureAsync(row.Spec);
    }
    private async void List_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is ShortcutRow row) await CaptureAsync(row.Spec);
    }

    private async Task CaptureAsync(ShortcutSpec? selected)
    {
        var action = new ComboBox { Header = "Ενέργεια", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var spec in ShortcutCatalog.All)
            action.Items.Add(new ComboBoxItem { Content = spec.Title, Tag = spec.Id });
        action.SelectedItem = action.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == selected?.Id) ?? action.Items[0];
        action.IsEnabled = selected is null;
        var keys = new TextBox { Header = "Πατήστε τον συνδυασμό πλήκτρων",
            IsReadOnly = true, PlaceholderText = "Π.χ. Ctrl+Shift+M",
            Text = selected is null ? "" : _preferences.GetShortcut(selected.Id)?.Display ?? "" };
        ShortcutBinding? captured = null;
        keys.KeyDown += (_, args) =>
        {
            if (args.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or VirtualKey.LeftWindows or VirtualKey.RightWindows)
                return;
            var modifiers = (VirtualKeyModifiers)0;
            if ((GetKeyState(0x11) & 0x8000) != 0) modifiers |= VirtualKeyModifiers.Control;
            if ((GetKeyState(0x10) & 0x8000) != 0) modifiers |= VirtualKeyModifiers.Shift;
            if ((GetKeyState(0x12) & 0x8000) != 0) modifiers |= VirtualKeyModifiers.Menu;
            captured = new ShortcutBinding(args.Key, modifiers);
            keys.Text = captured.Display;
            args.Handled = true;
        };
        var form = new StackPanel { Spacing = 12, MinWidth = 380,
            Children = { action, keys,
                new TextBlock { Text = "Κάντε κλικ στο πεδίο και πατήστε τα πλήκτρα μαζί. Δεν χρειάζεται να γράψετε τον συνδυασμό.",
                    TextWrapping = TextWrapping.Wrap } } };
        var dialog = new ContentDialog { XamlRoot = XamlRoot,
            Title = selected is null ? "Νέα συντόμευση" : "Επεξεργασία συντόμευσης",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        dialog.Opened += (_, _) => keys.Focus(FocusState.Programmatic);
        ShortcutCapture.IsCapturing = true;
        try
        {
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            if (captured is null) throw new ArgumentException("Πατήστε έναν νέο συνδυασμό πλήκτρων.");
            _preferences.SetShortcut(((ComboBoxItem)action.SelectedItem).Tag.ToString()!, captured);
            Refresh();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
        finally { ShortcutCapture.IsCapturing = false; }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is not ShortcutRow row)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε συντόμευση από τον πίνακα."); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή συντόμευσης",
            Content = $"Να αφαιρεθεί ο συνδυασμός για «{row.Action}»; Η ενέργεια θα παραμείνει διαθέσιμη από το μενού.",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try { _preferences.SetShortcut(row.Spec.Id, null); Refresh(); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private sealed record ShortcutRow(ShortcutSpec Spec, ShortcutBinding? Binding)
    {
        public string Action => Spec.Title;
        public string Keys => Binding?.Display ?? "—";
        public string State => Binding is null ? "Χωρίς συντόμευση" : "Ενεργή";
    }
}

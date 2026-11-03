using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class ShortcutsPage : Page
{
    private readonly WorkspacePreferences _preferences;

    public ShortcutsPage()
    {
        InitializeComponent();
        _preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        EnabledChoice.IsChecked = _preferences.ShortcutsEnabled;
        var entries = WorkspaceSections.All.Select((section, index) => new ShortcutRow(
            $"Ctrl+{(index + 1) % 10}", section.Title)).ToList();
        entries.AddRange([
            new("Ctrl+U", "Διαχείριση χρηστών"),
            new("Ctrl+J", "Αρχείο ελέγχου"),
            new("Ctrl+B", "Αντίγραφα ασφαλείας"),
            new("Ctrl+L", "Ημερολόγιο σφαλμάτων"),
            new("Ctrl+K", "Διαχείριση συντομεύσεων"),
            new("Ctrl+S", "Ρυθμίσεις συστήματος"),
            new("F1", "Οδηγός χρήσης"),
            new("Ctrl+Shift+Q", "Αποσύνδεση")
        ]);
        ShortcutsList.ItemsSource = entries;
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _preferences.SetShortcutsEnabled(EnabledChoice.IsChecked == true);
            StatusText.Text = "Οι ρυθμίσεις συντομεύσεων αποθηκεύτηκαν.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Δεν ήταν δυνατή η αποθήκευση των συντομεύσεων.";
        }
    }

    private sealed record ShortcutRow(string Keys, string Action)
    {
        public override string ToString() => $"{Keys}: {Action}";
    }
}

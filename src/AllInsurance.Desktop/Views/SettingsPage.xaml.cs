using AllInsurance.Application.Authentication;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class SettingsPage : Page
{
    private readonly WorkspacePreferences _preferences;

    public SettingsPage()
    {
        InitializeComponent();
        _preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        ThemeChoice.SelectedIndex = _preferences.Theme switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };
        DatabasePathText.Text = "Βάση δεδομένων: " + Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AllInsurance", "Data", "allinsurance.db");
        var policy = App.Services.GetRequiredService<AuthenticationPolicyOptions>();
        LockoutPolicyText.Text = $"Κλείδωμα μετά από {policy.MaximumFailedAttempts} αποτυχημένες προσπάθειες για {policy.LockoutDurationMinutes} λεπτά.";
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    private void SaveTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var theme = ThemeChoice.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            _preferences.SetTheme(theme);
            App.ApplyTheme(theme);
            StatusText.Text = "Το θέμα αποθηκεύτηκε.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Δεν ήταν δυνατή η αποθήκευση του θέματος. Ελέγξτε την πρόσβαση στον τοπικό φάκελο δεδομένων.";
        }
    }
}

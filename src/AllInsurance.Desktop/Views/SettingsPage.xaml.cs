using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class SettingsPage : Page
{
    private readonly WorkspacePreferences _preferences;
    private readonly ITransactionSettingsService _transactionSettings;

    public SettingsPage()
    {
        InitializeComponent();
        _preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        _transactionSettings = App.Services.GetRequiredService<ITransactionSettingsService>();
        var session = App.Services.GetRequiredService<ISessionService>();
        var administrator = session.Current?.Permissions.Contains(Permission.ManageUsers) == true;
        CurrencyChoice.IsEnabled = administrator;
        SaveCurrencyButton.IsEnabled = administrator;
        Loaded += async (_, _) =>
        {
            try
            {
                var currency = await _transactionSettings.GetCurrencyAsync();
                CurrencyChoice.SelectedItem = CurrencyChoice.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag as string == currency);
            }
            catch (Exception exception) { CurrencyStatusText.Text = exception.Message; }
        };
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

    private async void SaveCurrency_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CurrencyChoice.SelectedItem is not ComboBoxItem { Tag: string currency })
                throw new ArgumentException("Επιλέξτε νόμισμα.");
            await _transactionSettings.SetCurrencyAsync(currency);
            CurrencyStatusText.Text = $"Το βασικό νόμισμα ορίστηκε σε {currency}.";
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }
}

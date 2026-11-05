using AllInsurance.Application.Authentication;
using AllInsurance.Application.Authorization;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Settings;
using AllInsurance.Desktop.Logging;
using AllInsurance.Desktop.Updates;
using AllInsurance.Application.Backups;
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
        BackupScheduleChoice.IsEnabled = administrator;
        SaveBackupScheduleButton.IsEnabled = administrator;
        var backupSchedule = BackupScheduleSettings.Load();
        BackupScheduleChoice.SelectedItem = BackupScheduleChoice.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == backupSchedule.IntervalDays.ToString())
            ?? BackupScheduleChoice.Items[0];
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
        ThemeChoice.SelectedIndex = _preferences.EyeComfortEnabled ? 3 : _preferences.Theme switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };
        CreditDisplayChoice.SelectedIndex = _preferences.CreditAsNegative ? 0 : 1;
        DatabasePathText.Text = "Βάση δεδομένων: " + Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AllInsurance", "Data", "allinsurance.db");
        VersionText.Text = "Έκδοση εφαρμογής: " + ApplicationUpdater.CurrentVersion;
        BackupPathText.Text = "Αντίγραφα ασφαλείας: " +
            App.Services.GetRequiredService<IBackupService>().BackupFolder;
        ErrorPathText.Text = "Ημερολόγιο σφαλμάτων: " + LocalErrorLogStore.PathOnDisk;
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
                3 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            if (ThemeChoice.SelectedIndex == 3) _preferences.SetEyeComfort();
            else _preferences.SetTheme(theme);
            App.UpdateEyeComfortPalette(_preferences.EyeComfortEnabled);
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

    private async void SaveCreditDisplay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _preferences.SetCreditAsNegative(CreditDisplayChoice.SelectedIndex != 1);
            CreditDisplayStatusText.Text = "Η εμφάνιση πιστωτικού υπολοίπου αποθηκεύτηκε.";
            await UiAlerts.ShowSuccessAsync(XamlRoot, CreditDisplayStatusText.Text);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void SaveBackupSchedule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BackupScheduleChoice.SelectedItem is not ComboBoxItem item ||
                !int.TryParse(item.Tag?.ToString(), out var days))
                throw new ArgumentException("Επιλέξτε συχνότητα αντιγράφων.");
            var previous = BackupScheduleSettings.Load();
            new BackupScheduleSettings(days, previous.IntervalDays == days ? previous.LastRunUtc : null).Save();
            BackupStatusText.Text = days == 0 ? "Το αυτόματο πρόγραμμα απενεργοποιήθηκε." :
                $"Θα δημιουργείται αντίγραφο κάθε {days} ημέρες όταν η εφαρμογή λειτουργεί με διαχειριστή.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

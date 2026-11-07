using AllInsurance.Application.Authorization;
using AllInsurance.Application.Backups;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;

namespace AllInsurance.Desktop.Views;

public sealed partial class BackupPage : Page
{
    private readonly IBackupService _backups = App.Services.GetRequiredService<IBackupService>();
    private BackupRow[] _rows = [];

    public BackupPage()
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        InitializeComponent();
        TableRowStriping.Attach(BackupsList);
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _rows = (await _backups.ListAsync()).Select(file => new BackupRow(file)).ToArray();
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private void Filter_Changed(object sender, object args) => ApplyFilters();
    private void ApplyFilters()
    {
        if (BackupsList is null || SearchInput is null || StatusText is null) return;
        var term = SearchInput.Text.Trim();
        var from = FromDate?.Date?.Date;
        var until = UntilDate?.Date?.Date;
        var filtered = _rows.Where(row =>
            (term.Length == 0 || (row.FileName + row.Created).Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            (from is null || row.File.CreatedUtc.ToLocalTime().Date >= from) &&
            (until is null || row.File.CreatedUtc.ToLocalTime().Date <= until)).ToArray();
        BackupsList.ItemsSource = filtered;
        var schedule = BackupScheduleSettings.Load();
        StatusText.Text = $"{filtered.Length} από {_rows.Length} αντίγραφα · " +
            (schedule.IntervalDays > 0 ? $"Αυτόματα κάθε {schedule.IntervalDays} ημέρες" : "Χωρίς αυτόματο πρόγραμμα");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = ""; FromDate.Date = null; UntilDate.Date = null; ApplyFilters();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Δημιουργία αντιγράφου...";
            await _backups.CreateAsync();
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Schedule_Click(object sender, RoutedEventArgs e)
    {
        var current = BackupScheduleSettings.Load();
        var selector = new ComboBox { Header = "Αυτόματα αντίγραφα όταν λειτουργεί η εφαρμογή",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (label, days) in new[] { ("Χωρίς πρόγραμμα", 0), ("Κάθε ημέρα", 1),
            ("Κάθε 7 ημέρες", 7), ("Κάθε 30 ημέρες", 30) })
            selector.Items.Add(new ComboBoxItem { Content = label, Tag = days });
        selector.SelectedItem = selector.Items.OfType<ComboBoxItem>()
            .First(item => (int)item.Tag == current.IntervalDays ||
                (current.IntervalDays is not (0 or 1 or 7 or 30) && (int)item.Tag == 0));
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Προγραμματισμός αντιγράφων",
            Content = new StackPanel { Spacing = 8, Children = { selector,
                new TextBlock { Text = "Τα αντίγραφα δημιουργούνται τοπικά, όταν η εφαρμογή είναι ανοιχτή με διαχειριστή.", TextWrapping = TextWrapping.Wrap } } },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            var days = (int)((ComboBoxItem)selector.SelectedItem).Tag;
            new BackupScheduleSettings(days, current.IntervalDays == days ? current.LastRunUtc : null).Save();
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_backups.BackupFolder);
            Process.Start(new ProcessStartInfo(_backups.BackupFolder) { UseShellExecute = true });
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Backups_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (BackupsList.SelectedItem is not BackupRow row) return;
        try
        {
            var contents = await _backups.InspectAsync(row.FileName);
            var labels = new Dictionary<string, string> { ["Customers"] = "Πελάτες",
                ["BusinessRecords"] = "Εγγραφές οχημάτων, συμβολαίων και λοιπών ενοτήτων",
                ["CustomerTransactions"] = "Οικονομικές κινήσεις πελατών",
                ["PartnerPayouts"] = "Αποδόσεις συνεργατών", ["SecurityEvents"] = "Συμβάντα ελέγχου",
                ["Users"] = "Χρήστες", ["Roles"] = "Ρόλοι", ["FilterOptions"] = "Δεδομένα συστήματος" };
            foreach (var section in WorkspaceSections.All)
                labels["BusinessRecords:" + section.Key] = section.Title;
            var details = string.Join("\n", contents.Counts.Select(item =>
                $"{labels.GetValueOrDefault(item.Key, item.Key)}: {item.Value}"));
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Καρτέλα αντιγράφου ασφαλείας",
                Content = new ScrollViewer { MaxHeight = 390, MinWidth = 410,
                    Content = new TextBlock { Text = $"{row.FileName}\n{row.Created} · {row.Size}\nΈλεγχος ακεραιότητας: επιτυχής\n\n{details}",
                        TextWrapping = TextWrapping.Wrap } },
                PrimaryButtonText = "Επαναφορά", CloseButtonText = "Κλείσιμο" };
            if (await DialogButtonStyles.ShowAsync(dialog) == ContentDialogResult.Primary)
                await RestoreAsync(row.FileName);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task RestoreAsync(string fileName)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Επιβεβαίωση επαναφοράς",
            Content = "Η τρέχουσα βάση θα αντικατασταθεί. Θα δημιουργηθεί αυτόματα ένα νέο αντίγραφο πριν από την επαναφορά. Μετά την επαναφορά επανεκκινήστε την εφαρμογή.",
            PrimaryButtonText = "Επαναφορά", CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            await _backups.RestoreAsync(fileName);
            await RefreshAsync();
            var notice = new ContentDialog { XamlRoot = XamlRoot, Title = "Η επαναφορά ολοκληρώθηκε",
                Content = "Κλείστε και ανοίξτε ξανά την εφαρμογή πριν συνεχίσετε.", CloseButtonText = "Εντάξει" };
            await DialogButtonStyles.ShowAsync(notice);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private sealed record BackupRow(BackupFile File)
    {
        public string FileName => File.FileName;
        public string Created => File.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        public string Size => $"{File.SizeBytes / 1024d / 1024d:N2} MB";
    }
}

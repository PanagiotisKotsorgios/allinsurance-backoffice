using AllInsurance.Application.Authorization;
using AllInsurance.Application.Backups;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class BackupPage : Page
{
    private readonly IBackupService _backups = App.Services.GetRequiredService<IBackupService>();

    public BackupPage()
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        InitializeComponent();
        TableRowStriping.Attach(BackupsList);
        FolderText.Text = "Φάκελος αντιγράφων: " + _backups.BackupFolder;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    private async Task RefreshAsync()
    {
        try
        {
            var files = await _backups.ListAsync();
            BackupsList.Items.Clear();
            foreach (var file in files)
            {
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto } }, MinHeight = 58,
                    ColumnSpacing = 12 };
                var detail = new StackPanel { Spacing = 2 };
                detail.Children.Add(new TextBlock { Text = file.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                detail.Children.Add(new TextBlock { Text = $"{file.FileName} · {file.SizeBytes / 1024d / 1024d:N2} MB",
                    TextTrimming = TextTrimming.CharacterEllipsis });
                row.Children.Add(detail);
                var restore = new ActionButton { Kind = "Reset", Label = "Επαναφορά", Tag = file.FileName,
                    VerticalAlignment = VerticalAlignment.Center };
                restore.Click += Restore_Click;
                Grid.SetColumn(restore, 1);
                row.Children.Add(restore);
                BackupsList.Items.Add(row);
            }
            StatusText.Text = files.Count == 0 ? "Δεν υπάρχουν ακόμη αντίγραφα ασφαλείας." :
                $"{files.Count} διαθέσιμα αντίγραφα.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Δημιουργία αντιγράφου...";
            var file = await _backups.CreateAsync();
            await RefreshAsync();
            StatusText.Text = "Το αντίγραφο δημιουργήθηκε: " + file.FileName;
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ActionButton { Tag: string fileName }) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot,
            Title = "Επαναφορά αντιγράφου ασφαλείας",
            Content = "Η τρέχουσα βάση θα αντικατασταθεί από το επιλεγμένο αντίγραφο. " +
                "Πριν την αντικατάσταση θα δημιουργηθεί νέο αντίγραφο της τρέχουσας βάσης. " +
                "Μετά την επαναφορά κλείστε και ανοίξτε ξανά την εφαρμογή.",
            PrimaryButtonText = "Επαναφορά", CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            await _backups.RestoreAsync(fileName);
            await RefreshAsync();
            StatusText.Text = "Η επαναφορά ολοκληρώθηκε. Κλείστε και ανοίξτε ξανά την εφαρμογή πριν συνεχίσετε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

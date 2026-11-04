using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Settings;
using AllInsurance.Desktop.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspacePage : Page
{
    private readonly ISessionService _session;
    private readonly WorkspacePreferences _preferences;
    private readonly DispatcherTimer _databaseTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<TextBlock> _sidebarLabels = [];
    private readonly List<ToolTip> _sidebarTooltips = [];
    private readonly List<(string ActionId, MenuFlyoutItem Item)> _navigationShortcuts = [];
    private AllInsurance.Desktop.Logging.BackupScheduler? _backupScheduler;

    public WorkspacePage()
    {
        InitializeComponent();
        _session = App.Services.GetRequiredService<ISessionService>();
        _preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        if (!_session.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Απαιτείται σύνδεση για πρόσβαση στην εφαρμογή.");
        }

        UsersMenuItem.IsEnabled = _session.Current!.Permissions.Contains(Permission.ManageUsers);
        AuditMenuItem.IsEnabled = _session.Current.Permissions.Contains(Permission.ReadSecurityEvents);
        BackupMenuItem.IsEnabled = _session.Current.Permissions.Contains(Permission.ManageUsers);
        ErrorLogMenuItem.IsEnabled = _session.Current.Permissions.Contains(Permission.ManageUsers);
        CommissionMenuItem.IsEnabled = _session.Current.Permissions.Contains(Permission.ManageUsers);
        if (_session.Current.Permissions.Contains(Permission.ManageUsers))
        {
            _backupScheduler = new AllInsurance.Desktop.Logging.BackupScheduler(
                App.Services.GetRequiredService<AllInsurance.Application.Backups.IBackupService>());
            Unloaded += (_, _) => _backupScheduler?.Dispose();
        }
        SessionText.Text = $"Συνεδρία: {_session.Current.DisplayName} ({_session.Current.Username})";

        for (var index = 0; index < WorkspaceSections.All.Count; index++)
        {
            var section = WorkspaceSections.All[index];
            var sectionKey = section.Key;
            var actionId = "section:" + section.Key;
            var shortcut = _preferences.GetShortcut(actionId)?.Display ?? "";
            var menuItem = new MenuFlyoutItem { Text = $"{section.Title}      {shortcut}" };
            menuItem.Click += (_, _) => NavigateToSection(sectionKey);
            NavigationMenu.Items.Add(menuItem);
            _navigationShortcuts.Add((actionId, menuItem));
            var label = new TextBlock
            {
                Text = section.Title,
                FontSize = 18,
                Opacity = 0.88,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sidebarLabels.Add(label);
            var item = new NavigationViewItem
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new Image
                        {
                            Source = new BitmapImage(new Uri(section.Icon)),
                            Width = 38,
                            Height = 38,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        label
                    }
                },
                Tag = section.Key,
                MinHeight = 62
            };
            var tooltip = new ToolTip
            {
                Content = new TextBlock
                {
                    Text = section.Title,
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                },
                Padding = new Thickness(14, 9, 14, 9),
                IsEnabled = false
            };
            ToolTipService.SetToolTip(item, tooltip);
            _sidebarTooltips.Add(tooltip);
            MainNavigation.MenuItems.Add(item);
        }

        MainNavigation.IsPaneOpen = true;
        MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
        MainNavigation.PaneClosing += (_, _) => SetSidebarLabelsVisible(false);
        MainNavigation.PaneOpened += (_, _) =>
        {
            SetSidebarLabelsVisible(true);
            AnimateSidebarLabels();
        };
        SectionHost.Content = new CustomersPage();
        RegisterShortcuts();
        _preferences.ShortcutsChanged += Preferences_ShortcutsChanged;
        Unloaded += (_, _) => _preferences.ShortcutsChanged -= Preferences_ShortcutsChanged;
        _databaseTimer.Tick += async (_, _) => await RefreshDatabaseStatusAsync();
        Loaded += async (_, _) =>
        {
            SetSidebarLabelsVisible(MainNavigation.IsPaneOpen);
            AnimateSidebarLabels();
            try
            {
                await DemoDataSeeder.SeedOnceAsync(App.Services);
                if (MainNavigation.SelectedItem is NavigationViewItem { Tag: "customers" })
                    SectionHost.Content = new CustomersPage();
            }
            catch (Exception exception)
            {
                await UiAlerts.ShowErrorAsync(XamlRoot,
                    "Δεν ήταν δυνατή η δημιουργία δοκιμαστικών δεδομένων: " + exception.Message);
            }
            await RefreshDatabaseStatusAsync();
            _databaseTimer.Start();
        };
        Unloaded += (_, _) => _databaseTimer.Stop();
    }

    private void SetSidebarLabelsVisible(bool visible)
    {
        foreach (var label in _sidebarLabels)
            label.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tooltip in _sidebarTooltips)
            tooltip.IsEnabled = !visible;
    }

    private void AnimateSidebarLabels()
    {
        var storyboard = new Storyboard();
        foreach (var label in _sidebarLabels.Where(label => label.Visibility == Visibility.Visible))
        {
            label.Opacity = 0.88;
            var fade = new DoubleAnimation { From = 0.88, To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)) };
            Storyboard.SetTarget(fade, label);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
        }
        storyboard.Begin();
    }

    private async Task RefreshDatabaseStatusAsync()
    {
        try
        {
            var factory = App.Services.GetRequiredService<IApplicationDbContextFactory>();
            await using var db = await factory.CreateDbContextAsync();
            var connected = await db.Database.CanConnectAsync();
            DatabaseStatusText.Text = connected ? "Βάση δεδομένων: Συνδεδεμένη" : "Βάση δεδομένων: Χωρίς σύνδεση";
            DatabaseIndicator.Fill = new SolidColorBrush(connected
                ? Color.FromArgb(255, 28, 142, 82)
                : Color.FromArgb(255, 198, 40, 40));
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or System.IO.IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            DatabaseStatusText.Text = "Βάση δεδομένων: Χωρίς σύνδεση";
            DatabaseIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
        }
    }

    private void RegisterShortcuts()
    {
        RootGrid.KeyboardAccelerators.Clear();
        foreach (var spec in ShortcutCatalog.All)
        {
            var binding = _preferences.GetShortcut(spec.Id);
            if (binding is null) continue;
            AddShortcut(binding.Key, binding.Modifiers, () => ExecuteShortcut(spec.Id));
        }
    }

    private void Preferences_ShortcutsChanged(object? sender, EventArgs e)
    {
        RegisterShortcuts();
        foreach (var (actionId, item) in _navigationShortcuts)
            item.Text = WorkspaceSections.All.First(section => "section:" + section.Key == actionId).Title +
                "      " + (_preferences.GetShortcut(actionId)?.Display ?? "");
    }

    private void ExecuteShortcut(string actionId)
    {
        if (actionId.StartsWith("section:", StringComparison.Ordinal))
        {
            NavigateToSection(actionId["section:".Length..]);
            return;
        }
        switch (actionId)
        {
            case "users": ShowUsers(); break;
            case "audit": ShowAudit(); break;
            case "backups": ShowBackups(); break;
            case "errors": ShowErrorLog(); break;
            case "shortcuts": ShowShortcuts(); break;
            case "settings": ShowSettings(); break;
            case "guide": _ = ShowGuideAsync(); break;
            case "signout": SignOut(); break;
        }
    }

    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        shortcut.Invoked += (_, args) =>
        {
            if (!_preferences.ShortcutsEnabled || ShortcutCapture.IsCapturing)
            {
                return;
            }

            action();
            args.Handled = true;
        };
        RootGrid.KeyboardAccelerators.Add(shortcut);
    }

    private void NavigateToSection(string key)
    {
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", key);
        var section = WorkspaceSections.All.First(candidate => candidate.Key == key);
        var item = MainNavigation.MenuItems.OfType<NavigationViewItem>()
            .First(candidate => candidate.Tag is string itemKey && itemKey == key);
        MainNavigation.SelectedItem = item;
        SectionHost.Content = CreateSectionPage(section);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string key)
        {
            return;
        }

        var section = WorkspaceSections.All.FirstOrDefault(candidate => candidate.Key == key);
        if (section is not null)
        {
            AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", key);
            SectionHost.Content = CreateSectionPage(section);
        }
    }

    private void ShowUsers()
    {
        if (UsersMenuItem.IsEnabled)
        {
            AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "users");
            MainNavigation.SelectedItem = null;
            SectionHost.Content = new UserManagementPage();
        }
    }

    private void ShowAudit()
    {
        if (AuditMenuItem.IsEnabled)
        {
            AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "audit");
            MainNavigation.SelectedItem = null;
            SectionHost.Content = new AuditLogPage();
        }
    }

    private void ShowBackups()
    {
        if (!BackupMenuItem.IsEnabled) return;
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "backups");
        MainNavigation.SelectedItem = null;
        SectionHost.Content = new BackupPage();
    }

    private void ShowErrorLog()
    {
        if (!ErrorLogMenuItem.IsEnabled) return;
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "errors");
        MainNavigation.SelectedItem = null;
        SectionHost.Content = new ErrorLogPage();
    }

    private void ShowSettings() { MainNavigation.SelectedItem = null; SectionHost.Content = new SettingsPage(); }
    private void ShowFilterOptions() { MainNavigation.SelectedItem = null; SectionHost.Content = new FilterOptionsPage(); }
    private void ShowCommissions()
    {
        if (!CommissionMenuItem.IsEnabled) return;
        MainNavigation.SelectedItem = null;
        SectionHost.Content = new CommissionSettingsPage();
    }
    private void ShowShortcuts() { MainNavigation.SelectedItem = null; SectionHost.Content = new ShortcutsPage(); }

    private void Users_Click(object sender, RoutedEventArgs e) => ShowUsers();

    private void Audit_Click(object sender, RoutedEventArgs e) => ShowAudit();
    private void Backups_Click(object sender, RoutedEventArgs e) => ShowBackups();
    private void ErrorLog_Click(object sender, RoutedEventArgs e) => ShowErrorLog();
    private void NameDays_Click(object sender, RoutedEventArgs e)
    {
        MainNavigation.SelectedItem = null;
        SectionHost.Content = new NameDaysPage();
    }

    private static Page CreateSectionPage(WorkspaceSection section) => section.Key switch
    {
        "customers" => new CustomersPage(),
        "updates" => new CommunicationsPage(),
        "reports" => new ReportsPage(),
        "bridges" => new CompanyBridgesPage(),
        _ => new SectionPage(section)
    };
    private async void UpdateApplication_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var message = new TextBlock { Text = "Έλεγχος δημοσιευμένων εκδόσεων και αρχείων...", TextWrapping = TextWrapping.Wrap };
            var checking = new ContentDialog { XamlRoot = XamlRoot, Title = "Έλεγχος αναβάθμισης",
                Content = new StackPanel { Spacing = 14, MinWidth = 350, Children =
                {
                    message,
                    new ProgressBar { IsIndeterminate = true, Height = 8 },
                    new TextBlock { Text = "Επικοινωνία με το GitHub Releases" }
                } } };
            var checkingTask = checking.ShowAsync();
            AvailableUpdate? update;
            var minimumDelay = Task.Delay(TimeSpan.FromSeconds(4));
            try
            {
                update = await ApplicationUpdater.CheckAsync();
                message.Text = "Επαλήθευση έκδοσης και διαθέσιμου πακέτου...";
            }
            finally
            {
                await minimumDelay;
                checking.Hide();
                await checkingTask;
            }
            if (update is null)
            {
                await ShowInformationAsync("Αναβάθμιση εφαρμογής",
                    $"Χρησιμοποιείτε ήδη την πιο πρόσφατη δημοσιευμένη έκδοση ({ApplicationUpdater.CurrentVersion}).");
                return;
            }
            var confirmation = new ContentDialog { XamlRoot = XamlRoot, Title = "Νέα έκδοση διαθέσιμη",
                Content = $"Έκδοση {update.Version}. Να γίνει λήψη και επανεκκίνηση; Η τοπική βάση δεδομένων παραμένει στη θέση της.",
                PrimaryButtonText = "Λήψη και αναβάθμιση", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(confirmation) != ContentDialogResult.Primary) return;
            var progress = new ContentDialog { XamlRoot = XamlRoot, Title = "Αναβάθμιση εφαρμογής",
                Content = new StackPanel { Spacing = 12, Children =
                {
                    new TextBlock { Text = "Γίνεται λήψη και έλεγχος του πακέτου..." },
                    new ProgressRing { IsActive = true, Width = 36, Height = 36 }
                } } };
            var progressTask = progress.ShowAsync();
            try
            {
                var executable = await ApplicationUpdater.DownloadAndInstallAsync(update);
                progress.Hide();
                await progressTask;
                ApplicationUpdater.SwitchShortcutAndRestart(executable, ApplicationUpdater.CurrentVersion);
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
            catch
            {
                progress.Hide();
                await progressTask;
                throw;
            }
        }
        catch (System.Net.Http.HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await ShowInformationAsync("Αναβάθμιση εφαρμογής",
                "Δεν υπάρχει ακόμη δημοσιευμένη έκδοση στο GitHub Releases.");
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Η αναβάθμιση δεν ολοκληρώθηκε: " + exception.Message);
        }
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void FilterOptions_Click(object sender, RoutedEventArgs e) => ShowFilterOptions();
    private void Commissions_Click(object sender, RoutedEventArgs e) => ShowCommissions();
    private void Shortcuts_Click(object sender, RoutedEventArgs e) => ShowShortcuts();

    private async void Guide_Click(object sender, RoutedEventArgs e) => await ShowGuideAsync();
    private async void Contact_Click(object sender, RoutedEventArgs e) => await ShowContactAsync();
    private async void About_Click(object sender, RoutedEventArgs e) => await ShowAboutAsync();

    private async Task ShowGuideAsync()
    {
        var content = new StackPanel { Spacing = 13 };
        foreach (var (title, details) in new[]
        {
            ("1. Σύνδεση και πλοήγηση", "Συνδεθείτε με προσωπικό λογαριασμό. Επιλέξτε ενότητα από το αριστερό μενού ή την Πλοήγηση. Στο υποσέλιδο εμφανίζονται η κατάσταση της βάσης και ο τρέχων χρήστης."),
            ("2. Αναζήτηση και πίνακες", "Η αναζήτηση ενημερώνει ζωντανά τη λίστα. Χρησιμοποιήστε τα φίλτρα για περιορισμό αποτελεσμάτων, τις κεφαλίδες για ταξινόμηση, τα checkbox για μαζική επιλογή και το κουμπί στηλών για την προβολή τους. Διπλό κλικ ανοίγει καρτέλα, δεξί κλικ εμφανίζει γρήγορες ενέργειες."),
            ("3. Πελάτες", "Καταχωρίστε ταυτότητα, επικοινωνία, στοιχεία οδηγού και λοιπά πεδία. Τα tabs δείχνουν συμβόλαια, οχήματα, οικονομικά και ιστορικό ενεργειών. Πατήστε Επεξεργασία στην καρτέλα και μετά Αποθήκευση."),
            ("4. Συνεργάτες, εταιρείες και πρακτορεία", "Οι καρτέλες συνδέουν πρόσωπα, ασφαλιστήρια και οικονομικά. Οι εταιρείες και τα πρακτορεία περιλαμβάνουν πρόσωπα επικοινωνίας και αποδόσεις. Οι προμήθειες ρυθμίζονται ανά εταιρεία, κλάδο, χρήση, κάλυψη και πακέτο."),
            ("5. Οχήματα και ασφαλιστήρια", "Συνδέστε όχημα με πελάτη και δημιουργήστε αίτηση ή ασφαλιστήριο από τη σχετική καρτέλα. Συμπληρώστε ημερομηνίες, καλύψεις, ασφάλιστρα και συνεργάτη. Οι σχετικές εγγραφές εμφανίζονται σε κάθε συνδεδεμένη καρτέλα."),
            ("6. Ζημιές και ενημέρωση", "Καταχωρίστε ζημιές με σύνδεση σε πελάτη, όχημα και ασφαλιστήριο. Η Ενημέρωση δείχνει υποψήφιους παραλήπτες email/SMS. Η πραγματική αποστολή απαιτεί ρύθμιση παρόχων."),
            ("7. Αναφορές και γέφυρες", "Στις Αναφορές θα βρείτε συγκεντρωτικά στοιχεία με φίλτρα. Οι Γέφυρες εισάγουν τυποποιημένα αρχεία· για ειδικές μορφές εταιρειών απαιτείται αντιστοίχιση από δείγματα."),
            ("8. Εργαλεία διαχειριστή", "Διαχειριστείτε χρήστες και ρόλους, ελέγξτε το αρχείο ενεργειών και σφαλμάτων, δημιουργήστε ή προγραμματίστε αντίγραφα ασφαλείας. Πριν από επαναφορά ελέγξτε την καρτέλα αντιγράφου και κλείστε την εφαρμογή μετά την ολοκλήρωση."),
            ("9. Ρυθμίσεις και αναβάθμιση", "Επιλέξτε θέμα και βασικό νόμισμα στις Ρυθμίσεις. Αλλάξτε συντομεύσεις από τη διαχείρισή τους. Η Αναβάθμιση εφαρμογής ελέγχει τις εκδόσεις GitHub και διατηρεί την τοπική βάση δεδομένων.")
        })
        {
            content.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 15 });
            content.Children.Add(new TextBlock { Text = details, TextWrapping = TextWrapping.Wrap });
        }
        await ShowHelpAsync("Οδηγός χρήσης", content);
    }

    private async Task ShowContactAsync()
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock { Text = "KALYPSIS SOFTWARE SOLUTIONS",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        foreach (var (label, value) in new[] { ("Email", "info@mykalypsis.gr"),
            ("Τηλέφωνο", "2631028971") })
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 12 };
            row.Children.Add(new TextBlock { Text = $"{label}: {value}", VerticalAlignment = VerticalAlignment.Center });
            var copy = new AllInsurance.Desktop.Controls.ActionButton { Kind = "Open", Label = "Αντιγραφή" };
            copy.Click += (_, _) =>
            {
                var package = new DataPackage();
                package.SetText(value);
                Clipboard.SetContent(package);
                copy.Label = "Αντιγράφηκε";
            };
            Grid.SetColumn(copy, 1); row.Children.Add(copy);
            content.Children.Add(row);
        }
        await ShowHelpAsync("Επικοινωνία", content);
    }

    private async Task ShowAboutAsync()
    {
        var content = new StackPanel { Spacing = 10 };
        foreach (var line in new[]
        {
            "AllInsurance Backoffice Software",
            "Προϊόν της KALYPSIS SOFTWARE SOLUTIONS",
            $"Έκδοση {ApplicationUpdater.CurrentVersion}",
            "Τοπική εφαρμογή διαχείρισης ασφαλιστικού γραφείου με βάση SQLite.",
            "Ενότητες: πελάτες, συνεργάτες, οχήματα, εταιρείες, πρακτορεία, ασφαλιστήρια, ζημιές, ενημέρωση, αναφορές και γέφυρες εταιρειών.",
            "Δυνατότητες: συνδεδεμένες καρτέλες, οικονομικές κινήσεις, προμήθειες, αναζήτηση, φίλτρα, αρχείο ελέγχου και τοπικά αντίγραφα ασφαλείας.",
            "Οι αναβαθμίσεις εφαρμογής διατίθενται από το GitHub Releases. Η τοπική βάση δεδομένων παραμένει στον φάκελο δεδομένων χρήστη.",
            "Εικονίδια εργαλείων: Lucide (ISC/MIT)."
        }) content.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        await ShowHelpAsync("Σχετικά", content);
    }

    private async Task ShowHelpAsync(string title, UIElement body)
    {
        var branding = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        branding.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/logo-99.jpg")),
            Width = 60, Height = 60 });
        branding.Children.Add(new TextBlock { Text = "AllInsurance Backoffice Software",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 17,
            VerticalAlignment = VerticalAlignment.Center });
        var stack = new StackPanel { Spacing = 16, MinWidth = 440 };
        stack.Children.Add(branding);
        stack.Children.Add(body);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title,
            Content = new ScrollViewer { Content = stack, MaxHeight = 540 },
            CloseButtonText = "Κλείσιμο" };
        await DialogButtonStyles.ShowAsync(dialog, redClose: true);
    }

    private async Task ShowInformationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Κλείσιμο"
        };
        await DialogButtonStyles.ShowAsync(dialog);
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
        => SignOut();

    private void SignOut()
    {
        _session.EndSession();
        App.Navigation.NavigateTo(AppRoute.Login);
    }
}

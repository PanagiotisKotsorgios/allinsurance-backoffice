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
        SessionText.Text = $"Συνεδρία: {_session.Current.DisplayName} ({_session.Current.Username})";

        for (var index = 0; index < WorkspaceSections.All.Count; index++)
        {
            var section = WorkspaceSections.All[index];
            var sectionKey = section.Key;
            var shortcut = $"Ctrl+{(index + 1) % 10}";
            var menuItem = new MenuFlyoutItem { Text = $"{section.Title}      {shortcut}" };
            menuItem.Click += (_, _) => NavigateToSection(sectionKey);
            NavigationMenu.Items.Add(menuItem);
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
        var keys = new[]
        {
            VirtualKey.Number1, VirtualKey.Number2, VirtualKey.Number3, VirtualKey.Number4,
            VirtualKey.Number5, VirtualKey.Number6, VirtualKey.Number7, VirtualKey.Number8,
            VirtualKey.Number9, VirtualKey.Number0
        };
        for (var index = 0; index < keys.Length; index++)
        {
            var sectionKey = WorkspaceSections.All[index].Key;
            AddShortcut(keys[index], VirtualKeyModifiers.Control, () => NavigateToSection(sectionKey));
        }

        AddShortcut(VirtualKey.U, VirtualKeyModifiers.Control, ShowUsers);
        AddShortcut(VirtualKey.J, VirtualKeyModifiers.Control, ShowAudit);
        AddShortcut(VirtualKey.B, VirtualKeyModifiers.Control, ShowBackups);
        AddShortcut(VirtualKey.L, VirtualKeyModifiers.Control, ShowErrorLog);
        AddShortcut(VirtualKey.K, VirtualKeyModifiers.Control, ShowShortcuts);
        AddShortcut(VirtualKey.S, VirtualKeyModifiers.Control, ShowSettings);
        AddShortcut(VirtualKey.F1, (VirtualKeyModifiers)0, () => _ = ShowInformationAsync(
            "Οδηγός χρήσης", "Επιλέξτε ενότητα από το αριστερό μενού ή χρησιμοποιήστε τις συντομεύσεις πληκτρολογίου."));
        AddShortcut(VirtualKey.Q, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, SignOut);
    }

    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        shortcut.Invoked += (_, args) =>
        {
            if (!_preferences.ShortcutsEnabled)
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
            SectionHost.Content = CreateSectionPage(section);
        }
    }

    private void ShowUsers()
    {
        if (UsersMenuItem.IsEnabled)
        {
            MainNavigation.SelectedItem = null;
            SectionHost.Content = new UserManagementPage();
        }
    }

    private void ShowAudit()
    {
        if (AuditMenuItem.IsEnabled)
        {
            MainNavigation.SelectedItem = null;
            SectionHost.Content = new AuditLogPage();
        }
    }

    private void ShowBackups()
    {
        if (!BackupMenuItem.IsEnabled) return;
        MainNavigation.SelectedItem = null;
        SectionHost.Content = new BackupPage();
    }

    private void ShowErrorLog()
    {
        if (!ErrorLogMenuItem.IsEnabled) return;
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
            var update = await ApplicationUpdater.CheckAsync();
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

    private async void Guide_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Οδηγός χρήσης",
        "Επιλέξτε ενότητα από το αριστερό μενού ή από την Πλοήγηση. Τα Εργαλεία περιλαμβάνουν διαχείριση χρηστών, αρχείο ελέγχου, αντίγραφα ασφαλείας και ημερολόγιο σφαλμάτων για διαχειριστές. Οι συντομεύσεις φαίνονται στο μενού Πλοήγηση. Επιλέξτε Αποσύνδεση για να τερματίσετε τη συνεδρία.");

    private async void Contact_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Επικοινωνία",
        "KALYPSIS SOFTWARE SOLUTIONS\ninfo@mykalypsis.gr\n2631028971");

    private async void About_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Σχετικά",
        "AllInsurance Backoffice Software\nΠροϊόν της KALYPSIS SOFTWARE SOLUTIONS.\nΈκδοση 0.3 – Καρτέλες πελατών και συνεργατών.\nΕικονίδια εργαλείων: Lucide (ISC/MIT).");

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

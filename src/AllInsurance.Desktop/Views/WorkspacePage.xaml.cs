using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Navigation;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspacePage : Page
{
    private readonly ISessionService _session;
    private readonly WorkspacePreferences _preferences;
    private readonly DispatcherTimer _databaseTimer = new() { Interval = TimeSpan.FromSeconds(30) };

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
        SessionText.Text = $"Συνεδρία: {_session.Current.DisplayName} ({_session.Current.Username})";

        foreach (var section in WorkspaceSections.All)
        {
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
                        new TextBlock
                        {
                            Text = section.Title,
                            FontSize = 18,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Tag = section.Key,
                MinHeight = 62
            };
            MainNavigation.MenuItems.Add(item);
        }

        MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
        SectionHost.Content = new CustomersPage();
        RegisterShortcuts();
        _databaseTimer.Tick += async (_, _) => await RefreshDatabaseStatusAsync();
        Loaded += async (_, _) =>
        {
            App.MaximizeWorkspace();
            await RefreshDatabaseStatusAsync();
            _databaseTimer.Start();
        };
        Unloaded += (_, _) => _databaseTimer.Stop();
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
        SectionHost.Content = section.Key == "customers" ? new CustomersPage() : new SectionPage(section);
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
            SectionHost.Content = section.Key == "customers" ? new CustomersPage() : new SectionPage(section);
        }
    }

    private void Customers_Click(object sender, RoutedEventArgs e) => NavigateToSection("customers");
    private void Policies_Click(object sender, RoutedEventArgs e) => NavigateToSection("policies");
    private void Claims_Click(object sender, RoutedEventArgs e) => NavigateToSection("claims");
    private void Reports_Click(object sender, RoutedEventArgs e) => NavigateToSection("reports");

    private void ShowUsers()
    {
        if (UsersMenuItem.IsEnabled)
        {
            SectionHost.Content = new UserManagementPage();
        }
    }

    private void ShowAudit()
    {
        if (AuditMenuItem.IsEnabled)
        {
            SectionHost.Content = new AuditLogPage();
        }
    }

    private void ShowSettings() => SectionHost.Content = new SettingsPage();
    private void ShowShortcuts() => SectionHost.Content = new ShortcutsPage();

    private void Users_Click(object sender, RoutedEventArgs e) => ShowUsers();

    private void Audit_Click(object sender, RoutedEventArgs e) => ShowAudit();
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void Shortcuts_Click(object sender, RoutedEventArgs e) => ShowShortcuts();

    private async void Guide_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Οδηγός χρήσης",
        "Επιλέξτε ενότητα από το αριστερό μενού. Τα Εργαλεία περιλαμβάνουν τη διαχείριση χρηστών και το αρχείο ελέγχου για διαχειριστές. Κλείστε την εφαρμογή ή επιλέξτε Αποσύνδεση για να τερματίσετε τη συνεδρία.");

    private async void Contact_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Επικοινωνία",
        "KALYPSIS SOFTWARE SOLUTIONS\ninfo@mykalypsis.gr\n2631028971");

    private async void About_Click(object sender, RoutedEventArgs e) => await ShowInformationAsync(
        "Σχετικά",
        "AllInsurance Backoffice Software\nΠροϊόν της KALYPSIS SOFTWARE SOLUTIONS.\nΈκδοση 0.2 – Διεπαφή εργασίας.\nΕικονίδια εργαλείων: Lucide (ISC/MIT).");

    private async Task ShowInformationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Κλείσιμο"
        };
        await dialog.ShowAsync();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
        => SignOut();

    private void SignOut()
    {
        _session.EndSession();
        App.Navigation.NavigateTo(AppRoute.Login);
    }
}

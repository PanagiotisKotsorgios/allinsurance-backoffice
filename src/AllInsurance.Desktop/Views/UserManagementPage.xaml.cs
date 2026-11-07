using AllInsurance.Application.Authorization;
using AllInsurance.Application.Users;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace AllInsurance.Desktop.Views;

public sealed partial class UserManagementPage : Page
{
    private readonly IUserAdministrationService _users;
    private UserRow[] _rows = [];

    public UserManagementPage()
    {
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Χρήστες",
            ["Χρήστης", "Όνομα χρήστη", "Ρόλοι", "Κατάσταση", "Κωδικός πρόσβασης"],
            (UsersList.ItemsSource as IEnumerable<UserRow> ?? [])
                .Select(row => new[] { row.DisplayName, row.Username, row.Roles,
                    row.State, row.PasswordState }).ToArray()));
        TableRowStriping.Attach(UsersList);
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        _users = App.Services.GetRequiredService<IUserAdministrationService>();
        StateFilter.SelectedIndex = 0;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private UserAccountSummary? SelectedUser => (UsersList.SelectedItem as UserRow)?.Account;

    private async Task RefreshAsync()
    {
        try
        {
            var accounts = await _users.GetUsersAsync();
            _rows = accounts.Select(account => new UserRow(account)).ToArray();
            var selectedRole = (RoleFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            RoleFilter.Items.Clear();
            RoleFilter.Items.Add(new ComboBoxItem { Content = "Όλοι οι ρόλοι", Tag = "" });
            foreach (var role in await _users.GetRolesAsync())
                RoleFilter.Items.Add(new ComboBoxItem { Content = GreekRole(role), Tag = role });
            RoleFilter.SelectedItem = RoleFilter.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == selectedRole) ?? RoleFilter.Items[0];
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        StateFilter.SelectedIndex = 0;
        RoleFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (UsersList is null || SearchInput is null || StateFilter is null || RoleFilter is null) return;
        var term = SearchInput.Text.Trim();
        var role = (RoleFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var filtered = _rows.Where(row =>
            (term.Length == 0 || row.DisplayName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             row.Username.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             row.Roles.Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            (StateFilter.SelectedIndex == 0 || row.Account.IsActive == (StateFilter.SelectedIndex == 1)) &&
            (string.IsNullOrEmpty(role) || row.Account.Roles.Contains(role))).ToArray();
        UsersList.ItemsSource = filtered;
        StatusText.Text = $"{filtered.Length} από {_rows.Length} χρήστες";
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var username = new TextBox { Header = "Όνομα χρήστη *" };
        var fullName = new TextBox { Header = "Ονοματεπώνυμο *" };
        var password = new PasswordBox { Header = "Προσωρινός κωδικός *" };
        var role = new ComboBox { Header = "Ρόλος", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var name in await _users.GetRolesAsync())
            role.Items.Add(new ComboBoxItem { Content = GreekRole(name), Tag = name });
        role.SelectedIndex = 0;
        var form = new StackPanel { Spacing = 10, Children = { username, fullName, password, role } };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Νέος χρήστης",
            Content = form, PrimaryButtonText = "Δημιουργία", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            await _users.CreateUserAsync(username.Text, fullName.Text, password.Password,
                [((ComboBoxItem)role.SelectedItem).Tag.ToString()!]);
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Users_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => await ShowCardAsync();

    private async Task ShowCardAsync()
    {
        if (SelectedUser is not { } account) return;
        var info = new StackPanel { Spacing = 8, MinWidth = 380 };
        foreach (var line in new[] {
            $"Ονοματεπώνυμο: {account.DisplayName}", $"Όνομα χρήστη: {account.Username}",
            $"Ρόλοι: {string.Join(", ", account.Roles.Select(GreekRole))}",
            $"Κατάσταση: {(account.IsActive ? "Ενεργός" : "Ανενεργός")}",
            $"Αλλαγή κωδικού: {(account.MustChangePassword ? "Απαιτείται στην επόμενη σύνδεση" : "Δεν εκκρεμεί")}",
            $"Τελευταία σύνδεση: {account.LastLoginAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—"}",
            $"Αποτυχημένες προσπάθειες: {account.FailedLoginCount}",
            $"Κλείδωμα έως: {account.LockoutUntilUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—"}",
            $"Δημιουργία: {account.CreatedAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—"}",
            $"Τελευταία ενημέρωση: {account.UpdatedAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—"}" })
            info.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Καρτέλα χρήστη",
            Content = info, PrimaryButtonText = "Επεξεργασία", SecondaryButtonText = "Ρόλοι",
            CloseButtonText = "Κλείσιμο" };
        var result = await DialogButtonStyles.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary) await EditAsync();
        else if (result == ContentDialogResult.Secondary) await RolesAsync();
    }

    private void Users_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ListViewItem)
            source = VisualTreeHelper.GetParent(source);
        if (source is not ListViewItem { Content: UserRow row }) return;
        UsersList.SelectedItem = row;
        var menu = new MenuFlyout();
        AddAction(menu, "Άνοιγμα καρτέλας", async () => await ShowCardAsync());
        AddAction(menu, "Επεξεργασία", async () => await EditAsync());
        AddAction(menu, "Ρόλοι", async () => await RolesAsync());
        AddAction(menu, "Επαναφορά κωδικού", async () => await ResetAsync());
        AddAction(menu, row.Account.IsActive ? "Απενεργοποίηση" : "Ενεργοποίηση",
            async () => await ToggleActiveAsync());
        AddAction(menu, "Διαγραφή χρήστη", async () => await DeleteAsync());
        menu.ShowAt((FrameworkElement)source);
        e.Handled = true;
    }

    private static void AddAction(MenuFlyout menu, string title, Func<Task> action)
    {
        var item = new MenuFlyoutItem { Text = title };
        item.Click += async (_, _) => await action();
        menu.Items.Add(item);
    }

    private async Task EditAsync()
    {
        if (SelectedUser is not { } account) return;
        var username = new TextBox { Header = "Όνομα χρήστη", Text = account.Username };
        var name = new TextBox { Header = "Ονοματεπώνυμο", Text = account.DisplayName };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Επεξεργασία χρήστη",
            Content = new StackPanel { Spacing = 10, Children = { username, name } },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        await RunAsync(() => _users.UpdateUserAsync(account.Id, username.Text, name.Text));
    }

    private async Task RolesAsync()
    {
        if (SelectedUser is not { } account) return;
        var roles = await _users.GetRolesAsync();
        var selector = new ComboBox { Header = "Ρόλος", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var role in roles)
            selector.Items.Add(new ComboBoxItem { Content = GreekRole(role), Tag = role });
        selector.SelectedIndex = 0;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"Ρόλοι: {account.DisplayName}",
            Content = selector, PrimaryButtonText = "Προσθήκη", SecondaryButtonText = "Αφαίρεση",
            CloseButtonText = "Ακύρωση" };
        var result = await DialogButtonStyles.ShowAsync(dialog);
        if (result == ContentDialogResult.None) return;
        var name = ((ComboBoxItem)selector.SelectedItem).Tag.ToString()!;
        await RunAsync(() => result == ContentDialogResult.Primary
            ? _users.AssignRoleAsync(account.Id, name) : _users.RevokeRoleAsync(account.Id, name));
    }

    private async void ManageRoles_Click(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Header = "Όνομα νέου ρόλου" };
        var description = new TextBox { Header = "Περιγραφή" };
        var form = new StackPanel { Spacing = 10, Children = { name, description,
            new TextBlock { Text = "Ο νέος ρόλος ομαδοποιεί χρήστες. Δικαιώματα διαχειριστή παρέχει μόνο ο ρόλος «Διαχειριστής».",
                TextWrapping = TextWrapping.Wrap } } };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Δημιουργία ρόλου",
            Content = form, PrimaryButtonText = "Δημιουργία", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        await RunAsync(() => _users.CreateRoleAsync(name.Text, description.Text));
    }

    private async Task ResetAsync()
    {
        if (SelectedUser is not { } account) return;
        var password = new PasswordBox { Header = "Νέος προσωρινός κωδικός" };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Επαναφορά κωδικού",
            Content = password, PrimaryButtonText = "Επαναφορά", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        await RunAsync(() => _users.ResetEmployeePasswordAsync(account.Id, password.Password));
    }

    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is not { } account) return;
        var title = account.IsActive ? "Απενεργοποίηση" : "Ενεργοποίηση";
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"{title} χρήστη",
            Content = $"{title} του λογαριασμού {account.Username};",
            PrimaryButtonText = title, CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: account.IsActive) != ContentDialogResult.Primary) return;
        await RunAsync(() => account.IsActive
            ? _users.DeactivateUserAsync(account.Id) : _users.ActivateUserAsync(account.Id));
    }

    private async Task DeleteAsync()
    {
        if (SelectedUser is not { } account) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή χρήστη",
            Content = "Η διαγραφή λογαριασμού με ιστορικό ενεργειών μπορεί να διακόψει την ιχνηλασιμότητα. Επιβεβαιώστε την ενέργεια.",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        await RunAsync(() => _users.DeleteUserAsync(account.Id));
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); await RefreshAsync(); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private static string GreekRole(string role) => role.ToUpperInvariant() switch
    {
        "ADMINISTRATOR" => "Διαχειριστής", "EMPLOYEE" => "Υπάλληλος", _ => role
    };

    private sealed record UserRow(UserAccountSummary Account)
    {
        public string DisplayName => Account.DisplayName;
        public string Username => Account.Username;
        public string Roles => string.Join(", ", Account.Roles.Select(GreekRole));
        public string State => Account.IsActive ? "Ενεργός" : "Ανενεργός";
        public string PasswordState => Account.MustChangePassword ? "Αλλαγή στην είσοδο" : "Εντάξει";
    }
}

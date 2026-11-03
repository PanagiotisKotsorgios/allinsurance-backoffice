using AllInsurance.Application.Authorization;
using AllInsurance.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class UserManagementPage : Page
{
    private readonly IUserAdministrationService _users;

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    public UserManagementPage()
    {
        InitializeComponent();
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
        {
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        }

        _users = App.Services.GetRequiredService<IUserAdministrationService>();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private UserAccountSummary? SelectedUser => (UsersList.SelectedItem as UserRow)?.Account;

    private async Task RefreshAsync()
    {
        try
        {
            var accounts = await _users.GetUsersAsync();
            UsersList.ItemsSource = accounts.Select(account => new UserRow(
                account,
                account.DisplayName,
                $"{account.Username} · {string.Join(", ", account.Roles.Select(GreekRole))} · " +
                (account.IsActive ? "Ενεργός" : "Ανενεργός") +
                (account.MustChangePassword ? " · Απαιτείται αλλαγή κωδικού" : string.Empty))).ToArray();
            StatusText.Text = accounts.Count == 1 ? "1 χρήστης" : $"{accounts.Count} χρήστες";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = "Δεν είναι δυνατή η προβολή των χρηστών. Συνδεθείτε ξανά με λογαριασμό διαχειριστή.";
        }
    }

    private static string GreekRole(string role) => role.ToUpperInvariant() switch
    {
        "ADMINISTRATOR" => "Διαχειριστής",
        "EMPLOYEE" => "Υπάλληλος",
        _ => role
    };

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var username = new TextBox { Header = "Όνομα χρήστη" };
        var fullName = new TextBox { Header = "Ονοματεπώνυμο" };
        var password = new PasswordBox { Header = "Προσωρινός κωδικός πρόσβασης" };
        var role = new ComboBox { Header = "Ρόλος" };
        role.Items.Add(new ComboBoxItem { Content = "Υπάλληλος", Tag = "Employee" });
        role.Items.Add(new ComboBoxItem { Content = "Διαχειριστής", Tag = "Administrator" });
        role.SelectedIndex = 0;
        var form = new StackPanel { Spacing = 10 };
        form.Children.Add(username);
        form.Children.Add(fullName);
        form.Children.Add(password);
        form.Children.Add(role);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Νέος χρήστης",
            Content = form,
            PrimaryButtonText = "Δημιουργία",
            CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _users.CreateUserAsync(username.Text, fullName.Text, password.Password,
                [((ComboBoxItem)role.SelectedItem).Tag.ToString()!]);
            StatusText.Text = "Ο χρήστης δημιουργήθηκε. Δώστε του τον προσωρινό κωδικό με ασφαλή τρόπο.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusText.Text = ToGreekError(exception);
        }
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser is not { } account)
        {
            StatusText.Text = "Επιλέξτε πρώτα έναν χρήστη.";
            return;
        }

        var password = new PasswordBox { Header = "Νέος προσωρινός κωδικός" };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Επαναφορά κωδικού: {account.DisplayName}",
            Content = password,
            PrimaryButtonText = "Επαναφορά",
            CloseButtonText = "Ακύρωση"
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _users.ResetEmployeePasswordAsync(account.Id, password.Password);
            StatusText.Text = "Ο προσωρινός κωδικός ενημερώθηκε.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusText.Text = ToGreekError(exception);
        }
    }

    private async void Roles_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser is not { } account)
        {
            StatusText.Text = "Επιλέξτε πρώτα έναν χρήστη.";
            return;
        }

        var role = new ComboBox { Header = "Ρόλος" };
        role.Items.Add(new ComboBoxItem { Content = "Υπάλληλος", Tag = "Employee" });
        role.Items.Add(new ComboBoxItem { Content = "Διαχειριστής", Tag = "Administrator" });
        role.SelectedIndex = 0;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Ρόλοι: {account.DisplayName}",
            Content = role,
            PrimaryButtonText = "Προσθήκη",
            SecondaryButtonText = "Αφαίρεση",
            CloseButtonText = "Ακύρωση"
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None)
        {
            return;
        }

        try
        {
            var selectedRole = ((ComboBoxItem)role.SelectedItem).Tag.ToString()!;
            if (result == ContentDialogResult.Primary)
            {
                await _users.AssignRoleAsync(account.Id, selectedRole);
            }
            else
            {
                await _users.RevokeRoleAsync(account.Id, selectedRole);
            }
            StatusText.Text = "Οι ρόλοι ενημερώθηκαν.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusText.Text = ToGreekError(exception);
        }
    }

    private async void Deactivate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUser is not { } account)
        {
            StatusText.Text = "Επιλέξτε πρώτα έναν χρήστη.";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Απενεργοποίηση χρήστη",
            Content = $"Να απενεργοποιηθεί ο χρήστης {account.DisplayName};",
            PrimaryButtonText = "Απενεργοποίηση",
            CloseButtonText = "Ακύρωση"
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _users.DeactivateUserAsync(account.Id);
            StatusText.Text = "Ο χρήστης απενεργοποιήθηκε.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusText.Text = ToGreekError(exception);
        }
    }

    private static string ToGreekError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Δεν έχετε δικαίωμα για αυτή την ενέργεια.",
        ArgumentException => "Ελέγξτε τα στοιχεία. Ο κωδικός πρέπει να έχει τουλάχιστον 12 χαρακτήρες.",
        _ => exception.Message.Split(Environment.NewLine)[0]
    };

    private sealed record UserRow(UserAccountSummary Account, string DisplayName, string SecondaryLine)
    {
        public override string ToString() => DisplayName;
    }
}

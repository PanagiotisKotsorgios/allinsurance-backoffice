using AllInsurance.Application.Authorization;
using AllInsurance.Application.Authentication;
using AllInsurance.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class AuditLogPage : Page
{
    private readonly IUserAdministrationService _users;

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    public AuditLogPage()
    {
        InitializeComponent();
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(EventsList);
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ReadSecurityEvents) != true)
        {
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        }

        _users = App.Services.GetRequiredService<IUserAdministrationService>();
        Loaded += async (_, _) => await RefreshAsync();
    }


    private async Task RefreshAsync()
    {
        try
        {
            var events = await _users.GetSecurityEventsAsync();
            EventsList.ItemsSource = events.Select(entry => new AuditRow(
                GreekEvent(entry),
                $"{entry.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · " +
                (entry.Outcome == "SUCCESS" ? "Επιτυχία" : "Αποτυχία") +
                (entry.Username is null ? string.Empty : $" · {entry.Username}"))).ToArray();
            StatusText.Text = events.Count == 1 ? "1 πρόσφατο συμβάν" : $"{events.Count} πρόσφατα συμβάντα";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = "Δεν είναι δυνατή η προβολή του αρχείου ελέγχου.";
        }
    }

    private static string GreekEvent(SecurityEventSummary entry) => entry.EventType switch
    {
        "ADMIN_CREATED" => "Δημιουργία διαχειριστή",
        "LOGIN_SUCCESS" => "Επιτυχής σύνδεση",
        "LOGIN_FAILURE" => "Αποτυχημένη σύνδεση",
        "ACCOUNT_LOCKED" => "Κλείδωμα λογαριασμού",
        "PASSWORD_RESET" => "Επαναφορά κωδικού",
        "PASSWORD_CHANGED" => "Αλλαγή κωδικού",
        "RECOVERY_FAILURE" => "Αποτυχημένη ανάκτηση",
        "RECOVERY_CODE_ISSUED" => "Έκδοση κωδικού ανάκτησης",
        "USER_CREATED" => "Δημιουργία χρήστη",
        "USER_DEACTIVATED" => "Απενεργοποίηση χρήστη",
        "ROLE_CHANGED" => "Αλλαγή ρόλου",
        "ADMINISTRATOR_REPAIR_REQUIRED" => "Απαιτείται ανάκτηση διαχειριστή",
        _ => "Συμβάν ασφάλειας"
    };

    private sealed record AuditRow(string Title, string SecondaryLine)
    {
        public override string ToString() => Title;
    }
}

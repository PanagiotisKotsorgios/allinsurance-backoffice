using AllInsurance.Application.Authorization;
using AllInsurance.Application.Authentication;
using AllInsurance.Application.Users;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AllInsurance.Desktop.Views;

public sealed partial class AuditLogPage : Page
{
    private readonly IUserAdministrationService _users;
    private AuditRow[] _rows = [];

    public AuditLogPage()
    {
        InitializeComponent();
        TableRowStriping.Attach(EventsList);
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ReadSecurityEvents) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        _users = App.Services.GetRequiredService<IUserAdministrationService>();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _rows = (await _users.GetSecurityEventsAsync(10000)).Select(entry => new AuditRow(entry)).ToArray();
            FillFilter(ActionFilter, "Όλες οι ενέργειες", _rows.Select(row => row.Action));
            FillFilter(EntityFilter, "Όλες οι οντότητες", _rows.Select(row => row.Entity));
            FillFilter(UserFilter, "Όλοι οι χρήστες", _rows.Select(row => row.User));
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private static void FillFilter(ComboBox box, string all, IEnumerable<string> values)
    {
        var selected = (box.SelectedItem as ComboBoxItem)?.Content?.ToString();
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = all });
        foreach (var value in values.Where(value => value != "—").Distinct().OrderBy(value => value))
            box.Items.Add(new ComboBoxItem { Content = value });
        box.SelectedItem = box.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Content?.ToString() == selected) ?? box.Items[0];
    }

    private void Filter_Changed(object sender, object args) => ApplyFilters();

    private void ApplyFilters()
    {
        if (EventsList is null || SearchInput is null || ActionFilter is null ||
            EntityFilter is null || UserFilter is null || StatusText is null) return;
        var term = SearchInput.Text.Trim();
        var action = ActionFilter.SelectedIndex > 0 ? (ActionFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() : null;
        var entity = EntityFilter.SelectedIndex > 0 ? (EntityFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() : null;
        var user = UserFilter.SelectedIndex > 0 ? (UserFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() : null;
        var from = FromDate?.Date?.Date;
        var until = UntilDate?.Date?.Date;
        var filtered = _rows.Where(row =>
            (term.Length == 0 || (row.When + row.User + row.Action + row.Entity + row.Details)
                .Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            (action is null || row.Action == action) && (entity is null || row.Entity == entity) &&
            (user is null || row.User == user) &&
            (from is null || row.Entry.CreatedAtUtc.ToLocalTime().Date >= from) &&
            (until is null || row.Entry.CreatedAtUtc.ToLocalTime().Date <= until)).ToArray();
        EventsList.ItemsSource = filtered;
        StatusText.Text = $"{filtered.Length} από {_rows.Length} συμβάντα";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = ""; ActionFilter.SelectedIndex = 0;
        EntityFilter.SelectedIndex = 0; UserFilter.SelectedIndex = 0;
        FromDate.Date = null; UntilDate.Date = null;
        ApplyFilters();
    }

    private async void Events_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (EventsList.SelectedItem is not AuditRow row) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Καρτέλα συμβάντος",
            Content = new TextBox { Text = $"{row.When}\nΧρήστης: {row.User}\nΕνέργεια: {row.Action}\nΟντότητα: {row.Entity}\nΑποτέλεσμα: {row.Outcome}\n\n{row.Details}",
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Width = 520, Height = 250 }, CloseButtonText = "Κλείσιμο" };
        await DialogButtonStyles.ShowAsync(dialog);
    }

    private async void Export_Click(object sender, RoutedEventArgs e) => await ExportAsync(false);
    private async void Print_Click(object sender, RoutedEventArgs e) => await ExportAsync(true);

    private async Task ExportAsync(bool print)
    {
        try
        {
            var rows = (EventsList.ItemsSource as IEnumerable<AuditRow>) ?? [];
            string[] headers = ["Ημερομηνία / ώρα", "Χρήστης", "Ενέργεια", "Οντότητα", "Αποτέλεσμα", "Λεπτομέρειες"];
            var data = rows.Select(row => new[] { row.When, row.User, row.Action, row.Entity, row.Outcome, row.Details });
            var path = print ? ToolTableExport.Print("Αρχείο ελέγχου", headers, data)
                : ToolTableExport.Csv("Αρχείο ελέγχου", headers, data);
            StatusText.Text = $"Το αρχείο δημιουργήθηκε: {path}";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private sealed record AuditRow(SecurityEventSummary Entry)
    {
        public string When => Entry.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        public string User => Entry.Username ?? "—";
        public string Action => Entry.EventType.Split(':')[0] switch
        {
            "CREATE" => "Δημιουργία", "UPDATE" => "Επεξεργασία", "DELETE" => "Διαγραφή",
            "LOGIN_SUCCESS" => "Σύνδεση", "LOGIN_FAILURE" => "Αποτυχία σύνδεσης",
            "USER_CREATED" => "Δημιουργία χρήστη", "USER_UPDATED" => "Επεξεργασία χρήστη",
            "USER_ACTIVATED" => "Ενεργοποίηση χρήστη", "USER_DEACTIVATED" => "Απενεργοποίηση χρήστη",
            "ROLE_CREATED" => "Δημιουργία ρόλου", "ROLE_CHANGED" => "Αλλαγή ρόλου",
            _ => Entry.EventType
        };
        public string Entity => Entry.EventType.Contains(':') ? Entry.EventType.Split(':')[1] : "Σύστημα";
        public string Outcome => Entry.Outcome == "SUCCESS" ? "Επιτυχία" : "Αποτυχία";
        public string Details => Entry.Details ?? "—";
    }
}

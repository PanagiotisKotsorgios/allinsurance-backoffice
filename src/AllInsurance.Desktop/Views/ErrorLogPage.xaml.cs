using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class ErrorLogPage : Page
{
    private ErrorRow[] _rows = [];

    public ErrorLogPage()
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Ημερολόγιο σφαλμάτων",
            ["Ημερομηνία / ώρα", "Ενότητα", "Μήνυμα", "Τεχνικές λεπτομέρειες"],
            (ErrorsList.ItemsSource as IEnumerable<ErrorRow> ?? [])
                .Select(row => new[] { row.When, row.Category, row.Message,
                    row.Entry.Exception ?? "" }).ToArray()));
        TableRowStriping.Attach(ErrorsList);
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        try
        {
            _rows = LocalErrorLogStore.Recent(10000).Select(entry => new ErrorRow(entry)).ToArray();
            CategoryFilter.Items.Clear();
            CategoryFilter.Items.Add(new ComboBoxItem { Content = "Όλες οι ενότητες" });
            foreach (var category in _rows.Select(row => row.Category).Distinct().OrderBy(value => value))
                CategoryFilter.Items.Add(new ComboBoxItem { Content = category });
            CategoryFilter.SelectedIndex = 0;
            ApplyFilters();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { StatusText.Text = "Δεν ήταν δυνατή η ανάγνωση του ημερολογίου: " + exception.Message; }
    }

    private void Filter_Changed(object sender, object args) => ApplyFilters();
    private void ApplyFilters()
    {
        if (ErrorsList is null || SearchInput is null || CategoryFilter is null) return;
        var term = SearchInput.Text.Trim();
        var category = CategoryFilter.SelectedIndex > 0
            ? (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() : null;
        var from = FromDate?.Date?.Date;
        var until = UntilDate?.Date?.Date;
        var filtered = _rows.Where(row =>
            (term.Length == 0 || (row.Category + row.Message + row.Entry.Exception)
                .Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            (category is null || row.Category == category) &&
            (from is null || row.Entry.TimestampUtc.ToLocalTime().Date >= from) &&
            (until is null || row.Entry.TimestampUtc.ToLocalTime().Date <= until)).ToArray();
        ErrorsList.ItemsSource = filtered;
        StatusText.Text = $"{filtered.Length} από {_rows.Length} σφάλματα";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = ""; CategoryFilter.SelectedIndex = 0;
        FromDate.Date = null; UntilDate.Date = null; ApplyFilters();
    }

    private async void Errors_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ErrorsList.SelectedItem is not ErrorRow row) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Καρτέλα σφάλματος",
            Content = new TextBox { Text = $"{row.When}\nΕνότητα: {row.Category}\n\n{row.Message}\n\n{row.Entry.Exception}",
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Width = 540, Height = 320 }, CloseButtonText = "Κλείσιμο" };
        await DialogButtonStyles.ShowAsync(dialog);
    }

    private async void Export_Click(object sender, RoutedEventArgs e) => await ExportAsync(false);
    private async void Print_Click(object sender, RoutedEventArgs e) => await ExportAsync(true);
    private async Task ExportAsync(bool print)
    {
        try
        {
            var rows = (ErrorsList.ItemsSource as IEnumerable<ErrorRow>) ?? [];
            string[] headers = ["Ημερομηνία / ώρα", "Ενότητα", "Μήνυμα", "Τεχνικές λεπτομέρειες"];
            var data = rows.Select(row => new[] { row.When, row.Category, row.Message, row.Entry.Exception ?? "" });
            var path = print ? ToolTableExport.Print("Ημερολόγιο σφαλμάτων", headers, data)
                : ToolTableExport.Csv("Ημερολόγιο σφαλμάτων", headers, data);
            StatusText.Text = $"Το αρχείο δημιουργήθηκε: {path}";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private sealed record ErrorRow(LocalErrorEntry Entry)
    {
        public string When => Entry.TimestampUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        public string Category => Entry.Category;
        public string Message => Entry.Message;
    }
}

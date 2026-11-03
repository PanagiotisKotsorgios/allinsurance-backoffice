using AllInsurance.Application.Authorization;
using AllInsurance.Desktop.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class ErrorLogPage : Page
{
    private LocalErrorEntry[] _entries = [];
    private bool _showing;

    public ErrorLogPage()
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
        InitializeComponent();
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(ErrorsList);
        PathText.Text = "Αρχείο: " + LocalErrorLogStore.PathOnDisk;
        Loaded += (_, _) => Load();
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    private void Load()
    {
        try
        {
            _entries = [.. LocalErrorLogStore.Recent()];
            ApplyFilter();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { StatusText.Text = "Δεν ήταν δυνατή η ανάγνωση του ημερολογίου: " + exception.Message; }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (ErrorsList is null || SearchInput is null) return;
        var term = SearchInput.Text.Trim();
        var selected = _entries.Where(item => term.Length == 0 ||
            item.Category.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            item.Message.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            item.Exception?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true)
            .Select(item => new ErrorRow(item)).ToArray();
        ErrorsList.ItemsSource = selected;
        StatusText.Text = selected.Length == 0 ? "Δεν υπάρχουν καταγεγραμμένα σφάλματα." :
            $"{selected.Length} σφάλματα · Εμφανίζονται έως 300 πρόσφατες εγγραφές.";
    }

    private async void Errors_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showing || ErrorsList.SelectedItem is not ErrorRow row) return;
        _showing = true;
        try
        {
            var detail = new TextBox { Text = row.Entry.Exception ?? row.Entry.Message,
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Width = 520, Height = 300 };
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = row.Heading, Content = detail, CloseButtonText = "Κλείσιμο" };
            await DialogButtonStyles.ShowAsync(dialog);
        }
        finally { ErrorsList.SelectedItem = null; _showing = false; }
    }

    private sealed record ErrorRow(LocalErrorEntry Entry)
    {
        public string Heading => $"{Entry.TimestampUtc.ToLocalTime():dd/MM/yyyy HH:mm:ss} · {Entry.Category}";
        public string Message => Entry.Message;
    }
}

using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class SectionPage : Page
{
    private readonly WorkspaceSection _section;
    private readonly IBusinessRecordService _records;
    private readonly ICustomerService _customers;
    private CustomerCardWindow? _card;
    private CustomerChoice[] _customerChoices = [];
    private int _requestVersion;
    private BusinessRecordRow[] _filtered = [];
    private int _page = 1;
    private const int PageSize = 25;

    public SectionPage(WorkspaceSection section)
    {
        _section = section;
        _records = App.Services.GetRequiredService<IBusinessRecordService>();
        _customers = App.Services.GetRequiredService<ICustomerService>();
        InitializeComponent();
        var accent = Color.FromArgb(255,
            Convert.ToByte(section.Accent.Substring(1, 2), 16),
            Convert.ToByte(section.Accent.Substring(3, 2), 16),
            Convert.ToByte(section.Accent.Substring(5, 2), 16));
        IconBackground.Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B));
        SectionIcon.Source = new BitmapImage(new Uri(section.Icon));
        SectionTitle.Text = section.Title;
        SectionDescription.Text = section.Description;
        AddButton.Label = "Προσθήκη " + Singular(section.Title);
        SearchInput.PlaceholderText = section.SearchHint;
        TitleHeader.Text = section.Columns[0];
        PartyHeader.Text = section.Columns.Length > 1 ? section.Columns[1] : "Στοιχεία";
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await RefreshAsync();
        };
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(180, Math.Min(1380, e.NewSize.Width - 48));

    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customers.SearchAsync();
            _customerChoices = [new CustomerChoice(null, "Όλοι οι πελάτες"),
                .. customers.Select(item => new CustomerChoice(item.Id, item.FullName))];
            CustomerFilter.ItemsSource = _customerChoices;
            CustomerFilter.DisplayMemberPath = nameof(CustomerChoice.Name);
            CustomerFilter.SelectedIndex = 0;
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async Task RefreshAsync()
    {
        if (RecordsList is null || SearchInput is null || StatusText is null) return;
        var version = ++_requestVersion;
        try
        {
            var records = await _records.SearchAsync(_section.Key, SearchInput.Text);
            if (version != _requestVersion) return;
            IEnumerable<BusinessRecord> filtered = records;
            var status = StatusFilter.Text.Trim();
            if (status.Length > 0)
                filtered = filtered.Where(item => item.Status?.Contains(status,
                    StringComparison.CurrentCultureIgnoreCase) == true);
            if (CustomerFilter.SelectedItem is CustomerChoice choice && choice.Id.HasValue)
                filtered = filtered.Where(item => item.CustomerId == choice.Id.Value);
            if (ParseFilterDate(FromFilter.Text) is DateTime from)
                filtered = filtered.Where(item => item.EventDate >= from);
            if (ParseFilterDate(UntilFilter.Text) is DateTime until)
                filtered = filtered.Where(item => item.EventDate < until.AddDays(1));
            _filtered = filtered.Select(item => new BusinessRecordRow(item)).ToArray();
            _page = 1;
            ApplyPage();
            StatusText.Text = $"{_filtered.Length} από {records.Count} καταχωρίσεις";
            if (_section.Key is "vehicles" or "policies" or "claims")
            {
                var related = await _customers.GetRelatedRecordsAsync(_section.Key, SearchInput.Text);
                if (version != _requestVersion) return;
                RelatedRecordsList.ItemsSource = related;
                RelatedRecordsBorder.Visibility = related.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async void Filter_Changed(object sender, TextChangedEventArgs e) => await RefreshAsync();
    private async void CustomerFilter_Changed(object sender, SelectionChangedEventArgs e) => await RefreshAsync();
    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 1) { _page--; ApplyPage(); }
    }
    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page * PageSize < _filtered.Length) { _page++; ApplyPage(); }
    }
    private void ApplyPage()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Length / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);
        RecordsList.ItemsSource = _filtered.Skip((_page - 1) * PageSize).Take(PageSize).ToArray();
        TotalBadge.Text = $"{_filtered.Length} εγγραφές";
        PageText.Text = $"Σελίδα {_page} από {pages} · Σύνολο {_filtered.Length}";
    }
    private async void Add_Click(object sender, RoutedEventArgs e) => await EditRecordAsync(null);
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (RecordsList.SelectedItem is BusinessRecordRow row) await EditRecordAsync(row.Record);
        else await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε εγγραφή από τον πίνακα.");
    }
    private async void Record_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BusinessRecordRow row) await EditRecordAsync(row.Record);
    }

    private async Task EditRecordAsync(BusinessRecord? existing)
    {
        var record = existing is null ? new BusinessRecord { SectionKey = _section.Key, Title = "" } :
            await _records.GetAsync(existing.Id);
        if (record is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Η εγγραφή δεν υπάρχει."); return; }
        var title = new TextBox { Header = RequiredHeader.Create(_section.Columns[0]), Text = record.Title };
        var reference = new TextBox { Header = "Κωδικός / αριθμός", Text = record.Reference ?? "" };
        var party = new TextBox { Header = _section.Columns.Length > 1 ? _section.Columns[1] : "Σχετικό πρόσωπο",
            Text = record.RelatedParty ?? "" };
        var status = new TextBox { Header = "Κατάσταση", Text = record.Status ?? "" };
        var date = new TextBox { Header = "Ημερομηνία (ηη/μμ/εεεε)",
            Text = record.EventDate?.ToString("dd/MM/yyyy") ?? "" };
        var customer = new ComboBox { Header = "Συνδεδεμένος πελάτης", Width = 320,
            DisplayMemberPath = nameof(CustomerChoice.Name), ItemsSource = _customerChoices };
        customer.SelectedItem = _customerChoices.FirstOrDefault(item => item.Id == record.CustomerId) ??
            _customerChoices.FirstOrDefault();
        var notes = new TextBox { Header = "Σημειώσεις", Text = record.Notes ?? "",
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90 };
        var form = new StackPanel { Spacing = 10, Width = 500 };
        foreach (var control in new UIElement[] { title, reference, party, status, date, customer, notes })
            form.Children.Add(control);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existing is null ? "Προσθήκη " + Singular(_section.Title) :
                "Επεξεργασία " + Singular(_section.Title),
            Content = new ScrollViewer { Content = form, MaxHeight = 520 },
            PrimaryButtonText = "Αποθήκευση",
            CloseButtonText = "Ακύρωση"
        };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            record.Title = title.Text;
            record.Reference = reference.Text;
            record.RelatedParty = party.Text;
            record.Status = status.Text;
            record.EventDate = ParseDate(date.Text);
            record.CustomerId = (customer.SelectedItem as CustomerChoice)?.Id;
            record.Notes = notes.Text;
            await _records.SaveAsync(record);
            await RefreshAsync();
            StatusText.Text = "Η εγγραφή αποθηκεύτηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RecordsList.SelectedItem is not BusinessRecordRow row)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε εγγραφή από τον πίνακα.");
            return;
        }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή εγγραφής",
            Content = $"Να διαγραφεί το «{row.Title}»;", PrimaryButtonText = "Διαγραφή",
            CloseButtonText = "Ακύρωση", DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            await _records.DeleteAsync(row.Record.Id);
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Statistics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var records = await _records.SearchAsync(_section.Key);
            var linked = records.Count(item => item.CustomerId.HasValue);
            var statuses = records.GroupBy(item => string.IsNullOrWhiteSpace(item.Status) ? "Χωρίς κατάσταση" : item.Status)
                .OrderByDescending(group => group.Count())
                .Take(8)
                .Select(group => $"{group.Key}: {group.Count()}");
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = "Στατιστικά — " + _section.Title,
                Content = $"Σύνολο: {records.Count}\nΣυνδεδεμένα με πελάτη: {linked}\n" +
                    string.Join("\n", statuses), CloseButtonText = "Κλείσιμο" };
            await DialogButtonStyles.ShowAsync(dialog);
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void OpenRelatedCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (RelatedRecordsList.SelectedItem is not CustomerRelatedRecord row) return;
        try
        {
            if (_card is not null) { _card.Activate(); return; }
            _card = new CustomerCardWindow(row.CustomerId);
            _card.Closed += async (_, _) => { _card = null; await RefreshAsync(); };
            _card.Activate();
        }
        catch (Exception exception) { _card = null; StatusText.Text = exception.Message; }
    }

    private static DateTime? ParseFilterDate(string? input) =>
        DateTime.TryParseExact(input?.Trim(), "dd/MM/yyyy", CultureInfo.GetCultureInfo("el-GR"),
            DateTimeStyles.None, out var date) ? date : null;

    private static DateTime? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return ParseFilterDate(input) ??
            throw new ArgumentException("Η ημερομηνία πρέπει να είναι στη μορφή ηη/μμ/εεεε.");
    }

    private static string Singular(string title) => title switch
    {
        "Συνεργάτες" => "συνεργάτη",
        "Οχήματα" => "οχήματος",
        "Εταιρείες" => "εταιρείας",
        "Πρακτορεία" => "πρακτορείου",
        "Ασφαλιστήρια" => "ασφαλιστηρίου",
        "Ζημιές" => "ζημιάς",
        "Ενημέρωση" => "ενημέρωσης",
        "Αναφορές" => "αναφοράς",
        "Γέφυρες Εταιρειών" => "γέφυρας",
        _ => "εγγραφής"
    };

    private sealed record CustomerChoice(Guid? Id, string Name);

    private sealed record BusinessRecordRow(BusinessRecord Record)
    {
        public string Title => Record.Title;
        public string? Reference => Record.Reference;
        public string? RelatedParty => Record.RelatedParty;
        public string? Status => Record.Status;
        public string EventDateDisplay => Record.EventDate?.ToString("dd/MM/yyyy") ?? "";
        public string CustomerName => Record.Customer is null ? "" :
            $"{Record.Customer.LastName} {Record.Customer.FirstName}";
        public string CreatedDisplay => Record.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy");
        public override string ToString() => Record.Title;
    }
}

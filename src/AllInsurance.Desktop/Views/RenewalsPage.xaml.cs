using AllInsurance.Application.Business;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class RenewalsPage : Page
{
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private RenewalRow[] _all = [];
    private BusinessRecordCardWindow? _card;

    public RenewalsPage()
    {
        InitializeComponent();
        TableRowStriping.Attach(RenewalsList);
        Loaded += async (_, _) =>
        {
            FromDate.Date = new DateTimeOffset(DateTime.Today);
            UntilDate.Date = new DateTimeOffset(DateTime.Today.AddDays(30));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            var records = (await _records.SearchAsync("policies")).ToArray();
            var partners = (await _records.SearchAsync("partners"))
                .ToDictionary(item => item.Id, item => item.Title);
            _all = RenewalPlanning.Candidates(records)
                .Select(item => new RenewalRow(item.Record, item.Data,
                    item.Data.PartnerId is Guid partnerId &&
                    partners.TryGetValue(partnerId, out var partnerName)
                        ? partnerName : "—")).ToArray();
            var selected = CompanyFilter.SelectedItem?.ToString();
            CompanyFilter.Items.Clear();
            CompanyFilter.Items.Add("Όλες οι εταιρείες");
            foreach (var company in _all.Select(item => item.Insurer)
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value))
                CompanyFilter.Items.Add(company);
            CompanyFilter.SelectedItem = CompanyFilter.Items.OfType<string>()
                .FirstOrDefault(value => value == selected) ?? CompanyFilter.Items[0];
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private void ApplyFilters()
    {
        var search = SearchInput.Text.Trim();
        var company = CompanyFilter.SelectedIndex > 0 ? CompanyFilter.SelectedItem?.ToString() : null;
        var from = FromDate.Date?.Date;
        var until = UntilDate.Date?.Date;
        var items = _all.Where(item =>
                (from is null || item.EndDate.Date >= from) &&
                (until is null || item.EndDate.Date <= until) &&
                (company is null || item.Insurer == company) &&
                (search.Length == 0 || (item.Number + item.Customer + item.Insurer + item.Partner)
                    .Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(item => item.EndDate).ToArray();
        RenewalsList.ItemsSource = items;
        CountText.Text = $"{items.Length} προς ανανέωση";
        StatusText.Text = from > until ? "Η αρχική ημερομηνία είναι μετά την τελική." :
            $"{items.Length} ασφαλιστήρια στο επιλεγμένο διάστημα";
    }

    private void Search_Changed(object sender, TextChangedEventArgs args) => ApplyFilters();
    private void Company_Changed(object sender, SelectionChangedEventArgs args) => ApplyFilters();
    private void Date_Changed(object? sender, EventArgs args) => ApplyFilters();
    private void Days30_Click(object sender, RoutedEventArgs args) => SetDays(30);
    private void Days60_Click(object sender, RoutedEventArgs args) => SetDays(60);
    private void SetDays(int days)
    {
        FromDate.Date = new DateTimeOffset(DateTime.Today);
        UntilDate.Date = new DateTimeOffset(DateTime.Today.AddDays(days));
        ApplyFilters();
    }
    private void Clear_Click(object sender, RoutedEventArgs args)
    {
        SearchInput.Text = "";
        CompanyFilter.SelectedIndex = 0;
        SetDays(30);
    }

    private RenewalRow? Selected => RenewalsList.SelectedItem as RenewalRow;
    private void Open_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs args) =>
        OpenSelected(false);
    private void Open_Click(object sender, RoutedEventArgs args) => OpenSelected(false);
    private void Create_Click(object sender, RoutedEventArgs args) => OpenSelected(true);

    private async void OpenSelected(bool renewal)
    {
        if (Selected is not RenewalRow row)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πρώτα ασφαλιστήριο από τον πίνακα.");
            return;
        }
        if (renewal && row.Record.CustomerId is null)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Το ασφαλιστήριο χρειάζεται συνδεδεμένο πελάτη για να δημιουργηθεί ανανεωτήριο.");
            return;
        }
        if (_card is not null) { _card.BringToFront(); return; }
        _card = renewal
            ? new BusinessRecordCardWindow("policies", customerId: row.Record.CustomerId,
                vehicleId: row.Data.VehicleId, insurerName: row.Data.Insurer,
                agencyName: row.Data.Agency, previousPolicyId: row.Record.Id)
            : new BusinessRecordCardWindow("policies", recordId: row.Record.Id);
        _card.RecordSaved += async (_, _) => await ReloadAsync();
        _card.Closed += (_, _) => _card = null;
        _card.BringToFront();
    }

    public sealed class RenewalRow(BusinessRecord record, PolicyCardData data, string partner)
    {
        public BusinessRecord Record { get; } = record;
        public PolicyCardData Data { get; } = data;
        public DateTime EndDate { get; } = data.EndDate!.Value;
        public string Number { get; } = data.PolicyNumber ?? record.Reference ?? record.Title;
        public string Customer { get; } = record.Customer is { } customer
            ? $"{customer.LastName} {customer.FirstName}" : record.RelatedParty ?? "—";
        public string Insurer { get; } = data.Insurer ?? "—";
        public string Partner { get; } = partner;
        public string Expiry => EndDate.ToString("dd/MM/yyyy");
        public string DaysLeft => (EndDate.Date - DateTime.Today).Days.ToString();
    }
}

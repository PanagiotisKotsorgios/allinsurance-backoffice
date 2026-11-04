using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Globalization;
using System.Text;

namespace AllInsurance.Desktop.Views;

public sealed partial class ReportsPage : Page
{
    private readonly IApplicationDbContextFactory _factory = App.Services.GetRequiredService<IApplicationDbContextFactory>();
    private Customer[] _customers = [];
    private BusinessRecord[] _records = [];
    private PartnerPayout[] _payouts = [];
    private ReportEntry[] _shown = [];
    private string[] _headings = [];

    public ReportsPage()
    {
        InitializeComponent();
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(ReportList);
        ReportType.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            _customers = [.. await db.Customers.AsNoTracking().Include(item => item.Transactions).ToListAsync()];
            _records = [.. await db.BusinessRecords.AsNoTracking().Include(item => item.Customer).ToListAsync()];
            _payouts = [.. await db.PartnerPayouts.AsNoTracking().ToListAsync()];
            var policies = Policies().ToArray();
            SetOptions(CompanyFilter, "Όλες οι εταιρείες", policies.Select(item => item.Data.Insurer));
            SetOptions(BranchFilter, "Όλοι οι κλάδοι", policies.Select(item => item.Data.Branch));
            SetOptions(PartnerFilter, "Όλοι οι συνεργάτες", _records.Where(item => item.SectionKey == "partners")
                .Select(item => item.Title));
            Refresh();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν φορτώθηκαν οι αναφορές: " + exception.Message);
        }
    }

    private static void SetOptions(ComboBox box, string all, IEnumerable<string?> values)
    {
        box.Items.Clear();
        box.Items.Add(all);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value))
            box.Items.Add(value);
        box.SelectedIndex = 0;
    }

    private void FiltersChanged(object sender, object args) => Refresh();
    private string? Selected(ComboBox box) => box.SelectedIndex > 0 ? box.SelectedItem?.ToString() : null;
    private bool InPeriod(DateTime date) =>
        (FromDate.Date is null || date.Date >= FromDate.Date.Value.Date) &&
        (UntilDate.Date is null || date.Date <= UntilDate.Date.Value.Date);
    private bool Matches(params string?[] values) => SearchInput.Text.Trim() is not { Length: > 0 } query ||
        values.Any(value => value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true);

    private IEnumerable<(BusinessRecord Record, PolicyCardData Data)> Policies() =>
        _records.Where(item => item.SectionKey == "policies" &&
            (item.DocumentType is null or "Ασφαλιστήριο"))
            .Select(item => (Record: item, Data: BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson)))
            .Where(item => InPeriod(item.Data.IssueDate ?? item.Data.StartDate ?? item.Record.CreatedAtUtc) &&
                (Selected(CompanyFilter) is not string company || item.Data.Insurer == company) &&
                (Selected(BranchFilter) is not string branch || item.Data.Branch == branch) &&
                (Selected(PartnerFilter) is not string partner ||
                    item.Record.Customer?.PartnerName == partner ||
                    _records.Any(record => record.SectionKey == "partners" && record.Id == item.Data.PartnerId && record.Title == partner)));

    private void Refresh()
    {
        if (ReportList is null || ReportType is null || SearchInput is null || CompanyFilter is null ||
            BranchFilter is null || PartnerFilter is null || FromDate is null || UntilDate is null) return;
        var policies = Policies().ToArray();
        var kind = ReportType.SelectedIndex;
        _headings = kind switch
        {
            1 => ["Ασφαλιστήριο", "Πελάτης", "Εταιρεία", "Καθαρά", "Μικτά", "Λήξη"],
            2 => ["Ασφαλιστική", "Πλήθος", "Μικτά", "Προμήθεια γραφείου", "Προς απόδοση*", "Κλάδοι"],
            3 => ["Συνεργάτης", "Ασφαλιστήρια", "Προμήθειες", "Αποδόθηκαν", "Υπόλοιπο", "Κατηγορία"],
            4 => ["Πελάτης", "Κατάσταση", "Οφειλή", "Πιστωτικό", "Υπόλοιπο", "Πόλη"],
            5 => ["Ζημιά", "Πελάτης", "Είδος", "Εκτίμηση", "Πληρωμένο", "Κατάσταση"],
            6 => ["Όχημα", "Πελάτης", "Μάρκα", "Χρήση", "Κατάσταση", "Καταχώριση"],
            7 => ["Ημερομηνία", "Πελάτης", "Κίνηση", "Ποσό", "Τρόπος", "Ασφαλιστήριο / αναφορά"],
            _ => ["Ενότητα", "Πλήθος", "Ενεργά", "Ποσό", "Εκκρεμότητες", "Στοιχεία"]
        };
        var rows = kind switch
        {
            1 => PolicyRows(policies), 2 => InsurerRows(policies), 3 => PartnerRows(policies),
            4 => CustomerRows(), 5 => ClaimRows(), 6 => VehicleRows(),
            7 => CustomerMovementRows(), _ => SummaryRows(policies)
        };
        _shown = rows.Where(row => Matches(row.Values)).ToArray();
        var heads = new[] { HeadA, HeadB, HeadC, HeadD, HeadE, HeadF };
        for (var index = 0; index < heads.Length; index++) heads[index].Text = _headings[index];
        ReportList.Items.Clear();
        foreach (var row in _shown) ReportList.Items.Add(BuildRow(row));
        SummaryText.Text = $"{_shown.Length} γραμμές στην αναφορά";
        DetailText.Text = kind == 2
            ? "* Η προς απόδοση αξία εκτιμάται από τα μικτά ασφάλιστρα μείον την προμήθεια γραφείου επί των καθαρών. Οι καταβολές προς εταιρείες δεν καταχωρίζονται ακόμη."
            : "Τα στοιχεία υπολογίζονται από τις αποθηκευμένες εγγραφές και τα επιλεγμένα φίλτρα.";
    }

    private ReportEntry[] PolicyRows((BusinessRecord Record, PolicyCardData Data)[] policies) =>
        policies.OrderByDescending(item => item.Data.IssueDate ?? item.Data.StartDate)
            .Select(item => new ReportEntry(item.Data.PolicyNumber ?? item.Record.Reference ?? item.Record.Title,
                item.Record.Customer is { } customer ? $"{customer.LastName} {customer.FirstName}" : "—",
                item.Data.Insurer ?? "—", Money(item.Data.NetPremium ?? 0), Money(item.Data.GrossPremium ?? 0),
                item.Data.EndDate?.ToString("dd/MM/yyyy") ?? "—")).ToArray();

    private ReportEntry[] InsurerRows((BusinessRecord Record, PolicyCardData Data)[] policies) =>
        policies.GroupBy(item => item.Data.Insurer ?? "Χωρίς εταιρεία")
            .OrderBy(group => group.Key)
            .Select(group => new ReportEntry(group.Key, group.Count().ToString(),
                Money(group.Sum(item => item.Data.GrossPremium ?? 0)),
                Money(group.Sum(item => decimal.Round((item.Data.NetPremium ?? 0) *
                    (item.Data.OfficeCommissionPercent ?? 0) / 100, 2,
                    MidpointRounding.AwayFromZero))),
                Money(group.Sum(item => (item.Data.GrossPremium ?? 0) -
                    decimal.Round((item.Data.NetPremium ?? 0) *
                        (item.Data.OfficeCommissionPercent ?? 0) / 100, 2,
                        MidpointRounding.AwayFromZero))),
                string.Join(", ", group.Select(item => item.Data.Branch).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct().Take(3)))).ToArray();

    private ReportEntry[] PartnerRows((BusinessRecord Record, PolicyCardData Data)[] policies) =>
        _records.Where(item => item.SectionKey == "partners")
            .Where(item => Selected(PartnerFilter) is not string selected || item.Title == selected)
            .OrderBy(item => item.Title).Select(partner =>
            {
                var data = PartnerProfileData.Read(partner.DetailsJson);
                var related = policies.Where(item => item.Data.PartnerId == partner.Id ||
                    item.Data.PartnerId is null && item.Record.Customer?.PartnerName == partner.Title).ToArray();
                var earned = related.Sum(item => decimal.Round((item.Data.NetPremium ?? 0) *
                    (item.Data.PartnerCommissionPercent ?? data.DefaultCommissionPercent ?? 0) / 100,
                    2, MidpointRounding.AwayFromZero));
                var paid = _payouts.Where(item => item.PartnerId == partner.Id && InPeriod(item.OccurredAtUtc))
                    .Sum(item => item.Kind == "receipt" ? -item.Amount : item.Amount);
                return new ReportEntry(partner.Title, related.Length.ToString(), Money(earned), Money(paid),
                    Money(earned - paid), data.Category ?? "—");
            }).ToArray();

    private ReportEntry[] CustomerRows() => _customers.Where(item => InPeriod(item.CreatedAtUtc))
        .OrderBy(item => item.LastName).ThenBy(item => item.FirstName)
        .Select(item =>
        {
            var balance = CustomerService.Balance(item);
            return new ReportEntry($"{item.LastName} {item.FirstName}", item.IsActive ? "Ενεργός" : "Ανενεργός",
                Money(Math.Max(0, balance)), Money(Math.Max(0, -balance)), Money(balance), item.City ?? "—");
        }).ToArray();

    private ReportEntry[] ClaimRows() => _records.Where(item => item.SectionKey == "claims" &&
            InPeriod(item.EventDate ?? item.CreatedAtUtc))
        .OrderByDescending(item => item.EventDate).Select(item =>
        {
            var details = WorkspaceCardData.Read(item.DetailsJson).Fields;
            return new ReportEntry(item.Title, item.Customer is { } customer
                ? $"{customer.LastName} {customer.FirstName}" : "—",
                details.GetValueOrDefault("claimType") ?? "—", details.GetValueOrDefault("reserve") ?? "—",
                details.GetValueOrDefault("paid") ?? "—", item.Status ?? "—");
        }).ToArray();

    private ReportEntry[] VehicleRows() => _records.Where(item => item.SectionKey == "vehicles" &&
            InPeriod(item.CreatedAtUtc))
        .OrderBy(item => item.Title).Select(item =>
        {
            var details = BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson);
            return new ReportEntry(item.Reference ?? item.Title, item.Customer is { } customer
                ? $"{customer.LastName} {customer.FirstName}" : "—",
                details.Make ?? "—", details.Use ?? "—", item.Status ?? "—",
                item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy"));
        }).ToArray();

    private ReportEntry[] CustomerMovementRows() => _customers
        .SelectMany(customer => customer.Transactions.Where(transaction => InPeriod(transaction.OccurredAtUtc))
            .Select(transaction => (Customer: customer, Transaction: transaction)))
        .OrderByDescending(item => item.Transaction.OccurredAtUtc)
        .Select(item => new ReportEntry(
            item.Transaction.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            $"{item.Customer.LastName} {item.Customer.FirstName}",
            item.Transaction.Type == "receipt" ? "Είσπραξη" : "Πληρωμή",
            Money(item.Transaction.Amount), item.Transaction.PaymentMethod,
            item.Transaction.Reference ?? _records.FirstOrDefault(record =>
                record.Id == item.Transaction.BusinessRecordId)?.Title ?? "—"))
        .ToArray();

    private ReportEntry[] SummaryRows((BusinessRecord Record, PolicyCardData Data)[] policies)
    {
        var claims = _records.Where(item => item.SectionKey == "claims" && InPeriod(item.EventDate ?? item.CreatedAtUtc)).ToArray();
        var customers = _customers.Where(item => InPeriod(item.CreatedAtUtc)).ToArray();
        return
        [
            new("Πελάτες", customers.Length.ToString(), customers.Count(item => item.IsActive).ToString(),
                Money(customers.Sum(item => Math.Max(0, CustomerService.Balance(item)))),
                customers.Count(item => CustomerService.Balance(item) > 0).ToString(), "Οφειλές προς γραφείο"),
            new("Συνεργάτες", _records.Count(item => item.SectionKey == "partners").ToString(), "—",
                "—", "—", "Προμήθειες και αποδόσεις"),
            new("Ασφαλιστήρια", policies.Length.ToString(),
                policies.Count(item => item.Record.Status == "Ενεργό").ToString(),
                Money(policies.Sum(item => item.Data.GrossPremium ?? 0)),
                policies.Count(item => item.Data.EndDate < DateTime.Today.AddDays(30)).ToString(), "Μικτά ασφάλιστρα"),
            new("Ζημιές", claims.Length.ToString(),
                claims.Count(item => item.Status is not ("Κλειστή" or "Πληρωμένη")).ToString(),
                "—", "—", "Καταχωρισμένες υποθέσεις"),
            new("Οχήματα", _records.Count(item => item.SectionKey == "vehicles").ToString(),
                _records.Count(item => item.SectionKey == "vehicles" && item.Status == "Ενεργό").ToString(),
                "—", "—", "Συνδεδεμένα οχήματα")
        ];
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("el-GR")) + " €";

    private static ListViewItem BuildRow(ReportEntry row)
    {
        var grid = new Grid { Height = 42, ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }
        } };
        for (var index = 0; index < 6; index++)
        {
            var border = new Border { BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
                BorderThickness = new Thickness(0, 0, index == 5 ? 0 : 1, 1),
                Child = new TextBlock { Text = row.Values[index], Margin = new Thickness(9, 0, 9, 0),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } };
            Grid.SetColumn(border, index);
            grid.Children.Add(border);
        }
        return new ListViewItem { Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0), Margin = new Thickness(0) };
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_shown.Length == 0) { await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν υπάρχουν γραμμές για εξαγωγή."); return; }
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "AllInsurance-Αναφορά" };
            picker.FileTypeChoices.Add("CSV", [".csv"]);
            InitializeWithWindow.Initialize(picker, App.MainWindowHandle());
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            static string Cell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            var csv = string.Join(";", _headings.Select(Cell)) + Environment.NewLine +
                string.Join(Environment.NewLine, _shown.Select(row => string.Join(";", row.Values.Select(Cell))));
            await File.WriteAllTextAsync(file.Path, csv, new UTF8Encoding(true));
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, "Αποτυχία εξαγωγής: " + exception.Message); }
    }

    private sealed record ReportEntry(string A, string B, string C, string D, string E, string F)
    {
        public string[] Values => [A, B, C, D, E, F];
    }
}

using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using AllInsurance.Desktop.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private readonly Dictionary<Guid, string> _partnerNames = [];
    private readonly Dictionary<Guid, decimal> _partnerRates = [];
    private readonly TablePagination _pager;

    public ReportsPage()
    {
        InitializeComponent();
        TableRowStriping.Attach(ReportList);
        _pager = new TablePagination(ReportList);
        Grid.SetRow(_pager.Bar, 2);
        ReportTableGrid.Children.Add(_pager.Bar);
        ReportList.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler((_, args) =>
        {
            var row = FindReportRow(args.OriginalSource) ?? ReportList.SelectedItem as ListViewItem;
            if (row?.Tag is ReportEntry { TransactionId: Guid transactionId })
            {
                ReceiptCardWindow.Open(transactionId, App.MainWindowHandle());
                args.Handled = true;
            }
        }), true);
        ReportType.SelectedIndex = 0;
        foreach (var filter in new[] { ProductionCompanies, ProductionBranches,
            ProductionCoverages, ProductionPackages, ProductionPartners, ProductionCustomers })
            filter.SelectionChanged += (_, _) => Refresh();
        MonthFilter.Items.Add("Όλοι οι μήνες");
        for (var month = 0; month < 36; month++)
            MonthFilter.Items.Add(DateTime.Today.AddMonths(-month).ToString("MM/yyyy"));
        MonthFilter.SelectedIndex = 0;
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
            _partnerNames.Clear(); _partnerRates.Clear();
            foreach (var partner in _records.Where(item => item.SectionKey == "partners"))
            {
                _partnerNames[partner.Id] = partner.Title;
                _partnerRates[partner.Id] = PartnerProfileData.Read(partner.DetailsJson)
                    .DefaultCommissionPercent ?? 0m;
            }
            var sourcePolicies = _records.Where(item => item.SectionKey == "policies" &&
                item.DocumentType is null or "Ασφαλιστήριο")
                .Select(item => (Record: item,
                    Data: BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson))).ToArray();
            SetOptions(CompanyFilter, "Όλες οι εταιρείες", sourcePolicies.Select(item => item.Data.Insurer));
            SetOptions(BranchFilter, "Όλοι οι κλάδοι", sourcePolicies.Select(item => item.Data.Branch));
            SetOptions(PartnerFilter, "Όλοι οι συνεργάτες", _records.Where(item => item.SectionKey == "partners")
                .Select(item => item.Title));
            ProductionCompanies.SetOptions(sourcePolicies.Select(item => item.Data.Insurer ?? "Χωρίς εταιρεία"));
            ProductionBranches.SetOptions(sourcePolicies.Select(item => item.Data.Branch ?? "Χωρίς κλάδο"));
            ProductionCoverages.SetOptions(sourcePolicies.SelectMany(item =>
                item.Data.Coverages is { Count: > 0 } ? item.Data.Coverages.Select(coverage => coverage.Name)
                    : ["Χωρίς κάλυψη"]));
            ProductionPackages.SetOptions(sourcePolicies.Select(item => item.Data.Product ?? "Χωρίς πακέτο"));
            ProductionPartners.SetOptions(sourcePolicies.Select(PartnerName));
            ProductionCustomers.SetOptions(sourcePolicies.Select(CustomerName));
            SetOptions(ReceiptCustomerFilter, "Όλοι οι πελάτες", _customers.Select(CustomerDisplay));
            SetOptions(ReceiptMethodFilter, "Όλοι οι τρόποι πληρωμής", _customers
                .SelectMany(item => item.Transactions).Where(item => item.Type == "receipt")
                .Select(item => item.PaymentMethod));
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

    private static string CustomerDisplay(Customer customer) =>
        $"{customer.Code} · {customer.LastName} {customer.FirstName}".Trim();
    private string PartnerName((BusinessRecord Record, PolicyCardData Data) item) =>
        item.Data.PartnerId is Guid id && _partnerNames.TryGetValue(id, out var name)
            ? name : item.Record.Customer?.PartnerName ?? "Χωρίς συνεργάτη";
    private static string CustomerName((BusinessRecord Record, PolicyCardData Data) item) =>
        item.Record.Customer is { } customer ? CustomerDisplay(customer) : "Χωρίς πελάτη";

    private static ListViewItem? FindReportRow(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is ListViewItem { Tag: ReportEntry }) return (ListViewItem)current;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void FiltersChanged(object sender, object args) => Refresh();
    private void Month_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (MonthFilter?.SelectedIndex > 0 &&
            DateTime.TryParseExact(MonthFilter.SelectedItem?.ToString(), "MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            FromDate.Date = new DateTimeOffset(month);
            UntilDate.Date = new DateTimeOffset(month.AddMonths(1).AddDays(-1));
        }
        else if (MonthFilter?.SelectedIndex == 0)
        { FromDate.Date = null; UntilDate.Date = null; }
        Refresh();
    }
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
            .Where(item => InPeriod(DateBasis.SelectedIndex == 1
                    ? item.Data.StartDate ?? item.Data.IssueDate ?? item.Record.CreatedAtUtc
                    : item.Data.IssueDate ?? item.Data.StartDate ?? item.Record.CreatedAtUtc) &&
                (ReportType.SelectedIndex == 9 ?
                    ProductionCompanies.SelectedValues.Contains(item.Data.Insurer ?? "Χωρίς εταιρεία") &&
                    ProductionBranches.SelectedValues.Contains(item.Data.Branch ?? "Χωρίς κλάδο") &&
                    ProductionPackages.SelectedValues.Contains(item.Data.Product ?? "Χωρίς πακέτο") &&
                    ProductionPartners.SelectedValues.Contains(PartnerName(item)) &&
                    ProductionCustomers.SelectedValues.Contains(CustomerName(item)) &&
                    (item.Data.Coverages is { Count: > 0 }
                        ? item.Data.Coverages.Any(coverage =>
                            ProductionCoverages.SelectedValues.Contains(coverage.Name))
                        : ProductionCoverages.SelectedValues.Contains("Χωρίς κάλυψη")) &&
                    Matches(item.Record.Title, item.Record.Reference, item.Data.Insurer,
                        item.Data.Branch, item.Data.Product, PartnerName(item), CustomerName(item))
                    : (Selected(CompanyFilter) is not string company || item.Data.Insurer == company) &&
                      (Selected(BranchFilter) is not string branch || item.Data.Branch == branch) &&
                      (Selected(PartnerFilter) is not string partner ||
                          item.Record.Customer?.PartnerName == partner ||
                          _partnerNames.GetValueOrDefault(item.Data.PartnerId ?? Guid.Empty) == partner)));

    private void Refresh()
    {
        if (ReportList is null || ReportType is null || SearchInput is null || CompanyFilter is null ||
            BranchFilter is null || PartnerFilter is null || FromDate is null || UntilDate is null) return;
        var policies = Policies().ToArray();
        var kind = ReportType.SelectedIndex;
        var receiptsOnly = kind == 8;
        var production = kind == 9;
        CompanyFilter.Visibility = receiptsOnly || production ? Visibility.Collapsed : Visibility.Visible;
        BranchFilter.Visibility = receiptsOnly || production ? Visibility.Collapsed : Visibility.Visible;
        PartnerFilter.Visibility = receiptsOnly || production ? Visibility.Collapsed : Visibility.Visible;
        ProductionFilters.Visibility = production ? Visibility.Visible : Visibility.Collapsed;
        ReceiptCustomerFilter.Visibility = receiptsOnly ? Visibility.Visible : Visibility.Collapsed;
        ReceiptMethodFilter.Visibility = receiptsOnly ? Visibility.Visible : Visibility.Collapsed;
        ReportList.SelectionMode = receiptsOnly ? ListViewSelectionMode.Single : ListViewSelectionMode.None;
        _headings = kind switch
        {
            8 => ["Ημερομηνία", "Πελάτης", "Ασφαλιστήριο", "Ποσό", "Τρόπος πληρωμής", "Αναφορά"],
            9 => ["Ασφαλιστήριο / ομάδα", "Πελάτης / σύνολο", "Καθαρά", "Μικτά",
                "Προμήθεια έδρας € / %", "Προμήθεια συνεργάτη € / %"],
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
            7 => CustomerMovementRows(), 8 => ReceiptRows(),
            9 => ProductionRows(policies), _ => SummaryRows(policies)
        };
        _shown = production ? rows : rows.Where(row => Matches(row.Values)).ToArray();
        var heads = new[] { HeadA, HeadB, HeadC, HeadD, HeadE, HeadF };
        for (var index = 0; index < heads.Length; index++) heads[index].Text = _headings[index];
        ReportList.Items.Clear();
        foreach (var row in _shown) ReportList.Items.Add(BuildRow(row));
        _pager.Capture();
        SummaryText.Text = receiptsOnly
            ? $"{_shown.Length} εισπράξεις · Σύνολο: {_shown.Sum(row => row.ReceiptAmount ?? 0):N2} €"
            : production
            ? $"{_shown.Count(row => row.Kind == ProductionRowKind.Group)} ομάδες · " +
                $"{_shown.Count(row => row.Kind == ProductionRowKind.Detail)} ασφαλιστήρια · " +
                $"Καθαρά {Money(_shown.Where(row => row.Kind == ProductionRowKind.Detail).Sum(row => row.NetAmount))} · " +
                $"Μικτά {Money(_shown.Where(row => row.Kind == ProductionRowKind.Detail).Sum(row => row.GrossAmount))}"
            : $"{_shown.Length} γραμμές στην αναφορά";
        DetailText.Text = receiptsOnly
            ? "Διπλό κλικ σε μία είσπραξη για πλήρη καρτέλα και εξαγωγή απόδειξης PDF."
            : production
            ? $"Προμήθεια έδρας: {Money(_shown.Where(row => row.Kind == ProductionRowKind.Detail).Sum(row => row.OfficeAmount))} · " +
                $"Προμήθεια συνεργατών: {Money(_shown.Where(row => row.Kind == ProductionRowKind.Detail).Sum(row => row.PartnerAmount))}. " +
                "Τα ποσοστά είναι σταθμισμένα στα καθαρά ασφάλιστρα."
            : kind == 2
            ? "* Η προς απόδοση αξία εκτιμάται από τα μικτά ασφάλιστρα μείον την προμήθεια γραφείου επί των καθαρών. Οι καταβολές προς εταιρείες δεν καταχωρίζονται ακόμη."
            : "Τα στοιχεία υπολογίζονται από τις αποθηκευμένες εγγραφές και τα επιλεγμένα φίλτρα.";
    }

    private ReportEntry[] PolicyRows((BusinessRecord Record, PolicyCardData Data)[] policies) =>
        policies.OrderByDescending(item => item.Data.IssueDate ?? item.Data.StartDate)
            .Select(item => new ReportEntry(item.Data.PolicyNumber ?? item.Record.Reference ?? item.Record.Title,
                item.Record.Customer is { } customer ? $"{customer.LastName} {customer.FirstName}" : "—",
                item.Data.Insurer ?? "—", Money(item.Data.NetPremium ?? 0), Money(item.Data.GrossPremium ?? 0),
                item.Data.EndDate?.ToString("dd/MM/yyyy") ?? "—")).ToArray();

    private ReportEntry[] ProductionRows((BusinessRecord Record, PolicyCardData Data)[] policies)
    {
        var mode = GroupingMode.SelectedIndex;
        string GroupKey((BusinessRecord Record, PolicyCardData Data) item) => mode switch
        {
            1 => item.Data.Insurer ?? "Χωρίς εταιρεία",
            2 => PartnerName(item),
            3 => item.Data.Branch ?? "Χωρίς κλάδο",
            4 => CustomerName(item),
            5 => item.Data.Product ?? "Χωρίς πακέτο",
            _ => PartnerName(item) + " · " + (item.Data.Insurer ?? "Χωρίς εταιρεία")
        };
        decimal Office((BusinessRecord Record, PolicyCardData Data) item) =>
            decimal.Round((item.Data.NetPremium ?? 0m) *
                (item.Data.OfficeCommissionPercent ?? 0m) / 100m, 2,
                MidpointRounding.AwayFromZero);
        decimal Partner((BusinessRecord Record, PolicyCardData Data) item) =>
            decimal.Round((item.Data.NetPremium ?? 0m) *
                (item.Data.PartnerCommissionPercent ??
                    (item.Data.PartnerId is Guid id && _partnerRates.TryGetValue(id, out var rate)
                        ? rate : 0m)) / 100m, 2, MidpointRounding.AwayFromZero);
        static string Commission(decimal value, decimal net) =>
            $"{Money(value)} · {(net == 0 ? 0 : value / net * 100):N2}%";
        var rows = new List<ReportEntry>();
        foreach (var group in policies.GroupBy(GroupKey, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            rows.Add(new ReportEntry($"{group.Key}  ·  {group.Count()} ασφαλιστήρια",
                "", "", "", "", "") { Kind = ProductionRowKind.Group });
            foreach (var item in group.OrderBy(item => item.Data.IssueDate ?? item.Data.StartDate)
                .ThenBy(item => item.Record.Title))
            {
                var net = item.Data.NetPremium ?? 0m;
                var gross = item.Data.GrossPremium ?? 0m;
                var office = Office(item);
                var partner = Partner(item);
                rows.Add(new ReportEntry(item.Data.PolicyNumber ?? item.Record.Reference ?? item.Record.Title,
                    CustomerName(item), Money(net), Money(gross), Commission(office, net),
                    Commission(partner, net))
                { Kind = ProductionRowKind.Detail, PolicyCount = 1, NetAmount = net,
                    GrossAmount = gross, OfficeAmount = office, PartnerAmount = partner });
            }
            var groupNet = group.Sum(item => item.Data.NetPremium ?? 0m);
            var groupGross = group.Sum(item => item.Data.GrossPremium ?? 0m);
            var groupOffice = group.Sum(Office);
            var groupPartner = group.Sum(Partner);
            rows.Add(new ReportEntry("", "Σύνολο ομάδας", Money(groupNet), Money(groupGross),
                Commission(groupOffice, groupNet), Commission(groupPartner, groupNet))
            { Kind = ProductionRowKind.Subtotal });
        }
        if (policies.Length > 0)
        {
            var totalNet = policies.Sum(item => item.Data.NetPremium ?? 0m);
            var totalOffice = policies.Sum(Office);
            var totalPartner = policies.Sum(Partner);
            rows.Add(new ReportEntry("", "ΓΕΝΙΚΟ ΣΥΝΟΛΟ", Money(totalNet),
                Money(policies.Sum(item => item.Data.GrossPremium ?? 0m)),
                Commission(totalOffice, totalNet), Commission(totalPartner, totalNet))
            { Kind = ProductionRowKind.GrandTotal });
        }
        return [.. rows];
    }

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

    private ReportEntry[] ReceiptRows() => _customers
        .Where(customer => Selected(ReceiptCustomerFilter) is not string selected ||
            CustomerDisplay(customer) == selected)
        .SelectMany(customer => customer.Transactions
            .Where(transaction => transaction.Type == "receipt" &&
                InPeriod(transaction.OccurredAtUtc.ToLocalTime()) &&
                (Selected(ReceiptMethodFilter) is not string method || transaction.PaymentMethod == method))
            .Select(transaction => (Customer: customer, Transaction: transaction)))
        .OrderByDescending(item => item.Transaction.OccurredAtUtc)
        .Select(item => new ReportEntry(
            item.Transaction.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            $"{item.Customer.LastName} {item.Customer.FirstName}".Trim(),
            _records.FirstOrDefault(record => record.Id == item.Transaction.BusinessRecordId)?.Title ?? "—",
            Money(item.Transaction.Amount), item.Transaction.PaymentMethod,
            item.Transaction.Reference ?? "—")
        {
            TransactionId = item.Transaction.Id,
            ReceiptAmount = item.Transaction.Amount
        }).ToArray();

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
        var cells = new List<Border>();
        for (var index = 0; index < 6; index++)
        {
            var border = new Border { BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
                BorderThickness = new Thickness(0, 0, index == 5 ? 0 : 1, 1),
                Child = new TextBlock { Text = row.Values[index], Margin = new Thickness(9, 0, 9, 0),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = row.Kind is ProductionRowKind.Group or ProductionRowKind.Subtotal or
                        ProductionRowKind.GrandTotal ? Microsoft.UI.Text.FontWeights.SemiBold :
                        Microsoft.UI.Text.FontWeights.Normal } };
            Grid.SetColumn(border, index);
            grid.Children.Add(border);
            cells.Add(border);
        }
        var item = new ListViewItem { Content = grid, Tag = row,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0), Margin = new Thickness(0) };
        item.Loaded += (_, _) =>
        {
            var dark = item.ActualTheme == ElementTheme.Dark;
            for (var index = 0; index < cells.Count; index++)
            {
                var color = row.Kind switch
                {
                    ProductionRowKind.Group => dark
                        ? Windows.UI.Color.FromArgb(255, 55, 65, 77)
                        : Windows.UI.Color.FromArgb(255, 224, 233, 241),
                    ProductionRowKind.Subtotal => dark
                        ? Windows.UI.Color.FromArgb(255, 48, 76, 64)
                        : Windows.UI.Color.FromArgb(255, 207, 231, 215),
                    ProductionRowKind.GrandTotal => dark
                        ? Windows.UI.Color.FromArgb(255, 35, 94, 59)
                        : Windows.UI.Color.FromArgb(255, 180, 225, 193),
                    ProductionRowKind.Detail when index == 4 => dark
                        ? Windows.UI.Color.FromArgb(255, 99, 53, 53)
                        : Windows.UI.Color.FromArgb(255, 241, 213, 213),
                    ProductionRowKind.Detail when index == 5 => dark
                        ? Windows.UI.Color.FromArgb(255, 48, 76, 64)
                        : Windows.UI.Color.FromArgb(255, 207, 231, 215),
                    _ => default
                };
                cells[index].Background = color.A == 0 ? null : new SolidColorBrush(color);
            }
        };
        if (row.TransactionId is Guid transactionId)
        {
            var menu = new MenuFlyout();
            var open = new MenuFlyoutItem { Text = "Άνοιγμα καρτέλας είσπραξης" };
            open.Click += (_, _) => ReceiptCardWindow.Open(transactionId, App.MainWindowHandle());
            menu.Items.Add(open);
            item.ContextFlyout = menu;
        }
        return item;
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
        public Guid? TransactionId { get; init; }
        public decimal? ReceiptAmount { get; init; }
        public int PolicyCount { get; init; }
        public decimal NetAmount { get; init; }
        public decimal GrossAmount { get; init; }
        public decimal OfficeAmount { get; init; }
        public decimal PartnerAmount { get; init; }
        public ProductionRowKind Kind { get; init; }
    }
    private enum ProductionRowKind { None, Group, Detail, Subtotal, GrandTotal }
}

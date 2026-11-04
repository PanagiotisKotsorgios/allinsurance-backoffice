using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow
{
    private sealed record DurationChoice(int? Months, string Label);
    private PolicyCardData _policyData = new();
    private CustomerTransaction[] _policyTransactions = [];
    private ComboBox? _policyPartnerChoice;
    private readonly List<AutoSuggestBox> _policySearchFields = [];
    private string[] _policyBranchOptions = [];
    private string[] _policyPackageOptions = [];
    private ComboBox? _policyHolderChoice;
    private ComboBox? _policyDurationChoice;
    private CheckBox? _payInInstallments;
    private Border? _payInInstallmentsBadge;
    private TextBlock? _policyBalanceText;
    private Border? _policyBalanceBadge;
    private ListView? _policyMovementsList;
    private TablePagination? _policyMovementsPager;
    private ActionButton? _policyReceiptButton;
    private ActionButton? _policyPaymentButton;
    private ListView? _policyClaimsList;
    private BusinessRecordCardWindow? _relatedPolicyCard;
    private WorkspaceRecordCardWindow? _policyClaimCard;
    private readonly ITransactionSettingsService _policySettings =
        App.Services.GetRequiredService<ITransactionSettingsService>();
    private readonly IFilterOptionService _policyFilters =
        App.Services.GetRequiredService<IFilterOptionService>();
    private string _policyCurrency = "EUR";

    private void NormalizePolicyLists()
    {
        _policyData.Coverages ??= [];
        _policyData.InsuredObjects ??= [];
        _policyData.Endorsements ??= [];
        _policyData.GreenCards ??= [];
        _policyData.Installments ??= [];
    }

    private void AddPolicyPartnerChoice(StackPanel panel, PolicyCardData data)
    {
        var values = new[] { new Choice(null, "Χωρίς συνεργάτη") }
            .Concat(_partnerRecords.Select(item => new Choice(item.Id, item.Title))).ToArray();
        _policyPartnerChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(Choice.Name), Height = 34, MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        _policyPartnerChoice.SelectedItem = values.FirstOrDefault(item => item.Id == data.PartnerId) ?? values[0];
        panel.Children.Add(_policyPartnerChoice);
        var search = SearchChoice(values, _policyPartnerChoice, "Αναζήτηση συνεργάτη");
        panel.Children.Add(CardFieldLayout.CreateRow("Συνεργάτης", search));
    }

    private AutoSuggestBox SearchChoice(Choice[] choices, ComboBox selected, string hint)
    {
        var search = new AutoSuggestBox { Text = (selected.SelectedItem as Choice)?.Name ?? "",
            PlaceholderText = hint, MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        search.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            search.ItemsSource = choices.Select(item => item.Name)
                .Where(name => name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase))
                .Take(30).ToArray();
            selected.SelectedItem = choices.FirstOrDefault(item =>
                string.Equals(item.Name, search.Text, StringComparison.CurrentCultureIgnoreCase));
        };
        search.SuggestionChosen += (_, args) =>
        {
            search.Text = args.SelectedItem as string ?? search.Text;
            selected.SelectedItem = choices.FirstOrDefault(item => item.Name == search.Text);
        };
        _policySearchFields.Add(search);
        return search;
    }

    private AutoSuggestBox SearchValue(StackPanel panel, string key, string label,
        string? initial, IEnumerable<string> choices)
    {
        var backing = new TextBox { Text = initial ?? "", Visibility = Visibility.Collapsed };
        _text[key] = backing;
        panel.Children.Add(backing);
        var options = choices.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        var search = new AutoSuggestBox { Text = initial ?? "", PlaceholderText = "Αναζήτηση " + label,
            MinHeight = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        search.TextChanged += (_, args) =>
        {
            backing.Text = search.Text;
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                search.ItemsSource = options.Where(value => value.Contains(search.Text,
                    StringComparison.CurrentCultureIgnoreCase)).Take(30).ToArray();
        };
        search.SuggestionChosen += (_, args) => search.Text = args.SelectedItem as string ?? search.Text;
        _policySearchFields.Add(search);
        return search;
    }

    private void AddPolicyHolderChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers,
        PolicyCardData data)
    {
        var values = new[] { new Choice(null, "Ίδιος με ασφαλισμένο") }
            .Concat(customers.Select(item => new Choice(item.Id, item.FullName))).ToArray();
        _policyHolderChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(Choice.Name), Height = 34, MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        _policyHolderChoice.SelectedItem = values.FirstOrDefault(item => item.Id == data.HolderCustomerId) ?? values[0];
        panel.Children.Add(_policyHolderChoice);
        panel.Children.Add(CardFieldLayout.CreateRow("Κάτοχος / συμβαλλόμενος",
            SearchChoice(values, _policyHolderChoice, "Αναζήτηση κατόχου")));
    }

    private void PolicyCustomerChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers)
    {
        var values = customers.Select(item => new Choice(item.Id,
            $"{item.FullName} · {item.Code}")).ToArray();
        _customerChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(Choice.Name), Visibility = Visibility.Collapsed };
        _customerChoice.SelectedItem = values.FirstOrDefault(item => item.Id == _record.CustomerId);
        panel.Children.Add(_customerChoice);
        panel.Children.Add(CardFieldLayout.CreateRow("Πελάτης / ιδιοκτήτης *",
            SearchChoice(values, _customerChoice, "Αναζήτηση πελάτη ή κωδικού")));
    }

    private void AddPolicyDurationChoice(StackPanel panel, PolicyCardData data)
    {
        var values = new[] { new DurationChoice(null, "Προσαρμοσμένη"),
            new DurationChoice(1, "1 μήνας"), new DurationChoice(3, "3 μήνες"),
            new DurationChoice(6, "6 μήνες"), new DurationChoice(12, "12 μήνες"),
            new DurationChoice(24, "24 μήνες") };
        _policyDurationChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(DurationChoice.Label), Height = 34, MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _policyDurationChoice.SelectedItem = values.FirstOrDefault(item => item.Months == data.CoverageDurationMonths)
            ?? values[0];
        panel.Children.Add(CardFieldLayout.CreateRow("Διάρκεια κάλυψης", _policyDurationChoice));
        void UpdateEnd()
        {
            if (!_editing || _policyDurationChoice.SelectedItem is not DurationChoice { Months: int months } ||
                _dates.GetValueOrDefault("start")?.Date is not DateTimeOffset start) return;
            _dates["end"].Date = new DateTimeOffset(start.Date.AddMonths(months));
        }
        _policyDurationChoice.SelectionChanged += (_, _) => UpdateEnd();
        _dates["start"].DateChanged += (_, _) => UpdateEnd();
    }

    private void AddPolicyBalanceBadge(StackPanel panel, PolicyCardData data)
    {
        _policyBalanceText = new TextBlock { FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextWrapping = TextWrapping.Wrap };
        var badge = new Border { Child = _policyBalanceText,
            CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 5, 9, 5) };
        _policyBalanceBadge = badge;
        panel.Children.Add(CardFieldLayout.CreateRow("Υπόλοιπο ασφαλιστηρίου", badge));
        UpdatePolicyBalance(data, badge);
        _text["gross"].TextChanged += (_, _) =>
        {
            if (decimal.TryParse(_text["gross"].Text, NumberStyles.Number,
                CultureInfo.GetCultureInfo("el-GR"), out var gross))
                UpdatePolicyBalance(new PolicyCardData { GrossPremium = gross }, badge);
        };
    }

    private void UpdatePolicyBalance(PolicyCardData data, Border? badge = null)
    {
        if (_policyBalanceText is null) return;
        var gross = data.GrossPremium ?? 0;
        var receipts = _policyTransactions.Where(item => item.Type == "receipt").Sum(item => item.Amount);
        var refunds = _policyTransactions.Where(item => item.Type == "payment").Sum(item => item.Amount);
        var balance = gross - receipts + refunds;
        var symbol = TransactionSettingsService.Symbol(_policyCurrency);
        _policyBalanceText.Text = data.GrossPremium is null ? "Δεν έχουν οριστεί μικτά ασφάλιστρα" :
            balance > 0 ? $"Προς είσπραξη: {balance:N2} {symbol}" :
            balance < 0 ? $"Πιστωτικό: {-balance:N2} {symbol}" : $"0,00 {symbol}";
        if ((badge ?? _policyBalanceBadge) is Border target)
            target.Background = new SolidColorBrush(balance > 0 ? Color.FromArgb(255, 198, 40, 40) :
                balance < 0 ? Color.FromArgb(255, 25, 135, 75) : Color.FromArgb(255, 101, 111, 123));
    }

    private void BuildPolicyExtendedTabs(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> vehicles, PolicyCardData data)
    {
        var extra = TwoColumns(out var extraLeft, out var extraRight);
        Text(extraLeft, "insuredvalue", "Ασφαλιστική αξία (€)", data.InsuredValue?.ToString("N2"));
        Text(extraLeft, "discountpercent", "Έκπτωση (%)", data.DiscountPercent?.ToString("N2"));
        Text(extraLeft, "discountamount", "Έκπτωση (€)", data.DiscountAmount?.ToString("N2"));
        Text(extraLeft, "discountreason", "Αιτιολογία έκπτωσης", data.DiscountReason);
        Text(extraLeft, "previousinsurer", "Προηγούμενη ασφαλιστική", data.PreviousInsurer);
        Date(extraRight, "renewal", "Ημερομηνία ανανέωσης", data.RenewalDate);
        Date(extraRight, "companyrenewal", "Ανανέωση από εταιρεία", data.CompanyRenewalDate);
        Text(extraRight, "renewalnotes", "Σημειώσεις ανανέωσης", data.RenewalNotes, multiline: true);
        Text(extraRight, "notes", "Σημειώσεις", _record.Notes, multiline: true);
        AddTab("Πρόσθετα στοιχεία", extra);

        var objects = PolicyPanel();
        var linkedVehicle = vehicles.FirstOrDefault(item => item.Id == data.VehicleId);
        objects.Children.Add(new TextBlock { Text = linkedVehicle is null ?
            "Δεν έχει συνδεθεί όχημα." : $"Συνδεδεμένο όχημα: {linkedVehicle.Title}", FontSize = 16 });
        if (linkedVehicle is not null)
        {
            var open = new ActionButton { Kind = "Open", Label = "Άνοιγμα οχήματος",
                HorizontalAlignment = HorizontalAlignment.Left };
            open.Click += async (_, _) => await OpenRelatedVehicleAsync(linkedVehicle.Id);
            objects.Children.Add(open);
        }
        objects.Children.Add(BuildPolicyObjectsEditor());
        AddTab("Αντικείμενα", objects);

        var coverage = PolicyPanel();
        coverage.Children.Add(BuildPolicyCoveragesEditor());
        AddTab("Καλύψεις", coverage);

        BuildPolicyFinanceTab(data);
        BuildPolicyDocumentsTab();
        BuildPolicyHistoryTab();
    }

    private static StackPanel PolicyPanel() => new() { Spacing = 12,
        Margin = new Thickness(22, 12, 22, 20) };
}

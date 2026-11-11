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
    private ComboBox? _relatedPolicyChoice;
    private readonly List<AutoSuggestBox> _policySearchFields = [];
    private FilterOption[] _policyBranchOptions = [];
    private FilterOption[] _policyPackageOptions = [];
    private string[] _vehicleUseOptions = [];
    private ComboBox? _policyHolderChoice;
    private ComboBox? _policyDurationChoice;
    private TextBox? _durationValue;
    private ComboBox? _durationUnit;
    private ComboBox? _preferredPaymentMethod;
    private ComboBox? _paymentRecipient;
    private CheckBox? _payInInstallments;
    private Border? _payInInstallmentsBadge;
    private TextBlock? _policyBalanceText;
    private Border? _policyBalanceBadge;
    private ListView? _policyMovementsList;
    private TablePagination? _policyMovementsPager;
    private ActionButton? _policyReceiptButton;
    private ActionButton? _policyPaymentButton;
    private ActionButton? _footerReceiptButton;
    private ActionButton? _footerPaymentButton;
    private ActionButton? _footerRenewButton;
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
            DisplayMemberPath = nameof(Choice.Name), Height = 36, MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        _policyPartnerChoice.SelectedItem = values.FirstOrDefault(item => item.Id == data.PartnerId) ?? values[0];
        panel.Children.Add(_policyPartnerChoice);
        var search = SearchChoice(values, _policyPartnerChoice, "Αναζήτηση συνεργάτη");
        var host = new Grid { ColumnSpacing = 4, ColumnDefinitions =
        { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        host.Children.Add(search);
        var open = PartnerLinkButton();
        Grid.SetColumn(open, 1); host.Children.Add(open);
        panel.Children.Add(CardFieldLayout.CreateRow("Συνεργάτης", host));
    }

    private AutoSuggestBox SearchChoice(Choice[] choices, ComboBox selected, string hint) =>
        SearchChoice(() => choices, selected, hint);

    private AutoSuggestBox SearchChoice(Func<Choice[]> choices, ComboBox selected, string hint)
    {
        var search = new AutoSuggestBox { Text = (selected.SelectedItem as Choice)?.Name ?? "",
            PlaceholderText = hint, MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        SearchSuggestions.CloseOnLostFocus(search);
        void UpdateSuggestions(bool showAll = false)
        {
            search.ItemsSource = SearchSuggestions.FirstHundred(choices().Select(item => item.Name),
                showAll ? "" : search.Text);
            search.IsSuggestionListOpen = search.ItemsSource is string[] { Length: > 0 };
        }
        search.GotFocus += (_, _) => UpdateSuggestions(showAll: true);
        search.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            UpdateSuggestions();
            selected.SelectedItem = choices().FirstOrDefault(item =>
                string.Equals(item.Name, search.Text, StringComparison.CurrentCultureIgnoreCase));
        };
        search.SuggestionChosen += (_, args) =>
        {
            search.Text = args.SelectedItem as string ?? search.Text;
            selected.SelectedItem = choices().FirstOrDefault(item => item.Name == search.Text);
        };
        _policySearchFields.Add(search);
        return search;
    }

    private AutoSuggestBox SearchValue(StackPanel panel, string key, string label,
        string? initial, Func<string[]> choices)
    {
        var backing = new TextBox { Text = initial ?? "", Visibility = Visibility.Collapsed };
        _text[key] = backing;
        panel.Children.Add(backing);
        var search = new AutoSuggestBox { Text = initial ?? "", PlaceholderText = "Αναζήτηση " + label,
            MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch };
        SearchSuggestions.CloseOnLostFocus(search);
        void UpdateSuggestions(bool showAll = false)
        {
            search.ItemsSource = SearchSuggestions.FirstHundred(choices(), showAll ? "" : search.Text);
            search.IsSuggestionListOpen = search.ItemsSource is string[] { Length: > 0 };
        }
        search.GotFocus += (_, _) => UpdateSuggestions(showAll: true);
        search.TextChanged += (_, args) =>
        {
            backing.Text = search.Text;
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                UpdateSuggestions();
        };
        search.SuggestionChosen += (_, args) => search.Text = args.SelectedItem as string ?? search.Text;
        _policySearchFields.Add(search);
        return search;
    }

    private AutoSuggestBox SearchValue(StackPanel panel, string key, string label,
        string? initial, IEnumerable<string> choices) =>
        SearchValue(panel, key, label, initial, () => choices.ToArray());

    private static string[] CatalogChoices(IEnumerable<FilterOption> options,
        string? company, string? branch = null) => options
        .Where(item => (item.CompanyScope.Length == 0 ||
                item.CompanyScope.Equals(company?.Trim(), StringComparison.CurrentCultureIgnoreCase)) &&
            (item.BranchScope.Length == 0 ||
                item.BranchScope.Equals(branch?.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        .Select(item => item.Value).Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(value => value).ToArray();

    private void AddPolicyHolderChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers,
        PolicyCardData data)
    {
        var values = new[] { new Choice(null, "Ίδιος με ασφαλισμένο") }
            .Concat(customers.Select(item => new Choice(item.Id, item.FullName))).ToArray();
        _policyHolderChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(Choice.Name), Height = 36, MinHeight = 36,
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
        var host = new Grid { ColumnSpacing = 4, ColumnDefinitions =
        { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        host.Children.Add(SearchChoice(values, _customerChoice, "Αναζήτηση πελάτη ή κωδικού"));
        var open = CustomerLinkButton();
        Grid.SetColumn(open, 1); host.Children.Add(open);
        panel.Children.Add(CardFieldLayout.CreateRow("Πελάτης / ιδιοκτήτης *", host));
    }

    private void AddPolicyDurationChoice(StackPanel panel, PolicyCardData data)
    {
        var values = new[] { new DurationChoice(null, "Προσαρμοσμένη"),
            new DurationChoice(1, "1 μήνας"), new DurationChoice(3, "3 μήνες"),
            new DurationChoice(6, "6 μήνες"), new DurationChoice(12, "12 μήνες"),
            new DurationChoice(24, "24 μήνες") };
        _policyDurationChoice = new ComboBox { ItemsSource = values,
            DisplayMemberPath = nameof(DurationChoice.Label), Height = 36, MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _policyDurationChoice.SelectedItem = values.FirstOrDefault(item => item.Months == data.CoverageDurationMonths)
            ?? values[0];
        panel.Children.Add(CardFieldLayout.CreateRow("Διάρκεια κάλυψης", _policyDurationChoice));
        var duration = new Grid { ColumnSpacing = 6 };
        duration.ColumnDefinitions.Add(new ColumnDefinition());
        duration.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        _durationValue = new TextBox { Text = data.CoverageDurationValue?.ToString() ??
            data.CoverageDurationMonths?.ToString() ?? "", PlaceholderText = "π.χ. 3M ή 15Η", MinHeight = 36 };
        _durationUnit = new ComboBox { ItemsSource = new[] { "Ημέρες", "Μήνες", "Έτη" },
            SelectedItem = data.CoverageDurationUnit ?? "Μήνες", MinHeight = 36 };
        duration.Children.Add(_durationValue);
        Grid.SetColumn(_durationUnit, 1);
        duration.Children.Add(_durationUnit);
        panel.Children.Add(CardFieldLayout.CreateRow("Ακριβής διάρκεια", duration));
        void UpdateEnd()
        {
            if (!_editing || _dates.GetValueOrDefault("start")?.Date is not DateTimeOffset start ||
                !int.TryParse(_durationValue.Text, out var count) || count < 1) return;
            var unit = _durationUnit.SelectedItem as string;
            if (count > (unit == "Έτη" ? 10 : unit == "Μήνες" ? 120 : 3650)) return;
            var end = (_durationUnit.SelectedItem as string) switch
            {
                "Ημέρες" => start.Date.AddDays(count), "Έτη" => start.Date.AddYears(count),
                _ => start.Date.AddMonths(count)
            };
            _dates["end"].Date = new DateTimeOffset(end);
        }
        _policyDurationChoice.SelectionChanged += (_, _) =>
        {
            if (_policyDurationChoice.SelectedItem is DurationChoice { Months: int months })
            { _durationValue.Text = months.ToString(); _durationUnit.SelectedItem = "Μήνες"; }
            UpdateEnd();
        };
        _durationValue.TextChanged += (_, _) =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(_durationValue.Text,
                @"^\s*(\d+)\s*([DdMmYyΗηΜμΕε])\s*$");
            if (match.Success)
            {
                _durationUnit.SelectedItem = match.Groups[2].Value.ToUpperInvariant() switch
                {
                    "D" or "Η" => "Ημέρες", "Y" or "Ε" => "Έτη", _ => "Μήνες"
                };
                _durationValue.Text = match.Groups[1].Value;
            }
            UpdateEnd();
        };
        _durationUnit.SelectionChanged += (_, _) => UpdateEnd();
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
        var negativeCredit = App.Services.GetRequiredService<AllInsurance.Desktop.Settings.WorkspacePreferences>()
            .CreditAsNegative;
        _policyBalanceText.Text = data.GrossPremium is null ? "Δεν έχουν οριστεί μικτά ασφάλιστρα" :
            balance > 0 ? $"Προς είσπραξη: {balance:N2} {symbol}" :
            balance < 0 ? negativeCredit ? $"Υπόλοιπο: {balance:N2} {symbol}" :
                $"Πιστωτικό: {-balance:N2} {symbol}" : $"0,00 {symbol}";
        if ((badge ?? _policyBalanceBadge) is Border target)
            target.Background = StatusPalette.Brush(balance > 0 ? StatusPalette.Negative :
                balance < 0 ? negativeCredit ? StatusPalette.Neutral :
                    StatusPalette.Positive : StatusPalette.Neutral);
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
        Date(extraRight, "renewal", "Ημερομηνία ανανέωσης *", data.RenewalDate);
        Date(extraRight, "companyrenewal", "Ανανέωση από εταιρεία", data.CompanyRenewalDate);
        Text(extraRight, "renewalnotes", "Σημειώσεις ανανέωσης", data.RenewalNotes, multiline: true);
        Text(extraRight, "notes", "Σημειώσεις", _record.Notes, multiline: true);
        AddTab("Πρόσθετα στοιχεία", extra);

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

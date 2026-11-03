using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomersPage : Page
{
    private readonly ICustomerService _customers;
    private readonly IFilterOptionService _filters;
    private CustomerCardWindow? _card;
    private int _requestVersion;
    private CustomerListItem[] _filtered = [];
    private CustomerListItem[] _allItems = [];
    private bool _loading;
    private int _page = 1;
    private const int PageSize = 25;

    public CustomersPage()
    {
        _customers = App.Services.GetRequiredService<ICustomerService>();
        _filters = App.Services.GetRequiredService<IFilterOptionService>();
        InitializeComponent();
        ApplyColumnLayout();
        StatusFilter.SetOptions(["Ενεργοί", "Ανενεργοί"]);
        BalanceFilter.SetOptions(["Χρεωστικό", "Πιστωτικό", "Μηδενικό"]);
        OwnershipFilter.SetOptions(["Στο γραφείο", "Σε συνεργάτη"]);
        KindFilter.SetOptions(["Φυσικό πρόσωπο", "Επιχείρηση"]);
        foreach (var filter in new[] { StatusFilter, BalanceFilter, OwnershipFilter,
            KindFilter, CityFilter, PartnerFilter })
            filter.SelectionChanged += (_, _) => ApplyFilters();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(180, Math.Min(1380, e.NewSize.Width - 48));

    private async Task RefreshAsync()
    {
        if (SearchInput is null || StatusFilter is null ||
            BalanceFilter is null || OwnershipFilter is null || CustomersList is null || StatusText is null)
            return;
        var version = ++_requestVersion;
        try
        {
            var items = await _customers.SearchAsync();
            var cities = await _filters.ChoicesAsync("Πόλεις");
            var partners = await _filters.ChoicesAsync("Συνεργάτες");
            if (version != _requestVersion) return;
            _allItems = items.ToArray();
            _loading = true;
            KindFilter.SetOptions(new[] { "Φυσικό πρόσωπο", "Επιχείρηση" }
                .Concat(items.Select(item => item.CustomerKind)));
            CityFilter.SetOptions(cities.Concat(["Χωρίς πόλη"]));
            PartnerFilter.SetOptions(partners.Concat(["Χωρίς συνεργάτη"]));
            _loading = false;
            ApplyFilters();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private void ApplyFilters()
    {
        if (_loading || SearchInput is null || CustomersList is null) return;
        var term = SearchInput.Text.Trim();
        IEnumerable<CustomerListItem> rows = _allItems;
        if (term.Length > 0) rows = rows.Where(item =>
            item.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            ContainsGreek(item.SearchText, term));
        rows = rows.Where(item => StatusFilter.SelectedValues.Contains(item.IsActive ? "Ενεργοί" : "Ανενεργοί"));
        rows = rows.Where(item => BalanceFilter.SelectedValues.Contains(item.Balance > 0 ? "Χρεωστικό" :
            item.Balance < 0 ? "Πιστωτικό" : "Μηδενικό"));
        rows = rows.Where(item => OwnershipFilter.SelectedValues.Contains(item.BelongsToOffice
            ? "Στο γραφείο" : "Σε συνεργάτη"));
        rows = rows.Where(item => KindFilter.SelectedValues.Contains(item.CustomerKind));
        rows = rows.Where(item => CityFilter.SelectedValues.Contains(item.City ?? "Χωρίς πόλη"));
        rows = rows.Where(item => PartnerFilter.SelectedValues.Contains(item.PartnerName ?? "Χωρίς συνεργάτη"));
        _filtered = rows.ToArray();
        _page = 1;
        ApplyPage();
        StatusText.Text = $"{_filtered.Length} από {_allItems.Length} πελάτες";
    }

    private static bool ContainsGreek(string source, string term) =>
        CultureInfo.GetCultureInfo("el-GR").CompareInfo.IndexOf(source, term,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Customers_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectedBadge.Text = CustomersList.SelectedItem is null ? "0 επιλεγμένοι" : "1 επιλεγμένος";
    private CustomerListItem? SelectedCustomer() =>
        CustomersList.SelectedItem is ListViewItem { Tag: CustomerListItem item } ? item : null;
    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 1) { _page--; ApplyPage(); }
    }
    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page * PageSize < _filtered.Length) { _page++; ApplyPage(); }
    }
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        foreach (var filter in new[] { StatusFilter, BalanceFilter, OwnershipFilter,
            KindFilter, CityFilter, PartnerFilter }) filter.SelectAll();
    }

    private void ApplyPage()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Length / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);
        CustomersList.Items.Clear();
        foreach (var customer in _filtered.Skip((_page - 1) * PageSize).Take(PageSize))
            CustomersList.Items.Add(CustomerRow(customer));
        TotalBadge.Text = $"{_filtered.Length} εγγραφές";
        PageText.Text = $"Σελίδα {_page} από {pages} · Σύνολο {_filtered.Length}";
    }

    private static ListViewItem CustomerRow(CustomerListItem item)
    {
        var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(8, 9, 8, 9) };
        var values = new[] { item.Code, item.CustomerKind, item.LastName,
            item.FirstName, item.CompanyName, item.TaxNumber,
            item.PrimaryEmail, item.PrimaryMobile, item.City, item.BalanceDisplay };
        for (var index = 0; index < CustomerColumns.All.Length; index++)
        {
            var column = CustomerColumns.All[index];
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = CustomerColumns.Width(column.Key) });
            if (!CustomerColumns.IsVisible(column.Key)) continue;
            var text = new TextBlock { Text = values[index] ?? "—",
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = index == 0 ? Microsoft.UI.Text.FontWeights.SemiBold :
                    Microsoft.UI.Text.FontWeights.Normal };
            Grid.SetColumn(text, index);
            row.Children.Add(text);
        }
        return new ListViewItem { Content = row, Tag = item };
    }

    private void ApplyColumnLayout()
    {
        var columns = CustomerColumns.All;
        for (var index = 0; index < columns.Length; index++)
        {
            TableHeader.ColumnDefinitions[index].Width = CustomerColumns.Width(columns[index].Key);
            TableHeader.Children[index].Visibility = CustomerColumns.IsVisible(columns[index].Key)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        TableStack.Width = columns.Where(column => CustomerColumns.IsVisible(column.Key))
            .Sum(column => column.Width) + (columns.Length - 1) * 12 + 24;
        var page = _page;
        _page = page;
        ApplyPage();
    }

    private async void Columns_Click(object sender, RoutedEventArgs e)
    {
        var choices = CustomerColumns.All.Select(column =>
            (column.Key, Box: new CheckBox { Content = column.Title,
                IsChecked = CustomerColumns.IsVisible(column.Key) })).ToArray();
        var panel = new StackPanel { Spacing = 4 };
        foreach (var choice in choices) panel.Children.Add(choice.Box);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαχείριση στηλών",
            Content = new ScrollViewer { Content = panel, MaxHeight = 440 },
            PrimaryButtonText = "Εφαρμογή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            CustomerColumns.Save(choices.Where(choice => choice.Box.IsChecked == true)
                .Select(choice => choice.Key));
            ApplyColumnLayout();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Add_Click(object sender, RoutedEventArgs e) => await OpenCardAsync(null);
    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomer() is CustomerListItem item) await OpenCardAsync(item.Id);
        else await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πελάτη από τη λίστα.");
    }
    private async void Customer_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListViewItem { Tag: CustomerListItem item }) await OpenCardAsync(item.Id);
        else if (e.ClickedItem is CustomerListItem direct) await OpenCardAsync(direct.Id);
    }

    private async Task OpenCardAsync(Guid? id)
    {
        try
        {
            if (_card is not null)
            {
                _card.Activate();
                return;
            }
            _card = new CustomerCardWindow(id);
            _card.Closed += async (_, _) =>
            {
                _card = null;
                await RefreshAsync();
            };
            _card.Activate();
        }
        catch (Exception exception)
        {
            _card = null;
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomer() is not CustomerListItem item)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πελάτη από τη λίστα.");
            return;
        }
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Διαγραφή πελάτη",
            Content = $"Να διαγραφεί ο πελάτης «{item.FullName}» και όλα τα σχετικά στοιχεία και οι κινήσεις του;",
            PrimaryButtonText = "Διαγραφή",
            CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close
        };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            await _customers.DeleteAsync(item.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private async void Statistics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = await _customers.GetStatisticsAsync();
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Στατιστικά πελατών",
                Content = $"Σύνολο: {s.Total}\nΕνεργοί: {s.Active}\nΑνενεργοί: {s.Inactive}\n" +
                    $"Χρεωστικά υπόλοιπα: {s.Debtors}\nΑπαιτήσεις: {s.TotalReceivable:N2} €\n" +
                    $"Πιστωτικά υπόλοιπα: {s.TotalPayable:N2} €\nΑπό σύσταση: {s.Referrals}\nΟδηγοί: {s.Drivers}",
                CloseButtonText = "Κλείσιμο"
            };
            await DialogButtonStyles.ShowAsync(dialog);
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }
}

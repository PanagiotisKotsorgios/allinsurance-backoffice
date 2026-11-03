using AllInsurance.Application.Customers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomersPage : Page
{
    private readonly ICustomerService _customers;
    private CustomerCardWindow? _card;
    private int _requestVersion;
    private CustomerListItem[] _filtered = [];
    private int _page = 1;
    private const int PageSize = 25;

    public CustomersPage()
    {
        _customers = App.Services.GetRequiredService<ICustomerService>();
        InitializeComponent();
        StatusFilter.SelectedIndex = 0;
        BalanceFilter.SelectedIndex = 0;
        OwnershipFilter.SelectedIndex = 0;
        KindFilter.SelectedIndex = 0;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(180, Math.Min(1380, e.NewSize.Width - 48));

    private async Task RefreshAsync()
    {
        if (SearchInput is null || GroupFilter is null || StatusFilter is null ||
            BalanceFilter is null || OwnershipFilter is null || CustomersList is null || StatusText is null)
            return;
        var version = ++_requestVersion;
        try
        {
            var items = await _customers.SearchAsync(SearchInput.Text);
            if (version != _requestVersion) return;
            SetChoiceItems(CityFilter, ["Όλες οι πόλεις", ..
                items.Select(item => item.City).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value)]);
            SetChoiceItems(PartnerFilter, ["Όλοι οι συνεργάτες", ..
                items.Select(item => item.PartnerName).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value)]);
            if (version != _requestVersion) return;
            IEnumerable<CustomerListItem> filtered = items;
            filtered = StatusFilter.SelectedIndex switch
            {
                1 => filtered.Where(item => item.IsActive),
                2 => filtered.Where(item => !item.IsActive),
                _ => filtered
            };
            filtered = BalanceFilter.SelectedIndex switch
            {
                1 => filtered.Where(item => item.Balance > 0),
                2 => filtered.Where(item => item.Balance < 0),
                3 => filtered.Where(item => item.Balance == 0),
                _ => filtered
            };
            filtered = OwnershipFilter.SelectedIndex switch
            {
                1 => filtered.Where(item => item.BelongsToOffice),
                2 => filtered.Where(item => !item.BelongsToOffice),
                _ => filtered
            };
            filtered = KindFilter.SelectedIndex switch
            {
                1 => filtered.Where(item => item.CustomerKind == "Φυσικό πρόσωπο"),
                2 => filtered.Where(item => item.CustomerKind == "Επιχείρηση"),
                _ => filtered
            };
            if (CityFilter.SelectedIndex > 0 && CityFilter.SelectedItem is string city)
                filtered = filtered.Where(item => item.City == city);
            if (PartnerFilter.SelectedIndex > 0 && PartnerFilter.SelectedItem is string partner)
                filtered = filtered.Where(item => item.PartnerName == partner);
            var group = GroupFilter.Text.Trim();
            if (group.Length > 0)
                filtered = filtered.Where(item => item.GroupName?.Contains(group,
                    StringComparison.CurrentCultureIgnoreCase) == true);
            _filtered = filtered.ToArray();
            _page = 1;
            ApplyPage();
            StatusText.Text = $"{_filtered.Length} από {items.Count} πελάτες";
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private async void Filter_Changed(object sender, TextChangedEventArgs e) => await RefreshAsync();
    private async void Filter_Changed(object sender, SelectionChangedEventArgs e) => await RefreshAsync();
    private void Customers_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectedBadge.Text = CustomersList.SelectedItem is null ? "0 επιλεγμένοι" : "1 επιλεγμένος";
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
        GroupFilter.Text = "";
        StatusFilter.SelectedIndex = 0;
        BalanceFilter.SelectedIndex = 0;
        OwnershipFilter.SelectedIndex = 0;
        KindFilter.SelectedIndex = 0;
        CityFilter.SelectedIndex = 0;
        PartnerFilter.SelectedIndex = 0;
    }

    private void ApplyPage()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Length / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);
        CustomersList.ItemsSource = _filtered.Skip((_page - 1) * PageSize).Take(PageSize).ToArray();
        TotalBadge.Text = $"{_filtered.Length} εγγραφές";
        PageText.Text = $"Σελίδα {_page} από {pages} · Σύνολο {_filtered.Length}";
    }

    private static void SetChoiceItems(ComboBox combo, string?[] values)
    {
        var current = combo.SelectedItem as string;
        var old = combo.ItemsSource as string?[];
        if (old is not null && old.SequenceEqual(values)) return;
        combo.ItemsSource = values;
        combo.SelectedItem = current is not null && values.Contains(current) ? current : values[0];
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Add_Click(object sender, RoutedEventArgs e) => OpenCard(null);
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersList.SelectedItem is CustomerListItem item) OpenCard(item.Id);
        else StatusText.Text = "Επιλέξτε πελάτη από τη λίστα.";
    }
    private void Customer_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerListItem item) OpenCard(item.Id);
    }

    private void OpenCard(Guid? id)
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
            StatusText.Text = exception.Message;
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersList.SelectedItem is not CustomerListItem item)
        {
            StatusText.Text = "Επιλέξτε πελάτη από τη λίστα.";
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
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            await _customers.DeleteAsync(item.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
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
            await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }
}

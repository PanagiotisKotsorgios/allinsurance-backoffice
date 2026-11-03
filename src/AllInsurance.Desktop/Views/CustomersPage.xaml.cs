using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    private bool _updatingSelectAll;
    private readonly HashSet<Guid> _checkedCustomerIds = [];
    private string _sortKey = "LastName";
    private bool _sortDescending;
    private int _page = 1;
    private const int PageSize = 25;

    public CustomersPage()
    {
        _customers = App.Services.GetRequiredService<ICustomerService>();
        _filters = App.Services.GetRequiredService<IFilterOptionService>();
        InitializeComponent();
        CustomersList.AddHandler(UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(async (_, args) =>
            {
                if (IsInsideCheckBox(args.OriginalSource)) return;
                if (FindCustomerAt(args.OriginalSource) is CustomerListItem customer)
                    await OpenCardAsync(customer.Id);
            }), true);
        CustomersList.AddHandler(UIElement.RightTappedEvent,
            new RightTappedEventHandler((_, args) =>
            {
                if (FindCustomerAt(args.OriginalSource) is not CustomerListItem customer) return;
                if (FindCustomerContainer(args.OriginalSource) is not ListViewItem row) return;
                row.IsSelected = true;
                CreateCustomerMenu(customer).ShowAt(row);
                args.Handled = true;
            }), true);
        InitializeHeaderActions();
        ApplyColumnLayout();
        BalanceFilter.SetOptions(["Χρεωστικό", "Πιστωτικό", "Μηδενικό"]);
        KindFilter.SetOptions(["Φυσικό πρόσωπο", "Επιχείρηση"]);
        foreach (var filter in new[] { BalanceFilter, KindFilter, CityFilter, PartnerFilter })
            filter.SelectionChanged += (_, _) => ApplyFilters();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(180, Math.Min(1380, e.NewSize.Width - 48));

    private async Task RefreshAsync()
    {
        if (SearchInput is null || BalanceFilter is null ||
            CustomersList is null || StatusText is null)
            return;
        var version = ++_requestVersion;
        try
        {
            var items = await _customers.SearchAsync();
            var cities = await _filters.ChoicesAsync("Πόλεις");
            var partners = await _filters.ChoicesAsync("Συνεργάτες");
            if (version != _requestVersion) return;
            _allItems = items.ToArray();
            _checkedCustomerIds.RemoveWhere(id => !_allItems.Any(item => item.Id == id));
            _loading = true;
            KindFilter.SetOptions(new[] { "Φυσικό πρόσωπο", "Επιχείρηση" }
                .Concat(items.Select(item => item.CustomerKind)));
            CityFilter.SetOptions(cities.Concat(_allItems.Select(item => item.City ?? "Χωρίς πόλη"))
                .Concat(["Χωρίς πόλη"]));
            PartnerFilter.SetOptions(partners.Concat(_allItems.Select(item => item.PartnerName ?? "Χωρίς συνεργάτη"))
                .Concat(["Χωρίς συνεργάτη"]));
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
        rows = rows.Where(item => BalanceFilter.SelectedValues.Contains(item.Balance > 0 ? "Χρεωστικό" :
            item.Balance < 0 ? "Πιστωτικό" : "Μηδενικό"));
        rows = rows.Where(item => KindFilter.SelectedValues.Contains(item.CustomerKind));
        rows = rows.Where(item => CityFilter.SelectedValues.Contains(item.City ?? "Χωρίς πόλη"));
        rows = rows.Where(item => PartnerFilter.SelectedValues.Contains(item.PartnerName ?? "Χωρίς συνεργάτη"));
        if (_sortKey == "Balance")
            rows = _sortDescending ? rows.OrderByDescending(item => item.Balance) :
                rows.OrderBy(item => item.Balance);
        else
        {
            Func<CustomerListItem, string> key = _sortKey switch
            {
                "Code" => item => item.Code,
                "Kind" => item => item.CustomerKind,
                "FirstName" => item => item.FirstName,
                "Company" => item => item.CompanyName ?? "",
                "Tax" => item => item.TaxNumber ?? "",
                "Email" => item => item.PrimaryEmail ?? "",
                "Mobile" => item => item.PrimaryMobile ?? "",
                "City" => item => item.City ?? "",
                _ => item => item.LastName
            };
            rows = _sortDescending ? rows.OrderByDescending(key, StringComparer.CurrentCultureIgnoreCase) :
                rows.OrderBy(key, StringComparer.CurrentCultureIgnoreCase);
        }
        _filtered = rows.ToArray();
        _page = 1;
        ApplyPage();
        StatusText.Text = $"{_filtered.Length} από {_allItems.Length} πελάτες";
    }

    private static bool ContainsGreek(string source, string term) =>
        CultureInfo.GetCultureInfo("el-GR").CompareInfo.IndexOf(source, term,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Customers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCheckedState();
    }
    private void SelectAllCustomers_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingSelectAll || CustomersList is null) return;
        if (SelectAllCustomers.IsChecked == true)
            foreach (var item in _filtered) _checkedCustomerIds.Add(item.Id);
        else
            foreach (var item in _filtered) _checkedCustomerIds.Remove(item.Id);
        ApplyPage();
    }
    private void CustomerCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not VisibleSelectionCheckBox { Tag: Guid id } box) return;
        if (box.IsChecked == true) _checkedCustomerIds.Add(id);
        else _checkedCustomerIds.Remove(id);
        UpdateCheckedState();
    }
    private void UpdateCheckedState()
    {
        if (SelectedBadge is null || SelectAllCustomers is null) return;
        var count = _checkedCustomerIds.Count;
        SelectedBadge.Text = count == 1 ? "1 επιλεγμένος" : $"{count} επιλεγμένοι";
        _updatingSelectAll = true;
        SelectAllCustomers.IsChecked = _filtered.Length > 0 &&
            _filtered.All(item => _checkedCustomerIds.Contains(item.Id));
        _updatingSelectAll = false;
    }
    private CustomerListItem? SelectedCustomer() =>
        CustomersList.SelectedItem is ListViewItem { Tag: CustomerListItem item } ? item : null;
    private CustomerListItem[] SelectedCustomers() =>
        _allItems.Where(item => _checkedCustomerIds.Contains(item.Id)).ToArray();
    private static ListViewItem? FindCustomerContainer(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is ListViewItem { Tag: CustomerListItem }) return (ListViewItem)current;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
    private static CustomerListItem? FindCustomerAt(object source) =>
        FindCustomerContainer(source)?.Tag as CustomerListItem;
    private static bool IsInsideCheckBox(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is VisibleSelectionCheckBox or CheckBox) return true;
            if (current is ListViewItem) return false;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
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
        foreach (var filter in new[] { BalanceFilter, KindFilter, CityFilter, PartnerFilter })
            filter.SelectAll();
    }

    private void ApplyPage()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_filtered.Length / (double)PageSize));
        _page = Math.Clamp(_page, 1, pages);
        CustomersList.Items.Clear();
        foreach (var customer in _filtered.Skip((_page - 1) * PageSize).Take(PageSize))
            CustomersList.Items.Add(CustomerRow(customer));
        UpdateCheckedState();
        TotalBadge.Text = $"{_filtered.Length} εγγραφές";
        PageText.Text = $"Σελίδα {_page} από {pages} · Σύνολο {_filtered.Length}";
    }

    private ListViewItem CustomerRow(CustomerListItem item)
    {
        var row = new Grid { ColumnSpacing = 0, Width = TableStack.Width - 2, Height = 39 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        var check = new VisibleSelectionCheckBox { Tag = item.Id,
            IsChecked = _checkedCustomerIds.Contains(item.Id),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        check.Checked += CustomerCheck_Changed;
        check.Unchecked += CustomerCheck_Changed;
        row.Children.Add(new Border { Child = check,
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(0, 0, 1, 1) });
        var values = new[] { item.Code, item.CustomerKind, item.LastName,
            item.FirstName, item.CompanyName, item.TaxNumber,
            item.PrimaryEmail, item.PrimaryMobile, item.City, item.BalanceDisplay };
        var ordered = CustomerColumns.Ordered;
        for (var index = 0; index < ordered.Length; index++)
        {
            var column = ordered[index];
            var originalIndex = Array.FindIndex(CustomerColumns.All, item => item.Key == column.Key);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = CustomerColumns.Width(column.Key) });
            if (!CustomerColumns.IsVisible(column.Key)) continue;
            var text = new TextBlock { Text = values[originalIndex] ?? "—",
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = originalIndex == 0 ? Microsoft.UI.Text.FontWeights.SemiBold :
                    Microsoft.UI.Text.FontWeights.Normal };
            var cell = new Border
            {
                Child = text,
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == ordered.Length - 1 ? new Thickness(0, 0, 0, 1) :
                    new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 5, 10, 5)
            };
            if (column.Key == "Balance" && item.Balance != 0)
            {
                cell.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 198, 40, 40));
                text.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            }
            Grid.SetColumn(cell, index + 1);
            row.Children.Add(cell);
        }
        var result = new ListViewItem { Content = row, Tag = item, Padding = new Thickness(0),
            Margin = new Thickness(0), Width = TableStack.Width - 2, Height = 39,
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        return result;
    }

    private MenuFlyout CreateCustomerMenu(CustomerListItem item)
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Άνοιγμα καρτέλας" };
        open.Click += async (_, _) => await OpenCardAsync(item.Id);
        var edit = new MenuFlyoutItem { Text = "Επεξεργασία" };
        edit.Click += async (_, _) => await OpenCardAsync(item.Id, true);
        var delete = new MenuFlyoutItem { Text = "Διαγραφή" };
        delete.Click += async (_, _) => await DeleteCustomerAsync(item);
        menu.Items.Add(open);
        menu.Items.Add(edit);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(delete);
        return menu;
    }

    private void ApplyColumnLayout()
    {
        var columns = CustomerColumns.Ordered;
        for (var index = 0; index < columns.Length; index++)
        {
            TableHeader.ColumnDefinitions[index].Width = CustomerColumns.Width(columns[index].Key);
            var originalIndex = Array.FindIndex(CustomerColumns.All, item => item.Key == columns[index].Key);
            var header = (FrameworkElement)TableHeader.Children[originalIndex];
            Grid.SetColumn(header, index);
            header.Visibility = CustomerColumns.IsVisible(columns[index].Key)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        TableStack.Width = columns.Where(column => CustomerColumns.IsVisible(column.Key))
            .Sum(column => column.Width) + 42;
        var page = _page;
        _page = page;
        ApplyPage();
    }

    private void InitializeHeaderActions()
    {
        for (var index = 0; index < CustomerColumns.All.Length; index++)
        {
            var key = CustomerColumns.All[index].Key;
            var header = (FrameworkElement)TableHeader.Children[index];
            header.Tapped += (_, _) => SortBy(key, _sortKey == key && !_sortDescending);
            var menu = new MenuFlyout();
            var ascending = new MenuFlyoutItem { Text = "Ταξινόμηση αύξουσα" };
            ascending.Click += (_, _) => SortBy(key, false);
            var descending = new MenuFlyoutItem { Text = "Ταξινόμηση φθίνουσα" };
            descending.Click += (_, _) => SortBy(key, true);
            var moveLeft = new MenuFlyoutItem { Text = "Μεταφορά αριστερά" };
            moveLeft.Click += (_, _) => MoveColumn(key, -1);
            var moveRight = new MenuFlyoutItem { Text = "Μεταφορά δεξιά" };
            moveRight.Click += (_, _) => MoveColumn(key, 1);
            menu.Items.Add(ascending);
            menu.Items.Add(descending);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(moveLeft);
            menu.Items.Add(moveRight);
            header.ContextFlyout = menu;
        }
    }

    private void SortBy(string key, bool descending)
    {
        _sortKey = key;
        _sortDescending = descending;
        ApplyFilters();
    }

    private async void MoveColumn(string key, int offset)
    {
        try
        {
            CustomerColumns.Move(key, offset);
            ApplyColumnLayout();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
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
        if (SelectedCustomer() is CustomerListItem item) await OpenCardAsync(item.Id, true);
        else await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε έναν πελάτη από τη λίστα.");
    }

    private async Task OpenCardAsync(Guid? id, bool edit = false)
    {
        try
        {
            if (_card is not null)
            {
                _card.BringToFront();
                DispatcherQueue.TryEnqueue(() => _card?.BringToFront());
                return;
            }
            _card = new CustomerCardWindow(id, edit);
            _card.CustomerChanged += async (_, _) => await RefreshAsync();
            _card.Closed += async (_, _) =>
            {
                _card = null;
                await RefreshAsync();
            };
            _card.BringToFront();
            DispatcherQueue.TryEnqueue(() => _card?.BringToFront());
        }
        catch (Exception exception)
        {
            _card = null;
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedCustomers();
        if (selected.Length == 0 && SelectedCustomer() is CustomerListItem focused)
            selected = [focused];
        if (selected.Length == 0)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πελάτη από τη λίστα.");
            return;
        }
        if (selected.Length == 1) { await DeleteCustomerAsync(selected[0]); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Μαζική διαγραφή πελατών",
            Content = $"Να διαγραφούν οι {selected.Length} επιλεγμένοι πελάτες και τα σχετικά στοιχεία τους;",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            foreach (var customer in selected) await _customers.DeleteAsync(customer.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await RefreshAsync();
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private async Task DeleteCustomerAsync(CustomerListItem item)
    {
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

}

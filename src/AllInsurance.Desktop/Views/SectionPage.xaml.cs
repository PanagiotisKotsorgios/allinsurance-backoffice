using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Partners;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Runtime.InteropServices;
using Windows.System;

namespace AllInsurance.Desktop.Views;

public sealed partial class SectionPage : Page
{
    private readonly WorkspaceSection _section;
    private readonly IBusinessRecordService _records;
    private readonly ICustomerService _customers;
    private readonly IFilterOptionService _filters;
    private readonly IPartnerService _partners;
    private CustomerCardWindow? _card;
    private BusinessRecordCardWindow? _businessCard;
    private WorkspaceRecordCardWindow? _workspaceCard;
    private PartnerCardWindow? _partnerCard;
    private CustomerChoice[] _customerChoices = [];
    private string[] _partnerCategories = [];
    private string[] _catalogBranches = [];
    private string[] _catalogVehicleUses = [];
    private HashSet<Guid> _vehiclesWithActivePolicy = [];
    private Dictionary<Guid, string> _vehicleInsuranceStates = [];
    private Dictionary<Guid, string> _policyPartnerNames = [];
    private int _requestVersion;
    private BusinessRecordRow[] _filtered = [];
    private readonly int[] _columnOrder = [0, 1, 2, 3, 4, 5, 6];
    private readonly double[] _columnWidths;
    private readonly bool[] _columnVisible;
    private readonly HashSet<Grid> _recordGrids = [];
    private int _sortColumn = 0;
    private bool _sortDescending;
    private bool _updatingSelectAll;
    private bool _syncingChecks;
    private bool _filtersInPopup;
    private readonly HashSet<Guid> _checkedRecordIds = [];
    private Guid? _selectionAnchor;
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
    private static bool KeyHeld(int key) => (GetKeyState(key) & 0x8000) != 0;
    private int _page = 1;
    private double _lastTableViewportWidth;
    private const int PageSize = 25;

    public SectionPage(WorkspaceSection section)
    {
        _section = section;
        _columnOrder = SectionColumnOrder.Load(section.Key);
        _columnWidths = SectionColumnOrder.LoadWidths(section.Key);
        _columnVisible = SectionColumnOrder.LoadVisibility(section.Key);
        if (section.Key is "policies" or "claims")
        {
            for (var index = 0; index < _columnOrder.Length; index++)
            {
                _columnOrder[index] = index;
                _columnVisible[index] = section.Key == "policies" || index < 6;
            }
        }
        _records = App.Services.GetRequiredService<IBusinessRecordService>();
        _customers = App.Services.GetRequiredService<ICustomerService>();
        _filters = App.Services.GetRequiredService<IFilterOptionService>();
        _partners = App.Services.GetRequiredService<IPartnerService>();
        InitializeComponent();
        BatchButton.Visibility = section.Key is "partners" or "companies" or "agencies"
            ? Visibility.Visible : Visibility.Collapsed;
        TableExportActions.Attach(PaginationActions, this, ExportData);
        TableRowStriping.Attach(RecordsList);
        RecordsList.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, args) =>
            {
                if (IsInsideCheckBox(args.OriginalSource) ||
                    !args.GetCurrentPoint(RecordsList).Properties.IsLeftButtonPressed ||
                    FindRecordContainer(args.OriginalSource) is not ListViewItem
                        { Content: BusinessRecordRow row }) return;
                if (KeyHeld(0x10) && _selectionAnchor is Guid anchor)
                {
                    var start = Array.FindIndex(_filtered, item => item.Record.Id == anchor);
                    var end = Array.FindIndex(_filtered, item => item.Record.Id == row.Record.Id);
                    if (start >= 0 && end >= 0)
                        for (var index = Math.Min(start, end); index <= Math.Max(start, end); index++)
                            _checkedRecordIds.Add(_filtered[index].Record.Id);
                    args.Handled = true;
                    ApplyPage();
                }
                else if (KeyHeld(0x11))
                {
                    if (!_checkedRecordIds.Add(row.Record.Id)) _checkedRecordIds.Remove(row.Record.Id);
                    _selectionAnchor = row.Record.Id;
                    args.Handled = true;
                    ApplyPage();
                }
                else _selectionAnchor = row.Record.Id;
            }), true);
        RecordsList.KeyDown += (_, args) =>
        {
            if (RecordsList.SelectedItem is not BusinessRecordRow current) return;
            if (args.Key == VirtualKey.Space && KeyHeld(0x11))
            {
                if (!_checkedRecordIds.Add(current.Record.Id)) _checkedRecordIds.Remove(current.Record.Id);
                _selectionAnchor = current.Record.Id;
                args.Handled = true;
                ApplyPage();
            }
            else if (KeyHeld(0x10) && args.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var index = Array.FindIndex(_filtered, item => item.Record.Id == current.Record.Id);
                var next = index + (args.Key == VirtualKey.Up ? -1 : 1);
                if (next < 0 || next >= _filtered.Length) return;
                _selectionAnchor ??= current.Record.Id;
                var anchor = Array.FindIndex(_filtered, item => item.Record.Id == _selectionAnchor);
                for (var selected = Math.Min(anchor, next); selected <= Math.Max(anchor, next); selected++)
                    _checkedRecordIds.Add(_filtered[selected].Record.Id);
                args.Handled = true;
                ApplyPage();
            }
        };
        TableRowStriping.Attach(RelatedRecordsList);
        InitializeTableActions();
        TableColumnResizer.Attach(RecordsHeader, ResizeColumn,
            () => { try { SectionColumnOrder.SaveWidths(_section.Key, _columnWidths); }
                catch (Exception exception) { ShowStatus(exception.Message); } });
        ApplyColumnOrder(RecordsHeader);
        AddButton.Label = "Προσθήκη " + Singular(section.Title);
        SearchInput.PlaceholderText = section.SearchHint;
        ConfigureFilterBar();
        TitleHeader.Text = section.Columns[0];
        PartyHeader.Text = section.Key == "partners" ? "Πόλη" :
            section.Columns.Length > 1 ? section.Columns[1] : "Στοιχεία";
        if (section.Key == "partners")
        {
            StatusHeader.Text = "Τύπος προσώπου";
            EventHeader.Text = "Πιστωτικό όριο";
            CustomerHeader.Text = "Υπόλοιπο";
            CreatedHeader.Text = "Κατάσταση";
        }
        else if (section.Key == "vehicles")
        {
            TitleHeader.Text = "Όχημα";
            CodeHeader.Text = "Πινακίδα";
            PartyHeader.Text = "Πελάτης";
            StatusHeader.Text = "Μάρκα";
            EventHeader.Text = "Χρήση";
            CustomerHeader.Text = "Πρώτη άδεια";
            CreatedHeader.Text = "Καύσιμο";
            ExtraHeader.Text = "Κατάσταση";
        }
        else if (section.Key is "companies" or "agencies")
        {
            TitleHeader.Text = "Κωδικός";
            CodeHeader.Text = "Επωνυμία";
            PartyHeader.Text = "ΑΦΜ";
            StatusHeader.Text = "Τηλέφωνο";
            EventHeader.Text = "Email";
            CustomerHeader.Text = "Υπόλοιπο";
            CreatedHeader.Text = "Κατάσταση";
        }
        else if (section.Key == "policies")
        {
            TitleHeader.Text = "Αριθμός συμβολαίου";
            CodeHeader.Text = "Πελάτης";
            PartyHeader.Text = "Ασφαλιστική";
            StatusHeader.Text = "Συνεργάτης";
            EventHeader.Text = "Κλάδος";
            CustomerHeader.Text = "Έναρξη";
            CreatedHeader.Text = "Λήξη";
            ExtraHeader.Text = "Είδος";
        }
        else if (section.Key == "claims")
        {
            TitleHeader.Text = "Αριθμός";
            CodeHeader.Text = "Συμβόλαιο";
            PartyHeader.Text = "Πελάτης";
            StatusHeader.Text = "Ασφαλιστική";
            EventHeader.Text = "Ημερομηνία συμβάντος";
            CustomerHeader.Text = "Κατάσταση";
        }
        StatusFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        CustomerFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        CityFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        PersonTypeFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        PolicyCustomerFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        PolicyPartnerFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        PolicyDocumentFilter.SelectionChanged += (_, _) => _ = RefreshAsync();
        Loaded += (_, _) => UpdateResponsiveLayout(PageContent.ActualWidth);
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await RefreshAsync();
        };
    }

    private void TableViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RecordsList.Height = Math.Max(120, e.NewSize.Height - RecordsHeader.ActualHeight - 2);
        if (Math.Abs(_lastTableViewportWidth - e.NewSize.Width) < 1) return;
        _lastTableViewportWidth = e.NewSize.Width;
        ApplyColumnOrder(RecordsHeader);
        foreach (var grid in _recordGrids.ToArray()) ApplyColumnOrder(grid);
        foreach (var item in RecordsList.Items)
            if (RecordsList.ContainerFromItem(item) is ListViewItem container)
                container.Width = DisplayTableWidth;
    }

    private void PageContent_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateResponsiveLayout(e.NewSize.Width);

    private void UpdateResponsiveLayout(double width)
    {
        if (FilterBar is null || _visibleFilterCount == 0 || width <= 0) return;
        var requiredWidth = _visibleFilterCount * 188 + 180 + 230 + 160 +
            (BatchButton.Visibility == Visibility.Visible ? 175 : 0) + 56;
        var popup = width < requiredWidth;
        if (popup != _filtersInPopup)
        {
            if (popup)
            {
                InlineFiltersHost.Children.Remove(FilterBar);
                PopupFiltersHost.Children.Add(FilterBar);
                FilterBar.Orientation = Orientation.Vertical;
            }
            else
            {
                PopupFiltersHost.Children.Remove(FilterBar);
                InlineFiltersHost.Children.Add(FilterBar);
                FilterBar.Orientation = Orientation.Horizontal;
            }
            _filtersInPopup = popup;
        }
        foreach (var filter in FilterBar.Children.OfType<FrameworkElement>())
            filter.Width = popup ? 240 : 180;
        ShowFiltersButton.Visibility = popup ? Visibility.Visible : Visibility.Collapsed;
        var narrow = width < (BatchButton.Visibility == Visibility.Visible ? 950 : 760);
        AddButton.IconOnly = narrow;
        DeleteButton.IconOnly = narrow;
        BatchButton.IconOnly = narrow;
        AddButton.Width = narrow ? 44 : 230;
        DeleteButton.Width = narrow ? 44 : 160;
        BatchButton.Width = narrow ? 44 : 175;
        var veryNarrow = width < 480;
        ClearFiltersButton.IconOnly = veryNarrow;
        ClearFiltersButton.Width = veryNarrow ? 44 : 180;
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            if (_section.Key == "partners")
            {
                _partnerCategories = [.. await _filters.ChoicesAsync("Κατηγορίες συνεργατών")];
                return;
            }
            if (_section.Key == "policies")
                _catalogBranches = [.. await _filters.ChoicesAsync("Κλάδοι")];
            if (_section.Key == "vehicles")
                _catalogVehicleUses = [.. await _filters.ChoicesAsync("Χρήσεις οχημάτων")];
            var customers = await _customers.SearchAsync();
            _customerChoices = [new CustomerChoice(null, "Χωρίς πελάτη"),
                .. customers.Select(item => new CustomerChoice(item.Id, item.FullName))];
            CustomerFilter.SetOptions(_customerChoices.Select(item => item.Name));
        }
        catch (Exception exception) { ShowStatus(exception.Message); }
    }

    private async Task RefreshAsync()
    {
        if (RecordsList is null || SearchInput is null || StatusText is null) return;
        var version = ++_requestVersion;
        try
        {
            var records = await _records.SearchAsync(_section.Key);
            if (version != _requestVersion) return;
            _checkedRecordIds.RemoveWhere(id => !records.Any(record => record.Id == id));
            if (_section.Key == "vehicles")
            {
                var policies = await _records.SearchAsync("policies");
                if (version != _requestVersion) return;
                _vehiclesWithActivePolicy = policies.Where(IsActivePolicy)
                    .Select(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).VehicleId)
                    .OfType<Guid>().ToHashSet();
                _vehicleInsuranceStates = policies
                    .Select(item => (Record: item,
                        VehicleId: BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).VehicleId))
                    .Where(item => item.VehicleId.HasValue)
                    .GroupBy(item => item.VehicleId!.Value)
                    .ToDictionary(group => group.Key, group =>
                        group.Any(item => IsActivePolicy(item.Record)) ? "Ασφαλισμένο" :
                        group.Any(item => item.Record.DocumentType is "Αίτηση" or "Ειδοποιητήριο" ||
                            item.Record.Status == "Σε εξέλιξη") ? "Σε εξέλιξη" :
                        "Χωρίς ενεργό συμβόλαιο");
            }
            if (_section.Key == "policies")
            {
                _policyPartnerNames = (await _records.SearchAsync("partners"))
                    .ToDictionary(item => item.Id, item => item.Title);
                if (version != _requestVersion) return;
            }
            ConfigureFilterOptions(records);
            IEnumerable<BusinessRecord> filtered = records;
            var term = SearchInput.Text.Trim();
            if (term.Length > 0)
                filtered = filtered.Where(item => item.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Reference?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.RelatedParty?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Status?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true ||
                    (_section.Key == "partners" && new[]
                    {
                        PartnerProfileData.Read(item.DetailsJson).TaxNumber,
                        PartnerProfileData.Read(item.DetailsJson).Email,
                        PartnerProfileData.Read(item.DetailsJson).Phone,
                        PartnerProfileData.Read(item.DetailsJson).Category,
                        PartnerProfileData.Read(item.DetailsJson).City
                    }.Any(value => value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true)) ||
                    (_section.Key is "companies" or "agencies" && new[]
                    {
                        WorkspaceCardData.Read(item.DetailsJson).Fields.GetValueOrDefault("taxNumber"),
                        WorkspaceCardData.Read(item.DetailsJson).Fields.GetValueOrDefault("phone"),
                        WorkspaceCardData.Read(item.DetailsJson).Fields.GetValueOrDefault("email")
                    }.Any(value => value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true)));
            filtered = filtered.Where(PassesSectionFilters);
            if (FromFilter.Date is DateTimeOffset fromDate)
            {
                var from = fromDate.Date;
                filtered = filtered.Where(item => FilterDate(item) >= from);
            }
            if (UntilFilter.Date is DateTimeOffset untilDate)
            {
                var until = untilDate.Date;
                filtered = filtered.Where(item => FilterDate(item) < until.AddDays(1));
            }
            IReadOnlyDictionary<Guid, decimal>? balances = null;
            BusinessRecord[] organizationPolicies = [];
            Dictionary<Guid, BusinessRecord> claimPolicies = [];
            Dictionary<Guid, string> claimCompanies = [];
            if (_section.Key == "partners")
            {
                balances = await _partners.GetBalancesAsync();
                if (version != _requestVersion) return;
            }
            if (_section.Key is "companies" or "agencies")
            {
                organizationPolicies = (await _records.SearchAsync("policies")).ToArray();
                if (version != _requestVersion) return;
            }
            if (_section.Key == "claims")
            {
                claimPolicies = (await _records.SearchAsync("policies"))
                    .ToDictionary(item => item.Id);
                claimCompanies = (await _records.SearchAsync("companies"))
                    .ToDictionary(item => item.Id, item => item.Title);
                if (version != _requestVersion) return;
            }
            _filtered = filtered.Select(item =>
            {
                var profile = _section.Key == "partners"
                    ? PartnerProfileData.Read(item.DetailsJson) : null;
                var vehicle = _section.Key == "vehicles"
                    ? BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson) : null;
                var organization = _section.Key is "companies" or "agencies"
                    ? WorkspaceCardData.Read(item.DetailsJson) : null;
                var policy = _section.Key == "policies"
                    ? BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson) : null;
                var claim = _section.Key == "claims"
                    ? WorkspaceCardData.Read(item.DetailsJson) : null;
                var linkedPolicy = claim?.PolicyId is Guid policyId &&
                    claimPolicies.TryGetValue(policyId, out var foundPolicy) ? foundPolicy : null;
                var linkedPolicyData = linkedPolicy is null ? null :
                    BusinessRecordDetails.Read<PolicyCardData>(linkedPolicy.DetailsJson);
                return new BusinessRecordRow(item, profile,
                    balances is not null && balances.TryGetValue(item.Id, out var due) ? due : null,
                    vehicle, _section.Key == "vehicles" ? VehicleDisplayStatus(item) : null,
                    organization, organization is null ? null :
                        OrganizationBalance.Calculate(item, organization, organizationPolicies),
                    policy, policy is null ? null : PolicyPartnerName(item),
                    claim, linkedPolicy is null ? null :
                        linkedPolicyData?.PolicyNumber ?? linkedPolicy.Reference ?? linkedPolicy.Title,
                    claim?.CompanyId is Guid companyId && claimCompanies.TryGetValue(companyId, out var company)
                        ? company : linkedPolicyData?.Insurer);
            }).ToArray();
            SortRecords();
            _page = 1;
            ApplyPage();
            ShowStatus(null);
            if (_section.Key is "vehicles" or "policies" or "claims")
            {
                var related = await _customers.GetRelatedRecordsAsync(_section.Key, SearchInput.Text);
                if (version != _requestVersion) return;
                RelatedRecordsList.ItemsSource = related;
                RelatedRecordsBorder.Visibility = related.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (Exception exception) { ShowStatus(exception.Message); }
    }

    private async void Filter_Changed(object sender, TextChangedEventArgs e) => await RefreshAsync();
    private async void DateFilter_Changed(object? sender, EventArgs args)
    { UpdateDateRangeLabel(); await RefreshAsync(); }
    private async void DateBasis_Changed(object sender, SelectionChangedEventArgs e)
    { UpdateDateRangeLabel(); await RefreshAsync(); }
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        StatusFilter.SelectAll();
        CustomerFilter.SelectAll();
        CityFilter.SelectAll();
        PersonTypeFilter.SelectAll();
        PolicyCustomerFilter.SelectAll();
        PolicyPartnerFilter.SelectAll();
        PolicyDocumentFilter.SelectAll();
        if (_section.Key == "policies") DateBasisFilter.SelectedIndex = 0;
        FromFilter.Date = null;
        UntilFilter.Date = null;
    }
    private void Records_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCheckedState();
    }
    private void UpdateCheckedState()
    {
        if (SelectAllRecords is null) return;
        _updatingSelectAll = true;
        SelectAllRecords.IsChecked = _filtered.Length > 0 &&
            _filtered.All(row => _checkedRecordIds.Contains(row.Record.Id));
        _updatingSelectAll = false;
        var selectedCount = _checkedRecordIds.Count > 0 ? _checkedRecordIds.Count : RecordsList.SelectedItems.Count;
        SelectedBadge.Text = $"{selectedCount} επιλεγμένες";
    }
    private void SelectAllRecords_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingSelectAll || RecordsList is null) return;
        if (SelectAllRecords.IsChecked == true)
            foreach (var row in _filtered) _checkedRecordIds.Add(row.Record.Id);
        else
            foreach (var row in _filtered) _checkedRecordIds.Remove(row.Record.Id);
        ApplyPage();
    }
    private void RecordCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingChecks || sender is not VisibleSelectionCheckBox { Tag: Guid id } box) return;
        if (box.IsChecked == true) _checkedRecordIds.Add(id);
        else _checkedRecordIds.Remove(id);
        UpdateCheckedState();
    }
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
        UpdateCheckedState();
        TotalBadge.Text = $"{_filtered.Length} εγγραφές";
        PageText.Text = $"Σελίδα {_page} από {pages} · Σύνολο {_filtered.Length}";
    }

    private void InitializeTableActions()
    {
        RecordsList.AddHandler(UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(async (_, args) =>
        {
            if (IsInsideCheckBox(args.OriginalSource)) return;
            if (FindRecordContainer(args.OriginalSource) is ListViewItem { Content: BusinessRecordRow row })
                await EditRecordAsync(row.Record);
        }), true);
        RecordsList.AddHandler(UIElement.RightTappedEvent,
            new RightTappedEventHandler((_, args) =>
            {
                if (FindRecordContainer(args.OriginalSource) is not ListViewItem { Content: BusinessRecordRow row } container)
                    return;
                container.IsSelected = true;
                CreateRecordMenu(row).ShowAt(container);
                args.Handled = true;
            }), true);
        RecordsList.ContainerContentChanging += (_, args) =>
        {
            if (args.InRecycleQueue || args.Item is not BusinessRecordRow row || args.ItemContainer is null) return;
            args.ItemContainer.Padding = new Thickness(0);
            args.ItemContainer.Margin = new Thickness(0);
            args.ItemContainer.Width = DisplayTableWidth;
            args.ItemContainer.Height = TableRowResizer.HeightFor(_section.Key, row.Record.Id);
            args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        };
        for (var index = 0; index < _columnOrder.Length; index++)
        {
            var column = index;
            var header = (FrameworkElement)RecordsHeader.Children[index];
            header.CanDrag = true;
            header.AllowDrop = true;
            header.DragStarting += (_, args) => args.Data.SetText($"section-column:{_section.Key}:{column}");
            header.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            };
            header.Drop += async (_, args) =>
            {
                var dragged = await args.DataView.GetTextAsync();
                var prefix = $"section-column:{_section.Key}:";
                if (dragged.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(dragged[prefix.Length..], out var source))
                    MoveColumnTo(source, column);
            };
            header.Tapped += (_, _) => SortBy(column, _sortColumn == column && !_sortDescending);
            var menu = new MenuFlyout();
            var ascending = new MenuFlyoutItem { Text = "Ταξινόμηση αύξουσα" };
            ascending.Click += (_, _) => SortBy(column, false);
            var descending = new MenuFlyoutItem { Text = "Ταξινόμηση φθίνουσα" };
            descending.Click += (_, _) => SortBy(column, true);
            var left = new MenuFlyoutItem { Text = "Μεταφορά αριστερά" };
            left.Click += (_, _) => MoveColumn(column, -1);
            var right = new MenuFlyoutItem { Text = "Μεταφορά δεξιά" };
            right.Click += (_, _) => MoveColumn(column, 1);
            menu.Items.Add(ascending);
            menu.Items.Add(descending);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(left);
            menu.Items.Add(right);
            header.ContextFlyout = menu;
        }
    }

    private static ListViewItem? FindRecordContainer(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is ListViewItem { Content: BusinessRecordRow }) return (ListViewItem)current;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
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

    private MenuFlyout CreateRecordMenu(BusinessRecordRow row)
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Άνοιγμα καρτέλας" };
        open.Click += async (_, _) => await EditRecordAsync(row.Record);
        var edit = new MenuFlyoutItem { Text = "Επεξεργασία" };
        edit.Click += async (_, _) => await EditRecordAsync(row.Record, beginEditing: true);
        var delete = new MenuFlyoutItem { Text = "Διαγραφή" };
        delete.Click += async (_, _) => await DeleteRecordAsync(row);
        menu.Items.Add(open);
        menu.Items.Add(edit);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(delete);
        return menu;
    }

    private void SortBy(int column, bool descending)
    {
        _sortColumn = column;
        _sortDescending = descending;
        SortRecords();
        ApplyPage();
    }

    private void SortRecords()
    {
        if (_section.Key is "policies" or "claims")
        {
            Func<BusinessRecordRow, string> policyOrClaimKey = _sortColumn switch
            {
                1 => row => row.Reference ?? "",
                2 => row => row.RelatedParty ?? "",
                3 => row => row.Status ?? "",
                4 => row => row.EventDateDisplay,
                5 => row => row.CustomerName,
                6 => row => row.CreatedDisplay,
                7 => row => row.ExtraDisplay,
                _ => row => row.Title
            };
            if (_section.Key == "policies" && _sortColumn is 5 or 6)
            {
                Func<BusinessRecordRow, DateTime> date = _sortColumn == 5
                    ? row => row.PolicyProfile?.StartDate ?? DateTime.MinValue
                    : row => row.PolicyProfile?.EndDate ?? DateTime.MinValue;
                _filtered = (_sortDescending ? _filtered.OrderByDescending(date) :
                    _filtered.OrderBy(date)).ToArray();
                return;
            }
            if (_section.Key == "claims" && _sortColumn == 4)
            {
                Func<BusinessRecordRow, DateTime> date = row =>
                    row.Record.EventDate ?? DateTime.MinValue;
                _filtered = (_sortDescending ? _filtered.OrderByDescending(date) :
                    _filtered.OrderBy(date)).ToArray();
                return;
            }
            _filtered = (_sortDescending ? _filtered.OrderByDescending(policyOrClaimKey,
                StringComparer.CurrentCultureIgnoreCase) :
                _filtered.OrderBy(policyOrClaimKey, StringComparer.CurrentCultureIgnoreCase)).ToArray();
            return;
        }
        if (_section.Key == "vehicles")
        {
            if (_sortColumn == 5)
            {
                Func<BusinessRecordRow, DateTime> date = row =>
                    row.VehicleProfile?.FirstRegistrationDate ?? DateTime.MinValue;
                _filtered = (_sortDescending ? _filtered.OrderByDescending(date) :
                    _filtered.OrderBy(date)).ToArray();
                return;
            }
            Func<BusinessRecordRow, string> vehicleKey = _sortColumn switch
            {
                1 => row => row.Reference ?? "",
                2 => row => row.RelatedParty ?? "",
                3 => row => row.Status ?? "",
                4 => row => row.EventDateDisplay,
                6 => row => row.CreatedDisplay,
                7 => row => row.ExtraDisplay,
                _ => row => row.Title
            };
            _filtered = (_sortDescending ? _filtered.OrderByDescending(vehicleKey,
                StringComparer.CurrentCultureIgnoreCase) : _filtered.OrderBy(vehicleKey,
                StringComparer.CurrentCultureIgnoreCase)).ToArray();
            return;
        }
        if (_section.Key == "partners")
        {
            if (_sortColumn is 4 or 5)
            {
                Func<BusinessRecordRow, decimal> amount = _sortColumn == 4
                    ? row => row.PartnerProfile?.CreditLimit ?? 0m
                    : row => row.PartnerBalance ?? 0m;
                _filtered = (_sortDescending ? _filtered.OrderByDescending(amount) :
                    _filtered.OrderBy(amount)).ToArray();
                return;
            }
            Func<BusinessRecordRow, string> partnerKey = _sortColumn switch
            {
                1 => row => row.Reference ?? "",
                2 => row => row.RelatedParty ?? "",
                3 => row => row.Status ?? "",
                6 => row => row.Record.Status ?? "",
                _ => row => row.Title
            };
            _filtered = (_sortDescending ? _filtered.OrderByDescending(partnerKey,
                StringComparer.CurrentCultureIgnoreCase) : _filtered.OrderBy(partnerKey,
                StringComparer.CurrentCultureIgnoreCase)).ToArray();
            return;
        }
        if (_section.Key is "companies" or "agencies")
        {
            if (_sortColumn == 5)
            {
                Func<BusinessRecordRow, decimal> amount = row => row.OrganizationBalance ?? 0m;
                _filtered = (_sortDescending ? _filtered.OrderByDescending(amount) :
                    _filtered.OrderBy(amount)).ToArray();
                return;
            }
            Func<BusinessRecordRow, string> organizationKey = _sortColumn switch
            {
                1 => row => row.Reference ?? "", 2 => row => row.RelatedParty ?? "",
                3 => row => row.Status ?? "", 4 => row => row.EventDateDisplay,
                6 => row => row.CreatedDisplay, _ => row => row.Title
            };
            _filtered = (_sortDescending ? _filtered.OrderByDescending(organizationKey,
                StringComparer.CurrentCultureIgnoreCase) : _filtered.OrderBy(organizationKey,
                StringComparer.CurrentCultureIgnoreCase)).ToArray();
            return;
        }
        if (_sortColumn is 4 or 6)
        {
            Func<BusinessRecordRow, DateTime> date = _sortColumn == 4
                ? row => row.Record.EventDate ?? DateTime.MinValue
                : row => row.Record.CreatedAtUtc;
            _filtered = (_sortDescending ? _filtered.OrderByDescending(date) :
                _filtered.OrderBy(date)).ToArray();
            return;
        }
        Func<BusinessRecordRow, string> key = _sortColumn switch
        {
            1 => row => row.Reference ?? "",
            2 => row => row.RelatedParty ?? "",
            3 => row => row.Status ?? "",
            5 => row => row.CustomerName,
            _ => row => row.Title
        };
        _filtered = (_sortDescending ? _filtered.OrderByDescending(key, StringComparer.CurrentCultureIgnoreCase) :
            _filtered.OrderBy(key, StringComparer.CurrentCultureIgnoreCase)).ToArray();
    }

    private void MoveColumn(int column, int offset)
    {
        var position = Array.IndexOf(_columnOrder, column);
        var target = position + offset;
        if (target < 0 || target >= _columnOrder.Length) return;
        (_columnOrder[position], _columnOrder[target]) = (_columnOrder[target], _columnOrder[position]);
        try { SectionColumnOrder.Save(_section.Key, _columnOrder); }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        { ShowStatus(exception.Message); }
        ApplyColumnOrder(RecordsHeader);
        RecordsList.ItemsSource = null;
        ApplyPage();
    }

    private void MoveColumnTo(int column, int targetColumn)
    {
        if (column == targetColumn || !_columnOrder.Contains(column) ||
            !_columnOrder.Contains(targetColumn)) return;
        var order = _columnOrder.ToList();
        order.Remove(column);
        order.Insert(order.IndexOf(targetColumn), column);
        order.CopyTo(_columnOrder);
        try { SectionColumnOrder.Save(_section.Key, _columnOrder); }
        catch (Exception exception) { ShowStatus(exception.Message); }
        ApplyColumnOrder(RecordsHeader);
        RecordsList.ItemsSource = null;
        ApplyPage();
    }

    private void RecordRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var firstLoad = _recordGrids.Add(grid);
        ApplyColumnOrder(grid);
        if (grid.DataContext is not BusinessRecordRow row ||
            grid.Children[0] is not Border { Child: VisibleSelectionCheckBox check }) return;
        TableRowStriping.ApplyRow(grid, RecordsList.Items.IndexOf(row), RecordsList);
        if (_section.Key == "policies" &&
            grid.Children[8] is Border { Child: TextBlock policyText } policyCell)
        {
            policyCell.Background = PolicyStatusColors.DocumentTypeBackground(row.Record.DocumentType);
            policyText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        var statusIndex = _section.Key switch
        {
            "partners" => 7,
            "vehicles" => 8,
            "companies" or "agencies" => 7,
            "claims" => 6,
            "policies" => -1,
            _ => 4
        };
        if (statusIndex >= 0 && grid.Children[statusIndex] is Border { Child: TextBlock statusText } statusCell)
        {
            var status = _section.Key == "vehicles" ? row.ExtraDisplay : row.Record.Status;
            var highlighted = status is "Ενεργό" or "Ενεργός" or "Ασφαλισμένο" or
                "Σε εξέλιξη" or "Πιθανός";
            statusCell.Background = highlighted ? StatusPalette.Brush(
                status is "Ενεργό" or "Ενεργός" or "Ασφαλισμένο"
                    ? StatusPalette.Positive : StatusPalette.Caution) : null;
            statusText.Foreground = highlighted ? new SolidColorBrush(Microsoft.UI.Colors.White) : null;
        }
        if (_section.Key == "partners" &&
            grid.Children[6] is Border { Child: TextBlock balanceText } balanceCell)
        {
            var outstanding = row.PartnerBalance.GetValueOrDefault() != 0m;
            balanceCell.Background = outstanding
                ? StatusPalette.Brush(StatusPalette.Negative) : null;
            balanceText.Foreground = outstanding
                ? new SolidColorBrush(Microsoft.UI.Colors.White) : null;
        }
        if (_section.Key is "companies" or "agencies" &&
            grid.Children[6] is Border { Child: TextBlock dueText } dueCell)
        {
            var outstanding = row.OrganizationBalance.GetValueOrDefault() != 0m;
            dueCell.Background = outstanding ? StatusPalette.Brush(StatusPalette.Negative) : null;
            dueText.Foreground = outstanding ? new SolidColorBrush(Microsoft.UI.Colors.White) : null;
        }
        if (firstLoad && FindRecordContainer(grid) is ListViewItem container)
            TableRowResizer.Attach(grid, container, _section.Key, row.Record.Id);
        _syncingChecks = true;
        check.IsChecked = _checkedRecordIds.Contains(row.Record.Id);
        _syncingChecks = false;
    }

    private void RecordRow_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid grid) _recordGrids.Remove(grid);
    }

    private double TableWidth => 40 + _columnWidths.Where((_, index) => _columnVisible[index]).Sum();

    private TableExportData ExportData()
    {
        var names = new[] { TitleHeader.Text, CodeHeader.Text, PartyHeader.Text,
            StatusHeader.Text, EventHeader.Text, CustomerHeader.Text, CreatedHeader.Text,
            ExtraHeader.Text };
        var active = _columnOrder.Where(index => index < names.Length && _columnVisible[index])
            .ToArray();
        var rows = _filtered.Select(item =>
        {
            var values = new[] { item.Title, item.Reference, item.RelatedParty, item.Status,
                item.EventDateDisplay, item.CustomerName, item.CreatedDisplay, item.ExtraDisplay };
            return active.Select(index => values[index] ?? "").ToArray();
        }).ToArray();
        return new TableExportData(_section.Title, active.Select(index => names[index]).ToArray(), rows);
    }
    private double DisplayTableWidth => Math.Max(TableWidth, TableViewport.ActualWidth - 2);

    private double DisplayColumnWidth(int original)
    {
        if (!_columnVisible[original]) return 0;
        var baseWidth = TableWidth - 40;
        var scale = baseWidth <= 0 ? 1 : Math.Max(1, (DisplayTableWidth - 40) / baseWidth);
        return _columnWidths[original] * scale;
    }

    private void ResizeColumn(int original, double width)
    {
        _columnWidths[original] = Math.Clamp(width, 70, 520);
        ApplyColumnOrder(RecordsHeader);
        foreach (var grid in _recordGrids.ToArray()) ApplyColumnOrder(grid);
        RecordsTableStack.Width = DisplayTableWidth + 2;
        foreach (var item in RecordsList.Items)
            if (RecordsList.ContainerFromItem(item) is ListViewItem container)
                container.Width = DisplayTableWidth;
    }

    private void ApplyColumnOrder(Grid grid)
    {
        var isHeader = ReferenceEquals(grid, RecordsHeader);
        if (isHeader) RecordsTableStack.Width = DisplayTableWidth + 2;
        else grid.Width = DisplayTableWidth;
        for (var position = 0; position < _columnOrder.Length; position++)
        {
            var original = _columnOrder[position];
            var offset = isHeader ? 0 : 1;
            grid.ColumnDefinitions[position + offset].Width = new GridLength(DisplayColumnWidth(original));
            var cell = (FrameworkElement)grid.Children[original + offset];
            cell.Visibility = _columnVisible[original] ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(cell, position + offset);
            if (cell is Border border)
                border.BorderThickness = isHeader
                    ? position == _columnOrder.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0)
                    : position == _columnOrder.Length - 1 ? new Thickness(0, 0, 0, 1) :
                        new Thickness(0, 0, 1, 1);
        }
    }
    private async void Columns_Click(object sender, RoutedEventArgs e)
    {
        var names = new[] { TitleHeader.Text, CodeHeader.Text, PartyHeader.Text,
            StatusHeader.Text, EventHeader.Text, CustomerHeader.Text, CreatedHeader.Text }
            .Concat(_section.Key is "vehicles" or "policies" ? [ExtraHeader.Text] : [])
            .Take(_section.Key == "claims" ? 6 : int.MaxValue).ToArray();
        var fields = names.Select((name, index) => new CheckBox
        {
            Content = name, IsChecked = _columnVisible[index], IsEnabled = index != 0
        }).ToArray();
        var panel = new StackPanel { Spacing = 4 };
        foreach (var field in fields) panel.Children.Add(field);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαχείριση στηλών",
            Content = panel, PrimaryButtonText = "Εφαρμογή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        for (var index = 0; index < fields.Length; index++)
            _columnVisible[index] = fields[index].IsChecked == true;
        try { SectionColumnOrder.SaveVisibility(_section.Key, _columnVisible); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
        ApplyColumnOrder(RecordsHeader);
        foreach (var grid in _recordGrids.ToArray()) ApplyColumnOrder(grid);
        RecordsList.ItemsSource = null;
        ApplyPage();
    }
    private async void Add_Click(object sender, RoutedEventArgs e) => await EditRecordAsync(null);
    private async Task EditRecordAsync(BusinessRecord? existing, bool beginEditing = false)
    {
        if (_section.Key == "partners")
        {
            OpenPartnerCard(existing?.Id, beginEditing);
            return;
        }
        if (_section.Key is "policies" or "vehicles")
        {
            OpenBusinessRecordCard(existing?.Id, beginEditing);
            return;
        }
        OpenWorkspaceCard(existing?.Id, beginEditing);
    }

    private void OpenPartnerCard(Guid? id, bool beginEditing = false)
    {
        if (_partnerCard is not null) { _partnerCard.BringToFront(); return; }
        try
        {
            _partnerCard = new PartnerCardWindow(id, beginEditing);
            _partnerCard.RecordSaved += async (_, _) => await RefreshAsync();
            _partnerCard.Closed += async (_, _) =>
            {
                _partnerCard = null;
                await RefreshAsync();
            };
            _partnerCard.BringToFront();
        }
        catch (Exception exception) { _partnerCard = null; ShowStatus(exception.Message); }
    }

    private void OpenBusinessRecordCard(Guid? id, bool beginEditing = false)
    {
        if (_businessCard is not null) { _businessCard.BringToFront(); return; }
        try
        {
            _businessCard = new BusinessRecordCardWindow(_section.Key, recordId: id,
                beginEditing: beginEditing);
            _businessCard.RecordSaved += async (_, _) => await RefreshAsync();
            _businessCard.Closed += async (_, _) =>
            {
                _businessCard = null;
                await RefreshAsync();
            };
            _businessCard.BringToFront();
        }
        catch (Exception exception) { _businessCard = null; ShowStatus(exception.Message); }
    }

    private void OpenWorkspaceCard(Guid? id, bool beginEditing = false)
    {
        if (_workspaceCard is not null) { _workspaceCard.BringToFront(); return; }
        try
        {
            _workspaceCard = new WorkspaceRecordCardWindow(_section.Key, id, beginEditing);
            _workspaceCard.RecordSaved += async (_, _) => await RefreshAsync();
            _workspaceCard.Closed += async (_, _) =>
            {
                _workspaceCard = null;
                await RefreshAsync();
            };
            _workspaceCard.BringToFront();
        }
        catch (Exception exception) { _workspaceCard = null; ShowStatus(exception.Message); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filtered.Where(row => _checkedRecordIds.Contains(row.Record.Id)).ToArray();
        if (selected.Length == 0 && RecordsList.SelectedItem is BusinessRecordRow focused)
            selected = [focused];
        if (selected.Length == 0)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε εγγραφή από τον πίνακα.");
            return;
        }
        if (selected.Length == 1) { await DeleteRecordAsync(selected[0]); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Μαζική διαγραφή εγγραφών",
            Content = $"Να διαγραφούν οι {selected.Length} επιλεγμένες εγγραφές;",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            foreach (var row in selected) await _records.DeleteAsync(row.Record.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await RefreshAsync();
            await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message);
        }
    }

    private async void BulkReceipt_Click(object sender, RoutedEventArgs e)
        => await PostBulkAsync("receipt");

    private async void BulkPayment_Click(object sender, RoutedEventArgs e)
        => await PostBulkAsync("payment");

    private async Task PostBulkAsync(string direction)
    {
        if (_section.Key is not ("partners" or "companies" or "agencies")) return;
        try
        {
            var selectedIds = _checkedRecordIds.ToHashSet();
            if (selectedIds.Count == 0)
            {
                await UiAlerts.ShowErrorAsync(XamlRoot,
                    "Επιλέξτε πρώτα καρτέλες με τα τετράγωνα επιλογής.");
                return;
            }
            var records = await _records.SearchAsync(_section.Key);
            var targets = records.Where(item => selectedIds.Contains(item.Id))
                .Select(item => new BulkFinancialTarget(item.Id, item.Title)).ToArray();
            if (targets.Length != selectedIds.Count)
            {
                await UiAlerts.ShowErrorAsync(XamlRoot,
                    "Κάποια επιλεγμένη καρτέλα δεν υπάρχει πλέον. Ανανεώστε τη λίστα.");
                return;
            }
            if (await BulkFinancialMovementDialog.ShowAsync(XamlRoot, _section.Key,
                direction, targets))
            {
                _checkedRecordIds.Clear();
                await RefreshAsync();
            }
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task DeleteRecordAsync(BusinessRecordRow row)
    {
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

    private void OpenRelatedCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (RelatedRecordsList.SelectedItem is not CustomerRelatedRecord row) return;
        try
        {
            if (_card is not null) { _card.BringToFront(); return; }
            _card = new CustomerCardWindow(row.CustomerId);
            _card.CustomerChanged += async (_, _) => await RefreshAsync();
            _card.Closed += async (_, _) => { _card = null; await RefreshAsync(); };
            _card.BringToFront();
            DispatcherQueue.TryEnqueue(() => _card?.BringToFront());
        }
        catch (Exception exception) { _card = null; ShowStatus(exception.Message); }
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

    private void ShowStatus(string? message)
    {
        StatusText.Text = message ?? "";
        StatusText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private sealed record CustomerChoice(Guid? Id, string Name);

    private sealed record BusinessRecordRow(BusinessRecord Record,
        PartnerProfileData? PartnerProfile = null, decimal? PartnerBalance = null,
        VehicleCardData? VehicleProfile = null, string? VehicleInsuranceState = null,
        WorkspaceCardData? OrganizationProfile = null, decimal? OrganizationBalance = null,
        PolicyCardData? PolicyProfile = null, string? PolicyPartner = null,
        WorkspaceCardData? ClaimProfile = null, string? ClaimPolicyNumber = null,
        string? ClaimInsurer = null)
    {
        public string Title => PolicyProfile is not null
            ? PolicyProfile.PolicyNumber ?? PolicyProfile.ApplicationNumber ??
                PolicyProfile.NoticeNumber ?? Record.Reference ?? Record.Title :
            OrganizationProfile is not null ? Record.Reference ?? "—" :
            VehicleProfile is null ? Record.Title :
            !string.IsNullOrWhiteSpace(VehicleProfile.Model) ? VehicleProfile.Model :
            !string.IsNullOrWhiteSpace(VehicleProfile.Make) ? VehicleProfile.Make : Record.Title;
        public string? Reference => PolicyProfile is not null ? CustomerLabel :
            ClaimProfile is not null ? ClaimPolicyNumber ?? "—" :
            OrganizationProfile is not null ? Record.Title :
            VehicleProfile?.Registration ?? Record.Reference;
        public string? RelatedParty => PolicyProfile is not null ? PolicyProfile.Insurer ?? "—" :
            ClaimProfile is not null ? CustomerLabel : OrganizationProfile is not null
            ? OrganizationProfile.Fields.GetValueOrDefault("taxNumber") ?? "—" :
            VehicleProfile is not null ? CustomerLabel : PartnerProfile?.City ?? Record.RelatedParty;
        public string? Status => PolicyProfile is not null ? PolicyPartner ?? "Χωρίς συνεργάτη" :
            ClaimProfile is not null ? ClaimInsurer ?? "—" : OrganizationProfile is not null
            ? OrganizationProfile.Fields.GetValueOrDefault("phone") ?? "—" : VehicleProfile is not null
            ? VehicleProfile.Make ?? "—"
            : PartnerProfile?.PersonType ?? Record.Status;
        public string EventDateDisplay => PolicyProfile is not null ? PolicyProfile.Branch ?? "—" :
            ClaimProfile is not null ? Record.EventDate?.ToString("dd/MM/yyyy") ?? "—" :
            OrganizationProfile is not null
            ? OrganizationProfile.Fields.GetValueOrDefault("email") ?? "—" : VehicleProfile is not null
            ? VehicleProfile.Use ?? "—" : PartnerProfile is null
            ? Record.EventDate?.ToString("dd/MM/yyyy") ?? ""
            : PartnerProfile.CreditLimit is decimal credit ? $"{credit:N2} €" : "—";
        public string CustomerName => PolicyProfile is not null
            ? PolicyProfile.StartDate?.ToString("dd/MM/yyyy") ?? "—" :
            ClaimProfile is not null ? Record.Status ?? "—" : OrganizationProfile is not null
            ? $"{OrganizationBalance.GetValueOrDefault():N2} €" : VehicleProfile is not null
            ? VehicleProfile.FirstRegistrationDate?.ToString("dd/MM/yyyy") ?? "—"
            : PartnerProfile is null
            ? Record.Customer is null ? "" :
                $"{Record.Customer.LastName} {Record.Customer.FirstName}"
            : $"{PartnerBalance.GetValueOrDefault():N2} €";
        public string CreatedDisplay => PolicyProfile is not null
            ? PolicyProfile.EndDate?.ToString("dd/MM/yyyy") ?? "—" :
            ClaimProfile is not null ? "" : OrganizationProfile is not null
            ? Record.Status ?? "—" : VehicleProfile is not null
            ? VehicleProfile.Fuel ?? "—" : PartnerProfile is null
            ? Record.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy")
            : Record.Status ?? "";
        public string ExtraDisplay => PolicyProfile is not null
            ? Record.DocumentType ?? "Ασφαλιστήριο"
            : VehicleInsuranceState ?? Record.Status ?? "—";
        private string CustomerLabel => Record.Customer is null ? "—" :
            $"{Record.Customer.LastName} {Record.Customer.FirstName}".Trim();
        public override string ToString() => Record.Title;
    }
}

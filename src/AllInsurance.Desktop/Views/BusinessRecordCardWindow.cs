using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.Graphics;
using Windows.UI;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow : Window
{
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);

    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly ICustomerService _customers = App.Services.GetRequiredService<ICustomerService>();
    private readonly string _section;
    private readonly Guid? _fixedCustomerId;
    private readonly Guid? _recordId;
    private readonly nint _ownerHandle;
    private BusinessRecord _record;
    private readonly Grid _root = new() { RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(),
        new RowDefinition { Height = GridLength.Auto }
    } };
    private readonly Grid _tabs = new() { Margin = new Thickness(18, 0, 18, 0) };
    private readonly ContentControl _tabContent = new();
    private readonly List<(Button Button, TextBlock Label, UIElement Content)> _tabItems = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heading = new() { FontSize = 25,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerState = new() { Foreground = new SolidColorBrush(Colors.White) };
    private Border? _headerStateBadge;
    private readonly CardHeaderCode _headerCode = new();
    private readonly Dictionary<string, TextBox> _text = [];
    private readonly Dictionary<string, EditableDateInput> _dates = [];
    private readonly List<Grid> _formGrids = [];
    private readonly Dictionary<string, CheckBox> _vehicleChecks = [];
    private readonly Dictionary<string, Border> _vehicleCheckBadges = [];
    private readonly List<VehiclePerson> _vehiclePeople = [];
    private IReadOnlyList<CustomerListItem> _vehicleCustomers = [];
    private IReadOnlyList<BusinessRecord> _vehiclePolicies = [];
    private IReadOnlyList<BusinessRecord> _vehicleClaims = [];
    private IReadOnlyList<BusinessRecord> _agencyRecords = [];
    private IReadOnlyList<BusinessRecord> _partnerRecords = [];
    private IReadOnlyList<BusinessRecord> _allPolicyRecords = [];
    private WorkspaceRecordCardWindow? _linkedOrganizationCard;
    private ListView? _peopleList;
    private ListView? _policiesList;
    private ListView? _claimsList;
    private string? _kteoImagePath;
    private BusinessRecordCardWindow? _linkedPolicyCard;
    private double _contentWidth = 1100;
    private ComboBox? _customerChoice;
    private ComboBox? _vehicleChoice;
    private ComboBox? _typeChoice;
    private ComboBox? _statusChoice;
    private ComboBox? _kteoStatusChoice;
    private AutoSuggestBox? _insurerSuggest;
    private ActionButton? _convertButton;
    private ActionButton? _saveButton;
    private ActionButton? _editButton;
    private bool _editing;
    private ActionButton? _newVehiclePolicyButton;
    private ActionButton? _addVehicleClaimButton;
    private TextBlock? _policyFinanceSummary;
    private TextBlock? _policyCommissionSummary;
    private TextBlock? _historyText;
    private bool _conversionRequested;
    private sealed record Choice(Guid? Id, string Name);

    public event EventHandler? RecordSaved;

    public BusinessRecordCardWindow(string section, Guid? customerId = null, Guid? recordId = null,
        nint ownerHandle = default, Guid? vehicleId = null, bool beginEditing = false,
        string? insurerName = null, string? agencyName = null, Guid? previousPolicyId = null)
    {
        if (section is not ("policies" or "vehicles"))
            throw new ArgumentException("Μη υποστηριζόμενη καρτέλα.");
        _section = section;
        _fixedCustomerId = customerId;
        _recordId = recordId;
        _editing = recordId is null || beginEditing;
        _ownerHandle = ownerHandle == default ? App.MainWindowHandle() : ownerHandle;
        _record = new BusinessRecord { SectionKey = section, Title = "",
            CustomerId = customerId, DocumentType = section == "policies"
                ? previousPolicyId.HasValue ? "Ασφαλιστήριο" : "Αίτηση" : null,
            DetailsJson = section == "policies"
                ? BusinessRecordDetails.Write(new PolicyCardData { VehicleId = vehicleId,
                    Insurer = insurerName, Agency = agencyName,
                    PreviousPolicyId = previousPolicyId }) : null };
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        _root.Background = new SolidColorBrush(IsDarkTheme()
            ? Color.FromArgb(255, 31, 35, 42)
            : App.Services.GetRequiredService<WorkspacePreferences>().EyeComfortEnabled
                ? Color.FromArgb(255, 228, 225, 216) : Color.FromArgb(255, 221, 228, 235));
        Content = _root;
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1220, Math.Max(660, area.Width - 32));
        var height = Math.Min(900, Math.Max(560, area.Height - 32));
        _contentWidth = Math.Min(1100, width - 70);
        _tabContent.SizeChanged += (_, args) =>
        {
            var target = Math.Min(1100, Math.Max(280, args.NewSize.Width - 50));
            foreach (var grid in _formGrids) grid.Width = target;
            LayoutTabs(args.NewSize.Width - 36);
        };
        appWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        appWindow.Title = section == "policies" ? "Καρτέλα ασφάλισης — AllInsurance" :
            "Καρτέλα οχήματος — AllInsurance";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        _root.Children.Add(new TextBlock { Text = "Φόρτωση καρτέλας...", Margin = new Thickness(24) });
        _ = LoadAsync();
    }

    public void BringToFront()
    {
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", _section + "-card");
        var handle = WindowNative.GetWindowHandle(this);
        SetWindowOwner(handle, -8, _ownerHandle);
        Activate();
        const uint flags = 0x0001 | 0x0002 | 0x0040;
        SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, flags);
        SetWindowPos(handle, new nint(-2), 0, 0, 0, 0, flags);
        SetForegroundWindow(handle);
    }

    private async Task LoadAsync()
    {
        try
        {
            if (_recordId is Guid id)
                _record = await _records.GetAsync(id) ?? throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
            if (_record.SectionKey != _section ||
                (_fixedCustomerId is Guid fixedId && _record.CustomerId != fixedId))
                throw new InvalidOperationException("Η εγγραφή δεν ανήκει σε αυτή την καρτέλα.");
            if (_section == "policies" && _record.Id == Guid.Empty &&
                BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).PreviousPolicyId is Guid previousId)
            {
                var previous = await _records.GetAsync(previousId)
                    ?? throw new InvalidOperationException("Το προηγούμενο ασφαλιστήριο δεν υπάρχει.");
                if (previous.SectionKey != "policies" || previous.CustomerId != _record.CustomerId)
                    throw new InvalidOperationException("Η ανανέωση πρέπει να αφορά τον ίδιο πελάτη.");
                var old = BusinessRecordDetails.Read<PolicyCardData>(previous.DetailsJson);
                var start = old.EndDate ?? DateTime.Today;
                var duration = old.CoverageDurationValue ?? old.CoverageDurationMonths ?? 12;
                var unit = old.CoverageDurationUnit ?? "Μήνες";
                var end = unit switch
                {
                    "Ημέρες" => start.AddDays(duration), "Έτη" => start.AddYears(duration),
                    _ => start.AddMonths(duration)
                };
                _record.DetailsJson = BusinessRecordDetails.Write(new PolicyCardData
                {
                    PreviousPolicyId = previousId, Insurer = old.Insurer, Agency = old.Agency,
                    PreviousPolicyNumber = previous.Reference ?? old.PolicyNumber,
                    CompanyId = old.CompanyId, AgencyId = old.AgencyId,
                    Branch = old.Branch, Product = old.Product, VehicleId = old.VehicleId,
                    HolderCustomerId = old.HolderCustomerId, PartnerId = old.PartnerId,
                    PreviousInsurer = old.Insurer, CoverageDurationMonths = unit == "Μήνες" ? duration : null,
                    CoverageDurationValue = duration, CoverageDurationUnit = unit,
                    PreferredPaymentMethod = old.PreferredPaymentMethod,
                    PaymentRecipient = old.PaymentRecipient,
                    StartDate = start, EndDate = end,
                    RenewalDate = DateTime.Today, InsuredValue = old.InsuredValue,
                    CoverageNotes = old.CoverageNotes,
                    Coverages = (old.Coverages ?? []).Select(item => new PolicyCoverage
                    {
                        Name = item.Name, SumInsured = item.SumInsured,
                        Deductible = item.Deductible, Premium = item.Premium,
                        Notes = item.Notes
                    }).ToList(),
                    InsuredObjects = (old.InsuredObjects ?? []).Select(item => new PolicyInsuredObject
                    {
                        Kind = item.Kind, Description = item.Description,
                        InsuredValue = item.InsuredValue
                    }).ToList()
                });
            }
            var customers = await _customers.SearchAsync();
            var companies = _section == "policies"
                ? (await _records.SearchAsync("companies")).ToArray()
                : [];
            _agencyRecords = _section == "policies"
                ? (await _records.SearchAsync("agencies")).ToArray() : [];
            _partnerRecords = _section == "policies"
                ? (await _records.SearchAsync("partners")).ToArray() : [];
            _allPolicyRecords = _section == "policies"
                ? (await _records.SearchAsync("policies")).ToArray() : [];
            var vehicles = _section == "policies"
                ? (await _records.SearchAsync("vehicles")).ToArray()
                : [];
            var policies = _section == "vehicles" && _record.Id != Guid.Empty
                ? (await _records.SearchAsync("policies"))
                    .Where(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).VehicleId == _record.Id)
                    .ToArray()
                : [];
            var claims = _section == "vehicles" && _record.Id != Guid.Empty
                ? (await _records.SearchAsync("claims"))
                    .Where(item => BusinessRecordDetails.Read<ClaimCardData>(item.DetailsJson).VehicleId == _record.Id)
                    .ToArray()
                : [];
            var transactions = _section == "policies" && _record.CustomerId is Guid ownerId
                ? (await _customers.GetAsync(ownerId))?.Transactions
                    .Where(item => item.BusinessRecordId == _record.Id).ToArray() ?? []
                : [];
            _policyTransactions = transactions;
            if (_section == "policies") _policyCurrency = await _policySettings.GetCurrencyAsync();
            if (_section == "policies")
            {
                _policyBranchOptions = (await _policyFilters.ListAsync("Κλάδοι")).ToArray();
                _policyPackageOptions = (await _policyFilters.ListAsync("Πακέτα ασφαλίσεων")).ToArray();
            }
            if (_section == "vehicles")
                _vehicleUseOptions = (await _policyFilters.ChoicesAsync("Χρήσεις οχημάτων")).ToArray();
            _vehicleCustomers = customers;
            _vehiclePolicies = policies;
            _vehicleClaims = claims;
            BuildUi(customers, vehicles, policies, transactions, companies);
        }
        catch (Exception exception)
        {
            _root.Children.Clear();
            _root.Children.Add(new TextBlock { Text = exception.Message, Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap });
        }
    }

    private void BuildUi(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> vehicles, IReadOnlyList<BusinessRecord> policies,
        IReadOnlyList<CustomerTransaction> transactions, IReadOnlyList<BusinessRecord> companies)
    {
        _root.Children.Clear();
        var gradient = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var banner = new Border { Margin = new Thickness(18, 8, 18, 4),
            Padding = new Thickness(24, 12, 24, 12), CornerRadius = new CornerRadius(16),
            Background = gradient };
        _heading.Text = _section == "vehicles"
            ? (_record.Id == Guid.Empty ? "Νέο όχημα" : _record.Title)
            : (_record.Id == Guid.Empty
                ? BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).PreviousPolicyId.HasValue
                    ? "Νέο ανανεωτήριο · Ασφαλιστήριο" : "Νέο έγγραφο ασφάλισης" :
                $"✓ {(_section == "policies" && BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).PreviousPolicyId.HasValue ? "Ανανεωτήριο · " : "")}{_record.DocumentType ?? "Ασφαλιστήριο"} · {_record.Title}");
        _headerState.Text = $"● {_record.Status ?? (_section == "vehicles" ? "Ενεργό" : "Σε εξέλιξη")}";
        _headerCode.SetCode(_section == "policies"
            ? BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).CardCode ?? _record.Reference
            : BusinessRecordDetails.Read<VehicleCardData>(_record.DetailsJson).CardCode ?? _record.Title);
        var identity = new Grid { ColumnSpacing = 10, ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto }
        } };
        identity.Children.Add(new Image { Source = new BitmapImage(new Uri(_section == "vehicles"
            ? "ms-appx:///Assets/Sections/vehicles.png" : "ms-appx:///Assets/Sections/policies.png")),
            Width = 42, Height = 42 });
        _heading.FontSize = 22;
        _heading.TextTrimming = TextTrimming.CharacterEllipsis;
        _heading.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_heading, 1); identity.Children.Add(_heading);
        if (_section == "policies")
        {
            _headerStateBadge = new Border { Child = _headerState,
                CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 3, 7, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_headerStateBadge, 2); identity.Children.Add(_headerStateBadge);
            UpdatePolicyHeaderBadge();
        }
        else { _headerState.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_headerState, 2); identity.Children.Add(_headerState); }
        Grid.SetColumn(_headerCode, 3); identity.Children.Add(_headerCode);
        banner.Padding = new Thickness(18, 9, 18, 9);
        banner.Child = identity;
        _root.Children.Add(banner);
        Grid.SetRow(_tabs, 1);
        _root.Children.Add(_tabs);
        Grid.SetRow(_tabContent, 2);
        _root.Children.Add(_tabContent);
        var footer = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
            new RowDefinition { Height = GridLength.Auto } },
            Margin = new Thickness(18, 10, 18, 14) };
        footer.Children.Add(_status);
        var toolbar = new Grid { MinWidth = _section == "policies" ? 970 : 0,
            ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } } };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right };
        if (_section == "policies")
        {
            var quick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right };
            _footerReceiptButton = new ActionButton { Kind = "Receipt", Label = "Είσπραξη" };
            _footerPaymentButton = new ActionButton { Kind = "Payment", Label = "Πληρωμή" };
            _footerRenewButton = new ActionButton { Kind = "Renewals", Label = "Ανανέωση" };
            _footerCancelButton = new ActionButton { Kind = "Deactivate", Label = "Ακύρωση" };
            _footerReceiptButton.Click += async (_, _) => await AddPolicyTransactionAsync("receipt");
            _footerPaymentButton.Click += async (_, _) => await AddPolicyTransactionAsync("payment");
            _footerRenewButton.Click += async (_, _) => await OpenNewRenewalAsync();
            _footerCancelButton.Click += async (_, _) => await CancelPolicyAsync();
            quick.Children.Add(_footerReceiptButton);
            quick.Children.Add(_footerPaymentButton);
            quick.Children.Add(_footerRenewButton);
            quick.Children.Add(_footerCancelButton);
            toolbar.Children.Add(quick);
            _convertButton = new ActionButton { Kind = "Edit", Label = "Έκδοση και παραλαβή συμβολαίου" };
            _convertButton.Click += async (_, _) => await ConvertAsync();
            actions.Children.Add(_convertButton);
        }
        _editButton = new ActionButton { Kind = "Edit", Label = _section == "vehicles" ? "Επεξεργασία οχήματος" : "Επεξεργασία ασφαλιστηρίου" };
        _editButton.Click += (_, _) => { _editing = true; ApplyEditMode(); };
        actions.Children.Add(_editButton);
        _saveButton = new ActionButton { Kind = "Save", Label = "Αποθήκευση" };
        _saveButton.Click += async (_, _) => await SaveAsync();
        actions.Children.Add(_saveButton);
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        Grid.SetColumn(actions, 1);
        toolbar.Children.Add(actions);
        var footerScroll = new ScrollViewer { Content = toolbar,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        footerScroll.SizeChanged += (_, args) =>
            toolbar.Width = Math.Max(toolbar.MinWidth, args.NewSize.Width);
        Grid.SetRow(footerScroll, 1);
        footer.Children.Add(footerScroll);
        Grid.SetRow(footer, 3);
        _root.Children.Add(footer);
        if (_section == "policies") BuildPolicyTabs(customers, vehicles, transactions, companies);
        else BuildVehicleTabs(customers, policies);
        LayoutTabs(_contentWidth);
        SelectTab(0);
        UpdateConvertButton();
        ApplyEditMode();
    }

    private void ApplyEditMode()
    {
        _headerCode.SetEditing(_editing);
        if (_saveButton is not null) _saveButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        if (_editButton is not null) _editButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        foreach (var field in _text.Values) field.IsReadOnly = !_editing;
        foreach (var field in _dates.Values)
        {
            field.IsEnabled = true;
            field.IsHitTestVisible = _editing;
            field.IsTabStop = _editing;
        }
        foreach (var (key, field) in _vehicleChecks)
        {
            field.IsEnabled = _editing;
            field.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
            var badge = _vehicleCheckBadges[key];
            var checkedValue = field.IsChecked == true;
            badge.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
            badge.Background = new SolidColorBrush(checkedValue
                ? StatusPalette.Positive : StatusPalette.Neutral);
            if (badge.Child is TextBlock mark) mark.Text = checkedValue ? "✓" : "";
        }
        var ownerLockedByPolicy = _section == "vehicles" && _vehiclePolicies.Any(item =>
            item.DocumentType is null or "Ασφαλιστήριο" && item.Status == "Ενεργό");
        LockChoice(_customerChoice, _editing && _fixedCustomerId is null && !ownerLockedByPolicy);
        LockChoice(_vehicleChoice, _editing);
        LockChoice(_typeChoice, _editing);
        LockChoice(_statusChoice, _editing);
        LockChoice(_kteoStatusChoice, _editing);
        LockChoice(_policyPartnerChoice, _editing);
        LockChoice(_policyHolderChoice, _editing);
        LockChoice(_policyDurationChoice, _editing);
        LockChoice(_durationUnit, _editing);
        LockChoice(_preferredPaymentMethod, _editing);
        LockChoice(_paymentRecipient, _editing);
        if (_durationValue is not null) _durationValue.IsReadOnly = !_editing;
        if (_payInInstallments is not null)
        {
            _payInInstallments.IsEnabled = _editing;
            _payInInstallments.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
            if (_payInInstallmentsBadge is not null)
            {
                var checkedValue = _payInInstallments.IsChecked == true;
                _payInInstallmentsBadge.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
                _payInInstallmentsBadge.Background = new SolidColorBrush(checkedValue
                    ? StatusPalette.Positive : StatusPalette.Neutral);
                if (_payInInstallmentsBadge.Child is TextBlock mark)
                    mark.Text = checkedValue ? "✓" : "";
            }
        }
        LockChoice(_insurerSuggest, _editing);
        foreach (var search in _policySearchFields) LockChoice(search, _editing);
        if (_section == "vehicles" && _vehicleCustomerSearch is not null)
            LockChoice(_vehicleCustomerSearch, _editing && _fixedCustomerId is null && !ownerLockedByPolicy);
        if (_convertButton is not null) _convertButton.IsEnabled = true;
        foreach (var button in _editControls) button.IsEnabled = _editing;
    }

    private static void LockChoice(Control? control, bool editable)
    {
        if (control is null) return;
        control.IsEnabled = true;
        control.IsHitTestVisible = editable;
        control.IsTabStop = editable;
    }

    private Button OrganizationLinkButton(string section, Func<string> name,
        IReadOnlyList<BusinessRecord> organizations)
    {
        var button = LinkedCardButton(section == "companies" ?
            "Άνοιγμα ασφαλιστικής εταιρείας" : "Άνοιγμα πρακτορείου");
        ToolTipService.SetToolTip(button, section == "companies" ?
            "Άνοιγμα ασφαλιστικής εταιρείας" : "Άνοιγμα πρακτορείου");
        ButtonMotion.Attach(button);
        button.Click += async (_, _) =>
        {
            var record = organizations.FirstOrDefault(item =>
                string.Equals(item.Title, name().Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (record is null)
            {
                await UiAlerts.ShowErrorAsync(_root.XamlRoot,
                    "Δεν υπάρχει αντίστοιχη καταχωρημένη καρτέλα. Επιλέξτε εταιρεία ή πρακτορείο από τις καταχωρήσεις.");
                return;
            }
            if (_linkedOrganizationCard is not null) { _linkedOrganizationCard.BringToFront(); return; }
            _linkedOrganizationCard = new WorkspaceRecordCardWindow(section, record.Id,
                ownerHandle: WindowNative.GetWindowHandle(this));
            _linkedOrganizationCard.Closed += (_, _) =>
            { _linkedOrganizationCard = null; BringToFront(); };
            _linkedOrganizationCard.BringToFront();
        };
        return button;
    }

    private readonly List<Button> _editControls = [];

    private void AddTab(string title, UIElement content)
    {
        var index = _tabItems.Count;
        var icon = title switch
        {
            "Καρτέλα οχήματος" or "Όχημα" => "car-front",
            "Ιδιοκτήτης" or "Πρόσωπα" => "user-round",
            "Συμβόλαια" => "file-text",
            "Ζημιές" => "car-front",
            "Πρόσθετα στοιχεία" or "Λοιπά" => "settings-2",
            "Οικονομικά" => "chart-no-axes-combined",
            "Ιστορικό" => "clock-3",
            _ => "file-text"
        };
        var (button, label) = CardTabVisual.Create(title, icon, IsDarkTheme());
        button.Click += (_, _) => SelectTab(index);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.MinWidth = 0;
        _tabs.Children.Add(button);
        _tabItems.Add((button, label, new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }));
    }

    private void LayoutTabs(double availableWidth)
    {
        if (_tabItems.Count == 0 || availableWidth <= 0) return;
        var tabWidth = Math.Max(128, _tabItems.Max(item => item.Button.MinWidth));
        var columns = Math.Min(_tabItems.Count, Math.Max(1, (int)(availableWidth / tabWidth)));
        _tabs.ColumnDefinitions.Clear();
        _tabs.RowDefinitions.Clear();
        for (var index = 0; index < columns; index++)
            _tabs.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < (_tabItems.Count + columns - 1) / columns; index++)
            _tabs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < _tabItems.Count; index++)
        {
            Grid.SetRow(_tabItems[index].Button, index / columns);
            Grid.SetColumn(_tabItems[index].Button, index % columns);
        }
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabItems.Count) return;
        _tabContent.Content = _tabItems[index].Content;
        var dark = IsDarkTheme();
        for (var i = 0; i < _tabItems.Count; i++)
        {
            var selected = i == index;
            CardTabVisual.SetSelected(_tabItems[i].Button, _tabItems[i].Label, selected, dark);
        }
    }

    private bool IsDarkTheme() => _root.RequestedTheme == ElementTheme.Dark ||
        (_root.RequestedTheme == ElementTheme.Default &&
         Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);

    private Grid TwoColumns(out StackPanel left, out StackPanel right)
    {
        var grid = CardFieldLayout.TwoColumns(out left, out right, _contentWidth);
        if (_section == "policies")
        {
            left.Spacing = 5;
            right.Spacing = 5;
        }
        _formGrids.Add(grid);
        return grid;
    }

    private TextBox Text(StackPanel panel, string key, string label, string? value = null, bool multiline = false)
    {
        var field = new TextBox { Text = value ?? "", MinHeight = multiline ? 80 : 40,
            Height = multiline ? 150 : 40, AcceptsReturn = multiline,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(CardFieldLayout.CreateRow(label,
            multiline ? ResizableNoteField.Wrap(field) : field));
        _text[key] = field;
        return field;
    }

    private EditableDateInput Date(StackPanel panel, string key, string label, DateTime? value = null)
    {
        var field = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = value is DateTime date ? new DateTimeOffset(date) : null,
            MinHeight = 40, Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        _dates[key] = field;
        return field;
    }

    private ComboBox Choices(StackPanel panel, string label, IEnumerable<string> items, string? selected)
    {
        var field = new ComboBox { ItemsSource = items.ToArray(),
            MinHeight = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        field.SelectedItem = selected;
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        return field;
    }

    private ComboBox CustomerChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers)
    {
        var items = customers.Select(item => new Choice(item.Id, item.FullName)).ToArray();
        _customerChoice = new ComboBox { ItemsSource = items, DisplayMemberPath = nameof(Choice.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = _fixedCustomerId is null, MinHeight = 40, Height = 40 };
        _customerChoice.SelectedItem = items.FirstOrDefault(item => item.Id == _record.CustomerId);
        var search = SearchChoice(items, _customerChoice, "Αναζήτηση πελάτη ή κωδικού");
        _vehicleCustomerSearch = search;
        _customerChoice.SelectionChanged += (_, _) =>
            search.Text = (_customerChoice.SelectedItem as Choice)?.Name ?? "";
        _customerChoice.Visibility = Visibility.Collapsed;
        panel.Children.Add(_customerChoice);
        var host = new Grid { ColumnSpacing = 4, ColumnDefinitions =
        { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        host.Children.Add(search);
        var open = CustomerLinkButton();
        Grid.SetColumn(open, 1); host.Children.Add(open);
        panel.Children.Add(CardFieldLayout.CreateRow("Πελάτης / ιδιοκτήτης *", host));
        return _customerChoice;
    }

    private void BuildPolicyTabs(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> vehicles, IReadOnlyList<CustomerTransaction> transactions,
        IReadOnlyList<BusinessRecord> companies)
    {
        var data = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson);
        _policyData = data;
        NormalizePolicyLists();
        if (_record.DocumentType is null && _record.Id != Guid.Empty)
            data.PolicyNumber ??= _record.Reference ?? _record.Title;
        var main = TwoColumns(out var left, out var right);
        var availableTypes = new[] { "Προσφορά", "Αίτηση", "Ειδοποιητήριο", "Ασφαλιστήριο",
            "Πρόσθετη πράξη", "Πράσινη κάρτα" };
        _typeChoice = Choices(left, "Τύπος εγγράφου *", availableTypes,
            _record.DocumentType ?? "Αίτηση");
        Text(left, "offer", "Αριθμός προσφοράς", data.OfferNumber);
        Text(left, "application", "Αριθμός αίτησης", data.ApplicationNumber);
        Text(left, "notice", "Αριθμός ειδοποιητηρίου", data.NoticeNumber);
        Text(left, "policy", "Αριθμός ασφαλιστηρίου", data.PolicyNumber);
        Text(left, "renewalnumber", "Αριθμός ανανεωτηρίου", data.RenewalNumber);
        Text(left, "previouspolicy", "Προηγούμενο ασφαλιστήριο", data.PreviousPolicyNumber);
        void UpdateRequiredNumber()
        {
            var type = _typeChoice?.SelectedItem as string;
            CardFieldLayout.SetRequired(left, _text["offer"], "Αριθμός προσφοράς", type == "Προσφορά");
            CardFieldLayout.SetRequired(left, _text["application"], "Αριθμός αίτησης", type == "Αίτηση");
            CardFieldLayout.SetRequired(left, _text["notice"], "Αριθμός ειδοποιητηρίου", type == "Ειδοποιητήριο");
            CardFieldLayout.SetRequired(left, _text["policy"], "Αριθμός ασφαλιστηρίου", type == "Ασφαλιστήριο");
        }
        _typeChoice.SelectionChanged += (_, _) => UpdateRequiredNumber();
        UpdateRequiredNumber();
        var insurerNames = companies.Select(item => item.Title).Distinct().ToArray();
        _insurerSuggest = new AutoSuggestBox { Text = data.Insurer ?? "",
            PlaceholderText = "Αναζήτηση ασφαλιστικής εταιρείας", MinHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        void UpdateInsurerSuggestions(bool showAll = false)
        {
            _insurerSuggest.ItemsSource = SearchSuggestions.FirstHundred(insurerNames,
                showAll ? "" : _insurerSuggest.Text);
            _insurerSuggest.IsSuggestionListOpen =
                _insurerSuggest.ItemsSource is string[] { Length: > 0 };
        }
        _insurerSuggest.GotFocus += (_, _) => UpdateInsurerSuggestions(showAll: true);
        _insurerSuggest.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            UpdateInsurerSuggestions();
        };
        _insurerSuggest.SuggestionChosen += (_, args) =>
            _insurerSuggest.Text = args.SelectedItem as string ?? _insurerSuggest.Text;
        var insurerHost = new Grid { ColumnSpacing = 4,
            ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } } };
        insurerHost.Children.Add(_insurerSuggest);
        var openInsurer = OrganizationLinkButton("companies", () => _insurerSuggest.Text, companies);
        Grid.SetColumn(openInsurer, 1); insurerHost.Children.Add(openInsurer);
        left.Children.Add(CardFieldLayout.CreateRow("Ασφαλιστική εταιρεία", insurerHost));
        var agencyInput = SearchValue(left, "agency", "πρακτορείου", data.Agency,
            _agencyRecords.Select(item => item.Title));
        var agencyHost = new Grid { ColumnSpacing = 4,
            ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } } };
        agencyHost.Children.Add(agencyInput);
        var openAgency = OrganizationLinkButton("agencies", () => agencyInput.Text, _agencyRecords);
        Grid.SetColumn(openAgency, 1); agencyHost.Children.Add(openAgency);
        left.Children.Add(CardFieldLayout.CreateRow("Πρακτορείο", agencyHost));
        AddPolicyPartnerChoice(left, data);
        left.Children.Add(CardFieldLayout.CreateRow("Αντικείμενο ασφάλισης / κλάδος",
            SearchValue(left, "branch", "κλάδου", data.Branch, () =>
                CatalogChoices(_policyBranchOptions, _insurerSuggest.Text))));
        left.Children.Add(CardFieldLayout.CreateRow("Προϊόν / πακέτο",
            SearchValue(left, "product", "πακέτου", data.Product, () =>
                CatalogChoices(_policyPackageOptions, _insurerSuggest.Text,
                    _text.GetValueOrDefault("branch")?.Text))));
        PolicyCustomerChoice(right, customers);
        AddPolicyHolderChoice(right, customers, data);
        Choice[] VehicleItems(Guid? customerId) =>
            [new Choice(null, "Χωρίς όχημα"),
             .. vehicles.Where(item => item.CustomerId == customerId)
                 .Select(item => new Choice(item.Id, item.Title))];
        var vehicleItems = VehicleItems(_record.CustomerId);
        _vehicleChoice = new ComboBox { ItemsSource = vehicleItems,
            DisplayMemberPath = nameof(Choice.Name), MinHeight = 40, Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
        _vehicleChoice.SelectedItem = vehicleItems.FirstOrDefault(item => item.Id == data.VehicleId) ?? vehicleItems[0];
        var vehicleHost = new Grid { ColumnSpacing = 4 };
        vehicleHost.ColumnDefinitions.Add(new ColumnDefinition());
        vehicleHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        vehicleHost.Children.Add(_vehicleChoice);
        var vehicleSearch = SearchChoice(() =>
                (_vehicleChoice.ItemsSource as IEnumerable<Choice>)?.ToArray() ?? [],
            _vehicleChoice, "Αναζήτηση οχήματος του πελάτη");
        vehicleHost.Children.Add(vehicleSearch);
        _vehicleChoice.SelectionChanged += (_, _) =>
        {
            var name = (_vehicleChoice.SelectedItem as Choice)?.Name ?? "";
            if (vehicleSearch.Text != name) vehicleSearch.Text = name;
        };
        var openVehicle = new ActionButton { Kind = "Open", Label = "Άνοιγμα οχήματος",
            IconOnly = true, Width = 38, Height = 38 };
        openVehicle.Click += async (_, _) =>
        {
            if ((_vehicleChoice.SelectedItem as Choice)?.Id is Guid selectedVehicleId)
                await OpenRelatedVehicleAsync(selectedVehicleId);
        };
        Grid.SetColumn(openVehicle, 1);
        vehicleHost.Children.Add(openVehicle);
        right.Children.Add(CardFieldLayout.CreateRow("Συνδεδεμένο όχημα", vehicleHost));
        _customerChoice!.SelectionChanged += (_, _) =>
        {
            var selectedVehicle = (_vehicleChoice.SelectedItem as Choice)?.Id;
            var available = VehicleItems((_customerChoice.SelectedItem as Choice)?.Id);
            _vehicleChoice.ItemsSource = available;
            _vehicleChoice.SelectedItem = available.FirstOrDefault(item => item.Id == selectedVehicle) ?? available[0];
        };
        _statusChoice = Choices(right, "Κατάσταση", ["Σε εξέλιξη", "Ενεργό", "Πιθανό", "Ληγμένο",
            "Ακυρωμένο", "Ανανεωμένο", "Πρόσθετη πράξη", "Πρόσθετη πράξη ακύρωσης", "Ανενεργό", "Ολοκληρωμένο"],
            _record.Status ?? (_record.Id == Guid.Empty ? "Σε εξέλιξη" : "Ενεργό"));
        void UpdateStatuses()
        {
            var previous = _statusChoice.SelectedItem as string;
            string[] options = (_typeChoice?.SelectedItem as string) switch
            {
                "Προσφορά" => ["Πιθανό", "Σε αναμονή", "Αποδεκτή", "Απορρίφθηκε", "Ακυρωμένο"],
                "Αίτηση" => ["Σε εξέλιξη", "Προς έκδοση", "Εκδόθηκε", "Ακυρωμένο"],
                "Ειδοποιητήριο" => ["Σε αναμονή πληρωμής", "Πληρωμένο", "Ακυρωμένο"],
                "Πράσινη κάρτα" => ["Σε εξέλιξη", "Εκδόθηκε", "Παραδόθηκε", "Ακυρωμένο"],
                "Πρόσθετη πράξη" => ["Σε εξέλιξη", "Εκδόθηκε", "Παραδόθηκε", "Ακυρωμένο"],
                _ => ["Ενεργό", "Ανανεωμένο", "Ληγμένο", "Ακυρωμένο", "Ανενεργό"]
            };
            if (_record.Id != Guid.Empty && !string.IsNullOrWhiteSpace(previous) &&
                !options.Contains(previous)) options = [.. options, previous];
            _statusChoice.ItemsSource = options;
            _statusChoice.SelectedItem = options.Contains(previous) ? previous : options[0];
        }
        _typeChoice.SelectionChanged += (_, _) => UpdateStatuses();
        UpdateStatuses();
        Date(right, "issue", "Ημερομηνία έκδοσης", data.IssueDate);
        Date(right, "start", "Έναρξη κάλυψης", data.StartDate);
        AddPolicyDurationChoice(right, data);
        Date(right, "end", "Λήξη κάλυψης", data.EndDate);
        Date(right, "companydue", "Πληρωμή προς ασφαλιστική έως", data.CompanyPaymentDueDate);
        DateTimeOffset? calculatedDue = null;
        void ApplyCompanyPaymentTerms()
        {
            var company = companies.FirstOrDefault(item => string.Equals(item.Title,
                _insurerSuggest?.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
            var creditDaysText = company is null ? null :
                WorkspaceCardData.Read(company.DetailsJson).Fields.GetValueOrDefault("creditDays");
            if (!int.TryParse(creditDaysText, out var creditDays) || creditDays < 0 || creditDays > 3650 ||
                _dates["issue"].Date is not DateTimeOffset issue) return;
            var dueField = _dates["companydue"];
            if (dueField.Date is not null && dueField.Date != calculatedDue) return;
            calculatedDue = new DateTimeOffset(issue.Date.AddDays(creditDays));
            dueField.Date = calculatedDue;
        }
        _dates["issue"].DateChanged += (_, _) => ApplyCompanyPaymentTerms();
        _insurerSuggest.TextChanged += (_, _) => ApplyCompanyPaymentTerms();
        ApplyCompanyPaymentTerms();
        var dueText = new TextBlock { Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var dueBadge = new Border { Child = dueText, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 5, 9, 5) };
        void UpdateDueCountdown()
        {
            if (_dates["companydue"].Date is not DateTimeOffset due)
            {
                dueText.Text = "Δεν έχει οριστεί προθεσμία απόδοσης";
                dueBadge.Background = new SolidColorBrush(Color.FromArgb(255, 101, 111, 123));
                return;
            }
            var days = (due.Date - DateTime.Today).Days;
            dueText.Text = days < 0 ? $"Εκπρόθεσμο κατά {-days} ημέρες" :
                days == 0 ? "Η απόδοση λήγει σήμερα" : $"Απόδοση σε {days} ημέρες";
            dueBadge.Background = StatusPalette.Brush(days < 0
                ? StatusPalette.Negative
                : days <= 30 ? StatusPalette.Caution
                : StatusPalette.Positive);
        }
        _dates["companydue"].DateChanged += (_, _) => UpdateDueCountdown();
        UpdateDueCountdown();
        right.Children.Add(CardFieldLayout.CreateRow("Προθεσμία απόδοσης", dueBadge));
        _preferredPaymentMethod = Choices(right, "Συνήθης τρόπος πληρωμής",
            ["", "Μετρητά", "Κάρτα", "Τραπεζική μεταφορά", "POS", "Πάγια εντολή", "Ταχυπληρωμή"],
            data.PreferredPaymentMethod);
        _paymentRecipient = Choices(right, "Πού πληρώνει ο πελάτης",
            ["Στο γραφείο", "Στην ασφαλιστική", "Στο πρακτορείο"],
            data.PaymentRecipient ?? "Στο γραφείο");
        Text(right, "paymentcode", "Κωδικός ταχυπληρωμής / πληρωμής", data.PaymentCode);
        Text(right, "net", "Καθαρά ασφάλιστρα", data.NetPremium?.ToString("N2"));
        Text(right, "gross", "Μικτά ασφάλιστρα", data.GrossPremium?.ToString("N2"));
        AddPolicyBalanceBadge(right, data);
        AddTab("Καρτέλα ασφάλισης", main);
        BuildPolicyExtendedTabs(customers, vehicles, data);
    }

    private void BuildVehicleTabs(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> policies)
    {
        var data = BusinessRecordDetails.Read<VehicleCardData>(_record.DetailsJson);
        _vehiclePeople.Clear();
        _vehiclePeople.AddRange(data.People ?? []);
        if (_vehiclePeople.Count == 0 && _record.CustomerId is Guid ownerId)
            _vehiclePeople.Add(new VehiclePerson { CustomerId = ownerId, Role = "Ιδιοκτήτης",
                OwnershipPercent = 100, IsPrimaryOwner = true });
        if (_record.Id != Guid.Empty)
        {
            data.Registration ??= _record.Title;
            if (data.Make is null && data.Model is null) data.Make = _record.Reference;
        }
        var main = TwoColumns(out var left, out var right);
        left.Spacing = 5;
        right.Spacing = 5;
        var extra = TwoColumns(out var extraLeft, out var extraRight);
        extraLeft.Spacing = 5;
        extraRight.Spacing = 5;
        CustomerChoice(left, customers);
        var registration = Text(left, "registration", "Αριθμός κυκλοφορίας *", data.Registration);
        var plateWarningOpen = false;
        registration.BeforeTextChanging += (_, args) =>
        {
            if (args.NewText.Count(char.IsAsciiLetter) <= registration.Text.Count(char.IsAsciiLetter)) return;
            args.Cancel = true;
            if (plateWarningOpen) return;
            plateWarningOpen = true;
            DispatcherQueue.TryEnqueue(async () =>
            {
                try { await UiAlerts.ShowErrorAsync(_root.XamlRoot,
                    "Προσοχή, δεν επιτρέπονται αγγλικοί χαρακτήρες στην πινακίδα του οχήματος."); }
                finally { plateWarningOpen = false; }
            });
        };
        Text(left, "make", "Μάρκα / εργοστάσιο", data.Make);
        Text(left, "model", "Μοντέλο", data.Model);
        Text(left, "vin", "Αριθμός πλαισίου (VIN)", data.Vin);
        Text(left, "year", "Έτος κατασκευής", data.ManufactureYear?.ToString());
        Date(left, "first", "Πρώτη άδεια κυκλοφορίας", data.FirstRegistrationDate);
        Date(extraLeft, "transfer", "Ημερομηνία μεταβίβασης", data.TransferDate);
        Text(extraLeft, "taxclass", "Κατηγορία φορολόγησης", data.TaxClass);
        Text(left, "seats", "Θέσεις", data.Seats?.ToString());
        Text(left, "doors", "Πόρτες", data.Doors?.ToString());
        Text(extraLeft, "fuelcolor", "Χρώμα καυσίμου", data.FuelColor);
        Text(extraLeft, "permit", "Άδεια κυκλοφορίας", data.RegistrationPermit);
        Text(extraLeft, "licence", "Ειδικό δίπλωμα", data.SpecialLicence);
        Text(extraLeft, "discount", "Δικαιολογητικά έκπτωσης", data.DiscountDocuments);
        Text(extraLeft, "roadtax", "Τέλη κυκλοφορίας (€)", data.RoadTax?.ToString("N2"));
        Text(extraLeft, "reports", "Έντυπα αναγγελίας", data.ReportForms);
        VehicleCheck(right, "convertible", "Κάμπριο", data.IsConvertible);
        VehicleCheck(right, "sunroof", "Ηλιοροφή", data.HasSunroof);
        VehicleCheck(right, "heavy", "Βάρος άνω των 3,5 τόνων", data.OverThreeAndHalfTons);
        VehicleCheck(extraLeft, "dual", "Διπλοί πίσω τροχοί", data.HasRearDualWheels);
        right.Children.Add(CardFieldLayout.CreateRow("Χρήση",
            SearchValue(right, "use", "χρήσης οχήματος", data.Use, _vehicleUseOptions)));
        Text(right, "fuel", "Καύσιμο", data.Fuel);
        Text(right, "hp", "Ισχύς (HP)", data.HorsePower?.ToString("N2"));
        Text(right, "kw", "Ισχύς (kW)", data.PowerKw?.ToString("N2"));
        Text(right, "cc", "Κυβισμός (cc)", data.DisplacementCc?.ToString());
        Date(right, "acquired", "Ημερομηνία απόκτησης", data.AcquisitionDate);
        Text(right, "color", "Χρώμα", data.Color);
        Text(extraRight, "tonnage", "Τονάζ (τόνοι)", data.Tonnage?.ToString("N2"));
        Text(extraRight, "wheelbase", "Μεταξόνιο (cm)", data.WheelbaseCm?.ToString("N2"));
        Text(extraRight, "height", "Ύψος (cm)", data.HeightCm?.ToString("N2"));
        Text(extraRight, "track", "Μετατρόχιο (cm)", data.TrackWidthCm?.ToString("N2"));
        Text(right, "market", "Εκτιμώμενη αξία (€)", data.EstimatedMarketValue?.ToString("N2"));
        Text(extraRight, "co2", "Εκπομπές CO₂ (g/km)", data.Co2GramsPerKm?.ToString("N2"));
        Date(extraRight, "nextkteo", "Επόμενο ΚΤΕΟ", data.NextKteoDate);
        _kteoStatusChoice = Choices(extraRight, "Κατάσταση ΚΤΕΟ",
            ["Δεν απαιτείται", "Απαιτείται", "Σε ισχύ", "Ληγμένο"],
            data.KteoStatus ?? (data.NextKteoDate is null ? "Δεν απαιτείται" :
                data.NextKteoDate >= DateTime.Today ? "Σε ισχύ" : "Ληγμένο"));
        VehicleCheck(extraRight, "alarm", "Συναγερμός", data.HasAlarm);
        VehicleCheck(extraRight, "immobilizer", "Immobilizer", data.HasImmobilizer);
        VehicleCheck(extraRight, "guarded", "Χώρος φύλαξης", data.IsGuarded);
        VehicleCheck(extraRight, "emissions", "Κάρτα καυσαερίων", data.HasEmissionsCard);
        _statusChoice = Choices(extraRight, "Κατάσταση", ["Ενεργό", "Ανενεργό"],
            _record.Status ?? "Ενεργό");
        extraRight.Children.Add(CardFieldLayout.CreateRow("Καταχώριση",
            new TextBox { IsReadOnly = true, Text = _record.Id == Guid.Empty ? "—" :
                _record.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") }));
        extraRight.Children.Add(CardFieldLayout.CreateRow("Τελευταία ενημέρωση",
            new TextBox { IsReadOnly = true, Text = _record.Id == Guid.Empty ? "—" :
                _record.UpdatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") }));
        AddKteoImageRow(extraRight, data.KteoImagePath);
        Text(extraRight, "notes", "Πρόσθετα στοιχεία", data.Notes ?? _record.Notes, multiline: true);
        AddTab("Καρτέλα οχήματος", main);
        AddTab("Λοιπά", extra);
        BuildPeopleTab();
        BuildVehiclePoliciesTab();
        BuildVehicleClaimsTab();
    }

    private string Value(string key) => _text[key].Text.Trim();
    private DateTime? DateValue(string key) => _dates[key].GetValidatedDate(key);
    private static decimal? DecimalValue(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var value)
            && value >= 0) return value;
        throw new ArgumentException($"Το πεδίο «{label}» πρέπει να έχει έγκυρο μη αρνητικό ποσό.");
    }
    private static int? IntValue(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text, out var value) && value >= 0) return value;
        throw new ArgumentException($"Το πεδίο «{label}» πρέπει να έχει έγκυρο αριθμό.");
    }

    private async Task<bool> SaveAsync()
    {
        try
        {
            if (_customerChoice?.SelectedItem is not Choice { Id: Guid customerId })
                throw new ArgumentException("Επιλέξτε πελάτη / ιδιοκτήτη.");
            _record.CustomerId = customerId;
            _record.Status = _statusChoice?.SelectedItem as string;
            if (_section == "policies")
            {
                var type = _typeChoice?.SelectedItem as string ?? "Αίτηση";
                if (_record.Id != Guid.Empty && _record.DocumentType is ("Αίτηση" or "Ειδοποιητήριο") &&
                    type == "Ασφαλιστήριο" && !_conversionRequested)
                    throw new InvalidOperationException("Χρησιμοποιήστε «Έκδοση και παραλαβή συμβολαίου».");
                var data = new PolicyCardData
                {
                    CardCode = _policyData.CardCode is null &&
                        string.Equals(_headerCode.EnteredCode, _record.Reference, StringComparison.Ordinal)
                            ? null : _headerCode.EnteredCode,
                    PartnerId = (_policyPartnerChoice?.SelectedItem as Choice)?.Id,
                    PartnerCommissionPercent = _policyData.PartnerId == (_policyPartnerChoice?.SelectedItem as Choice)?.Id
                        ? _policyData.PartnerCommissionPercent : null,
                    OfficeCommissionPercent = _policyData.OfficeCommissionPercent,
                    CommissionRuleId = _policyData.CommissionRuleId,
                    OfferNumber = Value("offer"), ApplicationNumber = Value("application"), NoticeNumber = Value("notice"),
                    PolicyNumber = Value("policy"), RenewalNumber = Value("renewalnumber"),
                    PreviousPolicyId = _policyData.PreviousPolicyId,
                    PreviousPolicyNumber = Value("previouspolicy"),
                    HolderCustomerId = (_policyHolderChoice?.SelectedItem as Choice)?.Id,
                    Insurer = _insurerSuggest?.Text.Trim(),
                    Agency = Value("agency"), Branch = Value("branch"), Product = Value("product"),
                    VehicleId = (_vehicleChoice?.SelectedItem as Choice)?.Id,
                    IssueDate = DateValue("issue"), StartDate = DateValue("start"), EndDate = DateValue("end"),
                    RenewalDate = DateValue("renewal"), CompanyRenewalDate = DateValue("companyrenewal"),
                    CoverageDurationMonths = _durationUnit?.SelectedItem as string == "Μήνες" &&
                        int.TryParse(_durationValue?.Text, out var months) ? months : null,
                    CoverageDurationValue = int.TryParse(_durationValue?.Text, out var durationValue)
                        ? durationValue : null,
                    CoverageDurationUnit = _durationUnit?.SelectedItem as string,
                    PreferredPaymentMethod = _preferredPaymentMethod?.SelectedItem as string,
                    PaymentRecipient = _paymentRecipient?.SelectedItem as string,
                    PaymentCode = Value("paymentcode"),
                    CompanyPaymentDueDate = DateValue("companydue"),
                    CustomerReceivedAt = _policyData.CustomerReceivedAt,
                    CancellationDate = DateValue("canceldate"),
                    CancellationReason = Value("cancelreason"),
                    GrossPremium = DecimalValue(Value("gross"), "Μικτά ασφάλιστρα"),
                    NetPremium = DecimalValue(Value("net"), "Καθαρά ασφάλιστρα"),
                    InsuredValue = DecimalValue(Value("insuredvalue"), "Ασφαλιστική αξία"),
                    DiscountPercent = DecimalValue(Value("discountpercent"), "Έκπτωση (%)"),
                    DiscountAmount = DecimalValue(Value("discountamount"), "Έκπτωση (€)"),
                    DiscountReason = Value("discountreason"),
                    PreviousInsurer = Value("previousinsurer"), RenewalNotes = Value("renewalnotes"),
                    PayInInstallments = _payInInstallments?.IsChecked == true,
                    CoverageNotes = _policyData.CoverageNotes,
                    Coverages = _policyData.Coverages,
                    InsuredObjects = _policyData.InsuredObjects,
                    Endorsements = _policyData.Endorsements,
                    GreenCards = _policyData.GreenCards,
                    Installments = _policyData.Installments
                };
                var insurerChanged = !string.Equals(data.Insurer, _policyData.Insurer,
                    StringComparison.CurrentCultureIgnoreCase);
                var branchChanged = !string.Equals(data.Branch, _policyData.Branch,
                    StringComparison.CurrentCultureIgnoreCase);
                if (!string.IsNullOrWhiteSpace(data.Branch) && (insurerChanged || branchChanged) &&
                    !CatalogChoices(_policyBranchOptions, data.Insurer).Contains(data.Branch,
                        StringComparer.CurrentCultureIgnoreCase))
                    throw new ArgumentException("Επιλέξτε κλάδο που ισχύει για την ασφαλιστική εταιρεία.");
                if (!string.IsNullOrWhiteSpace(data.Product) &&
                    (insurerChanged || branchChanged || !string.Equals(data.Product, _policyData.Product,
                        StringComparison.CurrentCultureIgnoreCase)) &&
                    !CatalogChoices(_policyPackageOptions, data.Insurer, data.Branch).Contains(data.Product,
                        StringComparer.CurrentCultureIgnoreCase))
                    throw new ArgumentException("Επιλέξτε πακέτο που ισχύει για την εταιρεία και τον κλάδο.");
                if (data.StartDate > data.EndDate)
                    throw new ArgumentException("Η λήξη κάλυψης πρέπει να είναι μετά την έναρξη.");
                if (!string.IsNullOrWhiteSpace(_durationValue?.Text) &&
                    data.CoverageDurationValue is null)
                    throw new ArgumentException("Συμπληρώστε θετικό ακέραιο αριθμό στη διάρκεια.");
                if (data.CoverageDurationValue is int duration &&
                    (duration < 1 || duration > (data.CoverageDurationUnit == "Έτη" ? 10 :
                        data.CoverageDurationUnit == "Μήνες" ? 120 : 3650)))
                    throw new ArgumentException("Η διάρκεια επιτρέπεται έως 10 έτη, 120 μήνες ή 3650 ημέρες.");
                if (type == "Ασφαλιστήριο" && data.RenewalDate is null)
                    throw new ArgumentException("Συμπληρώστε ημερομηνία ανανέωσης.");
                if (data.DiscountPercent is > 100)
                    throw new ArgumentException("Η έκπτωση δεν μπορεί να ξεπερνά το 100%.");
                var number = type switch
                {
                    "Προσφορά" => data.OfferNumber,
                    "Αίτηση" => data.ApplicationNumber,
                    "Ειδοποιητήριο" => data.NoticeNumber,
                    "Ασφαλιστήριο" => data.PolicyNumber,
                    _ => data.RenewalNumber ?? data.PolicyNumber
                };
                if (string.IsNullOrWhiteSpace(number))
                    throw new ArgumentException($"Συμπληρώστε αριθμό για τον τύπο «{type}».");
                _record.Title = number;
                _record.Reference = number;
                _record.DocumentType = type;
                _record.RelatedParty = data.Insurer;
                _record.EventDate = data.StartDate;
                _record.Notes = Value("notes");
                _record.DetailsJson = BusinessRecordDetails.Write(data);
            }
            else
            {
                var plate = Value("registration");
                if (plate.Length == 0) throw new ArgumentException("Συμπληρώστε αριθμό κυκλοφορίας.");
                if (plate.Any(char.IsAsciiLetter))
                    throw new ArgumentException("Προσοχή, δεν επιτρέπονται αγγλικοί χαρακτήρες στην πινακίδα του οχήματος.");
                EnsurePrimaryOwner(customerId);
                ValidateVehiclePeople();
                var selectedUse = Value("use");
                var originalUse = BusinessRecordDetails.Read<VehicleCardData>(_record.DetailsJson).Use;
                if (!string.IsNullOrWhiteSpace(selectedUse) &&
                    !string.Equals(selectedUse, originalUse, StringComparison.CurrentCultureIgnoreCase) &&
                    !_vehicleUseOptions.Contains(selectedUse, StringComparer.CurrentCultureIgnoreCase))
                    throw new ArgumentException("Επιλέξτε καταχωρημένη χρήση οχήματος από τα δεδομένα συστήματος.");
                var data = new VehicleCardData
                {
                    CardCode = BusinessRecordDetails.Read<VehicleCardData>(_record.DetailsJson).CardCode is null &&
                        string.Equals(_headerCode.EnteredCode, _record.Title, StringComparison.Ordinal)
                            ? null : _headerCode.EnteredCode,
                    Registration = plate, Make = Value("make"), Model = Value("model"),
                    Vin = Value("vin"), ManufactureYear = IntValue(Value("year"), "Έτος κατασκευής"),
                    FirstRegistrationDate = DateValue("first"), Use = Value("use"),
                    Fuel = Value("fuel"), DisplacementCc = IntValue(Value("cc"), "Κυβισμός"),
                    PowerKw = DecimalValue(Value("kw"), "Ισχύς"),
                    AcquisitionDate = DateValue("acquired"), Color = Value("color"),
                    TaxClass = Value("taxclass"), HorsePower = DecimalValue(Value("hp"), "Ισχύς"),
                    TransferDate = DateValue("transfer"), Seats = IntValue(Value("seats"), "Θέσεις"),
                    Doors = IntValue(Value("doors"), "Πόρτες"), FuelColor = Value("fuelcolor"),
                    RegistrationPermit = Value("permit"), SpecialLicence = Value("licence"),
                    DiscountDocuments = Value("discount"), RoadTax = DecimalValue(Value("roadtax"), "Τέλη"),
                    ReportForms = Value("reports"), IsConvertible = VehicleChecked("convertible"),
                    HasSunroof = VehicleChecked("sunroof"),
                    OverThreeAndHalfTons = VehicleChecked("heavy"),
                    HasRearDualWheels = VehicleChecked("dual"),
                    Tonnage = DecimalValue(Value("tonnage"), "Τονάζ"),
                    WheelbaseCm = DecimalValue(Value("wheelbase"), "Μεταξόνιο"),
                    HeightCm = DecimalValue(Value("height"), "Ύψος"),
                    TrackWidthCm = DecimalValue(Value("track"), "Μετατρόχιο"),
                    EstimatedMarketValue = DecimalValue(Value("market"), "Εκτιμώμενη αξία"),
                    Co2GramsPerKm = DecimalValue(Value("co2"), "Εκπομπές CO₂"),
                    NextKteoDate = DateValue("nextkteo"), HasAlarm = VehicleChecked("alarm"),
                    HasImmobilizer = VehicleChecked("immobilizer"), IsGuarded = VehicleChecked("guarded"),
                    HasEmissionsCard = VehicleChecked("emissions"), KteoImagePath = _kteoImagePath,
                    KteoStatus = _kteoStatusChoice?.SelectedItem as string,
                    People = _vehiclePeople.Select(person => new VehiclePerson
                    { CustomerId = person.CustomerId, Role = person.Role,
                        OwnershipPercent = person.OwnershipPercent,
                        IsPrimaryOwner = person.IsPrimaryOwner }).ToList(),
                    Notes = Value("notes")
                };
                _record.Title = plate;
                _record.Reference = string.Join(" ", new[] { data.Make, data.Model }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                _record.RelatedParty = _record.Reference;
                _record.EventDate = data.AcquisitionDate;
                _record.Notes = data.Notes;
                _record.DetailsJson = BusinessRecordDetails.Write(data);
            }
            var id = await _records.SaveAsync(_record);
            _record = await _records.GetAsync(id) ?? _record;
            if (_section == "policies")
            {
                _policyData = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson);
                UpdatePolicyFinanceSummary(_policyData);
                UpdatePolicyTransactionButtons();
                await RefreshPolicyAttachmentsAsync();
                if (_policyClaimsPager is not null)
                    await RefreshPolicyClaimsAsync(_policyClaimsPager);
            }
            else
            {
                if (_newVehiclePolicyButton is not null) _newVehiclePolicyButton.IsEnabled = true;
                if (_addVehicleClaimButton is not null) _addVehicleClaimButton.IsEnabled = true;
            }
            if (_section == "policies" && _typeChoice is not null &&
            _record.DocumentType is "Προσφορά" or "Αίτηση" or "Ειδοποιητήριο")
            {
                var selected = _record.DocumentType;
                _typeChoice.ItemsSource = new[] { "Προσφορά", "Αίτηση", "Ειδοποιητήριο" };
                _typeChoice.SelectedItem = selected;
            }
            if (_historyText is not null)
                _historyText.Text = $"Δημιουργία: {_record.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                    $"Τελευταία ενημέρωση: {_record.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
            _heading.Text = _section == "policies"
                ? $"✓ {(_policyData.PreviousPolicyId.HasValue ? "Ανανεωτήριο · " : "")}{_record.DocumentType} · {_record.Title}"
                : _record.Title;
            _headerState.Text = $"● {_record.Status ?? "—"}";
            UpdatePolicyHeaderBadge();
            _headerCode.SetCode(_section == "policies" ? _policyData.CardCode ?? _record.Reference :
                BusinessRecordDetails.Read<VehicleCardData>(_record.DetailsJson).CardCode ?? _record.Title);
            var wasConversion = _conversionRequested;
            _conversionRequested = false;
            _editing = false;
            UpdateConvertButton();
            ApplyEditMode();
            RecordSaved?.Invoke(this, EventArgs.Empty);
            if (_section == "policies" && _policyHistoryPager is not null)
                await RefreshPolicyHistoryAsync(_policyHistoryPager);
            _status.Text = "Η καρτέλα αποθηκεύτηκε.";
            await UiAlerts.ShowSuccessAsync(_root.XamlRoot,
                wasConversion ? "Το ασφαλιστήριο εκδόθηκε και παραδόθηκε στον πελάτη." :
                    "Η εργασία σας αποθηκεύτηκε επιτυχώς.");
            return true;
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message);
            return false;
        }
    }

    private void UpdateConvertButton()
    {
        if (_footerRenewButton is not null)
            _footerRenewButton.IsEnabled = _record.Id != Guid.Empty &&
                (_record.DocumentType is null or "Ασφαλιστήριο") &&
                _record.Status != "Ακυρωμένο";
        if (_footerCancelButton is not null)
            _footerCancelButton.IsEnabled = _record.Id != Guid.Empty &&
                (_record.DocumentType is null or "Ασφαλιστήριο") &&
                _record.Status != "Ακυρωμένο";
        if (_convertButton is null) return;
        _convertButton.Visibility = _record.Id != Guid.Empty &&
            _record.DocumentType is "Προσφορά" or "Αίτηση" or "Ειδοποιητήριο"
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePolicyHeaderBadge()
    {
        if (_section != "policies" || _headerStateBadge is null) return;
        var data = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson);
        _headerStateBadge.Background = PolicyStatusColors.Background(_record.DocumentType,
            _record.Status, data.EndDate);
    }

    private void UpdatePolicyFinanceSummary(PolicyCardData data)
    {
        if (_policyFinanceSummary is null || _policyCommissionSummary is null) return;
        var symbol = AllInsurance.Application.Settings.TransactionSettingsService.Symbol(_policyCurrency);
        var gross = data.GrossPremium ?? 0;
        var net = data.NetPremium ?? 0;
        var receipts = _policyTransactions.Where(item => item.Type == "receipt").Sum(item => item.Amount);
        var payments = _policyTransactions.Where(item => item.Type == "payment").Sum(item => item.Amount);
        _policyFinanceSummary.Text = $"Καθαρά: {data.NetPremium?.ToString("N2") ?? "—"} {symbol}  ·  " +
            $"Μικτά: {data.GrossPremium?.ToString("N2") ?? "—"} {symbol}  ·  " +
            $"Εισπράχθηκε: {receipts:N2} {symbol}  ·  " +
            $"Πληρωμές προς πελάτη: {payments:N2} {symbol}";
        var office = data.OfficeCommissionPercent is decimal officeRate
            ? $"Εισερχόμενη προμήθεια έδρας: {officeRate:N2}% " +
              $"({decimal.Round(net * officeRate / 100m, 2, MidpointRounding.AwayFromZero):N2} {symbol})"
            : "Εισερχόμενη προμήθεια έδρας: χωρίς κανόνα";
        var partner = data.PartnerId is null ? "Χωρίς συνεργάτη" :
            data.PartnerCommissionPercent is decimal partnerRate
                ? $"Εξερχόμενη προμήθεια συνεργάτη: {partnerRate:N2}% " +
                  $"({decimal.Round(net * partnerRate / 100m, 2, MidpointRounding.AwayFromZero):N2} {symbol})"
                : "Εξερχόμενη προμήθεια συνεργάτη: χωρίς κανόνα";
        _policyCommissionSummary.Text = office + "  ·  " + partner +
            (data.OfficeCommissionPercent is decimal rate
                ? $"  ·  Εκτιμώμενη απόδοση στην εταιρεία: " +
                  $"{(gross - decimal.Round(net * rate / 100m, 2, MidpointRounding.AwayFromZero)):N2} {symbol}"
                : "");
        UpdatePolicyBalance(data);
    }

    private async Task ConvertAsync()
    {
        var number = new TextBox { Header = "Αριθμός ασφαλιστηρίου *",
            PlaceholderText = "Συμπληρώστε τον αριθμό που εκδόθηκε" };
        var renewal = new EditableDateInput { Date = _dates.GetValueOrDefault("renewal")?.Date ??
            (_dates.GetValueOrDefault("end")?.Date), MinHeight = 40 };
        var form = new StackPanel { Spacing = 10, Width = 400 };
        form.Children.Add(number);
        form.Children.Add(new TextBlock { Text = "Ημερομηνία ανανέωσης *" });
        form.Children.Add(renewal);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = "Έκδοση και παραλαβή συμβολαίου", Content = form,
            PrimaryButtonText = "Έκδοση και παραλαβή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(number.Text))
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Συμπληρώστε αριθμό ασφαλιστηρίου.");
            return;
        }
        if (renewal.Date is null)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Συμπληρώστε ημερομηνία ανανέωσης.");
            return;
        }
        var oldNumber = _text["policy"].Text;
        var oldRenewal = _dates["renewal"].Date;
        var oldType = _typeChoice?.SelectedItem as string;
        var oldStatus = _statusChoice?.SelectedItem as string;
        var oldReceivedAt = _policyData.CustomerReceivedAt;
        _text["policy"].Text = number.Text.Trim();
        _dates["renewal"].Date = renewal.Date;
        _typeChoice!.ItemsSource = new[] { "Αίτηση", "Ειδοποιητήριο", "Ασφαλιστήριο" };
        _typeChoice.SelectedItem = "Ασφαλιστήριο";
        _statusChoice!.SelectedItem = "Ενεργό";
        _policyData.CustomerReceivedAt = DateTime.UtcNow;
        _conversionRequested = true;
        if (await SaveAsync()) return;
        _conversionRequested = false;
        _text["policy"].Text = oldNumber;
        _dates["renewal"].Date = oldRenewal;
        _typeChoice.ItemsSource = new[] { "Προσφορά", "Αίτηση", "Ειδοποιητήριο" };
        _typeChoice.SelectedItem = oldType;
        _statusChoice.SelectedItem = oldStatus;
        _policyData.CustomerReceivedAt = oldReceivedAt;
    }
}

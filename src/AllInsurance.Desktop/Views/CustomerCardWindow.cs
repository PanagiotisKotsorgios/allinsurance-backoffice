using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.Graphics;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomerCardWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);

    public void BringToFront()
    {
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "customer-card");
        var window = WindowNative.GetWindowHandle(this);
        SetWindowOwner(window, -8, App.MainWindowHandle()); // GWLP_HWNDPARENT
        Activate();
        ShowWindow(window, 9); // SW_RESTORE
        const uint flags = 0x0001 | 0x0002 | 0x0040; // SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW
        SetWindowPos(window, new nint(-1), 0, 0, 0, 0, flags); // HWND_TOPMOST
        SetWindowPos(window, new nint(-2), 0, 0, 0, 0, flags); // HWND_NOTOPMOST
        SetForegroundWindow(window);
    }
    private readonly ICustomerService _service = App.Services.GetRequiredService<ICustomerService>();
    private readonly IFilterOptionService _filterService = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly ITransactionSettingsService _transactionSettings =
        App.Services.GetRequiredService<ITransactionSettingsService>();
    private sealed record PolicyChoice(Guid? Id, string Name);
    private string _currency = "EUR";
    private string CurrencySymbol => TransactionSettingsService.Symbol(_currency);
    private readonly Guid? _customerId;
    private readonly Grid _root = new() { RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
    private readonly Grid _tabs = new() { RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto }, new RowDefinition()
    } };
    private readonly Grid _tabStrip = new() { RowSpacing = 2, ColumnSpacing = 2 };
    private readonly ContentControl _tabContent = new();
    private readonly List<Button> _tabButtons = [];
    private readonly List<TextBlock> _tabLabels = [];
    private readonly List<UIElement> _tabContents = [];
    private int _selectedTabIndex;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<Action> _readFields = [];
    private Customer _customer = new() { LastName = "", FirstName = "" };
    private ComboBox? _referralChoice;
    private ComboBox? _referralPartnerChoice;
    private ComboBox? _partnerChoice;
    private readonly Dictionary<string, ListView> _detailLists = [];
    private BusinessRecordCardWindow? _recordCard;
    private Grid? _compactMain;
    private double _contentWidth = 1100;
    private bool _editing;
    private readonly TextBlock _headerTitle = new() { FontSize = 22,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White),
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _headerCode = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBox _codeInput = new() { Width = 170, Height = 34, MinHeight = 34,
        MaxLength = 30, PlaceholderText = "Κωδικός πελάτη",
        VerticalContentAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Button _copyCodeButton = new() { Background = new SolidColorBrush(Colors.Transparent),
        BorderThickness = new Thickness(0), Padding = new Thickness(6, 4, 6, 4),
        VerticalAlignment = VerticalAlignment.Center };
    private TextBlock? _mainBalanceText;
    private Border? _mainBalanceBadge;
    private readonly TextBlock _headerState = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly ActionButton _saveButton = new() { Kind = "Save", Label = "Αποθήκευση" };
    private readonly ActionButton _editButton = new() { Kind = "Edit", Label = "Επεξεργασία πελάτη" };

    public CustomerCardWindow(Guid? customerId, bool beginEditing = false)
    {
        _customerId = customerId;
        _editing = beginEditing || !customerId.HasValue;
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(18, 8, 18, 4) };
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var identity = new Grid { ColumnSpacing = 10, ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto }
        } };
        identity.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri("ms-appx:///Assets/Sections/customers.png")),
            Width = 42, Height = 42
        });
        Grid.SetColumn(_headerTitle, 1);
        identity.Children.Add(_headerTitle);
        _headerState.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_headerState, 2);
        identity.Children.Add(_headerState);
        var codeDisplay = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        codeDisplay.Children.Add(_headerCode);
        codeDisplay.Children.Add(new FontIcon { Glyph = "\uE8C8", FontSize = 14,
            Foreground = new SolidColorBrush(Colors.White) });
        _copyCodeButton.Content = codeDisplay;
        _copyCodeButton.Click += (_, _) =>
        {
            if (_customer.Id == Guid.Empty) return;
            var data = new DataPackage();
            data.SetText(_customer.Code);
            Clipboard.SetContent(data);
            var popup = new Flyout { Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 66, 130, 91)),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 6, 10, 6),
                Child = new TextBlock { Text = "Ο κωδικός αντιγράφηκε",
                    Foreground = new SolidColorBrush(Colors.White) }
            } };
            popup.ShowAt(_copyCodeButton);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (_, _) => { timer.Stop(); popup.Hide(); };
            timer.Start();
        };
        ToolTipService.SetToolTip(_copyCodeButton, "Αντιγραφή κωδικού πελάτη");
        var codeArea = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        codeArea.Children.Add(_copyCodeButton);
        codeArea.Children.Add(_codeInput);
        Grid.SetColumn(codeArea, 3);
        identity.Children.Add(codeArea);
        header.Children.Add(new Border { Background = gradient, CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 9, 18, 9), Child = identity });
        header.Children.Add(_status);
        _root.Children.Add(header);
        var tabScroller = new ScrollViewer { Content = _tabStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled };
        _tabs.Children.Add(tabScroller);
        Grid.SetRow(_tabContent, 1);
        _tabs.Children.Add(_tabContent);
        Grid.SetRow(_tabs, 1);
        _root.Children.Add(_tabs);
        var actions = new Grid { ColumnDefinitions = { new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto } },
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto } }, RowSpacing = 6,
            Margin = new Thickness(20, 10, 20, 18) };
        var leftActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        AddButton(leftActions, "Γενική είσπραξη", async () => await AddTransactionAsync("receipt"));
        AddButton(leftActions, "Γενική πληρωμή", async () => await AddTransactionAsync("payment"));
        AddButton(leftActions, "Μεταφορά πιστωτικού", TransferCreditAsync);
        actions.Children.Add(leftActions);
        var rightActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        Grid.SetColumn(rightActions, 1);
        actions.Children.Add(rightActions);
        _saveButton.Click += async (_, _) => await SaveAsync();
        _editButton.Click += (_, _) =>
        {
            _codeInput.Text = _customer.Code;
            _editing = true;
            BuildTabs();
            UpdateHeader();
            UpdateActionState();
        };
        rightActions.Children.Add(_editButton);
        rightActions.Children.Add(_saveButton);
        AddButton(rightActions, "Κλείσιμο", () => { Close(); return Task.CompletedTask; });
        actions.SizeChanged += (_, args) =>
        {
            var narrow = args.NewSize.Width < 1010;
            Grid.SetRow(rightActions, narrow ? 1 : 0);
            Grid.SetColumn(rightActions, narrow ? 0 : 1);
            rightActions.HorizontalAlignment = HorizontalAlignment.Right;
        };
        Grid.SetRow(actions, 2);
        _root.Children.Add(actions);
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        var dark = _root.RequestedTheme == ElementTheme.Dark ||
            (_root.RequestedTheme == ElementTheme.Default &&
             Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
        _root.Background = new SolidColorBrush(dark
            ? Color.FromArgb(255, 31, 31, 31)
            : App.Services.GetRequiredService<WorkspacePreferences>().EyeComfortEnabled
                ? Color.FromArgb(255, 228, 225, 216) : Color.FromArgb(255, 221, 228, 235));
        Content = _root;

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var working = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1260, Math.Max(680, working.Width - 40));
        var height = Math.Min(960, Math.Max(520, working.Height - 30));
        _contentWidth = Math.Min(1100, width - 70);
        _tabs.SizeChanged += (_, args) =>
        {
            if (_compactMain is not null)
                _compactMain.Width = Math.Min(1100, Math.Max(580, args.NewSize.Width - 50));
            ApplyResponsiveLayout(args.NewSize.Width);
        };
        appWindow.MoveAndResize(new RectInt32(working.X + (working.Width - width) / 2,
            working.Y + (working.Height - height) / 2, width, height));
        appWindow.Title = "Καρτέλα πελάτη — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        UpdateHeader();
        UpdateActionState();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _currency = await _transactionSettings.GetCurrencyAsync();
            if (_customerId is Guid id)
            {
                _customer = await _service.GetAsync(id) ??
                    throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
            }
            _codeInput.Text = _customer.Id == Guid.Empty ? "" : _customer.Code;
            BuildTabs();
            UpdateHeader();
            UpdateActionState();
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    public event EventHandler? CustomerChanged;
    public event EventHandler<Guid>? RelatedCustomerChanged;

    public async Task RefreshFromDatabaseAsync()
    {
        if (_editing || _customer.Id == Guid.Empty) return;
        _customer = await _service.GetAsync(_customer.Id) ?? _customer;
        BuildTabs(preserveSelectedTab: true);
        UpdateHeader();
    }

    private void BuildTabs(bool preserveSelectedTab = false)
    {
        var selectedTab = preserveSelectedTab ? _selectedTabIndex : 0;
        _readFields.Clear();
        _detailLists.Clear();
        _tabStrip.Children.Clear();
        _tabButtons.Clear();
        _tabLabels.Clear();
        _tabContents.Clear();
        _tabContent.Content = null;
        BuildCompactMainTab();
        BuildSupplementaryTab();
        AddDetailTab("Συμβόλαια", "policies");
        AddDetailTab("Οχήματα", "vehicles");
        BuildFinanceTab();
        BuildActivityHistoryTab();
        SelectTab(Math.Clamp(selectedTab, 0, _tabContents.Count - 1));
        ApplyResponsiveLayout(_tabs.ActualWidth);
    }

    private void UpdateHeader()
    {
        _headerTitle.Text = _customer.Id == Guid.Empty ? "Νέος πελάτης" :
            $"{_customer.LastName} {_customer.FirstName}".ToUpperInvariant();
        _headerCode.Text = "ΚΩΔΙΚΟΣ  " + (_customer.Id == Guid.Empty ? "Νέος" : _customer.Code);
        _headerState.Text = _customer.IsActive ? "● Ενεργός πελάτης" : "● Ανενεργός πελάτης";
        _copyCodeButton.IsEnabled = _customer.Id != Guid.Empty;
        _copyCodeButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        _codeInput.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMainBalance()
    {
        if (_mainBalanceText is null || _mainBalanceBadge is null) return;
        var balance = CustomerService.Balance(_customer);
        _mainBalanceText.Text = balance > 0 ? $"Οφειλή προς το γραφείο: {balance:N2} {CurrencySymbol}" :
            balance < 0 ? $"Υπόλοιπο πελάτη: {balance:N2} {CurrencySymbol}" :
            $"Υπόλοιπο: 0,00 {CurrencySymbol}";
        var dark = IsDarkTheme();
        _mainBalanceBadge.Background = balance == 0
            ? new SolidColorBrush(dark ? Color.FromArgb(255, 68, 74, 82) : Color.FromArgb(255, 225, 229, 234))
            : new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
        _mainBalanceText.Foreground = new SolidColorBrush(balance == 0
            ? dark ? Colors.White : Color.FromArgb(255, 37, 45, 55)
            : Colors.White);
        _mainBalanceBadge.Padding = new Thickness(8, 5, 8, 5);
    }

    private void UpdateActionState()
    {
        _saveButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        _editButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task LoadReferrersAsync()
    {
        try
        {
            var items = (await _service.SearchAsync()).Where(item => item.Id != _customer.Id).ToArray();
            if (_referralChoice is null) return;
            _referralChoice.ItemsSource = items;
            if (_customer.ReferredByCustomerId.HasValue)
                _referralChoice.SelectedValue = _customer.ReferredByCustomerId.Value;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    private async Task LoadPartnerReferrersAsync()
    {
        try
        {
            var names = await _filterService.ChoicesAsync("Συνεργάτες");
            if (_referralPartnerChoice is null) return;
            _referralPartnerChoice.ItemsSource = names;
            _referralPartnerChoice.SelectedItem = _customer.ReferredByPartnerName;
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private void AddDetailTab(string label, string category)
        => AddTab(label, CreateDetailPanel(category));

    private Grid CreateDetailPanel(string category)
    {
        var root = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
            new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
            new RowDefinition(), new RowDefinition { Height = GridLength.Auto } },
            Margin = new Thickness(16) };
        var search = new TextBox { PlaceholderText = category switch
            {
                "policies" => "Αναζήτηση συμβολαίου, ασφαλιστικής, κλάδου...",
                "vehicles" => "Αναζήτηση πινακίδας, μάρκας, στοιχείων...",
                _ => "Αναζήτηση τίτλου, αριθμού ή στοιχείων..."
            }, Height = 38, MinHeight = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var stateFilter = new ComboBox { Width = 195, Height = 38,
            ItemsSource = category == "policies"
                ? new[] { "Όλα", "Σε εξέλιξη", "Ενεργά", "Ιστορικά", "Αιτήσεις", "Ειδοποιητήρια",
                    "Ασφαλιστήρια", "Προσφορές", "Πρόσθετες πράξεις", "Πράσινες κάρτες" }
                : new[] { "Όλα", "Ενεργά", "Ανενεργά" }, SelectedIndex = 0 };
        var dateBasis = new ComboBox { Width = 195, Height = 38,
            ItemsSource = new[] { "Ημ. έναρξης", "Ημ. έκδοσης" }, SelectedIndex = 0 };
        var fromFilter = new EditableDateInput { Width = 195, Height = 38,
            PlaceholderText = "Από ημερομηνία" };
        var untilFilter = new EditableDateInput { Width = 195, Height = 38,
            PlaceholderText = "Έως ημερομηνία" };
        var filters = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 205, ItemHeight = 42 };
        filters.Children.Add(stateFilter);
        if (category == "policies")
        {
            filters.Children.Add(dateBasis);
            filters.Children.Add(fromFilter);
            filters.Children.Add(untilFilter);
        }
        var filterGroup = new StackPanel { Spacing = 7 };
        filterGroup.Children.Add(SearchFieldVisual.Wrap(search));
        filterGroup.Children.Add(filters);
        Grid.SetRow(filterGroup, 1);
        root.Children.Add(filterGroup);
        var commands = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 190, ItemHeight = 48 };
        var clear = new ActionButton { Kind = "Clear", Label = "Καθαρισμός", Width = 180 };
        clear.Click += (_, _) =>
        {
            search.Text = "";
            stateFilter.SelectedIndex = 0;
            fromFilter.Date = null;
            untilFilter.Date = null;
        };
        commands.Children.Add(clear);
        if (category is "policies" or "vehicles")
            AddButton(commands, "Προσθήκη", async () => await OpenBusinessCardAsync(category));
        Grid.SetRow(commands, 0);
        root.Children.Add(commands);
        var headers = new Grid { ColumnSpacing = 0, Width = 980,
            Background = TableHeaderBrush() };
        var widths = category == "policies"
            ? new[] { 140, 230, 145, 130, 145, 190 }
            : new[] { 150, 140, 220, 150, 150, 170 };
        foreach (var width in widths)
            headers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        var names = category == "policies"
            ? new[] { "Τύπος", "Εταιρεία", "Έναρξη", "Διάρκεια", "Λήξη", "Όχημα / πινακίδα" }
            : new[] { "Πινακίδα", "Χρήση", "Περιγραφή", "Μάρκα", "Μοντέλο", "Κατάσταση" };
        for (var index = 0; index < names.Length; index++)
        {
            var caption = new TextBlock { Text = names[index], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var cell = new Border { Child = caption,
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == names.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10, 8, 10, 8) };
            Grid.SetColumn(cell, index);
            headers.Children.Add(cell);
        }
        var headerFrame = new Border { Child = headers, Width = 982, HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) };
        Grid.SetRow(headerFrame, 2);
        root.Children.Add(headerFrame);
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single,
            Height = 410, Width = 982, HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1), Padding = new Thickness(0),
            Tag = category };
        _detailLists[category] = list;
        var pager = new TablePagination(list);
        list.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(async (_, args) =>
        {
            if (FindCardRow(args.OriginalSource) is ListViewItem row && row.Tag is not null)
                await OpenDetailRowAsync(category, row, row.Tag);
        }), true);
        list.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, args) =>
        {
            if (FindCardRow(args.OriginalSource) is not ListViewItem row || row.Tag is null) return;
            list.SelectedItem = row;
            CreateDetailMenu(category, row, row.Tag).ShowAt(row);
            args.Handled = true;
        }), true);
        var applyColumnLayout = TableInteractions.Attach(headers, list, _tabContent);
        void refresh()
        {
            FillDetailList(list, category, search.Text,
                stateFilter.SelectedIndex, fromFilter.Date?.Date, untilFilter.Date?.Date,
                dateBasis.SelectedIndex);
            pager.Capture();
            applyColumnLayout();
        }
        search.TextChanged += (_, _) => refresh();
        stateFilter.SelectionChanged += (_, _) => refresh();
        dateBasis.SelectionChanged += (_, _) => refresh();
        fromFilter.DateChanged += (_, _) => refresh();
        untilFilter.DateChanged += (_, _) => refresh();
        refresh();
        Grid.SetRow(list, 3);
        root.Children.Add(list);
        Grid.SetRow(pager.Bar, 4);
        root.Children.Add(pager.Bar);
        return root;
    }

    private void FillDetailList(ListView list, string category, string query,
        int state, DateTime? from, DateTime? until, int dateBasis)
    {
        list.Items.Clear();
        var term = query.Trim();
        DateTime DetailDate(CustomerDetail item) => dateBasis == 0
            ? item.EffectiveFrom ?? item.CreatedAtUtc.Date : item.CreatedAtUtc.Date;
        var details = _customer.Details.Where(item => item.Category == category)
            .Where(item => term.Length == 0 || new[] { item.Title, item.Value, item.Notes }
                .Any(value => value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true))
            .Where(item => state == 0 ||
                (state == 1 && !item.IsHistorical) ||
                (category == "policies" && state == 2 && !item.IsHistorical) ||
                (state == (category == "policies" ? 3 : 2) && item.IsHistorical))
            .Where(item => !from.HasValue || DetailDate(item) >= from.Value)
            .Where(item => !until.HasValue || DetailDate(item) <= until.Value)
            .OrderByDescending(item => item.CreatedAtUtc);
        foreach (var item in details)
        {
            var values = category == "policies"
                ? new[] { item.Value ?? "Παλαιά καταχώριση", item.Title,
                    item.EffectiveFrom?.ToString("dd/MM/yyyy"), "—",
                    item.EffectiveUntil?.ToString("dd/MM/yyyy"), "—" }
                : new[] { item.Title, item.Value, item.Notes, "—", "—",
                    item.IsHistorical ? "Ανενεργό" : "Ενεργό" };
            list.Items.Add(DetailListItem(category, item, values));
        }
        var linked = _customer.BusinessRecords.Where(item => item.SectionKey == category)
            .Where(item => term.Length == 0 || new[] { item.Title, item.Reference,
                item.RelatedParty, item.Status, item.Notes, item.DocumentType,
                item.SectionKey == "policies" ? BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Branch : null,
                item.SectionKey == "vehicles" ? BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson).Make : null }.Any(value =>
                    value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true))
            .Where(item => state == 0 || (category == "policies" ? state switch
            {
                1 => item.Status == "Σε εξέλιξη",
                2 => item.Status == "Ενεργό",
                3 => item.Status is "Ανενεργό" or "Ολοκληρωμένο",
                4 => item.DocumentType == "Αίτηση",
                5 => item.DocumentType == "Ειδοποιητήριο",
                6 => item.DocumentType == "Ασφαλιστήριο",
                7 => item.DocumentType == "Προσφορά",
                8 => item.DocumentType == "Πρόσθετη πράξη",
                9 => item.DocumentType == "Πράσινη κάρτα",
                _ => true
            } : state == 1 ? item.Status != "Ανενεργό" : item.Status == "Ανενεργό"))
            .Where(item => !from.HasValue || FilterDate(item, category, dateBasis) >= from)
            .Where(item => !until.HasValue || FilterDate(item, category, dateBasis) <= until)
            .OrderByDescending(item => item.CreatedAtUtc);
        foreach (var item in linked)
        {
            if (category == "policies")
            {
                var policy = BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson);
                var vehicle = policy.VehicleId is Guid vehicleId
                    ? _customer.BusinessRecords.FirstOrDefault(record => record.Id == vehicleId)
                    : null;
                var plate = vehicle is null ? null :
                    BusinessRecordDetails.Read<VehicleCardData>(vehicle.DetailsJson).Registration ?? vehicle.Reference;
                var duration = policy.CoverageDurationValue is int value
                    ? $"{value} {policy.CoverageDurationUnit ?? "μήνες"}"
                    : policy.CoverageDurationMonths is int months ? $"{months} μήνες" : "—";
                list.Items.Add(DetailListItem(category, item,
                    item.DocumentType ?? "Ασφαλιστήριο", policy.Insurer ?? item.RelatedParty,
                    policy.StartDate?.ToString("dd/MM/yyyy") ?? item.EventDate?.ToString("dd/MM/yyyy"),
                    duration, policy.EndDate?.ToString("dd/MM/yyyy"), plate));
            }
            else
            {
                var vehicle = BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson);
                list.Items.Add(DetailListItem(category, item,
                    vehicle.Registration ?? item.Reference ?? item.Title, vehicle.Use,
                    item.Notes ?? item.Title, vehicle.Make, vehicle.Model,
                    item.Status == "Ανενεργό" ? "Ανενεργό" : "Ενεργό"));
            }
        }
    }

    private static DateTime FilterDate(BusinessRecord record, string category, int dateBasis)
    {
        if (category != "policies") return record.EventDate ?? record.CreatedAtUtc.Date;
        var policy = BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson);
        return (dateBasis == 0 ? policy.StartDate : policy.IssueDate) ??
            record.EventDate ?? record.CreatedAtUtc.Date;
    }

    private ListViewItem DetailListItem(string category, object source, params string?[] cells)
    {
        var row = new Grid { ColumnSpacing = 0, Width = 980, Height = 39 };
        var widths = category == "policies"
            ? new[] { 140, 230, 145, 130, 145, 190 }
            : new[] { 150, 140, 220, 150, 150, 170 };
        foreach (var width in widths)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var index = 0; index < cells.Length; index++)
        {
            var text = new TextBlock { Text = cells[index] ?? "—", TextTrimming = TextTrimming.CharacterEllipsis };
            var cell = new Border { Child = text,
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == cells.Length - 1 ? new Thickness(0, 0, 0, 1) :
                    new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 5, 10, 5) };
            if (category == "policies" && source is BusinessRecord policyRecord && index == 0)
            {
                var policy = BusinessRecordDetails.Read<PolicyCardData>(policyRecord.DetailsJson);
                var cancellation = policyRecord.DocumentType?.Contains("ακύρ",
                    StringComparison.CurrentCultureIgnoreCase) == true;
                cell.Background = cancellation ? PolicyStatusColors.Background(policyRecord.DocumentType,
                    policyRecord.Status, policy.EndDate) : policyRecord.Status switch
                {
                    "Ενεργό" => new SolidColorBrush(Color.FromArgb(255, 69, 119, 88)),
                    "Σε εξέλιξη" => new SolidColorBrush(Color.FromArgb(255, 150, 113, 52)),
                    _ => PolicyStatusColors.Background(policyRecord.DocumentType,
                        policyRecord.Status, policy.EndDate)
                };
                text.Text = "✓ " + text.Text;
                text.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                ToolTipService.SetToolTip(cell, policyRecord.Status ?? policyRecord.DocumentType);
            }
            if (category == "vehicles" && index == cells.Length - 1)
            {
                cell.Background = new SolidColorBrush(cells[index] == "Ενεργό"
                    ? Color.FromArgb(255, 69, 119, 88) : Color.FromArgb(255, 104, 112, 121));
                text.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            Grid.SetColumn(cell, index);
            row.Children.Add(cell);
        }
        var result = new ListViewItem { Content = row, Tag = source, Width = 980, Height = 39,
            Padding = new Thickness(0), Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var id = source switch { CustomerDetail detail => detail.Id,
            BusinessRecord record => record.Id, _ => Guid.Empty };
        if (id != Guid.Empty) TableRowResizer.Attach(row, result, category, id);
        return result;
    }

    private MenuFlyout CreateDetailMenu(string category, ListViewItem row, object source)
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Άνοιγμα καρτέλας" };
        open.Click += async (_, _) => await OpenDetailRowAsync(category, row, source);
        menu.Items.Add(open);
        return menu;
    }

    private static ListViewItem? FindCardRow(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is ListViewItem { Tag: not null }) return (ListViewItem)current;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private async Task OpenDetailRowAsync(string category, ListViewItem row, object source)
    {
        _detailLists[category].SelectedItem = row;
        if (source is CustomerDetail detail)
        {
            if (category == "files") await OpenSelectedFileAsync();
            else
            {
                var view = new ContentDialog { XamlRoot = _root.XamlRoot, Title = detail.Title,
                    Content = $"Στοιχεία: {detail.Value ?? "—"}\nΣημειώσεις: {detail.Notes ?? "—"}",
                    CloseButtonText = "Κλείσιμο" };
                await DialogButtonStyles.ShowAsync(view);
            }
            return;
        }
        if (source is BusinessRecord record)
        {
            if (category is "policies" or "vehicles")
            {
                await OpenBusinessCardAsync(category, record.Id);
                return;
            }
            var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = record.Title,
                Content = $"Κωδικός: {record.Reference ?? "—"}\nΚατάσταση: {record.Status ?? "—"}\n" +
                    $"Ημερομηνία: {record.EventDate?.ToString("dd/MM/yyyy") ?? "—"}\n" +
                    $"{record.RelatedParty ?? record.Notes ?? ""}", CloseButtonText = "Κλείσιμο" };
            await DialogButtonStyles.ShowAsync(dialog);
        }
    }

    private async Task OpenBusinessCardAsync(string category, Guid? id = null)
    {
        if (!await EnsureSavedAsync()) return;
        if (_recordCard is not null) { _recordCard.BringToFront(); return; }
        try
        {
            _recordCard = new BusinessRecordCardWindow(category, _customer.Id, id,
                WindowNative.GetWindowHandle(this));
            _recordCard.RecordSaved += async (_, _) =>
            {
                _customer = await _service.GetAsync(_customer.Id) ?? _customer;
                BuildTabs(preserveSelectedTab: true);
                CustomerChanged?.Invoke(this, EventArgs.Empty);
            };
            _recordCard.Closed += (_, _) => { _recordCard = null; BringToFront(); };
            _recordCard.BringToFront();
        }
        catch (Exception exception)
        {
            _recordCard = null;
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message);
        }
    }

    private async Task EditDetailAsync(string category, CustomerDetail? existing)
    {
        if (!await EnsureSavedAsync()) return;
        var title = new TextBox { Header = RequiredHeader.Create("Τίτλος"), PlaceholderText = category == "vehicles" ? "Πινακίδα" : "Περιγραφή",
            Text = existing?.Title ?? "" };
        var value = new TextBox { Header = "Τιμή / αριθμός / στοιχεία", Text = existing?.Value ?? "" };
        var notes = new TextBox { Header = "Σημειώσεις", AcceptsReturn = true, Height = 90,
            Text = existing?.Notes ?? "" };
        var from = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.EffectiveFrom is DateTime start ? new DateTimeOffset(start) : null };
        var until = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.EffectiveUntil is DateTime end ? new DateTimeOffset(end) : null };
        var historical = new CheckBox { Content = "Ιστορική καταχώριση", IsChecked = existing?.IsHistorical ?? false };
        var form = new StackPanel { Spacing = 10, Width = 460 };
        foreach (var field in new UIElement[] { title, value, notes }) form.Children.Add(field);
        form.Children.Add(new TextBlock { Text = "Από" });
        form.Children.Add(from);
        form.Children.Add(new TextBlock { Text = "Έως" });
        form.Children.Add(until);
        form.Children.Add(historical);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Προσθήκη στοιχείου" : "Επεξεργασία στοιχείου",
            Content = new ScrollViewer { Content = form, MaxHeight = 460 },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            if (existing is null)
                await _service.AddDetailAsync(_customer.Id, category, title.Text, value.Text, notes.Text,
                    from.GetValidatedDate("Από"), until.GetValidatedDate("Έως"), historical.IsChecked == true);
            else
                await _service.UpdateDetailAsync(_customer.Id, existing.Id, title.Text, value.Text, notes.Text,
                    from.GetValidatedDate("Από"), until.GetValidatedDate("Έως"), historical.IsChecked == true);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab(category);
            _status.Text = "Το στοιχείο αποθηκεύτηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task DeleteDetailAsync(string category)
    {
        var detail = GetSelectedDetail(category);
        if (detail is null)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε στοιχείο.");
            return;
        }
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή στοιχείου",
            Content = $"Να διαγραφεί το «{detail.Title}»;", PrimaryButtonText = "Διαγραφή",
            CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            await _service.DeleteDetailAsync(_customer.Id, detail.Id);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab(category);
            _status.Text = "Το στοιχείο διαγράφηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task AddFileAsync()
    {
        if (!await EnsureSavedAsync()) return;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            await _service.AddFileAsync(_customer.Id, file.Path);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab("files");
            _status.Text = "Το αρχείο επισυνάφθηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task OpenSelectedFileAsync()
    {
        var file = GetSelectedDetail("files");
        if (file is null)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε αρχείο.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(_service.GetFilePath(file)) { UseShellExecute = true });
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private CustomerDetail? GetSelectedDetail(string category) =>
        _detailLists.TryGetValue(category, out var list) &&
        list.SelectedItem is ListViewItem { Tag: CustomerDetail detail } ? detail : null;

    private void SelectTab(string category)
    {
        SelectTab(category switch { "policies" => 2, "vehicles" => 3, _ => 5 });
    }

    private async Task AddTransactionAsync(string type)
    {
        if (!await EnsureSavedAsync()) return;
        IReadOnlyList<string> methods;
        try
        {
            _currency = await _transactionSettings.GetCurrencyAsync();
            methods = (await _filterService.ChoicesAsync("Τρόποι πληρωμής"))
                .Concat(["Ταχυπληρωμή"])
                .Concat(_customer.BusinessRecords.Where(item => item.SectionKey == "policies")
                    .Select(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson)
                        .PreferredPaymentMethod)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!))
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message);
            return;
        }
        var amount = new TextBox { Header = RequiredHeader.Create($"Ποσό ({CurrencySymbol})"),
            PlaceholderText = "π.χ. 125,50" };
        var method = new ComboBox { Header = RequiredHeader.Create("Τρόπος πληρωμής"),
            ItemsSource = methods, HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Επιλέξτε τρόπο πληρωμής" };
        var currency = new TextBox { Header = "Νόμισμα", Text = $"{_currency} ({CurrencySymbol})",
            IsReadOnly = true, IsTabStop = false };
        var reference = new TextBox { Header = "Αριθμός συναλλαγής / αναφορά",
            PlaceholderText = "π.χ. αριθμός τραπεζικής κίνησης" };
        var policies = new[] { new PolicyChoice(null, "Χωρίς σύνδεση με ασφαλιστήριο") }
            .Concat(_customer.BusinessRecords.Where(record => record.SectionKey == "policies" &&
                record.DocumentType is null or "Ασφαλιστήριο")
                .OrderBy(record => record.Title)
                .Select(record => new PolicyChoice(record.Id,
                    $"{record.Title} · {record.Reference ?? "χωρίς αριθμό"}"))).ToArray();
        var policy = new ComboBox { Header = "Σύνδεση με ασφαλιστήριο", ItemsSource = policies,
            DisplayMemberPath = nameof(PolicyChoice.Name), SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        policy.SelectionChanged += (_, _) =>
        {
            var selectedId = (policy.SelectedItem as PolicyChoice)?.Id;
            var selectedPolicy = _customer.BusinessRecords.FirstOrDefault(item => item.Id == selectedId);
            var preferred = selectedPolicy is null ? null :
                BusinessRecordDetails.Read<PolicyCardData>(selectedPolicy.DetailsJson).PreferredPaymentMethod;
            method.SelectedItem = methods.FirstOrDefault(value => value == preferred);
        };
        var description = new TextBox { Header = "Αιτιολογία / σημειώσεις", AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, Height = 74 };
        var form = new StackPanel { Spacing = 10, Width = 420 };
        form.Children.Add(amount);
        form.Children.Add(method);
        form.Children.Add(currency);
        form.Children.Add(reference);
        form.Children.Add(policy);
        form.Children.Add(description);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = type == "receipt" ? "Γενική είσπραξη" : "Γενική πληρωμή",
            Content = new ScrollViewer { Content = form, MaxHeight = 500 },
            PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            if (method.SelectedItem is not string paymentMethod)
                throw new ArgumentException("Επιλέξτε τρόπο πληρωμής.");
            if (await _transactionSettings.GetCurrencyAsync() != _currency)
                throw new InvalidOperationException("Το νόμισμα άλλαξε. Ανοίξτε ξανά την καταχώριση.");
            await _service.AddTransactionAsync(_customer.Id, type, ParseAmount(amount.Text, false),
                description.Text, paymentMethod, reference.Text,
                (policy.SelectedItem as PolicyChoice)?.Id);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs(preserveSelectedTab: true);
            UpdateHeader();
            CustomerChanged?.Invoke(this, EventArgs.Empty);
            _status.Text = $"Η κίνηση καταχωρίστηκε. Νέο υπόλοιπο: {CustomerService.Balance(_customer):N2} {CurrencySymbol}";
            await UiAlerts.ShowSuccessAsync(_root.XamlRoot, "Η οικονομική κίνηση καταχωρίστηκε επιτυχώς.");
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task<bool> EnsureSavedAsync()
    {
        if (_customer.Id != Guid.Empty) return true;
        return await SaveAsync();
    }

    private async Task<bool> SaveAsync()
    {
        try
        {
            foreach (var read in _readFields) read();
            _customer.CustomCode = string.IsNullOrWhiteSpace(_codeInput.Text)
                ? null : _codeInput.Text.Trim();
            _customer.ReferredByCustomerId = _referralChoice?.SelectedValue as Guid?;
            _customer.ReferredByPartnerName = _referralPartnerChoice?.SelectedItem as string;
            if (_customer.Source is not ("Σύσταση" or "Σύσταση από πελάτη"))
                _customer.ReferredByCustomerId = null;
            if (_customer.Source != "Σύσταση από συνεργάτη")
                _customer.ReferredByPartnerName = null;
            if (_customer.Source == "Σύσταση από πελάτη" &&
                !_customer.ReferredByCustomerId.HasValue)
                throw new ArgumentException("Επιλέξτε τον πελάτη που έκανε τη σύσταση.");
            if (_customer.Source == "Σύσταση από συνεργάτη" &&
                string.IsNullOrWhiteSpace(_customer.ReferredByPartnerName))
                throw new ArgumentException("Επιλέξτε τον συνεργάτη που έκανε τη σύσταση.");
            _customer.IsReferral = _customer.ReferredByCustomerId.HasValue ||
                !string.IsNullOrWhiteSpace(_customer.ReferredByPartnerName) ||
                _customer.Source == "Σύσταση";
            var id = await _service.SaveAsync(_customer);
            _customer = await _service.GetAsync(id) ?? _customer;
            _editing = false;
            BuildTabs();
            UpdateHeader();
            UpdateActionState();
            CustomerChanged?.Invoke(this, EventArgs.Empty);
            _status.Text = "Η καρτέλα πελάτη αποθηκεύτηκε.";
            await UiAlerts.ShowSuccessAsync(_root.XamlRoot, "Η εργασία σας αποθηκεύτηκε επιτυχώς.");
            return true;
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message);
            return false;
        }
    }

    private void AddTab(string header, UIElement content)
    {
        var icon = header switch
        {
            "Καρτέλα πελάτη" => "user-round",
            "Λοιπά" => "settings-2",
            "Συμβόλαια" => "file-text",
            "Οχήματα" => "car-front",
            "Οικονομικά" => "chart-no-axes-combined",
            _ => "clock-3"
        };
        var (button, title) = CardTabVisual.Create(header, icon, IsDarkTheme());
        var index = _tabButtons.Count;
        button.Click += (_, _) => SelectTab(index);
        _tabButtons.Add(button);
        _tabLabels.Add(title);
        _tabContents.Add(new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        _tabStrip.Children.Add(button);
    }

    private bool IsDarkTheme() => _root.RequestedTheme == ElementTheme.Dark ||
        (_root.RequestedTheme == ElementTheme.Default &&
         Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);

    private SolidColorBrush TableHeaderBrush()
    {
        var color = IsDarkTheme() ? Color.FromArgb(255, 48, 68, 90)
            : App.Services.GetRequiredService<WorkspacePreferences>().EyeComfortEnabled
                ? Color.FromArgb(255, 218, 216, 206)
                : Color.FromArgb(255, 208, 222, 235);
        return new SolidColorBrush(color);
    }

    private void ApplyResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0 || _tabButtons.Count == 0) return;
        var compact = availableWidth < 980;
        var narrow = availableWidth < 760;
        var captionWidth = narrow ? 105 : compact ? 125 : 145;
        if (_compactMain is not null)
            foreach (var column in _compactMain.Children.OfType<StackPanel>())
                foreach (var row in column.Children.OfType<Grid>().Where(row =>
                    row.ColumnDefinitions.Count >= 2 && row.Children.Count >= 2))
                {
                    row.ColumnDefinitions[0].Width = new GridLength(captionWidth);
                    if (row.Children[0] is StackPanel caption)
                        foreach (var text in caption.Children.OfType<TextBlock>())
                            text.FontSize = narrow ? 11 : 12;
                    if (row.Children[1] is Control control)
                        control.FontSize = narrow ? 12 : 13;
                }
        foreach (var button in _tabButtons)
        {
            button.Width = double.NaN;
            button.MinHeight = narrow ? 36 : 40;
            if (button.Content is not Grid surface) continue;
            var content = surface.Children.OfType<StackPanel>().FirstOrDefault();
            if (content is null) continue;
            content.Margin = narrow ? new Thickness(12, 0, 12, 0) : new Thickness(16, 0, 16, 0);
            content.Spacing = narrow ? 4 : 6;
            var iconSize = narrow ? 15 : 19;
            foreach (var icon in content.Children.OfType<Image>())
                icon.Width = icon.Height = iconSize;
            foreach (var label in content.Children.OfType<TextBlock>())
            {
                label.FontSize = narrow ? 11 : 13;
                label.TextWrapping = TextWrapping.NoWrap;
                label.MaxWidth = double.PositiveInfinity;
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                button.MinWidth = Math.Max(narrow ? 102 : 124,
                    Math.Ceiling(label.DesiredSize.Width + iconSize + content.Spacing +
                        (narrow ? 24 : 32)));
            }
        }
        var tabWidth = Math.Max(102, _tabButtons.Max(button => button.MinWidth));
        var columns = Math.Min(_tabButtons.Count, Math.Max(1, (int)(availableWidth / (tabWidth + 2))));
        _tabStrip.Width = availableWidth;
        _tabStrip.ColumnDefinitions.Clear();
        _tabStrip.RowDefinitions.Clear();
        for (var index = 0; index < columns; index++)
            _tabStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < (_tabButtons.Count + columns - 1) / columns; index++)
            _tabStrip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < _tabButtons.Count; index++)
        {
            var button = _tabButtons[index];
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(button, index / columns);
            Grid.SetColumn(button, index % columns);
        }
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabContents.Count) return;
        _selectedTabIndex = index;
        _tabContent.Content = _tabContents[index];
        var dark = IsDarkTheme();
        for (var i = 0; i < _tabButtons.Count; i++)
        {
            var selected = i == index;
            CardTabVisual.SetSelected(_tabButtons[i], _tabLabels[i], selected, dark);
        }
    }

    private static void AddHeading(StackPanel form, string title) => form.Children.Add(new TextBlock
    {
        Text = title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 2, 0, 2)
    });

    private TextBox AddText(StackPanel form, string label, Func<string?> getter,
        Action<string?> setter, bool multiline = false)
    {
        var input = new TextBox { Text = getter() ?? "", MinHeight = multiline ? 80 : 38,
            Height = multiline ? 80 : 38, FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            IsReadOnly = !_editing };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        if (!multiline)
        {
            input.CanDrag = !_editing;
            input.DragStarting += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(input.Text)) args.Data.SetText(input.Text);
            };
            input.AllowDrop = _editing;
            input.DragOver += (_, args) =>
            {
                if (_editing && args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            };
            input.Drop += async (_, args) =>
            {
                if (_editing && args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                    input.Text = await args.DataView.GetTextAsync();
            };
        }
        form.Children.Add(CreateFieldRow(label, multiline ? ResizableNoteField.Wrap(input) : input));
        _readFields.Add(() => setter(input.Text.Trim()));
        return input;
    }

    private CheckBox AddBool(StackPanel form, string label, Func<bool> getter, Action<bool> setter)
    {
        var checkedValue = getter();
        var input = new CheckBox { IsChecked = checkedValue, IsEnabled = _editing,
            MinHeight = 30, Height = 30 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        if (_editing) form.Children.Add(CreateFieldRow(label, input));
        else
        {
            var mark = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(checkedValue
                    ? Color.FromArgb(255, 25, 135, 75) : Color.FromArgb(255, 96, 105, 115)),
                Child = new TextBlock { Text = checkedValue ? "✓" : "",
                    Foreground = new SolidColorBrush(Colors.White), FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center } };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mark,
                label + (checkedValue ? ": Ναι" : ": Όχι"));
            form.Children.Add(CreateFieldRow(label, mark));
        }
        _readFields.Add(() => setter(input.IsChecked == true));
        return input;
    }

    private void AddChoice(StackPanel form, string label, Func<string> getter,
        Action<string> setter, string[] options)
    {
        var input = new ComboBox { ItemsSource = options,
            IsHitTestVisible = _editing, IsTabStop = _editing,
            MinHeight = 38, Height = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = options.Contains(getter()) ? getter() : options[0] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        WireChoiceTransfer(form, input);
        _readFields.Add(() => setter(input.SelectedItem as string ?? options[0]));
    }

    private ComboBox AddCatalogChoice(StackPanel form, string label, string category,
        Func<string?> getter, Action<string?> setter)
    {
        var selected = getter();
        var input = new ComboBox { IsHitTestVisible = _editing, IsTabStop = _editing,
            MinHeight = 38, Height = 38,
            PlaceholderText = "Επιλέξτε", HorizontalAlignment = HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        WireChoiceTransfer(form, input);
        _readFields.Add(() => setter(input.SelectedItem as string ?? selected));
        _ = LoadCatalogChoicesAsync(input, category, selected);
        return input;
    }

    private async Task LoadCatalogChoicesAsync(ComboBox input, string category, string? selected)
    {
        try
        {
            var choices = await _filterService.ChoicesAsync(category);
            var allChoices = category == "Πηγές πελατών"
                ? choices.Concat(["Σύσταση από πελάτη", "Σύσταση από συνεργάτη"]).Distinct().ToArray()
                : choices.ToArray();
            input.ItemsSource = string.IsNullOrWhiteSpace(selected) || allChoices.Contains(selected)
                ? allChoices : allChoices.Concat([selected]).ToArray();
            input.SelectedItem = selected;
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private void WireChoiceTransfer(StackPanel form, ComboBox input)
    {
        if (!_editing)
        {
            var row = (Grid)form.Children[^1];
            row.CanDrag = true;
            row.DragStarting += (_, args) =>
            {
                if (input.SelectedItem is string value) args.Data.SetText(value);
            };
            return;
        }
        input.AllowDrop = true;
        input.DragOver += (_, args) =>
        {
            if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        };
        input.Drop += async (_, args) =>
        {
            var value = await args.DataView.GetTextAsync();
            var match = input.Items.OfType<string>().FirstOrDefault(item =>
                string.Equals(item, value, StringComparison.CurrentCultureIgnoreCase));
            if (match is not null) input.SelectedItem = match;
            else await UiAlerts.ShowErrorAsync(_root.XamlRoot,
                "Η τιμή δεν υπάρχει στις επιλογές αυτού του πεδίου.");
        };
    }

    private EditableDateInput AddDate(StackPanel form, string label, Func<DateTime?> getter, Action<DateTime?> setter)
    {
        var input = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            MinHeight = 38, Height = 38,
            Date = getter() is DateTime value ? new DateTimeOffset(value) : null,
            IsHitTestVisible = _editing, IsTabStop = _editing };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        var row = CreateFieldRow(label, input);
        form.Children.Add(row);
        if (!_editing)
        {
            row.CanDrag = true;
            row.DragStarting += (_, args) =>
            {
                if (input.Date is DateTimeOffset current)
                    args.Data.SetText(current.ToString("dd/MM/yyyy"));
            };
        }
        else
        {
            input.AllowDrop = true;
            input.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            };
            input.Drop += async (_, args) =>
            {
                var value = await args.DataView.GetTextAsync();
                if (DateTime.TryParseExact(value, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"],
                        CultureInfo.GetCultureInfo("el-GR"), DateTimeStyles.None, out var date))
                    input.Date = new DateTimeOffset(date);
                else await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Μη έγκυρη ημερομηνία μεταφοράς.");
            };
        }
        _readFields.Add(() => setter(input.GetValidatedDate(label)));
        return input;
    }

    private static Grid CreateFieldRow(string label, FrameworkElement input)
        => CardFieldLayout.CreateRow(label, input);

    private static decimal ParseAmount(string? input, bool allowNegative)
    {
        if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var amount) ||
            (!allowNegative && amount <= 0))
            throw new ArgumentException("Συμπληρώστε έγκυρο ποσό.");
        return amount;
    }

    private static void AddButton(Panel panel, string label, Func<Task> action)
    {
        var button = new ActionButton { Label = label, Kind = ActionKind(label),
            Width = 180, MinHeight = 38 };
        button.Click += async (_, _) => await action();
        panel.Children.Add(button);
    }

    private static string ActionKind(string label) => label switch
    {
        "Γενική είσπραξη" => "Receipt",
        "Γενική πληρωμή" => "Payment",
        "Μεταφορά πιστωτικού" => "CreditTransfer",
        "Επισύναψη αρχείου" => "Upload",
        "Άνοιγμα" => "Open",
        "Προσθήκη" => "Add",
        "Επεξεργασία" => "Edit",
        "Διαγραφή επιλεγμένου" => "Delete",
        "Κλείσιμο" => "Close",
        _ => "Open"
    };

}

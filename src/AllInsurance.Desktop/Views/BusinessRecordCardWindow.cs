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
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly ContentControl _tabContent = new();
    private readonly List<(Button Button, TextBlock Label, UIElement Content)> _tabItems = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heading = new() { FontSize = 25,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerState = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerCode = new() { Foreground = new SolidColorBrush(Colors.White),
        Margin = new Thickness(68, 2, 0, 0) };
    private readonly Dictionary<string, TextBox> _text = [];
    private readonly Dictionary<string, CalendarDatePicker> _dates = [];
    private readonly List<Grid> _formGrids = [];
    private readonly Dictionary<string, CheckBox> _vehicleChecks = [];
    private readonly List<VehiclePerson> _vehiclePeople = [];
    private IReadOnlyList<CustomerListItem> _vehicleCustomers = [];
    private IReadOnlyList<BusinessRecord> _vehiclePolicies = [];
    private IReadOnlyList<BusinessRecord> _vehicleClaims = [];
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
    private ActionButton? _newVehiclePolicyButton;
    private ActionButton? _addVehicleClaimButton;
    private TextBlock? _policyFinanceSummary;
    private TextBlock? _policyCommissionSummary;
    private TextBlock? _historyText;
    private bool _conversionRequested;
    private sealed record Choice(Guid? Id, string Name);

    public event EventHandler? RecordSaved;

    public BusinessRecordCardWindow(string section, Guid? customerId = null, Guid? recordId = null,
        nint ownerHandle = default, Guid? vehicleId = null)
    {
        if (section is not ("policies" or "vehicles"))
            throw new ArgumentException("Μη υποστηριζόμενη καρτέλα.");
        _section = section;
        _fixedCustomerId = customerId;
        _recordId = recordId;
        _ownerHandle = ownerHandle == default ? App.MainWindowHandle() : ownerHandle;
        _record = new BusinessRecord { SectionKey = section, Title = "",
            CustomerId = customerId, DocumentType = section == "policies" ? "Αίτηση" : null,
            DetailsJson = section == "policies" && vehicleId is Guid linkedVehicle
                ? BusinessRecordDetails.Write(new PolicyCardData { VehicleId = linkedVehicle }) : null };
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        _root.Background = new SolidColorBrush(IsDarkTheme()
            ? Color.FromArgb(255, 31, 35, 42) : Color.FromArgb(255, 246, 249, 252));
        Content = _root;
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1220, Math.Max(660, area.Width - 32));
        var height = Math.Min(900, Math.Max(560, area.Height - 32));
        _contentWidth = Math.Min(1100, width - 70);
        _tabContent.SizeChanged += (_, args) =>
        {
            var target = Math.Min(1100, Math.Max(600, args.NewSize.Width - 50));
            foreach (var grid in _formGrids) grid.Width = target;
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
            var customers = await _customers.SearchAsync();
            var companies = _section == "policies"
                ? (await _records.SearchAsync("companies")).ToArray()
                : [];
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
            : (_record.Id == Guid.Empty ? "Νέα αίτηση ασφάλισης" :
                $"{_record.DocumentType ?? "Ασφαλιστήριο"} · {_record.Title}");
        _headerState.Text = $"● {_record.Status ?? (_section == "vehicles" ? "Ενεργό" : "Σε εξέλιξη")}";
        _headerCode.Text = "ΚΩΔΙΚΟΣ  " + (_record.Reference ?? "Νέο");
        var bannerText = new StackPanel { Spacing = 8 };
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        identity.Children.Add(new Image { Source = new BitmapImage(new Uri(_section == "vehicles"
            ? "ms-appx:///Assets/Sections/vehicles.png" : "ms-appx:///Assets/Sections/policies.png")),
            Width = 54, Height = 54 });
        var identityText = new StackPanel { Spacing = 3 };
        identityText.Children.Add(_heading);
        identityText.Children.Add(_headerState);
        identity.Children.Add(identityText);
        bannerText.Children.Add(identity);
        bannerText.Children.Add(_headerCode);
        banner.Child = bannerText;
        _root.Children.Add(banner);
        var strip = new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility =
            ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(18, 0, 18, 0) };
        Grid.SetRow(strip, 1);
        _root.Children.Add(strip);
        Grid.SetRow(_tabContent, 2);
        _root.Children.Add(_tabContent);
        var footer = new Grid { ColumnDefinitions =
        {
            new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }
        }, Margin = new Thickness(18, 10, 18, 14) };
        footer.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (_section == "policies")
        {
            _convertButton = new ActionButton { Kind = "Edit", Label = "Μετατροπή σε ασφαλιστήριο" };
            _convertButton.Click += async (_, _) => await ConvertAsync();
            actions.Children.Add(_convertButton);
        }
        var save = new ActionButton { Kind = "Save", Label = "Αποθήκευση" };
        save.Click += async (_, _) => await SaveAsync();
        actions.Children.Add(save);
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 3);
        _root.Children.Add(footer);
        if (_section == "policies") BuildPolicyTabs(customers, vehicles, transactions, companies);
        else BuildVehicleTabs(customers, policies);
        SelectTab(0);
        UpdateConvertButton();
    }

    private void AddTab(string title, UIElement content)
    {
        var index = _tabItems.Count;
        var icon = title switch
        {
            "Καρτέλα οχήματος" or "Όχημα" => "car-front",
            "Ιδιοκτήτης" or "Πρόσωπα" => "user-round",
            "Συμβόλαια" => "file-text",
            "Ζημιές" => "car-front",
            "Πρόσθετα στοιχεία" => "settings-2",
            "Οικονομικά" => "chart-no-axes-combined",
            "Ιστορικό" => "clock-3",
            _ => "file-text"
        };
        var (button, label) = CardTabVisual.Create(title, icon, IsDarkTheme());
        button.Click += (_, _) => SelectTab(index);
        _tabs.Children.Add(button);
        _tabItems.Add((button, label, new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }));
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
        _formGrids.Add(grid);
        return grid;
    }

    private TextBox Text(StackPanel panel, string key, string label, string? value = null)
    {
        var field = new TextBox { Text = value ?? "", MinHeight = 34, Height = 34,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        _text[key] = field;
        return field;
    }

    private CalendarDatePicker Date(StackPanel panel, string key, string label, DateTime? value = null)
    {
        var field = new CalendarDatePicker {
            Date = value is DateTime date ? new DateTimeOffset(date) : null,
            MinHeight = 36, Height = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        _dates[key] = field;
        return field;
    }

    private ComboBox Choices(StackPanel panel, string label, IEnumerable<string> items, string? selected)
    {
        var field = new ComboBox { ItemsSource = items.ToArray(),
            MinHeight = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
        field.SelectedItem = selected;
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        return field;
    }

    private ComboBox CustomerChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers)
    {
        var items = customers.Select(item => new Choice(item.Id, item.FullName)).ToArray();
        _customerChoice = new ComboBox { ItemsSource = items, DisplayMemberPath = nameof(Choice.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = _fixedCustomerId is null, MinHeight = 34, Height = 34 };
        _customerChoice.SelectedItem = items.FirstOrDefault(item => item.Id == _record.CustomerId);
        panel.Children.Add(CardFieldLayout.CreateRow("Πελάτης / ιδιοκτήτης *", _customerChoice));
        return _customerChoice;
    }

    private void BuildPolicyTabs(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> vehicles, IReadOnlyList<CustomerTransaction> transactions,
        IReadOnlyList<BusinessRecord> companies)
    {
        var data = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson);
        if (_record.DocumentType is null && _record.Id != Guid.Empty)
            data.PolicyNumber ??= _record.Reference ?? _record.Title;
        var main = TwoColumns(out var left, out var right);
        var availableTypes = _record.Id == Guid.Empty ||
            _record.DocumentType is ("Αίτηση" or "Ειδοποιητήριο")
            ? new[] { "Αίτηση", "Ειδοποιητήριο" }
            : new[] { "Αίτηση", "Ειδοποιητήριο", "Ασφαλιστήριο" };
        _typeChoice = Choices(left, "Τύπος εγγράφου *", availableTypes,
            _record.DocumentType ?? "Ασφαλιστήριο");
        Text(left, "application", "Αριθμός αίτησης", data.ApplicationNumber);
        Text(left, "notice", "Αριθμός ειδοποιητηρίου", data.NoticeNumber);
        Text(left, "policy", "Αριθμός ασφαλιστηρίου", data.PolicyNumber);
        void UpdateRequiredNumber()
        {
            var type = _typeChoice?.SelectedItem as string;
            CardFieldLayout.SetRequired(left, _text["application"], "Αριθμός αίτησης", type == "Αίτηση");
            CardFieldLayout.SetRequired(left, _text["notice"], "Αριθμός ειδοποιητηρίου", type == "Ειδοποιητήριο");
            CardFieldLayout.SetRequired(left, _text["policy"], "Αριθμός ασφαλιστηρίου", type == "Ασφαλιστήριο");
        }
        _typeChoice.SelectionChanged += (_, _) => UpdateRequiredNumber();
        UpdateRequiredNumber();
        var insurerNames = companies.Select(item => item.Title).Distinct().ToArray();
        _insurerSuggest = new AutoSuggestBox { Text = data.Insurer ?? "",
            PlaceholderText = "Αναζήτηση ασφαλιστικής εταιρείας", MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _insurerSuggest.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            _insurerSuggest.ItemsSource = insurerNames.Where(name => name.Contains(
                _insurerSuggest.Text, StringComparison.CurrentCultureIgnoreCase)).Take(30).ToArray();
        };
        _insurerSuggest.SuggestionChosen += (_, args) =>
            _insurerSuggest.Text = args.SelectedItem as string ?? _insurerSuggest.Text;
        left.Children.Add(CardFieldLayout.CreateRow("Ασφαλιστική εταιρεία", _insurerSuggest));
        Text(left, "agency", "Πρακτορείο", data.Agency);
        Text(left, "branch", "Κλάδος", data.Branch);
        Text(left, "product", "Προϊόν / πακέτο", data.Product);
        CustomerChoice(right, customers);
        Choice[] VehicleItems(Guid? customerId) =>
            [new Choice(null, "Χωρίς όχημα"),
             .. vehicles.Where(item => item.CustomerId == customerId)
                 .Select(item => new Choice(item.Id, item.Title))];
        var vehicleItems = VehicleItems(_record.CustomerId);
        _vehicleChoice = new ComboBox { ItemsSource = vehicleItems,
            DisplayMemberPath = nameof(Choice.Name), MinHeight = 28, Height = 28,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _vehicleChoice.SelectedItem = vehicleItems.FirstOrDefault(item => item.Id == data.VehicleId) ?? vehicleItems[0];
        right.Children.Add(CardFieldLayout.CreateRow("Συνδεδεμένο όχημα", _vehicleChoice));
        _customerChoice!.SelectionChanged += (_, _) =>
        {
            var selectedVehicle = (_vehicleChoice.SelectedItem as Choice)?.Id;
            var available = VehicleItems((_customerChoice.SelectedItem as Choice)?.Id);
            _vehicleChoice.ItemsSource = available;
            _vehicleChoice.SelectedItem = available.FirstOrDefault(item => item.Id == selectedVehicle) ?? available[0];
        };
        _statusChoice = Choices(right, "Κατάσταση", ["Σε εξέλιξη", "Ενεργό", "Ανενεργό", "Ολοκληρωμένο"],
            _record.Status ?? (_record.Id == Guid.Empty ? "Σε εξέλιξη" : "Ενεργό"));
        Date(right, "start", "Έναρξη κάλυψης", data.StartDate);
        Date(right, "end", "Λήξη κάλυψης", data.EndDate);
        Text(right, "gross", "Μικτά ασφάλιστρα", data.GrossPremium?.ToString("N2"));
        Text(right, "net", "Καθαρά ασφάλιστρα", data.NetPremium?.ToString("N2"));
        AddTab("Καρτέλα ασφάλισης", main);
        var extra = TwoColumns(out var extraLeft, out _);
        var notes = Text(extraLeft, "notes", "Σημειώσεις", _record.Notes);
        notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.Height = 150;
        AddTab("Πρόσθετα στοιχεία", extra);
        AddTab("Όχημα", new TextBlock { Text = data.VehicleId is null
            ? "Δεν έχει συνδεθεί όχημα."
            : vehicles.FirstOrDefault(item => item.Id == data.VehicleId)?.Title ?? "Το όχημα δεν βρέθηκε.",
            Margin = new Thickness(22), FontSize = 16 });
        var coverage = TwoColumns(out var coverageLeft, out _);
        var cover = Text(coverageLeft, "coverage", "Καλύψεις / όροι", data.CoverageNotes);
        cover.AcceptsReturn = true; cover.TextWrapping = TextWrapping.Wrap; cover.Height = 190;
        AddTab("Καλύψεις", coverage);
        var finance = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        _policyFinanceSummary = new TextBlock { FontSize = 16 };
        _policyCommissionSummary = new TextBlock { FontSize = 15,
            TextWrapping = TextWrapping.Wrap };
        finance.Children.Add(_policyFinanceSummary);
        finance.Children.Add(_policyCommissionSummary);
        UpdatePolicyFinanceSummary(data);
        var movements = transactions.OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => $"{item.OccurredAtUtc.ToLocalTime():dd/MM/yyyy}  ·  " +
                $"{(item.Type == "receipt" ? "Είσπραξη" : "Πληρωμή")}  ·  " +
                $"{item.Amount:N2} {item.Currency}  ·  {item.PaymentMethod}").ToArray();
        finance.Children.Add(new TextBlock { Text = "Συνδεδεμένες κινήσεις",
            FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        finance.Children.Add(new ListView { ItemsSource = movements.Length > 0 ? movements :
            ["Δεν υπάρχουν συνδεδεμένες κινήσεις."], Height = 220 });
        AddTab("Οικονομικά", finance);
        _historyText = new TextBlock { Text = _record.Id == Guid.Empty
            ? "Η ιστορία θα ξεκινήσει μετά την αποθήκευση της αίτησης."
            : $"Δημιουργία: {_record.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}\nΤελευταία ενημέρωση: {_record.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
            Margin = new Thickness(22), FontSize = 15 };
        AddTab("Ιστορικό", _historyText);
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
        CustomerChoice(left, customers);
        Text(left, "registration", "Αριθμός κυκλοφορίας *", data.Registration);
        Text(left, "make", "Μάρκα / εργοστάσιο", data.Make);
        Text(left, "model", "Μοντέλο", data.Model);
        Text(left, "vin", "Αριθμός πλαισίου (VIN)", data.Vin);
        Text(left, "year", "Έτος κατασκευής", data.ManufactureYear?.ToString());
        Date(left, "first", "Πρώτη άδεια κυκλοφορίας", data.FirstRegistrationDate);
        Date(left, "transfer", "Ημερομηνία μεταβίβασης", data.TransferDate);
        Text(left, "taxclass", "Κατηγορία φορολόγησης", data.TaxClass);
        Text(left, "hp", "Ισχύς (ίπποι)", data.HorsePower?.ToString("N2"));
        Text(left, "seats", "Θέσεις", data.Seats?.ToString());
        Text(left, "doors", "Πόρτες", data.Doors?.ToString());
        Text(left, "fuelcolor", "Χρώμα καυσίμου", data.FuelColor);
        Text(left, "permit", "Άδεια κυκλοφορίας", data.RegistrationPermit);
        Text(left, "licence", "Ειδικό δίπλωμα", data.SpecialLicence);
        Text(left, "discount", "Δικαιολογητικά έκπτωσης", data.DiscountDocuments);
        Text(left, "roadtax", "Τέλη κυκλοφορίας (€)", data.RoadTax?.ToString("N2"));
        Text(left, "reports", "Έντυπα αναγγελίας", data.ReportForms);
        VehicleCheck(left, "convertible", "Κάμπριο", data.IsConvertible);
        VehicleCheck(left, "sunroof", "Ηλιοροφή", data.HasSunroof);
        VehicleCheck(left, "heavy", "Βάρος άνω των 3,5 τόνων", data.OverThreeAndHalfTons);
        VehicleCheck(left, "dual", "Διπλοί πίσω τροχοί", data.HasRearDualWheels);
        Text(right, "use", "Χρήση", data.Use);
        Text(right, "fuel", "Καύσιμο", data.Fuel);
        Text(right, "cc", "Κυβισμός (cc)", data.DisplacementCc?.ToString());
        Text(right, "kw", "Ισχύς (kW)", data.PowerKw?.ToString("N2"));
        Date(right, "acquired", "Ημερομηνία απόκτησης", data.AcquisitionDate);
        Text(right, "color", "Χρώμα", data.Color);
        Text(right, "tonnage", "Τονάζ (τόνοι)", data.Tonnage?.ToString("N2"));
        Text(right, "wheelbase", "Μεταξόνιο (cm)", data.WheelbaseCm?.ToString("N2"));
        Text(right, "height", "Ύψος (cm)", data.HeightCm?.ToString("N2"));
        Text(right, "track", "Μετατρόχιο (cm)", data.TrackWidthCm?.ToString("N2"));
        Text(right, "market", "Εκτιμώμενη αξία (€)", data.EstimatedMarketValue?.ToString("N2"));
        Text(right, "co2", "Εκπομπές CO₂ (g/km)", data.Co2GramsPerKm?.ToString("N2"));
        Date(right, "nextkteo", "Επόμενο ΚΤΕΟ", data.NextKteoDate);
        _kteoStatusChoice = Choices(right, "Κατάσταση ΚΤΕΟ",
            ["Δεν απαιτείται", "Απαιτείται", "Σε ισχύ", "Ληγμένο"],
            data.KteoStatus ?? (data.NextKteoDate is null ? "Δεν απαιτείται" :
                data.NextKteoDate >= DateTime.Today ? "Σε ισχύ" : "Ληγμένο"));
        VehicleCheck(right, "alarm", "Συναγερμός", data.HasAlarm);
        VehicleCheck(right, "immobilizer", "Immobilizer", data.HasImmobilizer);
        VehicleCheck(right, "guarded", "Χώρος φύλαξης", data.IsGuarded);
        VehicleCheck(right, "emissions", "Κάρτα καυσαερίων", data.HasEmissionsCard);
        _statusChoice = Choices(right, "Κατάσταση", ["Ενεργό", "Ανενεργό"],
            _record.Status ?? "Ενεργό");
        right.Children.Add(CardFieldLayout.CreateRow("Καταχώριση",
            new TextBox { IsReadOnly = true, Text = _record.Id == Guid.Empty ? "—" :
                _record.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") }));
        right.Children.Add(CardFieldLayout.CreateRow("Τελευταία ενημέρωση",
            new TextBox { IsReadOnly = true, Text = _record.Id == Guid.Empty ? "—" :
                _record.UpdatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") }));
        AddKteoImageRow(right, data.KteoImagePath);
        var notes = Text(right, "notes", "Πρόσθετα στοιχεία", data.Notes ?? _record.Notes);
        notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.Height = 190;
        AddTab("Καρτέλα οχήματος", main);
        BuildPeopleTab();
        BuildVehiclePoliciesTab();
        BuildVehicleClaimsTab();
    }

    private string Value(string key) => _text[key].Text.Trim();
    private DateTime? DateValue(string key) => _dates[key].Date?.Date;
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
                    throw new InvalidOperationException("Χρησιμοποιήστε «Μετατροπή σε ασφαλιστήριο».");
                var data = new PolicyCardData
                {
                    PartnerId = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).PartnerId,
                    PartnerCommissionPercent = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).PartnerCommissionPercent,
                    OfficeCommissionPercent = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).OfficeCommissionPercent,
                    CommissionRuleId = BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson).CommissionRuleId,
                    ApplicationNumber = Value("application"), NoticeNumber = Value("notice"),
                    PolicyNumber = Value("policy"), Insurer = _insurerSuggest?.Text.Trim(),
                    Agency = Value("agency"), Branch = Value("branch"), Product = Value("product"),
                    VehicleId = (_vehicleChoice?.SelectedItem as Choice)?.Id,
                    StartDate = DateValue("start"), EndDate = DateValue("end"),
                    GrossPremium = DecimalValue(Value("gross"), "Μικτά ασφάλιστρα"),
                    NetPremium = DecimalValue(Value("net"), "Καθαρά ασφάλιστρα"),
                    CoverageNotes = Value("coverage")
                };
                if (data.StartDate > data.EndDate)
                    throw new ArgumentException("Η λήξη κάλυψης πρέπει να είναι μετά την έναρξη.");
                var number = type switch
                {
                    "Αίτηση" => data.ApplicationNumber,
                    "Ειδοποιητήριο" => data.NoticeNumber,
                    _ => data.PolicyNumber
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
                EnsurePrimaryOwner(customerId);
                ValidateVehiclePeople();
                var data = new VehicleCardData
                {
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
                UpdatePolicyFinanceSummary(BusinessRecordDetails.Read<PolicyCardData>(_record.DetailsJson));
            else
            {
                if (_newVehiclePolicyButton is not null) _newVehiclePolicyButton.IsEnabled = true;
                if (_addVehicleClaimButton is not null) _addVehicleClaimButton.IsEnabled = true;
            }
            if (_section == "policies" && _typeChoice is not null &&
                _record.DocumentType is "Αίτηση" or "Ειδοποιητήριο")
            {
                var selected = _record.DocumentType;
                _typeChoice.ItemsSource = new[] { "Αίτηση", "Ειδοποιητήριο" };
                _typeChoice.SelectedItem = selected;
            }
            if (_historyText is not null)
                _historyText.Text = $"Δημιουργία: {_record.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                    $"Τελευταία ενημέρωση: {_record.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
            _heading.Text = _section == "policies"
                ? $"{_record.DocumentType} · {_record.Title}" : _record.Title;
            _headerState.Text = $"● {_record.Status ?? "—"}";
            _headerCode.Text = "ΚΩΔΙΚΟΣ  " + (_record.Reference ?? "—");
            _conversionRequested = false;
            UpdateConvertButton();
            RecordSaved?.Invoke(this, EventArgs.Empty);
            _status.Text = "Η καρτέλα αποθηκεύτηκε.";
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
        if (_convertButton is null) return;
        _convertButton.Visibility = _record.Id != Guid.Empty &&
            _record.DocumentType is "Αίτηση" or "Ειδοποιητήριο"
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePolicyFinanceSummary(PolicyCardData data)
    {
        if (_policyFinanceSummary is null || _policyCommissionSummary is null) return;
        _policyFinanceSummary.Text = $"Μικτά: {data.GrossPremium?.ToString("N2") ?? "—"} €  ·  " +
            $"Καθαρά: {data.NetPremium?.ToString("N2") ?? "—"} €";
        if (data.OfficeCommissionPercent is decimal officeRate)
        {
            var officeAmount = decimal.Round((data.GrossPremium ?? 0) * officeRate / 100m, 2);
            _policyCommissionSummary.Text =
                $"Προμήθεια γραφείου: {officeRate:N2} % ({officeAmount:N2} €)  ·  " +
                $"Προς απόδοση στην ασφαλιστική: {((data.GrossPremium ?? 0) - officeAmount):N2} €";
        }
        else _policyCommissionSummary.Text = "Δεν έχει εφαρμοστεί κανόνας προμήθειας γραφείου.";
    }

    private async Task ConvertAsync()
    {
        var number = new TextBox { Header = "Αριθμός ασφαλιστηρίου *",
            PlaceholderText = "Συμπληρώστε τον αριθμό που εκδόθηκε" };
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = "Μετατροπή σε ασφαλιστήριο", Content = number,
            PrimaryButtonText = "Μετατροπή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(number.Text))
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Συμπληρώστε αριθμό ασφαλιστηρίου.");
            return;
        }
        _text["policy"].Text = number.Text.Trim();
        _typeChoice!.ItemsSource = new[] { "Αίτηση", "Ειδοποιητήριο", "Ασφαλιστήριο" };
        _typeChoice.SelectedItem = "Ασφαλιστήριο";
        _statusChoice!.SelectedItem = "Ενεργό";
        _conversionRequested = true;
        await SaveAsync();
    }
}

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
using WinRT.Interop;
using Windows.Graphics;
using Windows.UI;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

public sealed class BusinessRecordCardWindow : Window
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
    private readonly List<(Button Button, UIElement Content)> _tabItems = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heading = new() { FontSize = 25,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White) };
    private readonly Dictionary<string, TextBox> _text = [];
    private readonly Dictionary<string, CalendarDatePicker> _dates = [];
    private ComboBox? _customerChoice;
    private ComboBox? _vehicleChoice;
    private ComboBox? _typeChoice;
    private ComboBox? _statusChoice;
    private ActionButton? _convertButton;
    private TextBlock? _historyText;
    private bool _conversionRequested;
    private sealed record Choice(Guid? Id, string Name);

    public event EventHandler? RecordSaved;

    public BusinessRecordCardWindow(string section, Guid? customerId = null, Guid? recordId = null,
        nint ownerHandle = default)
    {
        if (section is not ("policies" or "vehicles"))
            throw new ArgumentException("Μη υποστηριζόμενη καρτέλα.");
        _section = section;
        _fixedCustomerId = customerId;
        _recordId = recordId;
        _ownerHandle = ownerHandle == default ? App.MainWindowHandle() : ownerHandle;
        _record = new BusinessRecord { SectionKey = section, Title = "",
            CustomerId = customerId, DocumentType = section == "policies" ? "Αίτηση" : null };
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        _root.Background = new SolidColorBrush(IsDarkTheme()
            ? Color.FromArgb(255, 31, 35, 42) : Color.FromArgb(255, 246, 249, 252));
        Content = _root;
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1220, Math.Max(660, area.Width - 32));
        var height = Math.Min(900, Math.Max(560, area.Height - 32));
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
            var vehicles = _section == "policies"
                ? (await _records.SearchAsync("vehicles")).ToArray()
                : [];
            var policies = _section == "vehicles" && _record.Id != Guid.Empty
                ? (await _records.SearchAsync("policies"))
                    .Where(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).VehicleId == _record.Id)
                    .ToArray()
                : [];
            var transactions = _section == "policies" && _record.CustomerId is Guid ownerId
                ? (await _customers.GetAsync(ownerId))?.Transactions
                    .Where(item => item.BusinessRecordId == _record.Id).ToArray() ?? []
                : [];
            BuildUi(customers, vehicles, policies, transactions);
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
        IReadOnlyList<CustomerTransaction> transactions)
    {
        _root.Children.Clear();
        var banner = new Border { Margin = new Thickness(14, 12, 14, 8),
            Padding = new Thickness(24, 14, 24, 14), CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(255, 17, 73, 130)) };
        _heading.Text = _section == "vehicles"
            ? (_record.Id == Guid.Empty ? "Νέο όχημα" : _record.Title)
            : (_record.Id == Guid.Empty ? "Νέα αίτηση ασφάλισης" :
                $"{_record.DocumentType ?? "Ασφαλιστήριο"} · {_record.Title}");
        var bannerText = new StackPanel { Spacing = 4 };
        bannerText.Children.Add(_heading);
        bannerText.Children.Add(new TextBlock { Text = _section == "vehicles"
            ? "Στοιχεία οχήματος και σύνδεση με τον ιδιοκτήτη"
            : "Αίτηση, ειδοποιητήριο και μετατροπή σε ασφαλιστήριο",
            Foreground = new SolidColorBrush(Colors.White) });
        banner.Child = bannerText;
        _root.Children.Add(banner);
        var strip = new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility =
            ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(14, 0, 14, 0) };
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
        if (_section == "policies") BuildPolicyTabs(customers, vehicles, transactions);
        else BuildVehicleTabs(customers, policies);
        SelectTab(0);
        UpdateConvertButton();
    }

    private void AddTab(string title, UIElement content)
    {
        var index = _tabItems.Count;
        var button = new Button { Content = title, MinHeight = 44,
            Padding = new Thickness(15, 7, 15, 7), Margin = new Thickness(0),
            CornerRadius = new CornerRadius(0), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            BorderThickness = new Thickness(0, 0, 1, 0) };
        button.Click += (_, _) => SelectTab(index);
        _tabs.Children.Add(button);
        _tabItems.Add((button, new ScrollViewer { Content = content,
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
            _tabItems[i].Button.Background = new SolidColorBrush(dark
                ? selected ? Color.FromArgb(255, 35, 77, 111) : Color.FromArgb(255, 54, 59, 66)
                : selected ? Color.FromArgb(255, 202, 228, 246) : Color.FromArgb(255, 226, 233, 239));
            _tabItems[i].Button.Foreground = new SolidColorBrush(dark
                ? Colors.White : selected ? Color.FromArgb(255, 16, 77, 126) :
                Color.FromArgb(255, 47, 57, 69));
        }
    }

    private bool IsDarkTheme() => _root.RequestedTheme == ElementTheme.Dark ||
        (_root.RequestedTheme == ElementTheme.Default &&
         Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);

    private static Grid TwoColumns(out StackPanel left, out StackPanel right)
    {
        var grid = new Grid { ColumnSpacing = 20, Margin = new Thickness(22, 16, 22, 20),
            MaxWidth = 1120, HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        left = new StackPanel { Spacing = 10 };
        right = new StackPanel { Spacing = 10 };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private TextBox Text(StackPanel panel, string key, string label, string? value = null)
    {
        var field = new TextBox { Header = label, Text = value ?? "",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(field);
        _text[key] = field;
        return field;
    }

    private CalendarDatePicker Date(StackPanel panel, string key, string label, DateTime? value = null)
    {
        var field = new CalendarDatePicker { Header = label,
            Date = value is DateTime date ? new DateTimeOffset(date) : null,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(field);
        _dates[key] = field;
        return field;
    }

    private ComboBox Choices(StackPanel panel, string label, IEnumerable<string> items, string? selected)
    {
        var field = new ComboBox { Header = label, ItemsSource = items.ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        field.SelectedItem = selected;
        panel.Children.Add(field);
        return field;
    }

    private ComboBox CustomerChoice(StackPanel panel, IReadOnlyList<CustomerListItem> customers)
    {
        var items = customers.Select(item => new Choice(item.Id, item.FullName)).ToArray();
        _customerChoice = new ComboBox { Header = "Πελάτης / ιδιοκτήτης *",
            ItemsSource = items, DisplayMemberPath = nameof(Choice.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = _fixedCustomerId is null };
        _customerChoice.SelectedItem = items.FirstOrDefault(item => item.Id == _record.CustomerId);
        panel.Children.Add(_customerChoice);
        return _customerChoice;
    }

    private void BuildPolicyTabs(IReadOnlyList<CustomerListItem> customers,
        IReadOnlyList<BusinessRecord> vehicles, IReadOnlyList<CustomerTransaction> transactions)
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
        Text(left, "insurer", "Ασφαλιστική εταιρεία", data.Insurer);
        Text(left, "agency", "Πρακτορείο", data.Agency);
        Text(left, "branch", "Κλάδος", data.Branch);
        Text(left, "product", "Προϊόν / πακέτο", data.Product);
        CustomerChoice(right, customers);
        Choice[] VehicleItems(Guid? customerId) =>
            [new Choice(null, "Χωρίς όχημα"),
             .. vehicles.Where(item => item.CustomerId == customerId)
                 .Select(item => new Choice(item.Id, item.Title))];
        var vehicleItems = VehicleItems(_record.CustomerId);
        _vehicleChoice = new ComboBox { Header = "Συνδεδεμένο όχημα",
            ItemsSource = vehicleItems, DisplayMemberPath = nameof(Choice.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _vehicleChoice.SelectedItem = vehicleItems.FirstOrDefault(item => item.Id == data.VehicleId) ?? vehicleItems[0];
        right.Children.Add(_vehicleChoice);
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
        var extra = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        var notes = Text(extra, "notes", "Σημειώσεις", _record.Notes);
        notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.Height = 150;
        AddTab("Πρόσθετα στοιχεία", extra);
        AddTab("Όχημα", new TextBlock { Text = data.VehicleId is null
            ? "Δεν έχει συνδεθεί όχημα."
            : vehicles.FirstOrDefault(item => item.Id == data.VehicleId)?.Title ?? "Το όχημα δεν βρέθηκε.",
            Margin = new Thickness(22), FontSize = 16 });
        var coverage = new StackPanel { Margin = new Thickness(22) };
        var cover = Text(coverage, "coverage", "Καλύψεις / όροι", data.CoverageNotes);
        cover.AcceptsReturn = true; cover.TextWrapping = TextWrapping.Wrap; cover.Height = 190;
        AddTab("Καλύψεις", coverage);
        var finance = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        finance.Children.Add(new TextBlock { Text =
            $"Μικτά: {data.GrossPremium?.ToString("N2") ?? "—"} €  ·  Καθαρά: {data.NetPremium?.ToString("N2") ?? "—"} €",
            FontSize = 16 });
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
        if (_record.Id != Guid.Empty)
        {
            data.Registration ??= _record.Title;
            if (data.Make is null && data.Model is null) data.Make = _record.Reference;
        }
        var main = TwoColumns(out var left, out var right);
        CustomerChoice(left, customers);
        Text(left, "registration", "Αριθμός κυκλοφορίας *", data.Registration);
        Text(left, "make", "Μάρκα / εργοστάσιο", data.Make);
        Text(left, "model", "Μοντέλο", data.Model);
        Text(left, "vin", "Αριθμός πλαισίου (VIN)", data.Vin);
        Text(left, "year", "Έτος κατασκευής", data.ManufactureYear?.ToString());
        Date(left, "first", "Πρώτη άδεια κυκλοφορίας", data.FirstRegistrationDate);
        Text(right, "use", "Χρήση", data.Use);
        Text(right, "fuel", "Καύσιμο", data.Fuel);
        Text(right, "cc", "Κυβισμός (cc)", data.DisplacementCc?.ToString());
        Text(right, "kw", "Ισχύς (kW)", data.PowerKw?.ToString("N2"));
        Date(right, "acquired", "Ημερομηνία απόκτησης", data.AcquisitionDate);
        Text(right, "color", "Χρώμα", data.Color);
        _statusChoice = Choices(right, "Κατάσταση", ["Ενεργό", "Ανενεργό"],
            _record.Status ?? "Ενεργό");
        AddTab("Καρτέλα οχήματος", main);
        var extra = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        var notes = Text(extra, "notes", "Πρόσθετες πληροφορίες", data.Notes ?? _record.Notes);
        notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.Height = 190;
        AddTab("Πρόσθετα στοιχεία", extra);
        AddTab("Ιδιοκτήτης", new TextBlock { Text = customers.FirstOrDefault(item =>
            item.Id == _record.CustomerId)?.FullName ?? "Επιλέξτε ιδιοκτήτη στην καρτέλα οχήματος.",
            Margin = new Thickness(22), FontSize = 16 });
        var policyPanel = new StackPanel { Margin = new Thickness(22), Spacing = 8 };
        policyPanel.Children.Add(new TextBlock { Text = "Αιτήσεις και ασφαλιστήρια του οχήματος",
            FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var linked = policies.Select(item => $"{item.DocumentType ?? "Ασφαλιστήριο"} · " +
            $"{item.Title} · {item.Status ?? "Χωρίς κατάσταση"}").ToArray();
        policyPanel.Children.Add(new ListView { ItemsSource = linked.Length > 0 ? linked :
            ["Δεν υπάρχουν συνδεδεμένες εγγραφές."], Height = 240 });
        AddTab("Συμβόλαια", policyPanel);
        AddTab("Ζημιές", new TextBlock { Text =
            "Οι σχετικές ζημιές καταχωρίζονται στην ενότητα Ζημιές.",
            Margin = new Thickness(22), TextWrapping = TextWrapping.Wrap });
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
                    ApplicationNumber = Value("application"), NoticeNumber = Value("notice"),
                    PolicyNumber = Value("policy"), Insurer = Value("insurer"),
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
                var data = new VehicleCardData
                {
                    Registration = plate, Make = Value("make"), Model = Value("model"),
                    Vin = Value("vin"), ManufactureYear = IntValue(Value("year"), "Έτος κατασκευής"),
                    FirstRegistrationDate = DateValue("first"), Use = Value("use"),
                    Fuel = Value("fuel"), DisplacementCc = IntValue(Value("cc"), "Κυβισμός"),
                    PowerKw = DecimalValue(Value("kw"), "Ισχύς"),
                    AcquisitionDate = DateValue("acquired"), Color = Value("color"),
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

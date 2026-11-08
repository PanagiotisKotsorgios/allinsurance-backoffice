using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Partners;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

public sealed partial class PartnerCardWindow : Window
{
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);

    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly IPartnerService _partners = App.Services.GetRequiredService<IPartnerService>();
    private readonly AllInsurance.Application.Customers.ICustomerService _customers = App.Services.GetRequiredService<AllInsurance.Application.Customers.ICustomerService>();
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly Guid? _id;
    private BusinessRecord _record = new() { SectionKey = "partners", Title = "" };
    private PartnerProfileData _profile = new();
    private PartnerSnapshot _snapshot = new([], [], 0, 0, []);
    private AllInsurance.Application.Customers.CustomerListItem[] _linkedCustomers = [];
    private bool _editing;
    private ActionButton? _saveButton;
    private ActionButton? _editButton;
    private readonly Grid _root = new() { RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(),
        new RowDefinition { Height = GridLength.Auto }
    } };
    private readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly ContentControl _tabContent = new();
    private readonly List<(Button Button, TextBlock Label, UIElement Content)> _tabs = [];
    private readonly Dictionary<string, TextBox> _text = [];
    private readonly Dictionary<string, ComboBox> _choice = [];
    private readonly Dictionary<string, EditableDateInput> _date = [];
    private readonly Dictionary<string, CheckBox> _check = [];
    private readonly Dictionary<string, AutoSuggestBox> _suggest = [];
    private readonly List<Grid> _formGrids = [];
    private double _contentWidth = 1100;
    private readonly TextBlock _heading = new() { FontSize = 25, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White), TextWrapping = TextWrapping.Wrap };
    private readonly CardHeaderCode _headerCode = new();
    private readonly TextBlock _headerState = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private string[] _categories = [];
    private string[] _cities = [];
    private string[] _businessTypes = [];
    private string[] _capacities = [];
    private string[] _paymentMethods = [];
    private string[] _genders = [];
    private string[] _taxOffices = [];
    private readonly List<PartnerBankAccount> _bankAccounts = [];
    private ComboBox? _bankChoice;
    private int _selectedTab;

    public event EventHandler? RecordSaved;

    public PartnerCardWindow(Guid? id = null, bool beginEditing = false)
    {
        _id = id;
        _editing = id is null || beginEditing;
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        _root.Background = new SolidColorBrush(IsDarkTheme()
            ? Color.FromArgb(255, 31, 35, 42)
            : App.Services.GetRequiredService<WorkspacePreferences>().EyeComfortEnabled
                ? Color.FromArgb(255, 228, 225, 216) : Color.FromArgb(255, 221, 228, 235));
        Content = _root;
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1260, Math.Max(650, area.Width - 32));
        var height = Math.Min(940, Math.Max(540, area.Height - 32));
        _contentWidth = Math.Min(1100, width - 70);
        _tabContent.SizeChanged += (_, args) =>
        {
            var target = Math.Min(1100, Math.Max(280, args.NewSize.Width - 50));
            foreach (var grid in _formGrids) grid.Width = target;
        };
        appWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        appWindow.Title = "Καρτέλα συνεργάτη — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        _root.Children.Add(new TextBlock { Text = "Φόρτωση καρτέλας συνεργάτη...",
            Margin = new Thickness(24) });
        _ = LoadAsync();
    }

    public void BringToFront()
    {
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", "partner-card");
        var handle = WindowNative.GetWindowHandle(this);
        SetWindowOwner(handle, -8, App.MainWindowHandle());
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
            if (_id is Guid id)
                _record = await _records.GetAsync(id) ?? throw new InvalidOperationException("Ο συνεργάτης δεν υπάρχει.");
            if (_record.SectionKey != "partners") throw new InvalidOperationException("Η εγγραφή δεν είναι συνεργάτης.");
            _profile = PartnerProfileData.Read(_record.DetailsJson);
            _bankAccounts.Clear();
            _bankAccounts.AddRange(_profile.BankAccounts.Select(item => new PartnerBankAccount
            { BankName = item.BankName, Iban = item.Iban, Bic = item.Bic }));
            var categories = await _filters.ChoicesAsync("Κατηγορίες συνεργατών");
            var cities = await _filters.ChoicesAsync("Πόλεις");
            var businesses = await _filters.ChoicesAsync("Τύποι επιχειρήσεων");
            var capacities = await _filters.ChoicesAsync("Επαγγελματικές ιδιότητες");
            var methods = await _filters.ChoicesAsync("Τρόποι πληρωμής");
            var genders = await _filters.ChoicesAsync("Φύλα");
            var offices = await _filters.ChoicesAsync("ΔΟΥ");
            _categories = [.. categories]; _cities = [.. cities];
            _businessTypes = [.. businesses]; _capacities = [.. capacities];
            _paymentMethods = [.. methods]; _genders = [.. genders]; _taxOffices = [.. offices];
            if (_record.Id != Guid.Empty) _snapshot = await _partners.GetSnapshotAsync(_record.Id);
            _linkedCustomers = (await _customers.SearchAsync()).Where(item =>
                string.Equals(item.PartnerName, _record.Title, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            BuildUi();
        }
        catch (Exception exception)
        {
            App.Services.GetRequiredService<ILogger<PartnerCardWindow>>()
                .LogError(exception, "Αποτυχία φόρτωσης καρτέλας συνεργάτη {PartnerId}", _id);
            _root.Children.Clear();
            _root.Children.Add(new TextBlock { Text = exception.Message, Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap });
        }
    }

    private bool IsDarkTheme() => _root.RequestedTheme == ElementTheme.Dark ||
        (_root.RequestedTheme == ElementTheme.Default &&
         Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);

    private void BuildUi()
    {
        _root.Children.Clear();
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var identity = new Grid { ColumnSpacing = 10, ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto }
        } };
        identity.Children.Add(new Image { Source = new BitmapImage(new Uri(
            "ms-appx:///Assets/Sections/partners.png")), Width = 42, Height = 42 });
        _heading.FontSize = 22;
        _heading.TextWrapping = TextWrapping.NoWrap;
        _heading.TextTrimming = TextTrimming.CharacterEllipsis;
        _heading.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_heading, 1); identity.Children.Add(_heading);
        _headerState.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_headerState, 2); identity.Children.Add(_headerState);
        Grid.SetColumn(_headerCode, 3); identity.Children.Add(_headerCode);
        _root.Children.Add(new Border { Background = gradient, CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 9, 18, 9), Margin = new Thickness(18, 8, 18, 4),
            Child = identity });
        UpdateHeader();
        var strip = new ScrollViewer { Content = _tabStrip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(18, 0, 18, 0) };
        Grid.SetRow(strip, 1); _root.Children.Add(strip);
        Grid.SetRow(_tabContent, 2); _root.Children.Add(_tabContent);
        var footer = new Grid { ColumnDefinitions = { new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto } }, Margin = new Thickness(18, 9, 18, 14) };
        var leftFooter = new StackPanel { Spacing = 5 };
        leftFooter.Children.Add(_status);
        var moneyActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center };
        var receipt = new ActionButton { Kind = "Receipt", Label = "Γενική είσπραξη",
            IsEnabled = _record.Id != Guid.Empty };
        receipt.Click += async (_, _) => await AddPayoutAsync("receipt");
        var payment = new ActionButton { Kind = "Payment", Label = "Γενική πληρωμή",
            IsEnabled = _record.Id != Guid.Empty };
        payment.Click += async (_, _) => await AddPayoutAsync("payment");
        moneyActions.Children.Add(receipt); moneyActions.Children.Add(payment);
        leftFooter.Children.Add(moneyActions);
        footer.Children.Add(leftFooter);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        _editButton = new ActionButton { Kind = "Edit", Label = "Επεξεργασία συνεργάτη" };
        _editButton.Click += (_, _) => { _editing = true; BuildTabs(preserve: true); UpdateEditState(); };
        actions.Children.Add(_editButton);
        _saveButton = new ActionButton { Kind = "Save", Label = "Αποθήκευση" };
        _saveButton.Click += async (_, _) => await SaveAsync();
        actions.Children.Add(_saveButton);
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        Grid.SetColumn(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(footer, 3); _root.Children.Add(footer);
        BuildTabs();
        UpdateEditState();
    }

    private void UpdateEditState()
    {
        if (_saveButton is not null) _saveButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        if (_editButton is not null) _editButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        _headerCode.SetEditing(_editing);
    }

    private void UpdateHeader()
    {
        _heading.Text = _record.Id == Guid.Empty ? "Νέος συνεργάτης" : _record.Title;
        _headerState.Text = $"● {_record.Status ?? "Ενεργός συνεργάτης"}";
        _headerCode.SetCode(_record.Reference);
    }

    private void AddTab(string label, UIElement content)
    {
        var index = _tabs.Count;
        var icon = label switch
        {
            "Καρτέλα συνεργάτη" => "user-round", "Πρόσθετα στοιχεία" => "settings-2",
            "Παραγωγή" or "Οικονομικά" or "Προμήθειες" => "chart-no-axes-combined",
            "Ιστορικό ενεργειών" => "clock-3", _ => "file-text"
        };
        var (button, title) = CardTabVisual.Create(label, icon, IsDarkTheme());
        button.Click += (_, _) => SelectTab(index);
        _tabStrip.Children.Add(button);
        _tabs.Add((button, title, new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }));
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _selectedTab = index;
        _tabContent.Content = _tabs[index].Content;
        var dark = IsDarkTheme();
        for (var i = 0; i < _tabs.Count; i++)
        {
            var selected = i == index;
            CardTabVisual.SetSelected(_tabs[i].Button, _tabs[i].Label, selected, dark);
        }
    }

    private Grid TwoColumns(out StackPanel left, out StackPanel right)
    {
        var grid = CardFieldLayout.TwoColumns(out left, out right, _contentWidth);
        left.Spacing = 5;
        right.Spacing = 5;
        _formGrids.Add(grid);
        return grid;
    }

    private TextBox Text(StackPanel panel, string key, string label, string? value, bool multiline = false)
    {
        var field = new TextBox { Text = value ?? "", MinHeight = multiline ? 80 : 40,
            Height = multiline ? 96 : 40, FontSize = 13, IsReadOnly = !_editing,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        if (multiline) { field.AcceptsReturn = true; field.TextWrapping = TextWrapping.Wrap; field.Height = 96; }
        panel.Children.Add(CardFieldLayout.CreateRow(label,
            multiline ? ResizableNoteField.Wrap(field) : field)); _text[key] = field;
        return field;
    }

    private ComboBox Choice(StackPanel panel, string key, string label,
        IEnumerable<string> options, string? value)
    {
        var choices = options.Prepend("").Concat(value is null ? [] : [value])
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        var field = new ComboBox { ItemsSource = choices,
            SelectedItem = value ?? "", MinHeight = 40, Height = 40,
            IsHitTestVisible = _editing, IsTabStop = _editing,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var host = new Grid { ColumnSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } } };
        host.Children.Add(field);
        if (_editing && (choices.Length > 12 || key is "capacity" or "taxoffice" or
            "business" or "category"))
        {
            field.Visibility = Visibility.Collapsed;
            host.Children.Add(CatalogSearchButton.CreateInline(field, label));
        }
        panel.Children.Add(CardFieldLayout.CreateRow(label, host)); _choice[key] = field;
        return field;
    }

    private EditableDateInput Date(StackPanel panel, string key, string label, DateTime? value)
    {
        var field = new EditableDateInput {
            Date = value is DateTime date ? new DateTimeOffset(date) : null,
            MinHeight = 40, Height = 40, IsHitTestVisible = _editing, IsTabStop = _editing,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(CardFieldLayout.CreateRow(label, field)); _date[key] = field;
        return field;
    }

    private CheckBox Check(StackPanel panel, string key, string label, bool value)
    {
        var field = new CheckBox { IsChecked = value, MinHeight = 40, Height = 40, IsEnabled = _editing };
        if (_editing) panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        else
        {
            var mark = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(value
                    ? StatusPalette.Positive : StatusPalette.Neutral),
                Child = new TextBlock { Text = value ? "✓" : "",
                    Foreground = new SolidColorBrush(Colors.White), FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center } };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mark,
                label + (value ? ": Ναι" : ": Όχι"));
            panel.Children.Add(CardFieldLayout.CreateRow(label, mark));
        }
        _check[key] = field;
        return field;
    }

    private AutoSuggestBox Suggest(StackPanel panel, string key, string label,
        string? value, string[] options)
    {
        var field = new AutoSuggestBox { Text = value ?? "", MinHeight = 40, Height = 40,
            IsHitTestVisible = _editing, IsTabStop = _editing,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Πληκτρολογήστε για αναζήτηση" };
        void UpdateSuggestions(bool showAll = false)
        {
            field.ItemsSource = SearchSuggestions.FirstHundred(options, showAll ? "" : field.Text);
            field.IsSuggestionListOpen = field.ItemsSource is string[] { Length: > 0 };
        }
        field.GotFocus += (_, _) => UpdateSuggestions(showAll: true);
        field.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            UpdateSuggestions();
        };
        field.SuggestionChosen += (_, args) => field.Text = args.SelectedItem as string ?? field.Text;
        panel.Children.Add(CardFieldLayout.CreateRow(label, field)); _suggest[key] = field;
        return field;
    }

    private string Val(string key) => _text[key].Text.Trim();
    private string? Optional(string key) => string.IsNullOrWhiteSpace(Val(key)) ? null : Val(key);
    private string? Selected(string key) => _choice[key].SelectedItem as string is { Length: > 0 } value ? value : null;
    private DateTime? Day(string key) => _date[key].GetValidatedDate(key);
    private bool Checked(string key) => _check[key].IsChecked == true;
    private string? Suggested(string key) => string.IsNullOrWhiteSpace(_suggest[key].Text)
        ? null : _suggest[key].Text.Trim();

    private static decimal? Money(string text, string name, decimal maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var value)
            && value >= 0 && value <= maximum) return value;
        throw new ArgumentException($"Το πεδίο «{name}» πρέπει να είναι από 0 έως {maximum:N2}.");
    }

    private async Task SaveAsync()
    {
        try
        {
            var profile = ReadProfile();
            if (profile.TaxNumber is null || profile.TaxNumber.Length != 9 ||
                !profile.TaxNumber.All(char.IsAsciiDigit))
                throw new ArgumentException("Συμπληρώστε έγκυρο ΑΦΜ 9 ψηφίων.");
            if (profile.PersonType == "Νομικό πρόσωπο" && string.IsNullOrWhiteSpace(profile.CompanyName))
                throw new ArgumentException("Η επωνυμία είναι υποχρεωτική για νομικό πρόσωπο.");
            if (profile.PersonType != "Νομικό πρόσωπο" &&
                (string.IsNullOrWhiteSpace(profile.FirstName) || string.IsNullOrWhiteSpace(profile.LastName)))
                throw new ArgumentException("Το όνομα και το επώνυμο είναι υποχρεωτικά.");
            if (profile.StartDate > profile.EndDate)
                throw new ArgumentException("Η λήξη συνεργασίας πρέπει να είναι μετά την έναρξη.");
            _record.Title = profile.PersonType == "Νομικό πρόσωπο" ? profile.CompanyName! :
                $"{profile.LastName} {profile.FirstName}".Trim();
            _record.Reference = _headerCode.EnteredCode.Length > 0 ? _headerCode.EnteredCode :
                _record.Id == Guid.Empty ? "P-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() :
                _record.Reference;
            _record.Status = Selected("status") ?? "Ενεργός";
            _record.RelatedParty = profile.City;
            _record.EventDate = profile.StartDate;
            _record.Notes = profile.Notes;
            _record.DetailsJson = profile.Write();
            var id = await _records.SaveAsync(_record);
            _record = await _records.GetAsync(id) ?? _record;
            _profile = profile;
            _snapshot = await _partners.GetSnapshotAsync(id);
            _linkedCustomers = (await _customers.SearchAsync()).Where(item =>
                string.Equals(item.PartnerName, _record.Title, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            _editing = false;
            UpdateHeader();
            BuildTabs(preserve: true);
            UpdateEditState();
            RecordSaved?.Invoke(this, EventArgs.Empty);
            _status.Text = "Η καρτέλα συνεργάτη αποθηκεύτηκε.";
            await UiAlerts.ShowSuccessAsync(_root.XamlRoot, "Η εργασία σας αποθηκεύτηκε επιτυχώς.");
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private PartnerProfileData ReadProfile() => new()
    {
        FirstName = Optional("first"), LastName = Optional("last"), CompanyName = Optional("company"),
        TaxNumber = Optional("tax"), TaxOffice = Selected("taxoffice"),
        PersonType = Selected("person"), BusinessType = Selected("business"),
        ProfessionalCapacity = Selected("capacity"), IdentityNumber = Optional("identity"),
        GemiNumber = Optional("gemi"),
        CooperationManager = Optional("manager"), Category = Selected("category"),
        IsPotential = Selected("status") == "Πιθανός", IsSuspended = Selected("status") == "Αναστολή",
        HasEnded = Selected("status") == "Λήξη συνεργασίας", HasActiveContract = Checked("contractactive"),
        Email = Optional("email"), Phone = Optional("phone"), Address = Optional("address"),
        City = Suggested("city"), PostalCode = Optional("postal"),
        FatherName = Optional("father"), MotherName = Optional("mother"), Gender = Selected("gender"),
        BirthDate = Day("birth"), NameDay = Day("nameday"), BirthPlace = Optional("birthplace"),
        Nationality = Optional("nationality"), PassportNumber = Optional("passport"),
        Education = Optional("education"),
        RegistryNumber = Optional("registry"), RegistryExpiry = Day("registryexpiry"),
        Chamber = Optional("chamber"), SecondaryEmail = Optional("email2"), ThirdEmail = Optional("email3"),
        SecondaryPhone = Optional("phone2"), Mobile = Optional("mobile"),
        SecondaryMobile = Optional("mobile2"), ThirdMobile = Optional("mobile3"), Website = Optional("website"),
        WorkAddress = Optional("workaddress"), WorkCity = Suggested("workcity"),
        WorkPostalCode = Optional("workpostal"), WorkPhone = Optional("workphone"),
        WorkEmail = Optional("workemail"), ContractNumber = Optional("contract"),
        StartDate = Day("start"), EndDate = Day("end"),
        CreditLimit = Money(Val("credit"), "Πιστωτικό όριο", 999_999_999m),
        DefaultCommissionPercent = _profile.DefaultCommissionPercent,
        BankName = _bankAccounts.FirstOrDefault()?.BankName,
        Iban = _bankAccounts.FirstOrDefault()?.Iban,
        BankAccounts = _bankAccounts.Select(item => new PartnerBankAccount
        { BankName = item.BankName, Iban = item.Iban, Bic = item.Bic }).ToList(),
        PaymentMethod = Selected("paymentmethod"),
        Notes = Optional("notes")
    };
}

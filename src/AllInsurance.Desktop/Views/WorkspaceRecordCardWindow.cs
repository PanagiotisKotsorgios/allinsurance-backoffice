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
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspaceRecordCardWindow : Window
{
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);

    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly ICustomerService _customerService = App.Services.GetRequiredService<ICustomerService>();
    private readonly WorkspaceSection _section;
    private readonly Guid? _recordId;
    private readonly Guid? _initialVehicleId;
    private readonly Guid? _initialPolicyId;
    private readonly nint _ownerHandle;
    private BusinessRecord _record;
    private WorkspaceCardData _data = new();
    private CustomerListItem[] _customers = [];
    private readonly Dictionary<string, BusinessRecord[]> _all = [];
    private readonly Grid _root = new() { RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(),
        new RowDefinition { Height = GridLength.Auto }
    } };
    private readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal };
    private readonly ContentControl _tabContent = new();
    private readonly List<(Button Button, TextBlock Label, UIElement Content)> _tabs = [];
    private readonly Dictionary<string, FrameworkElement> _inputs = [];
    private readonly Dictionary<string, Border> _booleanBadges = [];
    private readonly Dictionary<string, FieldSpec> _specs = [];
    private readonly List<Grid> _forms = [];
    private readonly TextBlock _heading = new() { FontSize = 25,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _subtitle = new() { Foreground = new SolidColorBrush(Colors.White),
        TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _metadata = new() { Foreground = new SolidColorBrush(Colors.White),
        FontSize = 12, Opacity = 0.86 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private ActionButton? _editButton;
    private ActionButton? _saveButton;
    private Window? _child;
    private bool _editing;
    private bool _building;
    private int _selectedTab;
    private double _contentWidth;

    public event EventHandler? RecordSaved;

    public WorkspaceRecordCardWindow(string sectionKey, Guid? recordId = null,
        bool beginEditing = false, Guid? customerId = null, Guid? vehicleId = null,
        Guid? policyId = null,
        nint ownerHandle = default)
    {
        _section = WorkspaceSections.All.FirstOrDefault(item => item.Key == sectionKey)
            ?? throw new ArgumentException("Άγνωστη ενότητα.");
        if (sectionKey is not ("companies" or "agencies" or "claims" or "updates" or "reports" or "bridges"))
            throw new ArgumentException("Η ενότητα έχει διαφορετική καρτέλα.");
        _recordId = recordId;
        _initialVehicleId = vehicleId;
        _initialPolicyId = policyId;
        _editing = recordId is null || beginEditing;
        _ownerHandle = ownerHandle == default ? App.MainWindowHandle() : ownerHandle;
        _record = new BusinessRecord { SectionKey = sectionKey, Title = "",
            CustomerId = customerId, Status = sectionKey == "claims" ? "Δηλωμένη" : "Ενεργό" };
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        _root.Background = new SolidColorBrush(IsDarkTheme()
            ? Color.FromArgb(255, 31, 35, 42) : Color.FromArgb(255, 221, 228, 235));
        Content = _root;
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1260, Math.Max(650, area.Width - 32));
        var height = Math.Min(930, Math.Max(540, area.Height - 32));
        _contentWidth = Math.Min(1100, width - 70);
        appWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        appWindow.Title = $"Καρτέλα {Singular()} — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        _tabContent.SizeChanged += (_, args) =>
        {
            var target = Math.Min(1100, Math.Max(600, args.NewSize.Width - 50));
            foreach (var form in _forms) form.Width = target;
        };
        _root.Children.Add(new TextBlock { Text = "Φόρτωση καρτέλας...", Margin = new Thickness(24) });
        _ = LoadAsync();
    }

    public void BringToFront()
    {
        AllInsurance.Desktop.Logging.UiAuditRecorder.Record("VIEW", _section.Key + "-card");
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
            if (_record.SectionKey != _section.Key)
                throw new InvalidOperationException("Η εγγραφή ανήκει σε άλλη ενότητα.");
            _data = WorkspaceCardData.Read(_record.DetailsJson);
            if (_record.Id == Guid.Empty)
            {
                _data.VehicleId ??= _initialVehicleId;
                _data.PolicyId ??= _initialPolicyId;
            }
            _customers = (await _customerService.SearchAsync()).ToArray();
            foreach (var section in new[] { "companies", "agencies", "partners", "policies",
                "vehicles", "claims", "updates", "bridges" })
                _all[section] = (await _records.SearchAsync(section)).ToArray();
            BuildUi();
        }
        catch (Exception exception)
        {
            _root.Children.Clear();
            _root.Children.Add(new TextBlock { Text = exception.Message,
                Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap });
        }
    }

    private void BuildUi()
    {
        _root.Children.Clear();
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        identity.Children.Add(new Image { Source = new BitmapImage(new Uri(_section.Icon)),
            Width = 54, Height = 54 });
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(_heading); text.Children.Add(_subtitle);
        identity.Children.Add(text);
        var banner = new StackPanel { Spacing = 5 };
        banner.Children.Add(identity);
        _metadata.Margin = new Thickness(68, 0, 0, 0);
        banner.Children.Add(_metadata);
        _root.Children.Add(new Border { Child = banner, Background = gradient,
            CornerRadius = new CornerRadius(16), Padding = new Thickness(24, 12, 24, 12),
            Margin = new Thickness(18, 8, 18, 4) });
        UpdateHeader();
        var tabScroll = new ScrollViewer { Content = _tabStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(18, 0, 18, 0) };
        Grid.SetRow(tabScroll, 1); _root.Children.Add(tabScroll);
        Grid.SetRow(_tabContent, 2); _root.Children.Add(_tabContent);
        var footer = new Grid { ColumnDefinitions = { new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto } },
            Margin = new Thickness(18, 9, 18, 14) };
        footer.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _editButton = new ActionButton { Kind = "Edit", Label = "Επεξεργασία " + Singular() };
        _editButton.Click += (_, _) => { _editing = true; ApplyEditMode(); };
        buttons.Children.Add(_editButton);
        _saveButton = new ActionButton { Kind = "Save", Label = "Αποθήκευση" };
        _saveButton.Click += async (_, _) => await SaveAsync();
        buttons.Children.Add(_saveButton);
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close();
        buttons.Children.Add(close);
        Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
        Grid.SetRow(footer, 3); _root.Children.Add(footer);
        BuildTabs();
        ApplyEditMode();
    }

    private void UpdateHeader()
    {
        _heading.Text = _record.Id == Guid.Empty ? "Νέα " + Singular() : _record.Title;
        _subtitle.Text = (_record.Reference ?? (_record.Id == Guid.Empty ? "Νέα καταχώριση" : "Χωρίς κωδικό")) +
            "   ·   " + (_record.Status ?? "Χωρίς κατάσταση");
        _metadata.Text = _record.Id == Guid.Empty ? "Η καταχώριση θα χρονοσημανθεί στην αποθήκευση." :
            $"Καταχώριση: {_record.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}   ·   " +
            $"Ενημέρωση: {_record.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
    }

    private string Singular() => _section.Key switch
    {
        "companies" => "εταιρείας", "agencies" => "πρακτορείου", "claims" => "ζημιάς",
        "updates" => "ενημέρωσης", "reports" => "αναφοράς", "bridges" => "γέφυρας",
        _ => "εγγραφής"
    };

    private bool IsDarkTheme() => _root.RequestedTheme == ElementTheme.Dark ||
        (_root.RequestedTheme == ElementTheme.Default &&
         Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
}

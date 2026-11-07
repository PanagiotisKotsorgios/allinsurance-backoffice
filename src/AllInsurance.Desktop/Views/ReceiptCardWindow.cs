using AllInsurance.Application.Abstractions;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

public sealed class ReceiptCardWindow : Window
{
    private static readonly Dictionary<Guid, ReceiptCardWindow> OpenCards = [];
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y,
        int width, int height, uint flags);

    private readonly Guid _transactionId;
    private readonly nint _ownerHandle;
    private readonly IApplicationDbContextFactory _factory =
        App.Services.GetRequiredService<IApplicationDbContextFactory>();
    private readonly Grid _root = new() { RowDefinitions =
    {
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(),
        new RowDefinition { Height = GridLength.Auto }
    } };
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal,
        Margin = new Thickness(20, 0, 20, 0) };
    private readonly ContentControl _tabContent = new();
    private readonly TextBlock _title = new() { FontSize = 24,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _subtitle = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _amount = new() { FontSize = 22,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Colors.White) };
    private readonly ActionButton _pdf = new() { Kind = "File", Label = "Εξαγωγή απόδειξης PDF" };
    private readonly ActionButton _customerButton = new() { Kind = "Open", Label = "Καρτέλα πελάτη" };
    private readonly ActionButton _policyButton = new() { Kind = "Open", Label = "Ασφαλιστήριο" };
    private CustomerTransaction? _transaction;
    private string _actor = "—";
    private CustomerCardWindow? _linkedCustomer;
    private BusinessRecordCardWindow? _linkedPolicy;

    public static void Open(Guid transactionId, nint ownerHandle = default)
    {
        if (OpenCards.TryGetValue(transactionId, out var existing))
        {
            existing.BringToFront();
            return;
        }
        var card = new ReceiptCardWindow(transactionId, ownerHandle);
        OpenCards.Add(transactionId, card);
        card.Closed += (_, _) => OpenCards.Remove(transactionId);
        card.BringToFront();
    }

    private ReceiptCardWindow(Guid transactionId, nint ownerHandle)
    {
        _transactionId = transactionId;
        _ownerHandle = ownerHandle == default ? App.MainWindowHandle() : ownerHandle;
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        _root.RequestedTheme = preferences.Theme;
        _root.Background = new SolidColorBrush(preferences.Theme == ElementTheme.Dark
            ? Color.FromArgb(255, 31, 35, 42)
            : preferences.EyeComfortEnabled ? Color.FromArgb(255, 228, 225, 216)
            : Color.FromArgb(255, 221, 228, 235));
        Content = _root;

        var banner = new Grid { Margin = new Thickness(18, 12, 18, 10),
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var labels = new StackPanel { Spacing = 2 };
        labels.Children.Add(_title);
        labels.Children.Add(_subtitle);
        banner.Children.Add(labels);
        Grid.SetColumn(_amount, 1);
        _amount.VerticalAlignment = VerticalAlignment.Center;
        banner.Children.Add(_amount);
        _root.Children.Add(new Border { Child = banner, Background = gradient,
            CornerRadius = new CornerRadius(14), Margin = new Thickness(16, 12, 16, 8) });
        Grid.SetRow(_tabs, 1);
        _root.Children.Add(_tabs);
        Grid.SetRow(_tabContent, 2);
        _root.Children.Add(_tabContent);

        var footer = new Grid { Margin = new Thickness(20, 10, 20, 18),
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        links.Children.Add(_customerButton);
        links.Children.Add(_policyButton);
        footer.Children.Add(links);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_pdf);
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close();
        actions.Children.Add(close);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 3);
        _root.Children.Add(footer);
        _customerButton.Click += (_, _) => OpenCustomer();
        _policyButton.Click += (_, _) => OpenPolicy();
        _pdf.Click += async (_, _) => await ExportPdfAsync();
        _customerButton.IsEnabled = false;
        _policyButton.IsEnabled = false;
        _pdf.IsEnabled = false;

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1050, Math.Max(740, area.Width - 42));
        var height = Math.Min(760, Math.Max(520, area.Height - 42));
        appWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        appWindow.Title = "Καρτέλα είσπραξης — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        _ = LoadAsync();
    }

    public void BringToFront()
    {
        var handle = WindowNative.GetWindowHandle(this);
        if (_ownerHandle != 0) SetWindowOwner(handle, -8, _ownerHandle);
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
            await using var db = await _factory.CreateDbContextAsync();
            _transaction = await db.CustomerTransactions.AsNoTracking()
                .Include(item => item.Customer).Include(item => item.BusinessRecord)
                .SingleOrDefaultAsync(item => item.Id == _transactionId)
                ?? throw new InvalidOperationException("Η οικονομική κίνηση δεν βρέθηκε.");
            _actor = _transaction.CreatedByUserId is Guid userId
                ? await db.Users.AsNoTracking().Where(item => item.Id == userId)
                    .Select(item => item.DisplayName).SingleOrDefaultAsync() ?? "—"
                : "—";
            _title.Text = _transaction.Type == "receipt" ? "Καρτέλα είσπραξης" : "Καρτέλα πληρωμής";
            AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(
                WindowNative.GetWindowHandle(this))).Title = _title.Text + " — AllInsurance Backoffice Software";
            _subtitle.Text = $"Αρ. {_transaction.Id.ToString("N")[..12].ToUpperInvariant()}  ·  " +
                _transaction.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            _amount.Text = $"{_transaction.Amount:N2} {_transaction.Currency}";
            _customerButton.IsEnabled = true;
            _policyButton.IsEnabled = _transaction.BusinessRecordId.HasValue;
            _pdf.IsEnabled = _transaction.Type == "receipt";
            BuildTabs();
        }
        catch (Exception error)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, error.Message);
        }
    }

    private void BuildTabs()
    {
        if (_transaction is null) return;
        var data = _transaction;
        var form = CardFieldLayout.TwoColumns(out var left, out var right, 900, preserveColumns: true);
        left.Spacing = 8;
        right.Spacing = 8;
        _root.SizeChanged += (_, args) =>
            form.Width = Math.Min(900, Math.Max(700, args.NewSize.Width - 50));
        void Field(StackPanel column, string label, string? value, bool multiline = false)
        {
            var input = new TextBox { Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                IsReadOnly = true, IsTabStop = false, FontSize = 14,
                Height = multiline ? 96 : 40, MinHeight = multiline ? 96 : 40,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                AcceptsReturn = multiline, VerticalContentAlignment = VerticalAlignment.Center };
            column.Children.Add(CardFieldLayout.CreateRow(label, input));
        }
        Field(left, "Αριθμός κίνησης", data.Id.ToString("N").ToUpperInvariant());
        Field(left, "Είδος", data.Type == "receipt" ? "Είσπραξη" : "Πληρωμή");
        Field(left, "Ημερομηνία", data.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        Field(left, "Πελάτης", $"{data.Customer.LastName} {data.Customer.FirstName}".Trim());
        Field(left, "Ασφαλιστήριο", data.BusinessRecord?.Title);
        Field(right, "Ποσό", data.Amount.ToString("N2"));
        Field(right, "Νόμισμα", data.Currency);
        Field(right, "Τρόπος πληρωμής", data.PaymentMethod);
        Field(right, "Αριθμός αναφοράς", data.Reference);
        Field(right, "Καταχωρίστηκε από", _actor);
        var content = new StackPanel { Spacing = 9 };
        content.Children.Add(form);
        Field(content, "Αιτιολογία", data.Description, multiline: true);
        var history = new StackPanel { Margin = new Thickness(24, 16, 24, 16), Spacing = 12 };
        history.Children.Add(new TextBlock { Text = "Ιστορικό ενεργειών", FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        history.Children.Add(new TextBlock { Text =
            $"{data.OccurredAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {_actor}  ·  " +
            (data.Type == "receipt" ? "Καταχώριση είσπραξης" : "Καταχώριση πληρωμής"),
            TextWrapping = TextWrapping.Wrap });
        history.Children.Add(new TextBlock { Text = data.Description ?? "Χωρίς επιπλέον αιτιολογία.",
            TextWrapping = TextWrapping.Wrap });
        AddTab("Καρτέλα κίνησης", "file-text", content);
        AddTab("Ιστορικό ενεργειών", "clock-3", history);
        SelectTab(0);
    }

    private readonly List<(Button Button, TextBlock Label, UIElement Content)> _tabItems = [];
    private void AddTab(string title, string icon, UIElement content)
    {
        var index = _tabItems.Count;
        var (button, label) = CardTabVisual.Create(title, icon, _root.ActualTheme == ElementTheme.Dark);
        button.Click += (_, _) => SelectTab(index);
        _tabs.Children.Add(button);
        _tabItems.Add((button, label, new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }));
    }

    private void SelectTab(int index)
    {
        _tabContent.Content = _tabItems[index].Content;
        for (var position = 0; position < _tabItems.Count; position++)
            CardTabVisual.SetSelected(_tabItems[position].Button, _tabItems[position].Label,
                position == index, _root.ActualTheme == ElementTheme.Dark);
    }

    private void OpenCustomer()
    {
        if (_transaction is null) return;
        if (_linkedCustomer is not null) { _linkedCustomer.BringToFront(); return; }
        _linkedCustomer = new CustomerCardWindow(_transaction.CustomerId);
        _linkedCustomer.Closed += (_, _) => { _linkedCustomer = null; BringToFront(); };
        _linkedCustomer.BringToFront();
    }

    private void OpenPolicy()
    {
        if (_transaction?.BusinessRecordId is not Guid policyId) return;
        if (_linkedPolicy is not null) { _linkedPolicy.BringToFront(); return; }
        _linkedPolicy = new BusinessRecordCardWindow("policies", recordId: policyId,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _linkedPolicy.Closed += (_, _) => { _linkedPolicy = null; BringToFront(); };
        _linkedPolicy.BringToFront();
    }

    private async Task ExportPdfAsync()
    {
        if (_transaction is not { Type: "receipt" } receipt) return;
        try
        {
            if (await ReceiptPdfExporter.ExportAsync(this, receipt,
                $"{receipt.Customer.LastName} {receipt.Customer.FirstName}".Trim(),
                receipt.BusinessRecord?.Title))
                await UiAlerts.ShowSuccessAsync(_root.XamlRoot, "Η απόδειξη PDF εξήχθη επιτυχώς.");
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, error.Message); }
    }
}

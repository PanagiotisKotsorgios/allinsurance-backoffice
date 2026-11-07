using AllInsurance.Application.OfficeOperations;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.Graphics;
using Windows.UI;
using System.IO;
using System.Runtime.InteropServices;

namespace AllInsurance.Desktop.Views;

/// <summary>Read-only card for partner, organization and office movements.</summary>
internal sealed class FinancialMovementCardWindow : Window
{
    private static readonly Dictionary<Guid, FinancialMovementCardWindow> OpenCards = [];
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowOwner(nint window, int index, nint owner);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);
    private readonly FinancialMovement _movement;
    private Window? _relatedCard;
    private BusinessRecordCardWindow? _linkedPolicy;

    public static void Open(FinancialMovement movement)
    {
        if (OpenCards.TryGetValue(movement.Id, out var existing))
        {
            existing.BringToFront();
            return;
        }
        var card = new FinancialMovementCardWindow(movement);
        OpenCards[movement.Id] = card;
        card.Closed += (_, _) => OpenCards.Remove(movement.Id);
        card.BringToFront();
    }

    private FinancialMovementCardWindow(FinancialMovement movement)
    {
        _movement = movement;
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        var dark = preferences.Theme == ElementTheme.Dark || preferences.Theme == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark;
        var root = new Grid { RequestedTheme = preferences.Theme,
            Background = new SolidColorBrush(dark ? Color.FromArgb(255, 31, 35, 42) :
                preferences.EyeComfortEnabled ? Color.FromArgb(255, 228, 225, 216) :
                Color.FromArgb(255, 221, 228, 235)) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        var title = movement.Direction == "receipt" ? "Καρτέλα είσπραξης" : "Καρτέλα πληρωμής";
        var banner = new Border { Margin = new Thickness(16, 12, 16, 10),
            Padding = new Thickness(18, 14, 18, 14), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(255, 20, 85, 134)),
            Child = new StackPanel { Spacing = 3, Children =
            {
                new TextBlock { Text = title, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White) },
                new TextBlock { Text = $"{movement.Amount:N2} {movement.Currency}  ·  " +
                    movement.OccurredOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Foreground = new SolidColorBrush(Colors.White) }
            } } };
        root.Children.Add(banner);
        var content = new StackPanel { Margin = new Thickness(22, 6, 22, 10), Spacing = 10 };
        var fields = CardFieldLayout.TwoColumns(out var left, out var right, 850, preserveColumns: true);
        root.SizeChanged += (_, args) => fields.Width = Math.Min(850, Math.Max(640, args.NewSize.Width - 54));
        left.Spacing = 8; right.Spacing = 8;
        Field(left, "Αριθμός κίνησης", movement.Id.ToString("N").ToUpperInvariant());
        Field(left, "Είδος", movement.Direction == "receipt" ? "Είσπραξη" : "Πληρωμή");
        Field(left, "Ημερομηνία", movement.OccurredOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        Field(left, "Πρόσωπο / φορέας", $"{movement.PartyType}: {movement.Party}");
        Field(left, "Ασφαλιστήριο", movement.Policy);
        Field(right, "Ποσό", movement.Amount.ToString("N2"));
        Field(right, "Νόμισμα", movement.Currency);
        Field(right, "Τρόπος πληρωμής", movement.Method);
        Field(right, "Αριθμός αναφοράς", movement.Reference);
        Field(right, "Πηγή καταχώρισης", movement.Source);
        content.Children.Add(fields);
        Field(content, "Αιτιολογία", movement.Description, multiline: true);
        var history = new TextBlock { Text = $"Η κίνηση καταχωρίστηκε στις " +
            $"{movement.OccurredOn.ToLocalTime():dd/MM/yyyy HH:mm}.\n" +
            $"Αναφορά: {movement.Reference ?? "—"}", TextWrapping = TextWrapping.Wrap };
        content.Children.Add(new TextBlock { Text = "Ιστορικό καταχώρισης", FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(history);
        var scroller = new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroller, 1); root.Children.Add(scroller);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(18, 10, 18, 16) };
        if (movement.PolicyId.HasValue)
        {
            var policy = new ActionButton { Kind = "Open", Label = "Ασφαλιστήριο" };
            policy.Click += (_, _) => OpenPolicy();
            footer.Children.Add(policy);
        }
        if (movement.PartyId.HasValue && movement.PartyType != "Γραφείο")
        {
            var open = new ActionButton { Kind = "Open", Label = "Καρτέλα " + movement.PartyType.ToLowerInvariant() };
            open.Click += (_, _) => OpenParty();
            footer.Children.Add(open);
        }
        var close = new ActionButton { Kind = "Close", Label = "Κλείσιμο" };
        close.Click += (_, _) => Close(); footer.Children.Add(close);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(980, Math.Max(720, area.Width - 42));
        var height = Math.Min(720, Math.Max(520, area.Height - 42));
        appWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        appWindow.Title = title + " — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
    }

    private static void Field(StackPanel column, string label, string? value, bool multiline = false)
    {
        var box = new TextBox { Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            IsReadOnly = true, IsTabStop = false, Height = multiline ? 92 : 40,
            MinHeight = multiline ? 92 : 40, AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalContentAlignment = VerticalAlignment.Center };
        column.Children.Add(CardFieldLayout.CreateRow(label, box));
    }

    private void OpenParty()
    {
        if (_movement.PartyId is not Guid id) return;
        if (_relatedCard is PartnerCardWindow partner) { partner.BringToFront(); return; }
        if (_relatedCard is WorkspaceRecordCardWindow organization) { organization.BringToFront(); return; }
        if (_movement.PartyType == "Συνεργάτης")
        {
            var card = new PartnerCardWindow(id);
            card.Closed += (_, _) => _relatedCard = null;
            _relatedCard = card;
            card.BringToFront();
        }
        else
        {
            var section = _movement.PartyType == "Πρακτορείο" ? "agencies" : "companies";
            var card = new WorkspaceRecordCardWindow(section, id);
            card.Closed += (_, _) => _relatedCard = null;
            _relatedCard = card;
            card.BringToFront();
        }
    }

    private void OpenPolicy()
    {
        if (_movement.PolicyId is not Guid id) return;
        if (_linkedPolicy is not null) { _linkedPolicy.BringToFront(); return; }
        _linkedPolicy = new BusinessRecordCardWindow("policies", recordId: id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _linkedPolicy.Closed += (_, _) => _linkedPolicy = null;
        _linkedPolicy.BringToFront();
    }

    private void BringToFront()
    {
        var handle = WindowNative.GetWindowHandle(this);
        SetWindowOwner(handle, -8, App.MainWindowHandle());
        Activate();
        SetForegroundWindow(handle);
    }
}

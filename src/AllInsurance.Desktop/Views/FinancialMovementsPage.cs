using AllInsurance.Application.OfficeOperations;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

/// <summary>One searchable, exportable ledger for receipts or payments from every financial source.</summary>
public sealed class FinancialMovementsPage : Page
{
    private readonly bool _receipts;
    private readonly FinancialMovementService _service = App.Services.GetRequiredService<FinancialMovementService>();
    private readonly TextBox _search = new() { PlaceholderText = "Αναζήτηση προσώπου, ασφαλιστηρίου, αιτιολογίας ή αναφοράς…",
        Height = 42, MinHeight = 42, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _party = new() { MinWidth = 175, Height = 38 };
    private readonly ComboBox _method = new() { MinWidth = 180, Height = 38 };
    private readonly ComboBox _source = new() { MinWidth = 195, Height = 38 };
    private readonly EditableDateInput _from = new() { Width = 155, Height = 38, PlaceholderText = "Από" };
    private readonly EditableDateInput _until = new() { Width = 155, Height = 38, PlaceholderText = "Έως" };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single,
        HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _total = new() { FontSize = 15,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TablePagination _pager;
    private IReadOnlyList<FinancialMovement> _all = [];
    private FinancialMovement[] _shown = [];
    private readonly double[] _widths = [130, 145, 220, 205, 135, 175, 165, 300];
    private static readonly string[] Headers = ["Ημερομηνία", "Προς / από", "Πρόσωπο / φορέας",
        "Ασφαλιστήριο", "Ποσό", "Τρόπος", "Αναφορά", "Αιτιολογία"];

    public FinancialMovementsPage(bool receipts)
    {
        _receipts = receipts;
        _pager = new TablePagination(_list);
        _pager.ConfigureExport(ExportData);
        BuildUi();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private void BuildUi()
    {
        var root = new Grid { Margin = new Thickness(16, 12, 16, 12), RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        title.Children.Add(new TextBlock { Text = _receipts ? "Εισπράξεις" : "Πληρωμές",
            FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        title.Children.Add(_count);
        root.Children.Add(title);

        var filters = new StackPanel { Spacing = 8 };
        filters.Children.Add(SearchFieldVisual.Wrap(_search));
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _party.Header = "Πρόσωπο / φορέας";
        _method.Header = "Τρόπος";
        _source.Header = "Πηγή";
        filterRow.Children.Add(_party);
        filterRow.Children.Add(_method);
        filterRow.Children.Add(_source);
        filterRow.Children.Add(new StackPanel { Children = { new TextBlock { Text = "Από ημερομηνία" }, _from } });
        filterRow.Children.Add(new StackPanel { Children = { new TextBlock { Text = "Έως ημερομηνία" }, _until } });
        var clear = new ActionButton { Kind = "Clear", Label = "Καθαρισμός φίλτρων", Height = 38,
            VerticalAlignment = VerticalAlignment.Bottom };
        clear.Click += (_, _) =>
        {
            _search.Text = ""; _party.SelectedIndex = 0; _method.SelectedIndex = 0;
            _source.SelectedIndex = 0; _from.Date = null; _until.Date = null; Render();
        };
        filterRow.Children.Add(clear);
        filters.Children.Add(new ScrollViewer { Content = filterRow,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var filterCard = new Border { Background = Brush("CardBackgroundBrush"),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Child = filters };
        Grid.SetRow(filterCard, 1); root.Children.Add(filterCard);

        var summary = new Border { Background = Brush("CardBackgroundBrush"),
            CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 8, 12, 8), Child = _total };
        Grid.SetRow(summary, 2); root.Children.Add(summary);

        var tableWidth = _widths.Sum();
        var header = new Grid { Width = tableWidth, Background = Brush("TableHeaderBackgroundBrush") };
        for (var i = 0; i < Headers.Length; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_widths[i]) });
            var cell = Cell(Headers[i], true);
            Grid.SetColumn(cell, i); header.Children.Add(cell);
        }
        TableRowStriping.Attach(_list);
        _list.DoubleTapped += (_, _) => ShowSelected();
        var table = new Grid();
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.RowDefinitions.Add(new RowDefinition());
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.Children.Add(header);
        Grid.SetRow(_list, 1); table.Children.Add(_list);
        Grid.SetRow(_pager.Bar, 2); table.Children.Add(_pager.Bar);
        var card = new Border { Background = Brush("CardBackgroundBrush"),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(10), Child = table };
        Grid.SetRow(card, 3); root.Children.Add(card);
        TableInteractions.Attach(header, _list, card, 60);
        _search.TextChanged += (_, _) => Render();
        _party.SelectionChanged += (_, _) => Render();
        _method.SelectionChanged += (_, _) => Render();
        _source.SelectionChanged += (_, _) => Render();
        _from.DateChanged += (_, _) => Render();
        _until.DateChanged += (_, _) => Render();
        Content = root;
    }

    private static Brush Brush(string key)
    {
        var theme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        var dark = theme == ElementTheme.Dark || theme == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark;
        var (r, g, b) = key switch
        {
            "CardBackgroundBrush" => dark ? (44, 44, 44) : (235, 240, 245),
            "TableHeaderBackgroundBrush" => dark ? (48, 68, 90) : (208, 222, 235),
            _ => dark ? (113, 134, 155) : (132, 153, 174)
        };
        return new SolidColorBrush(Color.FromArgb(255, (byte)r, (byte)g, (byte)b));
    }

    private static Border Cell(string text, bool heading = false) => new()
    {
        BorderBrush = Brush("TableBorderBrush"), BorderThickness = new Thickness(0, 0, 1, 1),
        Padding = new Thickness(9, 7, 9, 7),
        Child = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = heading ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal }
    };

    private async Task ReloadAsync()
    {
        try
        {
            _all = await _service.ListAsync();
            Populate(_party, "Όλα τα πρόσωπα", _all.Select(item => item.PartyType));
            Populate(_method, "Όλοι οι τρόποι", _all.Select(item => item.Method));
            Populate(_source, "Όλες οι πηγές", _all.Select(item => item.Source));
            Render();
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }

    private static void Populate(ComboBox box, string first, IEnumerable<string> values)
    {
        var selected = box.SelectedItem as string;
        box.ItemsSource = new[] { first }.Concat(values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value)).ToArray();
        box.SelectedItem = selected is not null && box.Items.Contains(selected) ? selected : first;
    }

    private void Render()
    {
        if (_list.XamlRoot is null) return;
        var direction = _receipts ? "receipt" : "payment";
        var query = _search.Text.Trim();
        _shown = _all.Where(item => item.Direction == direction)
            .Where(item => query.Length == 0 || new[] { item.Party, item.PartyType, item.Policy,
                item.Method, item.Reference, item.Description, item.Source }
                .Any(value => value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true))
            .Where(item => _party.SelectedIndex <= 0 || item.PartyType == _party.SelectedItem as string)
            .Where(item => _method.SelectedIndex <= 0 || item.Method == _method.SelectedItem as string)
            .Where(item => _source.SelectedIndex <= 0 || item.Source == _source.SelectedItem as string)
            .Where(item => _from.Date is not DateTimeOffset from || item.OccurredOn.Date >= from.Date)
            .Where(item => _until.Date is not DateTimeOffset until || item.OccurredOn.Date <= until.Date)
            .OrderByDescending(item => item.OccurredOn).ToArray();
        _count.Text = $"{_shown.Length} εγγραφές";
        _total.Text = $"Σύνολο {(_receipts ? "εισπράξεων" : "πληρωμών")}: " +
            string.Join("  ·  ", _shown.GroupBy(item => item.Currency)
                .Select(group => $"{group.Sum(item => item.Amount):N2} {group.Key}"));
        if (_shown.Length == 0) _total.Text += "0,00 €";
        _list.Items.Clear();
        foreach (var item in _shown)
        {
            var row = new Grid { Width = _widths.Sum() };
            var values = Values(item);
            for (var index = 0; index < values.Length; index++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_widths[index]) });
                var cell = Cell(values[index]); Grid.SetColumn(cell, index); row.Children.Add(cell);
            }
            var listItem = new ListViewItem { Content = row, Tag = item,
                Padding = new Thickness(0), MinHeight = 40 };
            var menu = new MenuFlyout();
            var details = new MenuFlyoutItem { Text = "Προβολή κίνησης" };
            details.Click += (_, _) => ShowDetails(item);
            menu.Items.Add(details); listItem.ContextFlyout = menu;
            _list.Items.Add(listItem);
        }
        _pager.Capture();
    }

    private static DateTime LocalDate(FinancialMovement item) => item.OccurredOn.Kind == DateTimeKind.Utc
        ? item.OccurredOn.ToLocalTime() : item.OccurredOn;

    private static string[] Values(FinancialMovement item) =>
    [LocalDate(item).ToString("dd/MM/yyyy HH:mm"), item.PartyType,
        item.Party, item.Policy ?? "—", $"{item.Amount:N2} {item.Currency}",
        item.Method, item.Reference ?? "—", item.Description ?? "—"];

    private TableExportData ExportData() => new(_receipts ? "Εισπράξεις" : "Πληρωμές",
        Headers, _shown.Select(Values).ToArray());

    private void ShowSelected()
    {
        if (_list.SelectedItem is ListViewItem { Tag: FinancialMovement item }) ShowDetails(item);
    }

    private async void ShowDetails(FinancialMovement item)
    {
        var panel = new StackPanel { Spacing = 9, Width = 420 };
        foreach (var (label, value) in new[]
        {
            ("Ημερομηνία", LocalDate(item).ToString("dd/MM/yyyy HH:mm")),
            ("Πρόσωπο / φορέας", $"{item.PartyType}: {item.Party}"),
            ("Ασφαλιστήριο", item.Policy ?? "—"), ("Ποσό", $"{item.Amount:N2} {item.Currency}"),
            ("Τρόπος", item.Method), ("Αναφορά", item.Reference ?? "—"),
            ("Πηγή", item.Source), ("Αιτιολογία", item.Description ?? "—")
        })
            panel.Children.Add(new TextBlock { Text = $"{label}: {value}", TextWrapping = TextWrapping.Wrap });
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = _receipts ? "Καρτέλα είσπραξης" : "Καρτέλα πληρωμής",
            Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
            CloseButtonText = "Κλείσιμο" });
    }
}

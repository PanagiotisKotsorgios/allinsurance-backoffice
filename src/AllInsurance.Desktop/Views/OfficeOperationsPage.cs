using AllInsurance.Application.Business;
using AllInsurance.Application.OfficeOperations;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class OfficeOperationsPage : Page
{
    private readonly bool _overcommissions;
    private readonly OfficeOperationService _service = App.Services.GetRequiredService<OfficeOperationService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly Grid _root = new() { RowSpacing = 10, Margin = new Thickness(16) };
    private readonly TextBox _search = new() { PlaceholderText = "Αναζήτηση περιγραφής, κατηγορίας ή αναφοράς…",
        Height = 42, MinHeight = 42, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _firstFilter = new() { MinWidth = 170, Height = 38 };
    private readonly ComboBox _secondFilter = new() { MinWidth = 170, Height = 38 };
    private readonly EditableDateInput _from = new() { Width = 155, Height = 38, PlaceholderText = "Από" };
    private readonly EditableDateInput _until = new() { Width = 155, Height = 38, PlaceholderText = "Έως" };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single,
        Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 };
    private readonly TextBlock _breakdown = new() { TextWrapping = TextWrapping.Wrap,
        FontSize = 12, Opacity = 0.82 };
    private readonly TablePagination _pager;
    private Action? _applyColumns;
    private IReadOnlyList<OfficeOperation> _all = [];
    private IReadOnlyList<BusinessRecord> _partners = [];
    private IReadOnlyList<BusinessRecord> _companies = [];
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");
    private double[] _columnWidths = [];

    public OfficeOperationsPage(bool overcommissions)
    {
        _overcommissions = overcommissions;
        _pager = new TablePagination(_list);
        BuildUi();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void BuildUi()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition());
        _root.Background = ThemeBrush(31, 31, 31, 221, 228, 235);
        var header = new StackPanel { Spacing = 9 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        titleRow.Children.Add(new TextBlock { Text = _overcommissions ? "Υπερπρομήθειες" : "Έσοδα – έξοδα γραφείου",
            FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        titleRow.Children.Add(_count);
        header.Children.Add(titleRow);
        header.Children.Add(SearchFieldVisual.Wrap(_search));
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _firstFilter.ItemsSource = _overcommissions ? new[] { "Όλοι οι συνεργάτες" } :
            new[] { "Όλες οι κινήσεις", "Έσοδα", "Έξοδα" };
        _firstFilter.SelectedIndex = 0;
        _secondFilter.ItemsSource = _overcommissions ? new[] { "Όλες οι εταιρείες" } :
            new[] { "Όλες οι κατηγορίες" };
        _secondFilter.SelectedIndex = 0;
        filters.Children.Add(_firstFilter);
        filters.Children.Add(_secondFilter);
        filters.Children.Add(_from);
        filters.Children.Add(_until);
        var clear = new ActionButton { Kind = "Clear", Label = "Καθαρισμός φίλτρων", Height = 38 };
        clear.Click += (_, _) =>
        {
            _search.Text = ""; _firstFilter.SelectedIndex = 0; _secondFilter.SelectedIndex = 0;
            _from.Date = null; _until.Date = null; Render();
        };
        filters.Children.Add(clear);
        var add = new ActionButton { Kind = "Add", Label = _overcommissions
            ? "Προσθήκη υπερπρομήθειας" : "Νέα κίνηση", Height = 38 };
        add.Click += async (_, _) => await EditAsync(null);
        filters.Children.Add(add);
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή", Height = 38 };
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        filters.Children.Add(delete);
        var filterWrap = new ScrollViewer { Content = filters,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        header.Children.Add(filterWrap);
        var headerCard = new Border { Background = ThemeBrush(44, 44, 44, 235, 240, 245),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(13), Child = header };
        _root.Children.Add(headerCard);
        var stats = new StackPanel { Spacing = 3 };
        stats.Children.Add(_summary);
        stats.Children.Add(_breakdown);
        var summaryCard = new Border { Background = ThemeBrush(44, 44, 44, 235, 240, 245),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Child = stats };
        Grid.SetRow(summaryCard, 1);
        _root.Children.Add(summaryCard);

        var headings = _overcommissions
            ? new[] { "Ημερομηνία", "Συνεργάτης", "Εταιρεία", "Μικτά", "Καθαρά", "Προμήθεια έδρας", "Υπερπρομήθεια" }
            : new[] { "Ημερομηνία", "Κίνηση", "Κατηγορία", "Περιγραφή", "Ποσό", "Τρόπος", "Αναφορά" };
        var widths = _overcommissions ? new[] { 120d, 185, 185, 120, 120, 145, 145 } :
            new[] { 120d, 110, 160, 265, 120, 160, 145 };
        _columnWidths = widths;
        var tableHeader = new Grid { Width = widths.Sum() };
        for (var index = 0; index < headings.Length; index++)
        {
            tableHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[index]) });
            var cell = Cell(headings[index], true);
            Grid.SetColumn(cell, index);
            tableHeader.Children.Add(cell);
        }
        tableHeader.Background = ThemeBrush(48, 68, 90, 208, 222, 235);
        var table = new Grid { RowDefinitions =
            { new RowDefinition { Height = GridLength.Auto }, new RowDefinition(),
                new RowDefinition { Height = GridLength.Auto } } };
        table.Children.Add(tableHeader);
        TableRowStriping.Attach(_list);
        _list.DoubleTapped += async (_, _) =>
        {
            if (_list.SelectedItem is ListViewItem { Tag: OfficeOperation selected })
                await EditAsync(selected);
        };
        Grid.SetRow(_list, 1);
        table.Children.Add(_list);
        Grid.SetRow(_pager.Bar, 2);
        table.Children.Add(_pager.Bar);
        var tableCard = new Border { Background = ThemeBrush(44, 44, 44, 235, 240, 245),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Child = table };
        Grid.SetRow(tableCard, 2);
        _root.Children.Add(tableCard);
        _applyColumns = TableInteractions.Attach(tableHeader, _list, tableCard, 26);
        _search.TextChanged += (_, _) => Render();
        _firstFilter.SelectionChanged += (_, _) => Render();
        _secondFilter.SelectionChanged += (_, _) => Render();
        _from.DateChanged += (_, _) => Render();
        _until.DateChanged += (_, _) => Render();
        Content = _root;
    }

    private SolidColorBrush ThemeBrush(byte dr, byte dg, byte db, byte lr, byte lg, byte lb)
    {
        var theme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        var dark = theme == ElementTheme.Dark || theme == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark;
        return new SolidColorBrush(dark
            ? Color.FromArgb(255, dr, dg, db) : Color.FromArgb(255, lr, lg, lb));
    }

    private Border Cell(string value, bool header = false)
    {
        return new Border { Child = new TextBlock { Text = value,
                FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold :
                    Microsoft.UI.Text.FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center },
            BorderBrush = ThemeBrush(113, 134, 155, 132, 153, 174),
            BorderThickness = new Thickness(0, 0, 1, header ? 1 : 1),
            Padding = new Thickness(9, 8, 9, 8) };
    }

    private async Task RefreshAsync()
    {
        try
        {
            _all = await _service.ListAsync(_overcommissions ? "overcommission" : "income");
            if (!_overcommissions)
                _all = _all.Concat(await _service.ListAsync("expense")).ToArray();
            _partners = await _records.SearchAsync("partners");
            _companies = await _records.SearchAsync("companies");
            if (_overcommissions)
            {
                _firstFilter.ItemsSource = new[] { "Όλοι οι συνεργάτες" }
                    .Concat(_partners.Select(item => item.Title).Distinct()).ToArray();
                _secondFilter.ItemsSource = new[] { "Όλες οι εταιρείες" }
                    .Concat(_companies.Select(item => item.Title).Distinct()).ToArray();
            }
            else
                _secondFilter.ItemsSource = new[] { "Όλες οι κατηγορίες" }
                    .Concat(_all.Select(item => item.Category).Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!).Distinct()).ToArray();
            _firstFilter.SelectedIndex = 0;
            _secondFilter.SelectedIndex = 0;
            Render();
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }

    private void Render()
    {
        if (_root.XamlRoot is null) return;
        var text = _search.Text.Trim();
        var rows = _all.Where(item => text.Length == 0 || new[] { item.Description,
                item.Category, item.Reference, RecordName(_partners, item.PartnerId),
                RecordName(_companies, item.CompanyId) }.Any(value =>
                value?.Contains(text, StringComparison.CurrentCultureIgnoreCase) == true))
            .Where(item => _from.Date is not DateTimeOffset from || item.OccurredOn >= from.Date)
            .Where(item => _until.Date is not DateTimeOffset until || item.OccurredOn <= until.Date);
        if (_overcommissions)
            rows = rows.Where(item => (_firstFilter.SelectedIndex <= 0 ||
                    RecordName(_partners, item.PartnerId) == _firstFilter.SelectedItem as string) &&
                (_secondFilter.SelectedIndex <= 0 ||
                    RecordName(_companies, item.CompanyId) == _secondFilter.SelectedItem as string));
        else rows = rows.Where(item => (_firstFilter.SelectedIndex switch
            {
                1 => item.Kind == "income", 2 => item.Kind == "expense", _ => true
            }) && (_secondFilter.SelectedIndex <= 0 ||
                item.Category == _secondFilter.SelectedItem as string));
        var visible = rows.OrderByDescending(item => item.OccurredOn).ToArray();
        _count.Text = $"{visible.Length} εγγραφές";
        _summary.Text = _overcommissions
            ? $"Σύνολο υπερπρομηθειών: {visible.Sum(item => item.OverCommission ?? 0):N2} €  ·  " +
              $"Καθαρά ασφάλιστρα: {visible.Sum(item => item.NetPremium ?? 0):N2} €  ·  " +
              $"Προμήθεια έδρας: {visible.Sum(item => item.OfficeCommission ?? 0):N2} €"
            : $"Έσοδα γραφείου: {visible.Where(item => item.Kind == "income").Sum(item => item.Amount):N2} €  ·  " +
              $"Έξοδα γραφείου: {visible.Where(item => item.Kind == "expense").Sum(item => item.Amount):N2} €  ·  " +
              $"Διαφορά: {visible.Sum(item => item.Kind == "income" ? item.Amount : -item.Amount):N2} €";
        var thisMonth = visible.Where(item => item.OccurredOn.Year == DateTime.Today.Year &&
            item.OccurredOn.Month == DateTime.Today.Month).ToArray();
        _breakdown.Text = _overcommissions
            ? $"Τρέχων μήνας: {thisMonth.Sum(item => item.OverCommission ?? 0):N2} €  ·  " +
              "Ανά εταιρεία: " + string.Join("  ·  ", visible.GroupBy(item =>
                  RecordName(_companies, item.CompanyId)).OrderByDescending(group =>
                  group.Sum(item => item.OverCommission ?? 0)).Take(3).Select(group =>
                  $"{group.Key} {group.Sum(item => item.OverCommission ?? 0):N2} €"))
            : $"Τρέχων μήνας: έσοδα {thisMonth.Where(item => item.Kind == "income").Sum(item => item.Amount):N2} € / " +
              $"έξοδα {thisMonth.Where(item => item.Kind == "expense").Sum(item => item.Amount):N2} €  ·  " +
              "Κυριότερες κατηγορίες: " + string.Join("  ·  ", visible.GroupBy(item =>
                  item.Category ?? "Χωρίς κατηγορία").OrderByDescending(group =>
                  group.Sum(item => item.Amount)).Take(3).Select(group =>
                  $"{group.Key} {group.Sum(item => item.Amount):N2} €"));
        _list.Items.Clear();
        foreach (var item in visible)
        {
            var cells = _overcommissions
                ? new[] { item.OccurredOn.ToString("dd/MM/yyyy"), RecordName(_partners, item.PartnerId),
                    RecordName(_companies, item.CompanyId), Money(item.GrossPremium), Money(item.NetPremium),
                    Money(item.OfficeCommission), Money(item.OverCommission) }
                : new[] { item.OccurredOn.ToString("dd/MM/yyyy"),
                    item.Kind == "income" ? "Έσοδο" : "Έξοδο", item.Category ?? "—",
                    item.Description, Money(item.Amount), item.PaymentMethod ?? "—", item.Reference ?? "—" };
            var grid = new Grid();
            for (var index = 0; index < cells.Length; index++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columnWidths[index]) });
                var cell = Cell(cells[index]);
                Grid.SetColumn(cell, index);
                grid.Children.Add(cell);
            }
            var row = new ListViewItem { Content = grid, Tag = item,
                Padding = new Thickness(0), MinHeight = 40 };
            var menu = new MenuFlyout();
            var edit = new MenuFlyoutItem { Text = "Επεξεργασία" };
            edit.Click += async (_, _) => await EditAsync(item);
            var delete = new MenuFlyoutItem { Text = "Διαγραφή" };
            delete.Click += async (_, _) => await DeleteAsync(item);
            menu.Items.Add(edit); menu.Items.Add(delete);
            row.ContextFlyout = menu;
            _list.Items.Add(row);
        }
        _pager.Capture();
        _applyColumns?.Invoke();
    }

    private static string Money(decimal? value) => value is decimal amount ? amount.ToString("N2", Greek) + " €" : "—";
    private static string RecordName(IReadOnlyList<BusinessRecord> records, Guid? id) =>
        records.FirstOrDefault(item => item.Id == id)?.Title ?? "—";

    private async Task DeleteSelectedAsync()
    {
        if (_list.SelectedItem is not ListViewItem { Tag: OfficeOperation operation })
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πρώτα εγγραφή από τον πίνακα."); return; }
        await DeleteAsync(operation);
    }

    private async Task DeleteAsync(OfficeOperation operation)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot,
            Title = "Διαγραφή εγγραφής", Content = operation.Description,
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Άκυρο" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            await _service.DeleteAsync(operation.Id);
            await RefreshAsync();
            await UiAlerts.ShowSuccessAsync(XamlRoot, "Η εγγραφή διαγράφηκε.");
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }
}

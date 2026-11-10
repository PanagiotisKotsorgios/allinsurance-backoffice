using AllInsurance.Application.Business;
using AllInsurance.Application.Commissions;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using AllInsurance.Desktop.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed class CommissionRecalculationPage : Page
{
    private readonly ICommissionRecalculationService _service =
        App.Services.GetRequiredService<ICommissionRecalculationService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly Action _back;
    private readonly ComboBox _company = Choice("Εταιρεία / πρακτορείο");
    private readonly ComboBox _partner = Choice("Συνεργάτης");
    private readonly ComboBox _branch = Choice("Κλάδος");
    private readonly ComboBox _use = Choice("Χρήση");
    private readonly ComboBox _coverage = Choice("Κάλυψη");
    private readonly EditableDateInput _startFrom = new() { Width = 150 };
    private readonly EditableDateInput _startUntil = new() { Width = 150 };
    private readonly EditableDateInput _endFrom = new() { Width = 150 };
    private readonly EditableDateInput _endUntil = new() { Width = 150 };
    private readonly TextBox _search = new() { PlaceholderText = "Αριθμός ασφαλιστηρίου ή συνεργάτης",
        Width = 260, Height = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.None };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressRing _progress = new() { Width = 24, Height = 24, IsActive = false };
    private readonly HashSet<Guid> _selected = [];
    private readonly Dictionary<string, Guid> _partnerIds = [];
    private readonly double[] _columnWidths = SectionColumnOrder.LoadWidths("commission_recalculation");
    private readonly int[] _columnOrder = SectionColumnOrder.Load("commission_recalculation");
    private readonly bool[] _columnVisible = SectionColumnOrder.LoadVisibility("commission_recalculation");
    private readonly List<Grid> _rows = [];
    private readonly List<CommissionRecalculationPreview> _rowPreviews = [];
    private readonly Grid _root;
    private readonly Grid _table;
    private readonly Grid _header;
    private readonly Border _filterCard;
    private readonly Border _tableCard;
    private readonly ScrollViewer _tableScroll;
    private bool _dark;
    private bool _warm;
    private SolidColorBrush _textBrush = Solid(30, 45, 62);
    private SolidColorBrush _borderBrush = Solid(132, 153, 174);
    private int _sortColumn = 4;
    private bool _sortDescending;
    private CommissionRecalculationPreview[] _preview = [];

    public CommissionRecalculationPage(Action back)
    {
        _back = back;
        _root = new Grid { Margin = new Thickness(4, 4, 6, 4), RowSpacing = 4,
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition(),
                new RowDefinition { Height = GridLength.Auto } } };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var backButton = new Button { Content = "← Πίσω" };
        backButton.Click += (_, _) => _back();
        title.Children.Add(backButton);
        title.Children.Add(new TextBlock { Text = "Επεξεργασία προμηθειών παλιών ασφαλιστηρίων",
            FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        _root.Children.Add(title);

        var filters = new StackPanel { Spacing = 8 };
        filters.Children.Add(new TextBlock { Text = "Οι κανόνες δεν αλλάζουν παλιά ασφαλιστήρια. Επιλέξτε ρητά όσα θέλετε να ενημερωθούν.",
            TextWrapping = TextWrapping.Wrap });
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var box in new[] { _company, _partner, _branch, _use, _coverage })
            filterRow.Children.Add(box);
        foreach (var (label, field) in new[] { ("Έναρξη από", _startFrom), ("Έναρξη έως", _startUntil),
            ("Λήξη από", _endFrom), ("Λήξη έως", _endUntil) })
        {
            var group = new StackPanel { Spacing = 2 };
            group.Children.Add(new TextBlock { Text = label });
            group.Children.Add(field);
            filterRow.Children.Add(group);
        }
        filters.Children.Add(new ScrollViewer { Content = filterRow,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_search);
        var previewButton = new Button { Content = "Προεπισκόπηση" };
        previewButton.Click += async (_, _) => await RefreshAsync();
        actions.Children.Add(previewButton);
        var selectAll = new Button { Content = "Επιλογή όλων των αποτελεσμάτων" };
        selectAll.Click += (_, _) =>
        {
            foreach (var item in VisiblePreview()) _selected.Add(item.PolicyId);
            ShowRows();
        };
        actions.Children.Add(selectAll);
        var clearSelection = new Button { Content = "Αποεπιλογή" };
        clearSelection.Click += (_, _) => { _selected.Clear(); ShowRows(); };
        actions.Children.Add(clearSelection);
        var columnsButton = new Button { Content = "Στήλες" };
        columnsButton.Click += async (_, _) => await ChooseColumnsAsync();
        actions.Children.Add(columnsButton);
        filters.Children.Add(actions);
        _filterCard = new Border { Child = filters, Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(14) };
        Grid.SetRow(_filterCard, 1); _root.Children.Add(_filterCard);

        _table = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        _header = Row(["✓", "Ασφαλιστήριο", "Εταιρεία", "Συνεργάτης", "Έναρξη", "Λήξη",
            "Καθαρά", "Έδρα πριν", "Έδρα μετά", "Συνεργάτης πριν", "Συνεργάτης μετά"], true);
        _table.Children.Add(_header);
        ConfigureColumns();
        _list.BorderBrush = _borderBrush;
        _list.BorderThickness = new Thickness(1);
        TableRowStriping.Attach(_list);
        Grid.SetRow(_list, 1); _table.Children.Add(_list);
        _table.Width = _columnWidths.Sum();
        _tableScroll = new ScrollViewer { Content = _table,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _tableCard = new Border { Child = _tableScroll, Padding = new Thickness(10),
            CornerRadius = new CornerRadius(14) };
        Grid.SetRow(_tableCard, 2); _root.Children.Add(_tableCard);

        var footer = new Grid { ColumnDefinitions = { new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
            ColumnSpacing = 10 };
        footer.Children.Add(_status);
        Grid.SetColumn(_progress, 1); footer.Children.Add(_progress);
        var apply = new Button { Content = "Αποθήκευση επιλεγμένων" };
        apply.Click += async (_, _) => await ApplyAsync();
        Grid.SetColumn(apply, 2); footer.Children.Add(apply);
        Grid.SetRow(footer, 3); _root.Children.Add(footer);
        Content = _root;
        _search.TextChanged += (_, _) => ShowRows();
        Loaded += async (_, _) => { ApplyPalette(); await LoadOptionsAsync(); };
        ActualThemeChanged += (_, _) => ApplyPalette();
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            var companies = await _records.SearchAsync("companies");
            var agencies = await _records.SearchAsync("agencies");
            Fill(_company, companies.Concat(agencies).Select(item => item.Title));
            var partners = await _records.SearchAsync("partners");
            foreach (var partner in partners)
                _partnerIds[partner.Title + " · " + partner.Id.ToString("N")[..8]] = partner.Id;
            Fill(_partner, _partnerIds.Keys);
            Fill(_branch, await _filters.ChoicesAsync("Κλάδοι"));
            Fill(_use, await _filters.ChoicesAsync("Χρήσεις οχημάτων"));
            Fill(_coverage, await _filters.ChoicesAsync("Καλύψεις"));
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task RefreshAsync()
    {
        try
        {
            _progress.IsActive = true;
            _status.Text = "Υπολογισμός προεπισκόπησης…";
            var partnerLabel = Pick(_partner);
            _preview = (await _service.PreviewAsync(new CommissionRecalculationFilter(
                Pick(_company), partnerLabel is null ? null : _partnerIds[partnerLabel],
                Pick(_branch), Pick(_use), Pick(_coverage),
                _startFrom.Date?.Date, _startUntil.Date?.Date,
                _endFrom.Date?.Date, _endUntil.Date?.Date))).ToArray();
            _selected.Clear();
            ShowRows();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
        finally { _progress.IsActive = false; }
    }

    private void ShowRows()
    {
        _list.Items.Clear();
        _rows.Clear();
        _rowPreviews.Clear();
        foreach (var item in VisiblePreview())
        {
            var row = Row(["", item.PolicyNumber, item.Insurer, item.PartnerName ?? "—",
                item.StartDate?.ToString("dd/MM/yyyy") ?? "—", item.EndDate?.ToString("dd/MM/yyyy") ?? "—",
                $"{item.NetPremium:N2} €", $"{item.OldOfficePercent:N2}% ({item.OldOfficeAmount:N2} €)",
                $"{item.NewOfficePercent:N2}% ({item.NewOfficeAmount:N2} €)",
                $"{item.OldPartnerPercent:N2}% ({item.OldPartnerAmount:N2} €)",
                $"{item.NewPartnerPercent:N2}% ({item.NewPartnerAmount:N2} €)"], false);
            var check = new VisibleSelectionCheckBox { IsChecked = _selected.Contains(item.PolicyId),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            check.Checked += (_, _) => { _selected.Add(item.PolicyId); UpdateStatus(); };
            check.Unchecked += (_, _) => { _selected.Remove(item.PolicyId); UpdateStatus(); };
            ((Border)row.Children[0]).Child = check;
            PaintRow(row, item);
            _rows.Add(row);
            _rowPreviews.Add(item);
            TableRowStriping.ApplyRow(row, _rows.Count - 1, _list);
            _list.Items.Add(new ListViewItem { Content = row, Height = 36,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0),
                Foreground = _textBrush });
        }
        TableRowStriping.Refresh(_list, true);
        UpdateStatus();
    }

    private async Task ApplyAsync()
    {
        var chosen = _preview.Where(item => _selected.Contains(item.PolicyId)).ToArray();
        if (chosen.Length == 0)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε ασφαλιστήρια από την προεπισκόπηση.");
            return;
        }
        var confirm = new ContentDialog { XamlRoot = XamlRoot, Title = "Εφαρμογή νέων προμηθειών",
            Content = $"Θα ενημερωθούν {chosen.Length} ασφαλιστήρια. Οι οικονομικές τους προμήθειες θα αλλάξουν σύμφωνα με την προεπισκόπηση.",
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(confirm) != ContentDialogResult.Primary) return;
        try
        {
            _progress.IsActive = true;
            _status.Text = $"Ενημέρωση {chosen.Length} ασφαλιστηρίων…";
            var count = await _service.ApplyAsync(chosen);
            await RefreshAsync();
            _status.Text = $"Ενημερώθηκαν {count} ασφαλιστήρια.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
        finally { _progress.IsActive = false; }
    }

    private void UpdateStatus() => _status.Text =
        $"{_preview.Length} ασφαλιστήρια στην προεπισκόπηση · {_selected.Count} επιλεγμένα";

    private IEnumerable<CommissionRecalculationPreview> VisiblePreview()
    {
        var term = _search.Text.Trim();
        var filtered = _preview.Where(item => term.Length == 0 ||
            (item.PolicyNumber + item.Insurer + item.PartnerName)
                .Contains(term, StringComparison.CurrentCultureIgnoreCase));
        return _sortDescending
            ? filtered.OrderByDescending(item => SortKey(item, _sortColumn))
            : filtered.OrderBy(item => SortKey(item, _sortColumn));
    }

    private static IComparable SortKey(CommissionRecalculationPreview item, int column) => column switch
    {
        1 => item.PolicyNumber,
        2 => item.Insurer,
        3 => item.PartnerName ?? "",
        4 => item.StartDate ?? DateTime.MinValue,
        5 => item.EndDate ?? DateTime.MinValue,
        6 => item.NetPremium,
        7 => item.OldOfficePercent ?? 0m,
        8 => item.NewOfficePercent,
        9 => item.OldPartnerPercent ?? 0m,
        10 => item.NewPartnerPercent,
        _ => item.PolicyNumber
    };

    private void ConfigureColumns()
    {
        for (var index = 1; index < _columnOrder.Length; index++)
        {
            var original = index;
            var cell = (Border)_header.Children[original];
            cell.CanDrag = true;
            cell.AllowDrop = true;
            cell.DragStarting += (_, args) => args.Data.SetText($"commission-column:{original}");
            cell.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(StandardDataFormats.Text))
                    args.AcceptedOperation = DataPackageOperation.Move;
            };
            cell.Drop += async (_, args) =>
            {
                var dragged = await args.DataView.GetTextAsync();
                const string prefix = "commission-column:";
                if (dragged.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(dragged[prefix.Length..], out var source))
                    MoveColumn(source, original);
            };
            cell.Tapped += (_, _) =>
            {
                _sortDescending = _sortColumn == original && !_sortDescending;
                _sortColumn = original;
                ShowRows();
            };
        }
        TableColumnResizer.Attach(_header, ResizeColumn,
            () => SectionColumnOrder.SaveWidths("commission_recalculation", _columnWidths));
        ApplyColumnLayout();
    }

    private void ResizeColumn(int original, double width)
    {
        _columnWidths[original] = Math.Clamp(width, 70, 520);
        ApplyColumnLayout();
    }

    private void MoveColumn(int source, int target)
    {
        if (source <= 0 || target <= 0 || source == target) return;
        var ordered = _columnOrder.ToList();
        ordered.Remove(source);
        ordered.Insert(ordered.IndexOf(target), source);
        ordered.CopyTo(_columnOrder);
        SectionColumnOrder.Save("commission_recalculation", _columnOrder);
        ApplyColumnLayout();
    }

    private void ApplyColumnLayout()
    {
        ApplyRowLayout(_header);
        foreach (var row in _rows) ApplyRowLayout(row);
        if (_table is not null) _table.Width = _columnWidths.Where((_, index) => _columnVisible[index]).Sum();
    }

    private void ApplyRowLayout(Grid row)
    {
        for (var position = 0; position < _columnOrder.Length; position++)
        {
            var original = _columnOrder[position];
            row.ColumnDefinitions[position].Width = new GridLength(
                _columnVisible[original] ? _columnWidths[original] : 0);
            var cell = (Border)row.Children[original];
            cell.Visibility = _columnVisible[original] ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(cell, position);
            cell.BorderThickness = position == _columnOrder.Length - 1
                ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 1);
        }
        row.Width = _columnWidths.Where((_, index) => _columnVisible[index]).Sum();
    }

    private async Task ChooseColumnsAsync()
    {
        var options = new StackPanel { Spacing = 4 };
        var names = new[] { "Επιλογή", "Ασφαλιστήριο", "Εταιρεία", "Συνεργάτης", "Έναρξη", "Λήξη",
            "Καθαρά", "Έδρα πριν", "Έδρα μετά", "Συνεργάτης πριν", "Συνεργάτης μετά" };
        var checks = names.Select((name, index) => new CheckBox
        {
            Content = name, IsChecked = _columnVisible[index], IsEnabled = index != 0
        }).ToArray();
        foreach (var check in checks) options.Children.Add(check);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαχείριση στηλών",
            Content = new ScrollViewer { Content = options, MaxHeight = 440 },
            PrimaryButtonText = "Εφαρμογή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        for (var index = 1; index < checks.Length; index++)
            _columnVisible[index] = checks[index].IsChecked == true;
        SectionColumnOrder.SaveVisibility("commission_recalculation", _columnVisible);
        ApplyColumnLayout();
    }

    private void ApplyPalette()
    {
        var preferences = App.Services.GetRequiredService<WorkspacePreferences>();
        _dark = preferences.Theme == ElementTheme.Dark ||
            preferences.Theme == ElementTheme.Default && ActualTheme == ElementTheme.Dark;
        _warm = !_dark && preferences.EyeComfortEnabled;
        _textBrush = _dark ? Solid(241, 245, 249) : Solid(30, 45, 62);
        _borderBrush = _dark ? Solid(113, 134, 155) : Solid(132, 153, 174);
        _root.Background = _dark ? Solid(31, 31, 31) :
            _warm ? Solid(229, 226, 216) : Solid(221, 228, 235);
        Background = _root.Background;
        _filterCard.Background = _tableCard.Background = _dark ? Solid(44, 44, 44) :
            _warm ? Solid(235, 232, 222) : Solid(235, 240, 245);
        _list.Background = _dark ? Solid(43, 49, 57) :
            _warm ? Solid(235, 232, 222) : Solid(235, 241, 246);
        _list.Foreground = _textBrush;
        _list.BorderBrush = _borderBrush;
        PaintRow(_header, null);
        for (var index = 0; index < _rows.Count; index++)
            PaintRow(_rows[index], _rowPreviews[index]);
        TableRowStriping.Refresh(_list, true);
        for (var index = 0; index < _rows.Count; index++)
            TableRowStriping.ApplyRow(_rows[index], index, _list);
    }

    private void PaintRow(Grid row, CommissionRecalculationPreview? preview)
    {
        if (ReferenceEquals(row, _header))
            row.Background = _dark ? Solid(53, 63, 75) :
                _warm ? Solid(210, 207, 195) : Solid(208, 222, 235);
        foreach (var cell in row.Children.OfType<Border>())
        {
            cell.BorderBrush = _borderBrush;
            if (cell.Child is TextBlock text) text.Foreground = _textBrush;
            else if (cell.Child is Grid host)
                foreach (var textInHeader in host.Children.OfType<TextBlock>())
                    textInHeader.Foreground = _textBrush;
        }
        if (preview is null || ReferenceEquals(row, _header)) return;
        foreach (var (column, previous, updated) in new[]
            { (8, preview.OldOfficePercent ?? 0m, preview.NewOfficePercent),
                (10, preview.OldPartnerPercent ?? 0m, preview.NewPartnerPercent) })
        {
            ((Border)row.Children[column]).Background = updated > previous
                ? _dark ? Solid(52, 77, 63) : Solid(211, 235, 218)
                : updated < previous ? _dark ? Solid(88, 57, 57) : Solid(244, 222, 220)
                : null;
        }
    }

    private static SolidColorBrush Solid(byte red, byte green, byte blue) =>
        new(Color.FromArgb(255, red, green, blue));

    private static ComboBox Choice(string label)
    {
        var box = new ComboBox { Header = label, Width = 170 };
        box.Items.Add("Όλα"); box.SelectedIndex = 0;
        return box;
    }

    private static void Fill(ComboBox box, IEnumerable<string> values)
    {
        box.Items.Clear(); box.Items.Add("Όλα");
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value))
            box.Items.Add(value);
        box.SelectedIndex = 0;
    }

    private static string? Pick(ComboBox box) => box.SelectedIndex > 0
        ? box.SelectedItem?.ToString() : null;

    private Grid Row(string[] values, bool header)
    {
        var row = new Grid { Width = _columnWidths.Sum(), Height = header ? 42 : 40 };
        foreach (var width in _columnWidths)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var index = 0; index < values.Length; index++)
        {
            var cell = new Border { BorderBrush = _borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(5),
                Child = new TextBlock { Text = values[index], VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = _textBrush,
                    FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold :
                        Microsoft.UI.Text.FontWeights.Normal } };
            Grid.SetColumn(cell, index); row.Children.Add(cell);
        }
        ApplyRowLayout(row);
        if (header) PaintRow(row, null);
        return row;
    }
}

using AllInsurance.Application.Business;
using AllInsurance.Application.Commissions;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
    private CommissionRecalculationPreview[] _preview = [];

    public CommissionRecalculationPage(Action back)
    {
        _back = back;
        var root = new Grid { Margin = new Thickness(16, 8, 16, 8), RowSpacing = 8,
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition(),
                new RowDefinition { Height = GridLength.Auto } } };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var backButton = new Button { Content = "← Κανόνες προμηθειών" };
        backButton.Click += (_, _) => _back();
        title.Children.Add(backButton);
        title.Children.Add(new TextBlock { Text = "Επεξεργασία προμηθειών παλιών ασφαλιστηρίων",
            FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(title);

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
        filters.Children.Add(actions);
        Grid.SetRow(filters, 1); root.Children.Add(filters);

        var table = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        table.Children.Add(Row(["✓", "Ασφαλιστήριο", "Εταιρεία", "Συνεργάτης", "Έναρξη", "Λήξη",
            "Καθαρά", "Έδρα πριν", "Έδρα μετά", "Συνεργάτης πριν", "Συνεργάτης μετά"], true));
        _list.BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"];
        _list.BorderThickness = new Thickness(1);
        TableRowStriping.Attach(_list);
        Grid.SetRow(_list, 1); table.Children.Add(_list);
        table.Width = 1435;
        var tableScroll = new ScrollViewer { Content = table,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(tableScroll, 2); root.Children.Add(tableScroll);

        var footer = new Grid { ColumnDefinitions = { new ColumnDefinition(),
            new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
            ColumnSpacing = 10 };
        footer.Children.Add(_status);
        Grid.SetColumn(_progress, 1); footer.Children.Add(_progress);
        var apply = new Button { Content = "Αποθήκευση επιλεγμένων" };
        apply.Click += async (_, _) => await ApplyAsync();
        Grid.SetColumn(apply, 2); footer.Children.Add(apply);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        _search.TextChanged += (_, _) => ShowRows();
        Loaded += async (_, _) => await LoadOptionsAsync();
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
            _list.Items.Add(new ListViewItem { Content = row, Height = 42,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) });
        }
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
        return _preview.Where(item => term.Length == 0 ||
            (item.PolicyNumber + item.Insurer + item.PartnerName)
                .Contains(term, StringComparison.CurrentCultureIgnoreCase));
    }

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

    private static Grid Row(string[] values, bool header)
    {
        var widths = new[] { 45d, 150, 150, 150, 100, 100, 100, 160, 160, 160, 160 };
        var row = new Grid { Width = widths.Sum(), Height = header ? 42 : 40,
            Background = header ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderBackgroundBrush"] : null };
        foreach (var width in widths)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var index = 0; index < values.Length; index++)
        {
            var cell = new Border { BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
                BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(5),
                Child = new TextBlock { Text = values[index], VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold :
                        Microsoft.UI.Text.FontWeights.Normal } };
            Grid.SetColumn(cell, index); row.Children.Add(cell);
        }
        return row;
    }
}

using AllInsurance.Application.Commissions;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Business;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed class CommissionSettingsPage : Page
{
    private readonly ICommissionRuleService _service = App.Services.GetRequiredService<ICommissionRuleService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox _search = new() { PlaceholderText = "Αναζήτηση εταιρείας, κλάδου, χρήσης, κάλυψης ή πακέτου",
        Height = 42, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _companyFilter = new() { Width = 175 };
    private readonly ComboBox _branchFilter = new() { Width = 155 };
    private readonly ComboBox _useFilter = new() { Width = 155 };
    private readonly ComboBox _coverageFilter = new() { Width = 155 };
    private readonly ComboBox _packageFilter = new() { Width = 155 };
    private readonly ComboBox _statusFilter = new() { Width = 130 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Grid _root;
    private readonly Border _filterCard;
    private readonly Border _tableCard;
    private readonly Grid _header;
    private bool _dark;
    private double _tableWidth = 1200;
    private CommissionRule[] _rules = [];
    private string[] _organizationNames = [];
    private sealed record CommissionCandidate(string Organization, string? Branch,
        string? Use, string? Coverage, string? Package);

    public CommissionSettingsPage()
    {
        _root = new Grid { Margin = new Thickness(16, 8, 16, 8), RowSpacing = 8,
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        var filters = new StackPanel { Spacing = 8 };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        title.Children.Add(new TextBlock { Text = "Παραμετροποίηση προμηθειών",
            FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        title.Children.Add(_status);
        filters.Children.Add(title);
        _search.TextChanged += (_, _) => ApplyFilters();
        filters.Children.Add(SearchFieldVisual.Wrap(_search));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (box, label) in new[] { (_companyFilter, "Εταιρεία / πρακτορείο"), (_branchFilter, "Κλάδος"),
            (_useFilter, "Χρήση"), (_coverageFilter, "Κάλυψη"), (_packageFilter, "Πακέτο"),
            (_statusFilter, "Κατάσταση") })
        {
            box.Items.Add(new ComboBoxItem { Content = label + ": όλα" });
            box.SelectedIndex = 0;
            box.SelectionChanged += (_, _) => ApplyFilters();
            actions.Children.Add(box);
        }
        var clear = new ActionButton { Kind = "Clear", Label = "Καθαρισμός φίλτρων" };
        clear.Click += (_, _) =>
        {
            _search.Text = "";
            foreach (var box in new[] { _companyFilter, _branchFilter, _useFilter,
                _coverageFilter, _packageFilter, _statusFilter }) box.SelectedIndex = 0;
            ApplyFilters();
        };
        actions.Children.Add(clear);
        var add = new ActionButton { Kind = "Add", Label = "Νέος κανόνας" };
        add.Click += async (_, _) => await EditAsync(null);
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        delete.Click += async (_, _) => await DeleteAsync();
        actions.Children.Add(add); actions.Children.Add(delete);
        filters.Children.Add(new ScrollViewer { Content = actions,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _filterCard = new Border { Child = filters, Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(16) };
        _root.Children.Add(_filterCard);
        var table = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
            new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
        _header = TableRow(["Εταιρεία / πρακτορείο", "Κλάδος", "Χρήση", "Κάλυψη", "Πακέτο",
            "Εισερχόμενη %", "Προς συνεργάτη %", "Κατάσταση"], true);
        table.Children.Add(_header);
        _list.SelectionMode = ListViewSelectionMode.Single;
        TableRowStriping.Attach(_list);
        _list.DoubleTapped += async (_, _) =>
        {
            if (_list.SelectedItem is not ListViewItem selected) return;
            if (selected.Tag is CommissionRule rule) await EditAsync(rule);
            else if (selected.Tag is CommissionCandidate candidate)
                await EditAsync(null, candidate);
        };
        Grid.SetRow(_list, 1); table.Children.Add(_list);
        var exportHost = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        TableExportActions.Attach(exportHost, this, () => new TableExportData("Κανόνες προμηθειών",
            ["Εταιρεία / πρακτορείο", "Κλάδος", "Χρήση", "Κάλυψη", "Πακέτο",
                "Προμήθεια έδρας %", "Προμήθεια συνεργάτη %", "Κατάσταση"],
            _list.Items.OfType<ListViewItem>().Where(item => item.Tag is CommissionRule)
                .Select(item => (CommissionRule)item.Tag)
                .Select(rule => new[] { rule.Insurer, rule.Branch ?? "Όλοι", rule.VehicleUse ?? "Όλες",
                    CommissionCoverageSelection.Display(rule.Coverage), rule.Package ?? "Όλα",
                    $"{rule.OfficePercent:N2}%", $"{rule.PartnerPercent:N2}%",
                    rule.IsActive ? "Ενεργός" : "Ανενεργός" }).ToArray()));
        Grid.SetRow(exportHost, 2); table.Children.Add(exportHost);
        var tableScroll = new ScrollViewer { Content = table,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        tableScroll.SizeChanged += (_, args) =>
        {
            var width = Math.Max(1200, args.NewSize.Width - 2);
            if (Math.Abs(width - _tableWidth) < 2) return;
            _tableWidth = width;
            ResizeRow(_header);
            ApplyFilters();
        };
        Grid.SetRow(tableScroll, 1);
        _tableCard = new Border { Child = tableScroll, Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(16) };
        Grid.SetRow(_tableCard, 1);
        _root.Children.Add(_tableCard);
        Content = _root;
        Loaded += async (_, _) => { ApplyPalette(); await RefreshAsync(); };
        ActualThemeChanged += (_, _) => ApplyPalette();
    }

    private CommissionRule? Selected() => (_list.SelectedItem as ListViewItem)?.Tag as CommissionRule;

    private async Task RefreshAsync()
    {
        try
        {
            _rules = (await _service.ListAsync()).ToArray();
            _organizationNames = await OrganizationNamesAsync();
            SetFilter(_companyFilter, "Εταιρεία / πρακτορείο",
                _organizationNames.Concat(_rules.Select(rule => rule.Insurer)));
            SetFilter(_branchFilter, "Κλάδος",
                (await _filters.ListAsync("Κλάδοι")).Select(item => item.Value)
                    .Concat(_rules.Select(rule => rule.Branch))!);
            SetFilter(_useFilter, "Χρήση",
                (await _filters.ListAsync("Χρήσεις οχημάτων")).Select(item => item.Value)
                    .Concat(_rules.Select(rule => rule.VehicleUse))!);
            SetFilter(_coverageFilter, "Κάλυψη",
                (await _filters.ListAsync("Καλύψεις")).Select(item => item.Value).Concat(_rules.SelectMany(rule =>
                    CommissionCoverageSelection.Read(rule.Coverage))));
            SetFilter(_packageFilter, "Πακέτο",
                (await _filters.ListAsync("Πακέτα ασφαλίσεων")).Select(item => item.Value)
                    .Concat(_rules.Select(rule => rule.Package))!);
            SetFilter(_statusFilter, "Κατάσταση", ["Ενεργός", "Ανενεργός"]);
            ApplyFilters();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task<string[]> OrganizationNamesAsync()
    {
        var companies = await _records.SearchAsync("companies");
        var agencies = await _records.SearchAsync("agencies");
        return companies.Concat(agencies).Select(item => item.Title)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private void ApplyPalette()
    {
        _dark = ActualTheme == ElementTheme.Dark;
        _root.Background = Solid(_dark ? (byte)31 : (byte)221,
            _dark ? (byte)31 : (byte)228, _dark ? (byte)31 : (byte)235);
        Background = _root.Background;
        _filterCard.Background = Solid(_dark ? 44 : 235, _dark ? 44 : 240, _dark ? 44 : 245);
        _tableCard.Background = Solid(_dark ? 44 : 235, _dark ? 44 : 240, _dark ? 44 : 245);
        _list.Background = Solid(_dark ? 38 : 235, _dark ? 43 : 240, _dark ? 49 : 245);
        _list.Foreground = Solid(_dark ? 241 : 30, _dark ? 245 : 45, _dark ? 249 : 62);
        _status.Foreground = Solid(_dark ? 180 : 80, _dark ? 193 : 97, _dark ? 207 : 115);
        _header.Background = Solid(_dark ? 53 : 203, _dark ? 63 : 215, _dark ? 75 : 226);
        foreach (var cell in _header.Children.OfType<Border>())
            if (cell.Child is TextBlock label)
                label.Foreground = Solid(_dark ? 241 : 30, _dark ? 245 : 45, _dark ? 249 : 62);
        foreach (var box in new[] { _companyFilter, _branchFilter, _useFilter,
            _coverageFilter, _packageFilter, _statusFilter })
        {
            box.Background = Solid(_dark ? 48 : 223, _dark ? 55 : 232, _dark ? 64 : 241);
            box.Foreground = Solid(_dark ? 245 : 29, _dark ? 247 : 43, _dark ? 250 : 59);
            box.BorderBrush = Solid(_dark ? 111 : 139, _dark ? 130 : 162, _dark ? 148 : 184);
        }
        ApplyFilters();
    }

    private static SolidColorBrush Solid(int red, int green, int blue) =>
        new(Color.FromArgb(255, (byte)red, (byte)green, (byte)blue));

    private static void SetFilter(ComboBox box, string title, IEnumerable<string> values)
    {
        var selected = (box.SelectedItem as ComboBoxItem)?.Content?.ToString();
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = title + ": όλα" });
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct().OrderBy(value => value))
            box.Items.Add(new ComboBoxItem { Content = value });
        box.SelectedItem = box.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Content?.ToString() == selected) ?? box.Items[0];
    }

    private static string? Chosen(ComboBox box) => box.SelectedIndex > 0
        ? (box.SelectedItem as ComboBoxItem)?.Content?.ToString() : null;

    private void ApplyFilters()
    {
        var term = _search.Text.Trim();
        var company = Chosen(_companyFilter);
        var branch = Chosen(_branchFilter);
        var use = Chosen(_useFilter);
        var coverage = Chosen(_coverageFilter);
        var package = Chosen(_packageFilter);
        var status = Chosen(_statusFilter);
        var rows = _rules.Where(rule =>
            (term.Length == 0 || (rule.Insurer + rule.Branch + rule.VehicleUse +
                rule.Coverage + rule.Package).Contains(term, StringComparison.CurrentCultureIgnoreCase)) &&
            (company is null || rule.Insurer == company) &&
            (branch is null || (rule.Branch ?? "Όλοι") == branch) &&
            (use is null || (rule.VehicleUse ?? "Όλες") == use) &&
            (coverage is null || CommissionCoverageSelection.Read(rule.Coverage)
                .Contains(coverage, StringComparer.CurrentCultureIgnoreCase)) &&
            (package is null || (rule.Package ?? "Όλα") == package) &&
            (status is null || (rule.IsActive ? "Ενεργός" : "Ανενεργός") == status)).ToArray();
        _list.Items.Clear();
        foreach (var rule in rows)
            _list.Items.Add(new ListViewItem { Tag = rule,
                Content = TableRow([rule.Insurer, rule.Branch ?? "Όλοι", rule.VehicleUse ?? "Όλες",
                    CommissionCoverageSelection.Display(rule.Coverage), rule.Package ?? "Όλα",
                    $"{rule.OfficePercent:N2}%",
                    $"{rule.PartnerPercent:N2}%", rule.IsActive ? "Ενεργός" : "Ανενεργός"], false),
                Height = 40, Width = _tableWidth, Padding = new Thickness(0),
                Background = Solid(_dark ? 38 : 235, _dark ? 43 : 240, _dark ? 49 : 245),
                Foreground = Solid(_dark ? 241 : 30, _dark ? 245 : 45, _dark ? 249 : 62),
                HorizontalContentAlignment = HorizontalAlignment.Stretch });
        var candidates = status is null ? _organizationNames
            .Where(name => company is null || name == company)
            .Where(name => term.Length == 0 || name.Contains(term,
                StringComparison.CurrentCultureIgnoreCase))
            .Where(name => !rows.Any(rule => rule.Insurer == name &&
                (branch is null || rule.Branch == branch) &&
                (use is null || rule.VehicleUse == use) &&
                (coverage is null || CommissionCoverageSelection.Read(rule.Coverage)
                    .Contains(coverage, StringComparer.CurrentCultureIgnoreCase)) &&
                (package is null || rule.Package == package)))
            .Select(name => new CommissionCandidate(name, branch, use, coverage, package))
            .ToArray() : [];
        foreach (var candidate in candidates)
            _list.Items.Add(new ListViewItem { Tag = candidate,
                Content = TableRow([candidate.Organization, candidate.Branch ?? "Όλοι",
                    candidate.Use ?? "Όλες", candidate.Coverage ?? "Όλες",
                    candidate.Package ?? "Όλα", "—", "—", "Χωρίς κανόνα"], false),
                Height = 40, Width = _tableWidth, Padding = new Thickness(0),
                Foreground = Solid(_dark ? 241 : 30, _dark ? 245 : 45, _dark ? 249 : 62),
                HorizontalContentAlignment = HorizontalAlignment.Stretch });
        _status.Text = $"{rows.Length} κανόνες · {candidates.Length} διαθέσιμες επιλογές";
    }

    private Grid TableRow(string[] values, bool header)
    {
        var widths = new[] { 220, 145, 145, 145, 145, 105, 125, 170 };
        var grid = new Grid { Width = _tableWidth, Height = 40,
            Background = header ? new SolidColorBrush(_dark
                ? Color.FromArgb(255, 53, 63, 75) : Color.FromArgb(255, 203, 215, 226)) : null };
        foreach (var width in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(width * _tableWidth / widths.Sum()) });
        for (var index = 0; index < values.Length; index++)
        {
            var cell = new Border { BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 108, 125, 141)),
                Padding = new Thickness(9, 5, 9, 5),
                Child = new TextBlock { Text = values[index],
                    Foreground = Solid(_dark ? 241 : 30, _dark ? 245 : 45, _dark ? 249 : 62),
                    FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center } };
            ToolTipService.SetToolTip(cell, values[index]);
            Grid.SetColumn(cell, index); grid.Children.Add(cell);
        }
        return grid;
    }

    private void ResizeRow(Grid row)
    {
        var widths = new[] { 220, 145, 145, 145, 145, 105, 125, 170 };
        row.Width = _tableWidth;
        for (var index = 0; index < widths.Length; index++)
            row.ColumnDefinitions[index].Width = new GridLength(widths[index] *
                _tableWidth / widths.Sum());
    }

    private async Task EditAsync(CommissionRule? existing, CommissionCandidate? candidate = null)
    {
        try
        {
            var companies = await OrganizationNamesAsync();
            var branchOptions = await _filters.ListAsync("Κλάδοι");
            var useOptions = await _filters.ListAsync("Χρήσεις οχημάτων");
            var coverageOptions = await _filters.ListAsync("Καλύψεις");
            var packageOptions = await _filters.ListAsync("Πακέτα ασφαλίσεων");
            var selectedCoverages = CommissionCoverageSelection.Read(existing?.Coverage ?? candidate?.Coverage);
            var insurer = Suggest("Εταιρεία ή πρακτορείο *", existing?.Insurer ?? candidate?.Organization, companies);
            var branch = Suggest("Κλάδος (κενό = όλοι)", existing?.Branch ?? candidate?.Branch,
                () => CatalogChoices(branchOptions, insurer.Text));
            var use = Suggest("Χρήση οχήματος (κενό = όλες)", existing?.VehicleUse ?? candidate?.Use,
                () => CatalogChoices(useOptions, insurer.Text, branch.Text));
            var coveragePanel = new StackPanel { Spacing = 6 };
            coveragePanel.Children.Add(new TextBlock { Text = "Καλύψεις (χωρίς επιλογή = όλες)",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var coverageSearch = new TextBox { PlaceholderText = "Αναζήτηση κάλυψης…",
                Height = 36, VerticalContentAlignment = VerticalAlignment.Center };
            coveragePanel.Children.Add(coverageSearch);
            var coverageChecks = new List<CheckBox>();
            var coverageItems = new StackPanel { Spacing = 2 };
            var initialCoverages = selectedCoverages.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var coverageRendered = false;
            void RefreshCoverages()
            {
                if (coverageRendered)
                    foreach (var check in coverageChecks)
                    {
                        var name = check.Content?.ToString() ?? "";
                        if (check.IsChecked == true) initialCoverages.Add(name);
                        else initialCoverages.Remove(name);
                    }
                coverageChecks.Clear();
                coverageItems.Children.Clear();
                foreach (var value in CatalogChoices(coverageOptions, insurer.Text, branch.Text)
                    .Concat(selectedCoverages).Distinct(StringComparer.CurrentCultureIgnoreCase))
                {
                    var check = new CheckBox { Content = value,
                        IsChecked = initialCoverages.Contains(value),
                        Visibility = value.Contains(coverageSearch.Text,
                            StringComparison.CurrentCultureIgnoreCase)
                            ? Visibility.Visible : Visibility.Collapsed };
                    coverageChecks.Add(check);
                    coverageItems.Children.Add(check);
                }
                coverageRendered = true;
            }
            RefreshCoverages();
            insurer.TextChanged += (_, _) => RefreshCoverages();
            branch.TextChanged += (_, _) => RefreshCoverages();
            coveragePanel.Children.Add(new ScrollViewer { Content = coverageItems,
                MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            coverageSearch.TextChanged += (_, _) =>
            {
                foreach (var check in coverageChecks)
                    check.Visibility = check.Content?.ToString()?.Contains(coverageSearch.Text,
                        StringComparison.CurrentCultureIgnoreCase) == true
                        ? Visibility.Visible : Visibility.Collapsed;
            };
            var coverageActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var selectAll = new Button { Content = "Επιλογή όλων" };
            selectAll.Click += (_, _) => { foreach (var check in coverageChecks) check.IsChecked = true; };
            var clearAll = new Button { Content = "Αποεπιλογή όλων" };
            clearAll.Click += (_, _) => { foreach (var check in coverageChecks) check.IsChecked = false; };
            coverageActions.Children.Add(selectAll);
            coverageActions.Children.Add(clearAll);
            coveragePanel.Children.Add(coverageActions);
            var package = Suggest("Πακέτο (κενό = όλα)", existing?.Package ?? candidate?.Package,
                () => CatalogChoices(packageOptions, insurer.Text, branch.Text));
            var office = new TextBox { Header = "Εισερχόμενη προμήθεια έδρας επί καθαρών (%) *",
                Text = existing?.OfficePercent.ToString("N2") ?? "0" };
            var partner = new TextBox { Header = "Εξερχόμενη προμήθεια προς συνεργάτη επί καθαρών (%) *",
                Text = existing?.PartnerPercent.ToString("N2") ?? "0" };
            var active = new CheckBox { Content = "Ενεργός κανόνας",
                IsChecked = existing?.IsActive ?? true };
            var form = new StackPanel { Spacing = 8, Width = 460 };
            foreach (var control in new UIElement[]
                { insurer, branch, use, coveragePanel, package, office, partner, active })
                form.Children.Add(control);
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = existing is null ? "Νέος κανόνας προμηθειών" : "Επεξεργασία κανόνα",
                Content = new ScrollViewer { Content = form, MaxHeight = 560 },
                PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            if (!companies.Contains(insurer.Text.Trim(), StringComparer.CurrentCultureIgnoreCase))
                throw new ArgumentException("Επιλέξτε καταχωρημένη εταιρεία ή πρακτορείο από τα αποτελέσματα.");
            var branches = CatalogChoices(branchOptions, insurer.Text);
            var uses = CatalogChoices(useOptions, insurer.Text, branch.Text);
            var packages = CatalogChoices(packageOptions, insurer.Text, branch.Text);
            var sameCompany = existing is not null && string.Equals(existing.Insurer, insurer.Text.Trim(),
                StringComparison.CurrentCultureIgnoreCase);
            var sameBranch = sameCompany && string.Equals(existing!.Branch, branch.Text.Trim(),
                StringComparison.CurrentCultureIgnoreCase);
            foreach (var (box, values, label, unchanged) in new[]
                { (branch, branches, "κλάδο", sameCompany && existing!.Branch == branch.Text.Trim()),
                  (use, uses, "χρήση", sameBranch && existing!.VehicleUse == use.Text.Trim()),
                  (package, packages, "πακέτο", sameBranch && existing!.Package == package.Text.Trim()) })
                if (!string.IsNullOrWhiteSpace(box.Text) &&
                    !values.Contains(box.Text.Trim(), StringComparer.CurrentCultureIgnoreCase) && !unchanged)
                    throw new ArgumentException($"Επιλέξτε καταχωρημένο {label} από τα αποτελέσματα.");
            var allowedCoverages = CatalogChoices(coverageOptions, insurer.Text, branch.Text);
            if (coverageChecks.Where(check => check.IsChecked == true).Any(check =>
                !allowedCoverages.Contains(check.Content?.ToString() ?? "",
                    StringComparer.CurrentCultureIgnoreCase) &&
                !(sameBranch && selectedCoverages.Contains(check.Content?.ToString() ?? "",
                    StringComparer.CurrentCultureIgnoreCase))))
                throw new ArgumentException("Οι επιλεγμένες καλύψεις δεν ισχύουν για αυτή την εταιρεία και τον κλάδο.");
            var rule = new CommissionRule
            {
                Id = existing?.Id ?? Guid.Empty, Insurer = insurer.Text,
                Branch = branch.Text, VehicleUse = use.Text,
                Coverage = CommissionCoverageSelection.Write(coverageChecks
                    .Where(check => check.IsChecked == true)
                    .Select(check => check.Content?.ToString() ?? "")),
                Package = package.Text, OfficePercent = Percentage(office.Text),
                PartnerPercent = Percentage(partner.Text), IsActive = active.IsChecked == true
            };
            await _service.SaveAsync(rule);
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private static AutoSuggestBox Suggest(string label, string? value, string[] options)
        => Suggest(label, value, () => options);

    private static AutoSuggestBox Suggest(string label, string? value, Func<string[]> options)
    {
        var box = new AutoSuggestBox { Header = label, Text = value ?? "",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        void UpdateSuggestions(bool showAll = false)
        {
            box.ItemsSource = SearchSuggestions.FirstHundred(options(), showAll ? "" : box.Text);
            box.IsSuggestionListOpen = box.ItemsSource is string[] { Length: > 0 };
        }
        box.GotFocus += (_, _) => UpdateSuggestions(showAll: true);
        box.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                UpdateSuggestions();
        };
        box.SuggestionChosen += (_, args) => box.Text = args.SelectedItem as string ?? box.Text;
        return box;
    }

    private static string[] CatalogChoices(IEnumerable<FilterOption> options,
        string? company, string? branch = null) => options
        .Where(item => (item.CompanyScope.Length == 0 ||
                item.CompanyScope.Equals(company?.Trim(), StringComparison.CurrentCultureIgnoreCase)) &&
            (item.BranchScope.Length == 0 ||
                item.BranchScope.Equals(branch?.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        .Select(item => item.Value).Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(value => value).ToArray();

    private static decimal Percentage(string text)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"),
            out var value)) return value;
        throw new ArgumentException("Συμπληρώστε έγκυρα ποσοστά προμήθειας.");
    }

    private async Task DeleteAsync()
    {
        var rule = Selected();
        if (rule is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε κανόνα."); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή κανόνα",
            Content = "Να διαγραφεί ο επιλεγμένος κανόνας προμηθειών; Τα ποσοστά που έχουν ήδη αποτυπωθεί σε ασφαλιστήρια παραμένουν.",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try { await _service.DeleteAsync(rule.Id); await RefreshAsync(); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

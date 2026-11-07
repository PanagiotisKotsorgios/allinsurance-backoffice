using AllInsurance.Application.Filters;
using AllInsurance.Application.Business;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AllInsurance.Desktop.Views;

public sealed partial class FilterOptionsPage : Page
{
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private FilterOption[] _options = [];
    private string[] _companies = [];
    private FilterOption[] _branches = [];
    private bool _ready;
    private int _refreshVersion;

    public FilterOptionsPage()
    {
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Παραμετροποίηση δεδομένων",
            ["Κατηγορία", "Τιμή", "Εταιρεία / πρακτορείο", "Κλάδος"],
            (OptionsList.ItemsSource as IEnumerable<OptionRow> ?? [])
                .Select(row => new[] { row.Category, row.Value, row.Company, row.Branch }).ToArray()));
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(OptionsList);
        Loaded += async (_, _) =>
        {
            _ready = true;
            await LoadCategoriesAsync();
            await LoadScopesAsync();
            await RefreshAsync();
        };
    }

    private string Category => string.IsNullOrWhiteSpace(CustomCategory.Text)
        ? CategoryChoice.SelectedItem as string ?? "Πόλεις" : CustomCategory.Text.Trim();

    private async Task LoadCategoriesAsync()
    {
        var current = CategoryChoice.SelectedItem as string ?? "Πόλεις";
        var categories = await _filters.CategoriesAsync();
        CategoryChoice.ItemsSource = categories;
        CategoryChoice.SelectedItem = categories.Contains(current) ? current : categories.FirstOrDefault();
        UpdateScopeFields();
    }

    private static bool SupportsScopes(string category) => category is
        "Κλάδοι" or "Καλύψεις" or "Χρήσεις οχημάτων" or "Πακέτα ασφαλίσεων";

    private void UpdateScopeFields()
    {
        if (CompanyScopeInput is null || BranchScopeInput is null) return;
        var visible = SupportsScopes(Category) ? Visibility.Visible : Visibility.Collapsed;
        CompanyScopeInput.Visibility = visible;
        BranchScopeInput.Visibility = Category == "Κλάδοι" ? Visibility.Collapsed : visible;
    }

    private async Task LoadScopesAsync()
    {
        var companies = await _records.SearchAsync("companies");
        var agencies = await _records.SearchAsync("agencies");
        _companies = companies.Concat(agencies).Select(item => item.Title)
            .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value).ToArray();
        _branches = (await _filters.ListAsync("Κλάδοι")).ToArray();
    }

    private string[] AvailableBranches() => _branches
        .Where(item => item.CompanyScope.Length == 0 ||
            item.CompanyScope.Equals(CompanyScopeInput.Text.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .Select(item => item.Value).Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(value => value).ToArray();

    private void CompanyScope_Focused(object sender, RoutedEventArgs e) =>
        CompanyScopeInput.ItemsSource = SearchSuggestions.FirstHundred(_companies, "");
    private void CompanyScope_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = SearchSuggestions.FirstHundred(_companies, sender.Text);
    }
    private void CompanyScope_Chosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs e) =>
        sender.Text = e.SelectedItem as string ?? sender.Text;
    private void BranchScope_Focused(object sender, RoutedEventArgs e) =>
        BranchScopeInput.ItemsSource = SearchSuggestions.FirstHundred(AvailableBranches(), "");
    private void BranchScope_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = SearchSuggestions.FirstHundred(AvailableBranches(), sender.Text);
    }
    private void BranchScope_Chosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs e) =>
        sender.Text = e.SelectedItem as string ?? sender.Text;

    private (string Company, string Branch) Scope()
    {
        if (!SupportsScopes(Category)) return ("", "");
        var company = CompanyScopeInput.Text.Trim();
        var branch = Category == "Κλάδοι" ? "" : BranchScopeInput.Text.Trim();
        if (company.Length > 0 && !_companies.Contains(company, StringComparer.CurrentCultureIgnoreCase))
            throw new ArgumentException("Επιλέξτε καταχωρημένη εταιρεία ή πρακτορείο.");
        if (branch.Length > 0 && !AvailableBranches().Contains(branch,
            StringComparer.CurrentCultureIgnoreCase))
            throw new ArgumentException("Επιλέξτε καταχωρημένο κλάδο.");
        return (company, branch);
    }

    private async Task RefreshAsync()
    {
        if (!_ready || string.IsNullOrWhiteSpace(Category)) return;
        var category = Category;
        var version = ++_refreshVersion;
        try
        {
            var options = (await _filters.ListAsync(category)).ToArray();
            if (version != _refreshVersion) return;
            _options = options;
            ApplyFilter();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Category_Changed(object sender, SelectionChangedEventArgs e)
    { UpdateScopeFields(); await RefreshAsync(); }
    private async void CustomCategory_Changed(object sender, TextChangedEventArgs e)
    { UpdateScopeFields(); await RefreshAsync(); }
    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (OptionsList is null || SearchInput is null || StatusText is null) return;
        var term = SearchInput.Text.Trim();
        var visible = _options.Where(option => term.Length == 0 ||
            (option.Value + option.CompanyScope + option.BranchScope)
                .Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .Select(option => new OptionRow(option)).ToArray();
        OptionsList.ItemsSource = visible;
        StatusText.Text = $"{visible.Length} από {_options.Length} τιμές στην κατηγορία «{Category}».";
    }
    private void Options_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OptionsList.SelectedItem is OptionRow row)
        {
            ValueInput.Text = row.Value;
            CompanyScopeInput.Text = row.Option.CompanyScope;
            BranchScopeInput.Text = row.Option.BranchScope;
        }
    }

    private void Options_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        ValueInput.Focus(FocusState.Programmatic);

    private async void Add_Click(object sender, RoutedEventArgs e) => await SaveAsync(null);
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected();
        if (item is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε τιμή από τη λίστα."); return; }
        await SaveAsync(item.Id);
    }

    private async Task SaveAsync(Guid? id)
    {
        try
        {
            var category = Category;
            var scope = Scope();
            await _filters.SaveScopedAsync(id, category, ValueInput.Text, scope.Company, scope.Branch);
            ValueInput.Text = "";
            CustomCategory.Text = "";
            await LoadCategoriesAsync();
            await LoadScopesAsync();
            CategoryChoice.SelectedItem = category;
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected();
        if (item is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε τιμή από τη λίστα."); return; }
        var confirm = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή τιμής",
            Content = $"Να διαγραφεί η τιμή «{item.Value}» από την κατηγορία «{item.Category}»;",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση",
            DefaultButton = ContentDialogButton.Close };
        if (await DialogButtonStyles.ShowAsync(confirm, destructive: true) != ContentDialogResult.Primary) return;
        try { await _filters.DeleteAsync(item.Id); await RefreshAsync(); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private FilterOption? Selected() => (OptionsList.SelectedItem as OptionRow)?.Option;

    private sealed record OptionRow(FilterOption Option)
    {
        public string Category => Option.Category;
        public string Value => Option.Value;
        public string Company => Option.CompanyScope.Length == 0 ? "Όλες" : Option.CompanyScope;
        public string Branch => Option.BranchScope.Length == 0 ? "Όλοι" : Option.BranchScope;
    }

    private async void ImportText_Click(object sender, RoutedEventArgs e) => await ImportAsync(BulkInput.Text);

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".txt");
            InitializeWithWindow.Initialize(picker, App.MainWindowHandle());
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            await ImportAsync(await File.ReadAllTextAsync(file.Path));
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void ImportGreekCities_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Reference", "greek-settlements.txt");
            var values = await File.ReadAllLinesAsync(path);
            var count = await _filters.ImportAsync("Πόλεις", values);
            CustomCategory.Text = "";
            CategoryChoice.SelectedItem = "Πόλεις";
            await RefreshAsync();
            StatusText.Text = $"Προστέθηκαν {count} ελληνικοί οικισμοί από το GeoNames.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task ImportAsync(string text)
    {
        try
        {
            var category = Category;
            var scope = Scope();
            var values = text.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).Select(value => value.Trim('"'));
            var count = await _filters.ImportScopedAsync(category, values, scope.Company, scope.Branch);
            CustomCategory.Text = "";
            await LoadCategoriesAsync();
            CategoryChoice.SelectedItem = category;
            await RefreshAsync();
            StatusText.Text = $"Προστέθηκαν {count} νέες τιμές στην κατηγορία «{category}».";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

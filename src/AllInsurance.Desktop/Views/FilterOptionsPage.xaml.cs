using AllInsurance.Application.Filters;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AllInsurance.Desktop.Views;

public sealed partial class FilterOptionsPage : Page
{
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private FilterOption[] _options = [];
    private bool _ready;
    private int _refreshVersion;

    public FilterOptionsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _ready = true;
            await LoadCategoriesAsync();
            await RefreshAsync();
        };
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PageContent.Width = Math.Max(280, Math.Min(1100, e.NewSize.Width - 48));

    private string Category => string.IsNullOrWhiteSpace(CustomCategory.Text)
        ? CategoryChoice.SelectedItem as string ?? "Πόλεις" : CustomCategory.Text.Trim();

    private async Task LoadCategoriesAsync()
    {
        var current = CategoryChoice.SelectedItem as string ?? "Πόλεις";
        var categories = await _filters.CategoriesAsync();
        CategoryChoice.ItemsSource = categories;
        CategoryChoice.SelectedItem = categories.Contains(current) ? current : categories.FirstOrDefault();
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
            OptionsList.ItemsSource = _options.Select(option => option.Value).ToArray();
            StatusText.Text = $"{_options.Length} καταχωρισμένες τιμές στην κατηγορία «{category}».";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Category_Changed(object sender, SelectionChangedEventArgs e) => await RefreshAsync();
    private async void CustomCategory_Changed(object sender, TextChangedEventArgs e) => await RefreshAsync();
    private void Options_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OptionsList.SelectedItem is string value) ValueInput.Text = value;
    }

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
            await _filters.SaveAsync(id, category, ValueInput.Text);
            ValueInput.Text = "";
            CustomCategory.Text = "";
            await LoadCategoriesAsync();
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

    private FilterOption? Selected() => OptionsList.SelectedItem is string value
        ? _options.FirstOrDefault(item => item.Value == value) : null;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
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
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "greek-settlements.txt");
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
            var values = text.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).Select(value => value.Trim('"'));
            var count = await _filters.ImportAsync(category, values);
            CustomCategory.Text = "";
            await LoadCategoriesAsync();
            CategoryChoice.SelectedItem = category;
            await RefreshAsync();
            StatusText.Text = $"Προστέθηκαν {count} νέες τιμές στην κατηγορία «{category}».";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

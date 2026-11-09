using AllInsurance.Application.Bridges;
using AllInsurance.Application.Business;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class BridgeMappingsPage : Page
{
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly BridgeMappingStore _store;
    private BridgeMappingEntry[] _entries = [];
    private bool _ready;

    public BridgeMappingsPage()
    {
        InitializeComponent();
        _store = new BridgeMappingStore(_filters, _records);
        TableRowStriping.Attach(MappingsList);
        TableExportActions.Attach(ExportHost, this, () => new TableExportData(
            "Αντιστοιχίσεις γεφυρών εταιρειών",
            ["Εταιρεία", "Παραμετρικό", "Κωδικός", "Συνδεδεμένη τιμή", "Κατάσταση"],
            (MappingsList.ItemsSource as IEnumerable<MappingRow> ?? [])
                .Select(row => new[] { row.Company, row.Kind, row.Code, row.Target,
                    row.Validity }).ToArray()));
        Loaded += async (_, _) =>
        {
            if (_ready) return;
            CompanyFilter.ItemsSource = new[] { "Όλες", "Υδρόγειος", "Ορίζων 1964" };
            KindFilter.ItemsSource = new[] { "Όλα", "Κλάδοι", "Χρήσεις οχημάτων",
                "Καλύψεις", "Συνεργάτες" };
            ValidityFilter.ItemsSource = new[] { "Όλες", "Έγκυρες", "Χρειάζονται διόρθωση" };
            CompanyFilter.SelectedIndex = 0;
            KindFilter.SelectedIndex = 0;
            ValidityFilter.SelectedIndex = 0;
            _ready = true;
            await RefreshAsync();
        };
    }

    private async Task RefreshAsync()
    {
        try
        {
            var selected = (MappingsList.SelectedItem as MappingRow)?.Entry.Id;
            _entries = (await _store.ListAsync()).ToArray();
            ApplyFilters(selected);
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Choice_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters(Guid? selectedId = null)
    {
        if (!_ready || MappingsList is null) return;
        var term = SearchInput.Text.Trim();
        var source = CompanyFilter.SelectedItem as string;
        var kind = KindFilter.SelectedItem as string;
        var validity = ValidityFilter.SelectedItem as string;
        var visible = _entries.Where(item =>
                (source is null or "Όλες" || BridgeMappingStore.Company(item.Source) == source) &&
                (kind is null or "Όλα" || BridgeMappingStore.Label(item.Kind) == kind) &&
                (validity is null or "Όλες" ||
                    item.IsValid == (validity == "Έγκυρες")) &&
                (term.Length == 0 || CultureInfo.GetCultureInfo("el-GR").CompareInfo.IndexOf(
                    item.Code + " " + item.Target + " " + BridgeMappingStore.Company(item.Source) +
                    " " + BridgeMappingStore.Label(item.Kind), term,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
            .Select(item => new MappingRow(item)).ToArray();
        MappingsList.ItemsSource = visible;
        if (selectedId is Guid id)
            MappingsList.SelectedItem = visible.FirstOrDefault(item => item.Entry.Id == id);
        CountText.Text = $"{visible.Length} από {_entries.Length} αντιστοιχίσεις";
        TableRowStriping.Refresh(MappingsList, true);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true) Frame.GoBack();
        else Frame?.Navigate(typeof(CompanyBridgesPage));
    }

    private async void Add_Click(object sender, RoutedEventArgs e) => await EditAsync(null);
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (MappingsList.SelectedItem is not MappingRow selected)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πρώτα μία αντιστοίχιση."); return; }
        await EditAsync(selected.Entry);
    }

    private async void Mappings_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (MappingsList.SelectedItem is MappingRow selected) await EditAsync(selected.Entry);
    }

    private async Task EditAsync(BridgeMappingEntry? entry)
    {
        var sourceChoice = new ComboBox { Header = "Εταιρεία", Width = 410,
            ItemsSource = new[] { "Υδρόγειος", "Ορίζων 1964" } };
        var sourceIndex = entry is null ? CompanyFilter.SelectedIndex - 1 :
            entry.Source == NativeBridgeSource.Ydrogeios ? 0 : 1;
        sourceChoice.SelectedIndex = Math.Clamp(sourceIndex, 0, 1);
        var kindChoice = new ComboBox { Header = "Παραμετρικό", Width = 410,
            ItemsSource = new[] { "Κλάδοι", "Χρήσεις οχημάτων", "Καλύψεις", "Συνεργάτες" } };
        kindChoice.SelectedIndex = entry is null ? Math.Max(0, KindFilter.SelectedIndex - 1) :
            (int)entry.Kind;
        var codeInput = new TextBox { Header = "Κωδικός στο αρχείο *", Width = 410,
            MaxLength = 30, Text = entry?.Code ?? "" };
        var targetInput = new AutoSuggestBox { Header = "Συνδεδεμένη τιμή *", Width = 410,
            PlaceholderText = "Αναζήτηση υπάρχουσας τιμής", Text = entry is { Kind:
                BridgeMappingKind.Partner, PartnerId: null } ? "" : entry?.Target ?? "" };
        var createTarget = new CheckBox { Content =
            "Δημιουργία νέας τιμής στα παραμετρικά, αν δεν υπάρχει", IsChecked = false };
        var noPartner = new CheckBox { Content = "Χωρίς συνεργάτη", IsChecked =
            entry is { Kind: BridgeMappingKind.Partner, PartnerId: null } };
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = .72 };
        string[] suggestions = [];
        async Task UpdateChoicesAsync()
        {
            if (sourceChoice.SelectedIndex < 0 || kindChoice.SelectedIndex < 0) return;
            var source = (NativeBridgeSource)sourceChoice.SelectedIndex;
            var kind = (BridgeMappingKind)kindChoice.SelectedIndex;
            if (kind == BridgeMappingKind.Partner)
            {
                suggestions = (await _records.SearchAsync("partners"))
                    .Select(item => item.Title).ToArray();
                createTarget.Visibility = Visibility.Collapsed;
                noPartner.Visibility = Visibility.Visible;
                targetInput.IsEnabled = noPartner.IsChecked != true;
                hint.Text = "Επιλέξτε καταχωρημένο συνεργάτη. Για νέο συνεργάτη, δημιουργήστε πρώτα την καρτέλα του.";
            }
            else
            {
                suggestions = (await _filters.ScopedChoicesAsync(BridgeMappingStore.Label(kind),
                    BridgeMappingStore.Company(source), null)).ToArray();
                createTarget.Visibility = Visibility.Visible;
                noPartner.Visibility = Visibility.Collapsed;
                targetInput.IsEnabled = true;
                hint.Text = "Πληκτρολογήστε ή επιλέξτε τιμή. Νέα τιμή θα δημιουργηθεί μόνο αν επιλέξετε το πλαίσιο.";
            }
            targetInput.ItemsSource = SearchSuggestions.FirstHundred(suggestions, targetInput.Text);
        }
        sourceChoice.SelectionChanged += async (_, _) => await UpdateChoicesAsync();
        kindChoice.SelectionChanged += async (_, _) => await UpdateChoicesAsync();
        targetInput.GotFocus += (_, _) =>
            targetInput.ItemsSource = SearchSuggestions.FirstHundred(suggestions, "");
        targetInput.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                targetInput.ItemsSource = SearchSuggestions.FirstHundred(suggestions, targetInput.Text);
        };
        targetInput.SuggestionChosen += (_, args) =>
            targetInput.Text = args.SelectedItem as string ?? targetInput.Text;
        noPartner.Checked += (_, _) => targetInput.IsEnabled = false;
        noPartner.Unchecked += (_, _) => targetInput.IsEnabled = true;
        var content = new StackPanel { Spacing = 10, Width = 430 };
        content.Children.Add(sourceChoice);
        content.Children.Add(kindChoice);
        content.Children.Add(codeInput);
        content.Children.Add(targetInput);
        content.Children.Add(createTarget);
        content.Children.Add(noPartner);
        content.Children.Add(hint);
        await UpdateChoicesAsync();
        if (noPartner.IsChecked == true) targetInput.IsEnabled = false;
        var dialog = new ContentDialog { XamlRoot = XamlRoot,
            Title = entry is null ? "Νέα αντιστοίχιση γέφυρας" : "Επεξεργασία αντιστοίχισης",
            Content = content, PrimaryButtonText = "Αποθήκευση",
            CloseButtonText = "Ακύρωση" };
        while (await DialogButtonStyles.ShowAsync(dialog) == ContentDialogResult.Primary)
        {
            try
            {
                var source = (NativeBridgeSource)sourceChoice.SelectedIndex;
                var kind = (BridgeMappingKind)kindChoice.SelectedIndex;
                Guid? partnerId = null;
                if (kind == BridgeMappingKind.Partner && noPartner.IsChecked != true)
                {
                    var partner = (await _records.SearchAsync("partners"))
                        .FirstOrDefault(item => item.Title.Equals(targetInput.Text.Trim(),
                            StringComparison.CurrentCultureIgnoreCase));
                    if (partner is null) throw new ArgumentException("Επιλέξτε υπάρχοντα συνεργάτη.");
                    partnerId = partner.Id;
                }
                await _store.SaveAsync(entry?.Id, source, kind, codeInput.Text,
                    kind == BridgeMappingKind.Partner ? null : targetInput.Text,
                    partnerId, createTarget.IsChecked == true);
                await RefreshAsync();
                return;
            }
            catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (MappingsList.SelectedItem is not MappingRow selected)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πρώτα μία αντιστοίχιση."); return; }
        var answer = await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Διαγραφή αντιστοίχισης",
            Content = $"Να αφαιρεθεί ο κωδικός {selected.Code} από την {selected.Company}; " +
                "Η συνδεδεμένη τιμή θα παραμείνει στα δεδομένα του συστήματος.",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" });
        if (answer != ContentDialogResult.Primary) return;
        try { await _store.DeleteAsync(selected.Entry.Id); await RefreshAsync(); }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }

    private sealed record MappingRow(BridgeMappingEntry Entry)
    {
        public string Company => BridgeMappingStore.Company(Entry.Source);
        public string Kind => BridgeMappingStore.Label(Entry.Kind);
        public string Code => Entry.Code;
        public string Target => Entry.Target;
        public string Validity => Entry.IsValid ? "Έγκυρη" : "Χρειάζεται διόρθωση";
    }
}

using AllInsurance.Application.Business;
using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AllInsurance.Desktop.Views;

public sealed partial class CompanyBridgesPage : Page
{
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly IApplicationDbContextFactory _factory = App.Services.GetRequiredService<IApplicationDbContextFactory>();
    private static readonly string[] Insurers =
    [
        "Allianz", "Ατλαντική Ένωση", "Εθνική Ασφαλιστική", "Ευρωπαϊκή Πίστη",
        "Generali", "Groupama", "Interamerican", "Interlife", "Μινέττα", "NP Ασφαλιστική",
        "Ορίζων 1964", "Υδρόγειος"
    ];

    public CompanyBridgesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var companies = await _records.SearchAsync("companies");
            var agencies = await _records.SearchAsync("agencies");
            var names = Insurers.Select(name => (Name: name, Kind: "Ασφαλιστική"))
                .Concat(companies.Select(item => (item.Title, "Ασφαλιστική")))
                .Concat(agencies.Select(item => (item.Title, "Πρακτορείο")))
                .DistinctBy(item => item.Item1 + "|" + item.Item2, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(item => item.Item1).ThenBy(item => item.Item2).ToArray();
            Folders.Children.Clear();
            foreach (var (name, kind) in names)
            {
                var button = new Button { HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left, MinHeight = 56, Tag = (name, kind) };
                var folderIcon = new Image { Width = 32, Height = 32,
                    Source = new SvgImageSource(new Uri("ms-appx:///Assets/Icons/bridge-folder-light.svg")) };
                void UpdateFolderIcon() => folderIcon.Source = new SvgImageSource(new Uri(
                    folderIcon.ActualTheme == ElementTheme.Dark
                        ? "ms-appx:///Assets/Icons/bridge-folder-dark.svg"
                        : "ms-appx:///Assets/Icons/bridge-folder-light.svg"));
                folderIcon.Loaded += (_, _) => UpdateFolderIcon();
                folderIcon.ActualThemeChanged += (_, _) => UpdateFolderIcon();
                button.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children =
                {
                    folderIcon,
                    new TextBlock { Text = name, FontSize = 17, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "· " + kind, Opacity = .65, VerticalAlignment = VerticalAlignment.Center }
                } };
                button.Click += Folder_Click;
                Folders.Children.Add(button);
            }
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ValueTuple<string, string> source }) return;
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, App.MainWindowHandle());
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        try
        {
            var csv = await File.ReadAllTextAsync(file.Path);
            var firstLine = csv.Split(['\r', '\n'], 2)[0];
            var delimiter = firstLine.Count(character => character == ';') >= firstLine.Count(character => character == ',') ? ';' : ',';
            var parsed = ParseCsv(csv, delimiter);
            if (parsed.Length < 2) throw new InvalidOperationException("Το αρχείο δεν περιέχει εγγραφές.");
            var headers = parsed[0];
            var numberColumn = Column(headers, "policy_number", "Αριθμός ασφαλιστηρίου", "Αριθμός εγγράφου");
            var taxColumn = Column(headers, "customer_tax_number", "ΑΦΜ πελάτη", "ΑΦΜ");
            if (numberColumn < 0 || taxColumn < 0)
            {
                await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
                    Title = "Άγνωστη μορφή αρχείου", Content = "Για τυποποιημένο CSV χρειάζονται οι στήλες policy_number και customer_tax_number. Προαιρετικά: document_type, start_date, end_date, gross_premium, net_premium, branch. Για την πρωτότυπη μορφή της εταιρείας χρειάζεται δείγμα αρχείου.",
                    CloseButtonText = "Κλείσιμο" });
                return;
            }
            var rows = parsed.Skip(1).Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
            var preview = string.Join("\n", rows.Take(8).Select(row => numberColumn < row.Length ? row[numberColumn] : "—"));
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"Εισαγωγή · {source.Item1}",
                Content = $"Βρέθηκαν {rows.Length} γραμμές.\n\nΑριθμοί εγγράφων:\n{preview}\n\nΘα εισαχθούν μόνο εγγραφές με υπάρχον ΑΦΜ πελάτη. Οι ίδιες εγγραφές δεν θα εισαχθούν ξανά.",
                PrimaryButtonText = "Εισαγωγή", CloseButtonText = "Άκυρο" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            var result = await ImportAsync(source.Item1, source.Item2, rows, headers, numberColumn, taxColumn);
            await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
                Title = "Αποτέλεσμα εισαγωγής", Content = result, CloseButtonText = "Κλείσιμο" });
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task<string> ImportAsync(string source, string sourceKind, string[][] rows, string[] headers,
        int numberColumn, int taxColumn)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var customers = await db.Customers.AsNoTracking().Where(item => item.TaxNumber != null)
            .Select(item => new { item.Id, item.TaxNumber }).ToListAsync();
        var customerByTax = customers.GroupBy(item => item.TaxNumber!).ToDictionary(
            group => group.Key, group => group.First().Id);
        var existing = await _records.SearchAsync("policies");
        var known = existing.Where(item => item.Reference is not null)
            .Select(item => (item.Reference! + "|" + (sourceKind == "Πρακτορείο"
                ? BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Agency
                : BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Insurer)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var number = Cell(row, numberColumn);
            var tax = Cell(row, taxColumn);
            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(tax) ||
                !customerByTax.TryGetValue(tax, out var customerId))
            { errors.Add($"Γραμμή {index + 2}: λείπει αριθμός ή δεν βρέθηκε ΑΦΜ πελάτη."); continue; }
            var uniqueKey = number + "|" + source;
            if (known.Contains(uniqueKey)) { skipped++; continue; }
            var type = Value(row, headers, "document_type", "Τύπος εγγράφου");
            var documentType = type?.Trim().ToLowerInvariant() switch
            {
                "application" or "αίτηση" => "Αίτηση",
                "notice" or "ειδοποιητήριο" => "Ειδοποιητήριο",
                null or "" or "policy" or "ασφαλιστήριο" => "Ασφαλιστήριο",
                _ => null
            };
            if (documentType is null)
            { errors.Add($"Γραμμή {index + 2}: άγνωστος τύπος εγγράφου."); continue; }
            try
            {
                var data = new PolicyCardData
                {
                    Insurer = sourceKind == "Πρακτορείο" ? Value(row, headers, "insurer", "Ασφαλιστική") : source,
                    Agency = sourceKind == "Πρακτορείο" ? source : null,
                    Branch = Value(row, headers, "branch", "Κλάδος"),
                    PolicyNumber = documentType == "Ασφαλιστήριο" ? number : null,
                    ApplicationNumber = documentType == "Αίτηση" ? number : null,
                    NoticeNumber = documentType == "Ειδοποιητήριο" ? number : null,
                    StartDate = ParseDate(Value(row, headers, "start_date", "Έναρξη")),
                    EndDate = ParseDate(Value(row, headers, "end_date", "Λήξη")),
                    GrossPremium = ParseAmount(Value(row, headers, "gross_premium", "Μεικτά")),
                    NetPremium = ParseAmount(Value(row, headers, "net_premium", "Καθαρά"))
                };
                await _records.SaveAsync(new BusinessRecord
                {
                    Id = Guid.NewGuid(), SectionKey = "policies", Title = number,
                    Reference = number, DocumentType = documentType, CustomerId = customerId,
                    Status = documentType == "Ασφαλιστήριο"
                        ? data.EndDate < DateTime.Today ? "Ληγμένο" : "Ενεργό"
                        : "Σε εξέλιξη",
                    EventDate = data.StartDate, DetailsJson = BusinessRecordDetails.Write(data)
                });
                known.Add(uniqueKey);
                imported++;
            }
            catch (Exception exception) { errors.Add($"Γραμμή {index + 2}: {exception.Message}"); }
        }
        return $"Εισήχθησαν: {imported}\nΉδη υπήρχαν: {skipped}\nΣφάλματα: {errors.Count}" +
            (errors.Count > 0 ? "\n\n" + string.Join("\n", errors.Take(8)) : "");
    }

    private static int Column(string[] headers, params string[] names) => Array.FindIndex(headers,
        header => names.Any(name => header.Equals(name, StringComparison.CurrentCultureIgnoreCase)));
    private static string Cell(string[] row, int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";
    private static string? Value(string[] row, string[] headers, params string[] names)
    {
        var value = Cell(row, Column(headers, names));
        return value.Length == 0 ? null : value;
    }
    private static DateTime? ParseDate(string? value)
    {
        if (value is null) return null;
        if (DateTime.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"],
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None,
            out var date)) return date;
        throw new FormatException($"Άκυρη ημερομηνία: {value}");
    }
    private static decimal? ParseAmount(string? value)
    {
        if (value is null) return null;
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.GetCultureInfo("el-GR"), out var amount) ||
            decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out amount)) return amount;
        throw new FormatException($"Άκυρο ποσό: {value}");
    }

    private static string[][] ParseCsv(string csv, char delimiter)
    {
        var rows = new List<string[]>();
        var cells = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == delimiter && !quoted) { cells.Add(value.ToString().Trim()); value.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                cells.Add(value.ToString().Trim()); value.Clear();
                rows.Add(cells.ToArray()); cells.Clear();
            }
            else value.Append(character);
        }
        if (quoted) throw new FormatException("Το CSV έχει εισαγωγικά που δεν κλείνουν.");
        if (value.Length > 0 || cells.Count > 0)
        {
            cells.Add(value.ToString().Trim());
            rows.Add(cells.ToArray());
        }
        return rows.ToArray();
    }
}

using AllInsurance.Application.Bridges;
using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class CompanyBridgesPage
{
    private readonly IFilterOptionService _bridgeFilters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly ICustomerService _bridgeCustomers = App.Services.GetRequiredService<ICustomerService>();

    private async Task ImportNativeAsync(string path, NativeBridgeSource source, string company)
    {
        var rows = NativePolicyBridge.Read(path, source);
        await using var db = await _factory.CreateDbContextAsync();
        var knownCustomers = await db.Customers.AsNoTracking().Where(item => item.TaxNumber != null)
            .Select(item => new { item.Id, item.TaxNumber }).ToListAsync();
        var customerIds = knownCustomers.GroupBy(item => item.TaxNumber!)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var missing = rows.Where(row => !customerIds.ContainsKey(row.CustomerTaxNumber))
            .Select(row => row.CustomerTaxNumber).Distinct().Count();
        var existing = await _records.SearchAsync("policies");
        var known = existing.Where(item => item.Reference is not null)
            .Where(item => string.Equals(BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Insurer,
                company, StringComparison.CurrentCultureIgnoreCase))
            .Select(item => item.Reference!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createCustomers = new CheckBox { Content =
            $"Δημιουργία {missing} πελατών που δεν βρέθηκαν με ΑΦΜ (από τα στοιχεία του αρχείου)",
            IsChecked = false, IsEnabled = missing > 0 };
        var createVehicles = new CheckBox { Content =
            "Δημιουργία οχημάτων που δεν βρέθηκαν με πινακίδα (για συνδέσεις ασφαλιστηρίων)",
            IsChecked = false };
        var renewalRule = new ComboBox { Header = "Νέα εγγραφή ή ανανεωτήριο", Width = 370,
            ItemsSource = new[] { "Αυτόματα από τον αριθμό προηγούμενου συμβολαίου",
                "Όλες ως νέες εγγραφές", "Όλες ως ανανεωτήρια" }, SelectedIndex = 0 };
        var preview = new StackPanel { Spacing = 12, MaxWidth = 560 };
        preview.Children.Add(new TextBlock { Text =
            $"{company}: {rows.Count} εγγραφές. Ήδη καταχωρισμένες: {rows.Count(row => known.Contains(row.PolicyNumber))}. " +
            $"Πελάτες χωρίς αντιστοίχιση ΑΦΜ: {missing}.\n\n" +
            "Στη συνέχεια θα ζητηθεί αντιστοίχιση για κλάδους, χρήσεις, καλύψεις και συνεργάτες. " +
            "Οι εγγραφές με άλυτες αντιστοιχίσεις θα παραλειφθούν.", TextWrapping = TextWrapping.Wrap });
        preview.Children.Add(createCustomers);
        preview.Children.Add(createVehicles);
        preview.Children.Add(renewalRule);
        if (await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Προεπισκόπηση εισαγωγής ασφαλιστηρίων", Content = preview,
            PrimaryButtonText = "Συνέχεια", CloseButtonText = "Ακύρωση" }) != ContentDialogResult.Primary) return;

        var companies = await _records.SearchAsync("companies");
        var companyRecord = companies.FirstOrDefault(item =>
            string.Equals(item.Title, company, StringComparison.CurrentCultureIgnoreCase));
        if (companyRecord is null)
        {
            companyRecord = new BusinessRecord { SectionKey = "companies", Title = company,
                Status = "Ενεργή" };
            companyRecord.Id = await _records.SaveAsync(companyRecord);
        }
        var branches = await MapFilterCodesAsync(source, "Κλάδοι", "κλάδο", company,
            rows.Select(row => row.BranchCode));
        if (branches is null) return;
        var uses = await MapFilterCodesAsync(source, "Χρήσεις οχημάτων", "χρήση", company,
            rows.Select(row => row.UseCode));
        if (uses is null) return;
        var coverages = await MapFilterCodesAsync(source, "Καλύψεις", "κάλυψη", company,
            rows.SelectMany(row => row.Coverages.Select(coverage => coverage.Code)));
        if (coverages is null) return;
        var partners = await _records.SearchAsync("partners");
        var partnerMap = await MapPartnerCodesAsync(source,
            rows.Select(row => row.PartnerCode), partners);
        if (partnerMap is null) return;

        var vehicles = (await _records.SearchAsync("vehicles"))
            .Select(item => (item.Id, item.CustomerId,
                BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson).Registration))
            .Where(item => !string.IsNullOrWhiteSpace(item.Registration)).ToList();
        var existingByNumber = existing.Where(item => item.Reference is not null)
            .GroupBy(item => item.Reference!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var cancellations = 0;
        var duplicates = 0;
        var unresolved = 0;
        var errors = new List<string>();
        var processedCancellations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.IsCancellation)
            {
                if (!processedCancellations.Add(row.PolicyNumber)) { duplicates++; continue; }
                var current = existingByNumber.GetValueOrDefault(row.PolicyNumber)?
                    .FirstOrDefault(item => string.Equals(
                        BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Insurer,
                        company, StringComparison.CurrentCultureIgnoreCase));
                if (current is not null)
                {
                    if (!customerIds.TryGetValue(row.CustomerTaxNumber, out var cancellingCustomer) ||
                        current.CustomerId != cancellingCustomer)
                    {
                        errors.Add($"Γραμμή {row.Line}: η ακύρωση δεν αντιστοιχεί στον πελάτη του ασφαλιστηρίου.");
                        continue;
                    }
                    if (current.Status == "Ακυρωμένο") { duplicates++; continue; }
                    try
                    {
                        var oldData = BusinessRecordDetails.Read<PolicyCardData>(current.DetailsJson);
                        oldData.CancellationDate = row.IssueDate;
                        oldData.CancellationReason = "Ακύρωση από γέφυρα " + company;
                        current.Status = "Ακυρωμένο";
                        current.DetailsJson = BusinessRecordDetails.Write(oldData);
                        await _records.SaveAsync(current);
                        cancellations++;
                    }
                    catch (Exception error)
                    { errors.Add($"Γραμμή {row.Line}: ακύρωση: {error.Message}"); }
                    continue;
                }
            }
            else if (known.Contains(row.PolicyNumber)) { duplicates++; continue; }
            if (!branches.TryGetValue(row.BranchCode, out var branch) ||
                !uses.TryGetValue(row.UseCode, out var use) ||
                row.Coverages.Any(coverage => !coverages.ContainsKey(coverage.Code)) ||
                !partnerMap.ContainsKey(row.PartnerCode))
            { unresolved++; continue; }
            if (!customerIds.TryGetValue(row.CustomerTaxNumber, out var customerId))
            {
                if (createCustomers.IsChecked != true) { unresolved++; continue; }
                try
                {
                    var parts = row.CustomerName.Split(' ', 2,
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    customerId = await _bridgeCustomers.SaveAsync(new Customer
                    {
                        LastName = parts.FirstOrDefault() ?? "Χωρίς επώνυμο",
                        FirstName = parts.Length > 1 ? parts[1] : "Χωρίς όνομα",
                        TaxNumber = row.CustomerTaxNumber
                    });
                    customerIds.Add(row.CustomerTaxNumber, customerId);
                }
                catch (Exception error)
                { errors.Add($"Γραμμή {row.Line}: πελάτης: {error.Message}"); continue; }
            }
            try
            {
                var isRenewal = !row.IsCancellation && (renewalRule.SelectedIndex == 2 ||
                    renewalRule.SelectedIndex == 0 && row.PreviousPolicyNumber is not null);
                var previous = row.PreviousPolicyNumber is null ? null :
                    existingByNumber.GetValueOrDefault(row.PreviousPolicyNumber)?
                        .FirstOrDefault(item => item.CustomerId == customerId &&
                            string.Equals(BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).Insurer,
                                company, StringComparison.CurrentCultureIgnoreCase));
                var vehicle = vehicles.FirstOrDefault(item => item.CustomerId == customerId &&
                    string.Equals(item.Registration, row.Registration,
                        StringComparison.CurrentCultureIgnoreCase));
                if (vehicle.Id == Guid.Empty && row.Registration is not null &&
                    createVehicles.IsChecked == true)
                {
                    var vehicleRecord = new BusinessRecord
                    {
                        SectionKey = "vehicles", Title = row.Registration,
                        Reference = row.Registration, CustomerId = customerId,
                        Status = "Ενεργό", DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
                        {
                            Registration = row.Registration, Make = row.Make, Use = use,
                            People = [new VehiclePerson { CustomerId = customerId,
                                Role = "Ιδιοκτήτης", OwnershipPercent = 100,
                                IsPrimaryOwner = true }]
                        })
                    };
                    vehicleRecord.Id = await _records.SaveAsync(vehicleRecord);
                    vehicle = (vehicleRecord.Id, customerId, row.Registration);
                    vehicles.Add(vehicle);
                }
                var data = NativePolicyBridge.ToPolicyData(row, companyRecord.Id, company,
                    branch, use, coverages, partnerMap[row.PartnerCode],
                    vehicle.Id == Guid.Empty ? null : vehicle.Id, isRenewal, previous?.Id);
                var record = new BusinessRecord
                {
                    SectionKey = "policies", Title = row.PolicyNumber,
                    Reference = row.PolicyNumber, CustomerId = customerId,
                    DocumentType = "Ασφαλιστήριο", EventDate = row.StartDate,
                    Status = row.IsCancellation ? "Ακυρωμένο" :
                        row.EndDate < DateTime.Today ? "Ληγμένο" : "Ενεργό",
                    DetailsJson = BusinessRecordDetails.Write(data)
                };
                record.Id = await _records.SaveAsync(record);
                known.Add(row.PolicyNumber);
                existingByNumber[row.PolicyNumber] = [record];
                imported++;
            }
            catch (Exception error) { errors.Add($"Γραμμή {row.Line}: {error.Message}"); }
        }
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Αποτέλεσμα εισαγωγής", Content =
                $"Εισήχθησαν: {imported}\nΑκυρώσεις υπαρχόντων: {cancellations}\n" +
                $"Ήδη υπήρχαν: {duplicates}\n" +
                $"Χωρίς αντιστοίχιση: {unresolved}\nΣφάλματα: {errors.Count}" +
                (errors.Count == 0 ? "" : "\n\n" + string.Join("\n", errors.Take(12))),
            CloseButtonText = "Εντάξει" });
    }

    private async Task<Dictionary<string, string>?> MapFilterCodesAsync(NativeBridgeSource source,
        string category, string label, string company, IEnumerable<string> rawCodes)
    {
        var mappingCategory = $"bridge:{source}:{category}";
        var saved = (await _bridgeFilters.ListAsync(mappingCategory))
            .Select(item => (item.Id, Parts: item.Value.Split('|', 2)))
            .Where(item => item.Parts.Length == 2)
            .GroupBy(item => item.Parts[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var choices = (await _bridgeFilters.ScopedChoicesAsync(category, company, null)).ToList();
        var result = saved.Where(item => choices.Contains(item.Value.Parts[1],
                StringComparer.CurrentCultureIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value.Parts[1], StringComparer.Ordinal);
        foreach (var code in rawCodes.Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal).OrderBy(code => code))
        {
            if (result.ContainsKey(code)) continue;
            var input = new AutoSuggestBox { PlaceholderText = $"Αναζήτηση ή νέο όνομα για {label}",
                Width = 380 };
            void Suggest() => input.ItemsSource = SearchSuggestions.FirstHundred(choices, input.Text);
            input.GotFocus += (_, _) => Suggest();
            input.TextChanged += (_, _) => Suggest();
            input.SuggestionChosen += (_, args) => input.Text = args.SelectedItem as string ?? input.Text;
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock { Text =
                $"Κωδικός {code} ({source}). Επιλέξτε υπάρχουσα τιμή ή γράψτε νέα.",
                TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(input);
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = $"Αντιστοίχιση: {label}", Content = panel,
                PrimaryButtonText = "Αντιστοίχιση / δημιουργία",
                CloseButtonText = "Ακύρωση εισαγωγής" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
            var name = input.Text.Trim();
            if (name.Length == 0) throw new InvalidOperationException($"Δεν επιλέχθηκε {label} για {code}.");
            if (!choices.Contains(name, StringComparer.CurrentCultureIgnoreCase))
            {
                await _bridgeFilters.ImportScopedAsync(category, [name], company, null);
                choices.Add(name);
            }
            await _bridgeFilters.SaveAsync(saved.TryGetValue(code, out var old) ? old.Id : null,
                mappingCategory, $"{code}|{name}");
            result[code] = name;
        }
        return result;
    }

    private async Task<Dictionary<string, Guid?>?> MapPartnerCodesAsync(NativeBridgeSource source,
        IEnumerable<string> rawCodes, IReadOnlyList<BusinessRecord> partners)
    {
        var category = $"bridge:{source}:partner";
        var saved = (await _bridgeFilters.ListAsync(category))
            .Select(item => (item.Id, Parts: item.Value.Split('|', 2)))
            .Where(item => item.Parts.Length == 2)
            .GroupBy(item => item.Parts[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var result = saved.Where(item => !Guid.TryParse(item.Value.Parts[1], out var id) ||
                partners.Any(partner => partner.Id == id))
            .ToDictionary(item => item.Key, item => Guid.TryParse(item.Value.Parts[1], out var id)
                ? (Guid?)id : null, StringComparer.Ordinal);
        foreach (var code in rawCodes.Distinct().OrderBy(code => code))
        {
            if (result.ContainsKey(code)) continue;
            var names = partners.Select(item => item.Title).ToArray();
            var input = new AutoSuggestBox { Width = 380,
                PlaceholderText = "Αναζήτηση συνεργάτη" };
            input.GotFocus += (_, _) => input.ItemsSource = SearchSuggestions.FirstHundred(names, "");
            input.TextChanged += (_, _) => input.ItemsSource = SearchSuggestions.FirstHundred(names, input.Text);
            input.SuggestionChosen += (_, args) => input.Text = args.SelectedItem as string ?? input.Text;
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = $"Συνεργάτης κωδικού {code}", Content = input,
                PrimaryButtonText = "Αντιστοίχιση", SecondaryButtonText = "Χωρίς συνεργάτη",
                CloseButtonText = "Ακύρωση εισαγωγής" };
            var answer = await DialogButtonStyles.ShowAsync(dialog);
            if (answer == ContentDialogResult.None) return null;
            var partner = answer == ContentDialogResult.Primary
                ? partners.FirstOrDefault(item => string.Equals(item.Title, input.Text.Trim(),
                    StringComparison.CurrentCultureIgnoreCase)) : null;
            if (answer == ContentDialogResult.Primary && partner is null)
                throw new InvalidOperationException($"Ο συνεργάτης για τον κωδικό {code} δεν βρέθηκε.");
            await _bridgeFilters.SaveAsync(saved.TryGetValue(code, out var old) ? old.Id : null,
                category, $"{code}|{partner?.Id}");
            result[code] = partner?.Id;
        }
        return result;
    }
}

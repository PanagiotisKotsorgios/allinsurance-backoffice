using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Application.Filters;
using AllInsurance.Application.OfficeOperations;
using AllInsurance.Application.Partners;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

internal static class FinancialMovementCreateDialog
{
    private sealed record Choice(Guid Id, string Name);
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    public static async Task<FinancialMovement?> ShowAsync(XamlRoot root, bool receipt)
    {
        try
        {
            var customers = App.Services.GetRequiredService<ICustomerService>();
            var records = App.Services.GetRequiredService<IBusinessRecordService>();
            var partners = App.Services.GetRequiredService<IPartnerService>();
            var office = App.Services.GetRequiredService<OfficeOperationService>();
            var bulk = App.Services.GetRequiredService<BulkFinancialMovementService>();
            var ledger = App.Services.GetRequiredService<FinancialMovementService>();
            var filterService = App.Services.GetRequiredService<IFilterOptionService>();
            var currency = await App.Services.GetRequiredService<ITransactionSettingsService>().GetCurrencyAsync();
            var customerChoices = (await customers.SearchAsync())
                .Select(item => new Choice(item.Id, item.FullName)).ToArray();
            var partnerChoices = (await records.SearchAsync("partners"))
                .Select(item => new Choice(item.Id, item.Title)).ToArray();
            var companyChoices = (await records.SearchAsync("companies"))
                .Select(item => new Choice(item.Id, item.Title)).ToArray();
            var agencyChoices = (await records.SearchAsync("agencies"))
                .Select(item => new Choice(item.Id, item.Title)).ToArray();
            var policyRecords = (await records.SearchAsync("policies"))
                .Where(item => item.DocumentType is null or "Ασφαλιστήριο").ToArray();
            var methods = (await filterService.ChoicesAsync("Τρόποι πληρωμής"))
                .Concat(["Μετρητά", "Τραπεζική μεταφορά", "Κάρτα", "Ταχυπληρωμή"])
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
            var sections = new[] { "Πελάτης", "Συνεργάτης", "Ασφαλιστική", "Πρακτορείο", "Γραφείο" };
            var section = new ComboBox { Header = RequiredHeader.Create("Πρόσωπο / φορέας"),
                ItemsSource = sections, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var party = new SearchablePicker<Choice>("Επιλογή καρτέλας *", customerChoices,
                item => item.Name);
            var policy = new SearchablePicker<BusinessRecord>("Σύνδεση με ασφαλιστήριο", [],
                item => item.Title);
            void UpdateChoices()
            {
                party.Visibility = section.SelectedIndex == 4 ? Visibility.Collapsed : Visibility.Visible;
                party.SetItems(section.SelectedIndex switch
                {
                    1 => partnerChoices, 2 => companyChoices, 3 => agencyChoices,
                    4 => [], _ => customerChoices
                });
                policy.SetItems([]);
                policy.Visibility = section.SelectedIndex is 0 or 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            section.SelectionChanged += (_, _) => UpdateChoices();
            party.SelectionChanged += (_, _) =>
            {
                var target = party.SelectedItem;
                policy.SetItems(target is null ? [] : policyRecords.Where(item =>
                    section.SelectedIndex == 0 ? item.CustomerId == target.Id :
                        BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).PartnerId == target.Id));
            };
            var amount = new TextBox { Header = RequiredHeader.Create($"Ποσό ({currency})"),
                PlaceholderText = "π.χ. 125,50" };
            var method = new ComboBox { Header = RequiredHeader.Create("Τρόπος πληρωμής"),
                ItemsSource = methods, SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            var reference = new TextBox { Header = "Αριθμός συναλλαγής / αναφορά" };
            var description = new TextBox { Header = "Αιτιολογία / τι αφορά η κίνηση",
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 75 };
            var error = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var form = new StackPanel { Width = 450, Spacing = 9,
                Children = { section, party, policy, amount, method, reference, description, error } };
            UpdateChoices();
            var dialog = new ContentDialog { XamlRoot = root,
                Title = receipt ? "Νέα είσπραξη" : "Νέα πληρωμή",
                Content = new ScrollViewer { Content = form, MaxHeight = 540 },
                PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                if ((section.SelectedIndex != 4 && party.SelectedItem is null) ||
                    !decimal.TryParse(amount.Text, NumberStyles.Number, Greek, out var value) ||
                    value <= 0 || method.SelectedItem is not string ||
                    (receipt && policy.SelectedItem is null &&
                     string.IsNullOrWhiteSpace(description.Text)) ||
                    (section.SelectedIndex == 4 && string.IsNullOrWhiteSpace(description.Text)))
                {
                    error.Text = "Επιλέξτε καρτέλα, θετικό ποσό και τρόπο πληρωμής. " +
                        "Για είσπραξη συνδέστε ασφαλιστήριο ή γράψτε τι αφορά.";
                    error.Visibility = Visibility.Visible;
                    args.Cancel = true;
                }
            };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
            var selected = party.SelectedItem;
            var valueAmount = decimal.Parse(amount.Text, NumberStyles.Number, Greek);
            var direction = receipt ? "receipt" : "payment";
            var paymentMethod = (string)method.SelectedItem;
            Guid? id = null;
            switch (section.SelectedIndex)
            {
                case 0:
                    id = (await customers.AddTransactionAsync(selected!.Id, direction, valueAmount,
                        description.Text, paymentMethod, reference.Text, policy.SelectedItem?.Id)).Id;
                    break;
                case 1:
                    id = await partners.AddMovementAsync(selected!.Id, direction, valueAmount,
                        paymentMethod, reference.Text, description.Text, policy.SelectedItem?.Id);
                    break;
                case 2 or 3:
                    var batch = await bulk.PostAsync(section.SelectedIndex == 2 ? "companies" : "agencies",
                        direction, [new BulkMovementItem(selected!.Id, valueAmount)],
                        paymentMethod, reference.Text, description.Text, currency);
                    id = batch.MovementIds[0];
                    break;
                default:
                    id = await office.SaveAsync(new OfficeOperation
                    {
                        Kind = receipt ? "income" : "expense", OccurredOn = DateTime.Today,
                        Amount = valueAmount, Description = description.Text,
                        PaymentMethod = paymentMethod, Reference = reference.Text,
                        Category = "Γενική κίνηση"
                    });
                    break;
            }
            var created = (await ledger.ListAsync()).FirstOrDefault(item => item.Id == id);
            await UiAlerts.ShowSuccessAsync(root, "Η οικονομική κίνηση καταχωρίστηκε επιτυχώς.");
            return created;
        }
        catch (Exception error)
        {
            await UiAlerts.ShowErrorAsync(root, error.Message);
            return null;
        }
    }
}

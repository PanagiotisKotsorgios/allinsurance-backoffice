using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using AllInsurance.Desktop.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomerCardWindow
{
    private async Task TransferCreditAsync()
    {
        if (!await EnsureSavedAsync()) return;
        try
        {
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            var available = -CustomerService.Balance(_customer);
            if (available <= 0)
                throw new InvalidOperationException("Ο πελάτης δεν έχει διαθέσιμο πιστωτικό υπόλοιπο για μεταφορά.");
            var candidates = (await _service.SearchAsync()).Where(item => item.Id != _customer.Id)
                .OrderBy(item => item.FullName).ToArray();
            var destination = new SearchablePicker<CustomerListItem>("Πελάτης προορισμού *",
                candidates, item => item.FullName);
            var amount = new TextBox { Header = $"Ποσό (έως {available:N2} {CurrencySymbol}) *",
                PlaceholderText = "0,00", MinHeight = 40,
                VerticalContentAlignment = VerticalAlignment.Center };
            var noPolicy = new PolicyChoice(null, "Χωρίς σύνδεση");
            var policies = new SearchablePicker<PolicyChoice>("Ασφαλιστήριο πελάτη προορισμού",
                [noPolicy], item => item.Name, noPolicy);
            destination.SelectionChanged += async (_, _) =>
            {
                if (destination.SelectedItem is not CustomerListItem selected)
                {
                    policies.SetItems([noPolicy], noPolicy);
                    return;
                }
                var target = await _service.GetAsync(selected.Id);
                var choices = new[] { noPolicy }
                    .Concat(target?.BusinessRecords.Where(item => item.SectionKey == "policies")
                        .Select(item => new PolicyChoice(item.Id,
                            $"{item.Reference ?? item.Title} · {item.DocumentType ?? "Ασφαλιστήριο"}"))
                        ?? [] ).ToArray();
                policies.SetItems(choices, noPolicy);
            };
            var reason = new TextBox { Header = "Αιτιολογία / αναφορά ακύρωσης",
                PlaceholderText = "Προαιρετική αιτιολογία", MinHeight = 40 };
            var form = new StackPanel { Spacing = 10, Width = 440 };
            form.Children.Add(new TextBlock { Text =
                "Το διαθέσιμο πιστωτικό θα μεταφερθεί με δύο συνδεδεμένες κινήσεις. " +
                "Η μεταφορά δεν δημιουργεί νέα είσπραξη μετρητών.", TextWrapping = TextWrapping.Wrap });
            form.Children.Add(destination);
            form.Children.Add(amount);
            form.Children.Add(policies);
            form.Children.Add(reason);
            var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
                Title = "Μεταφορά υπολοίπου σε άλλον πελάτη", Content = form,
                PrimaryButtonText = "Μεταφορά", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            if (destination.SelectedItem is not CustomerListItem targetCustomer)
                throw new ArgumentException("Επιλέξτε πελάτη προορισμού.");
            await _service.TransferCreditAsync(_customer.Id, targetCustomer.Id,
                ParseAmount(amount.Text, false), policies.SelectedItem?.Id, reason.Text);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs(preserveSelectedTab: true);
            CustomerChanged?.Invoke(this, EventArgs.Empty);
            RelatedCustomerChanged?.Invoke(this, targetCustomer.Id);
            _status.Text = "Η μεταφορά πιστωτικού καταχωρίστηκε και στους δύο πελάτες.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }
}

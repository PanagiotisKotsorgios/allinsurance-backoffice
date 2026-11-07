using AllInsurance.Desktop.Controls;
using AllInsurance.Application.Filters;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class OfficeOperationsPage
{
    private sealed record Choice(Guid Id, string Name);

    private async Task EditAsync(OfficeOperation? existing)
    {
        var form = new StackPanel { Spacing = 8, Width = 440 };
        void Add(string label, Control control)
        {
            form.Children.Add(new TextBlock { Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            form.Children.Add(control);
        }
        var date = new EditableDateInput { Date = new DateTimeOffset(existing?.OccurredOn ?? DateTime.Today),
            MinHeight = 40 };
        Add("Ημερομηνία *", date);
        var description = new TextBox { Text = existing?.Description ?? "", MinHeight = 40,
            PlaceholderText = "Π.χ. αγορά γραφικής ύλης" };
        if (!_overcommissions) Add("Περιγραφή *", description);
        ComboBox? kind = null;
        TextBox? category = null;
        TextBox? amount = null;
        ComboBox? method = null;
        TextBox? gross = null;
        TextBox? net = null;
        TextBox? partnerCommission = null;
        TextBox? over = null;
        SearchablePicker<Choice>? partner = null;
        SearchablePicker<Choice>? company = null;
        if (_overcommissions)
        {
            var partners = _partners.Select(item => new Choice(item.Id, item.Title)).ToArray();
            partner = new SearchablePicker<Choice>("Συνεργάτης *", partners, item => item.Name,
                partners.FirstOrDefault(item => item.Id == existing?.PartnerId));
            form.Children.Add(partner);
            var companies = _companies.Select(item => new Choice(item.Id, item.Title)).ToArray();
            company = new SearchablePicker<Choice>("Ασφαλιστική εταιρεία *", companies,
                item => item.Name, companies.FirstOrDefault(item => item.Id == existing?.CompanyId));
            form.Children.Add(company);
            gross = new TextBox { Text = Format(existing?.GrossPremium), PlaceholderText = "0,00" };
            net = new TextBox { Text = Format(existing?.NetPremium), PlaceholderText = "0,00" };
            partnerCommission = new TextBox { Text = Format(existing?.PartnerCommission), PlaceholderText = "0,00" };
            over = new TextBox { Text = Format(existing?.OverCommission), PlaceholderText = "0,00" };
            Add("Μικτά ασφάλιστρα (€) *", gross);
            Add("Καθαρά ασφάλιστρα (€) *", net);
            Add("Προμήθεια συνεργάτη (€) *", partnerCommission);
            Add("Υπερπρομήθεια (€) *", over);
        }
        else
        {
            kind = new ComboBox { ItemsSource = new[] { "Έξοδο", "Έσοδο" },
                SelectedIndex = existing?.Kind == "income" ? 1 : 0, MinHeight = 40 };
            Add("Είδος κίνησης *", kind);
            category = new TextBox { Text = existing?.Category ?? "",
                PlaceholderText = "Π.χ. ενοίκιο, εξοπλισμός, λειτουργικά" };
            Add("Κατηγορία", category);
            amount = new TextBox { Text = Format(existing?.Amount), PlaceholderText = "0,00" };
            Add("Ποσό (€) *", amount);
            var methods = (await App.Services.GetRequiredService<IFilterOptionService>()
                    .ChoicesAsync("Τρόποι πληρωμής"))
                .Concat(["Μετρητά", "Τραπεζική μεταφορά", "Κάρτα", "Ταχυπληρωμή"])
                .Concat(existing?.PaymentMethod is string oldMethod ? [oldMethod] : [])
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
            method = new ComboBox { ItemsSource = methods, MinHeight = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedItem = existing?.PaymentMethod is string savedMethod && methods.Contains(savedMethod)
                    ? savedMethod : methods.FirstOrDefault() };
            Add("Τρόπος πληρωμής", method);
        }
        var reference = new TextBox { Text = existing?.Reference ?? "",
            PlaceholderText = "Παραστατικό ή αριθμός αναφοράς" };
        if (!_overcommissions) Add("Αναφορά", reference);
        var notes = new TextBox { Text = existing?.Notes ?? "", AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinHeight = 75 };
        if (!_overcommissions) Add("Σημειώσεις", notes);
        var dialog = new ContentDialog { XamlRoot = XamlRoot,
            Title = existing is null ? _overcommissions ? "Νέα υπερπρομήθεια" : "Νέα κίνηση γραφείου"
                : "Επεξεργασία εγγραφής",
            Content = new ScrollViewer { Content = form, MaxHeight = 540 },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Άκυρο" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            var operation = new OfficeOperation
            {
                Id = existing?.Id ?? Guid.Empty,
                Kind = _overcommissions ? "overcommission" : kind!.SelectedIndex == 1 ? "income" : "expense",
                OccurredOn = date.Date?.Date ?? throw new ArgumentException("Επιλέξτε ημερομηνία."),
                Description = _overcommissions
                    ? $"{partner?.SelectedItem?.Name ?? "Συνεργάτης"} · {company?.SelectedItem?.Name ?? "Ασφαλιστική"}"
                    : description.Text,
                Category = category?.Text,
                Amount = _overcommissions ? 0 : Parse(amount!.Text, "ποσό"),
                PaymentMethod = method?.SelectedItem as string,
                Reference = _overcommissions ? existing?.Reference : reference.Text,
                Notes = _overcommissions ? existing?.Notes : notes.Text,
                PartnerId = partner?.SelectedItem?.Id,
                CompanyId = company?.SelectedItem?.Id,
                GrossPremium = gross is null ? null : Parse(gross.Text, "μικτά ασφάλιστρα"),
                NetPremium = net is null ? null : Parse(net.Text, "καθαρά ασφάλιστρα"),
                OfficeCommission = existing?.OfficeCommission,
                PartnerCommission = partnerCommission is null ? null : Parse(partnerCommission.Text, "προμήθεια συνεργάτη"),
                OverCommission = over is null ? null : Parse(over.Text, "υπερπρομήθεια")
            };
            await _service.SaveAsync(operation);
            await RefreshAsync();
            await UiAlerts.ShowSuccessAsync(XamlRoot, "Η εργασία σας αποθηκεύτηκε επιτυχώς.");
        }
        catch (Exception error) { await UiAlerts.ShowErrorAsync(XamlRoot, error.Message); }
    }

    private static decimal Parse(string text, string label)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, Greek, out var value))
            throw new ArgumentException($"Συμπληρώστε έγκυρο αριθμό στο πεδίο «{label}».");
        return value;
    }

    private static string Format(decimal? value) => value?.ToString("N2", Greek) ?? "";
}

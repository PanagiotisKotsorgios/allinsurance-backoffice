using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
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
            PlaceholderText = _overcommissions ? "Περιγραφή υπερπρομήθειας" : "Π.χ. αγορά γραφικής ύλης" };
        Add("Περιγραφή *", description);
        ComboBox? kind = null;
        TextBox? category = null;
        TextBox? amount = null;
        TextBox? method = null;
        TextBox? gross = null;
        TextBox? net = null;
        TextBox? office = null;
        TextBox? over = null;
        ComboBox? partner = null;
        ComboBox? company = null;
        if (_overcommissions)
        {
            partner = new ComboBox { ItemsSource = _partners.Select(item =>
                    new Choice(item.Id, item.Title)).ToArray(),
                DisplayMemberPath = nameof(Choice.Name), MinHeight = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            partner.SelectedItem = (partner.ItemsSource as Choice[])?.FirstOrDefault(item =>
                item.Id == existing?.PartnerId);
            Add("Συνεργάτης *", partner);
            company = new ComboBox { ItemsSource = _companies.Select(item =>
                    new Choice(item.Id, item.Title)).ToArray(),
                DisplayMemberPath = nameof(Choice.Name), MinHeight = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            company.SelectedItem = (company.ItemsSource as Choice[])?.FirstOrDefault(item =>
                item.Id == existing?.CompanyId);
            Add("Ασφαλιστική εταιρεία *", company);
            gross = new TextBox { Text = Format(existing?.GrossPremium), PlaceholderText = "0,00" };
            net = new TextBox { Text = Format(existing?.NetPremium), PlaceholderText = "0,00" };
            office = new TextBox { Text = Format(existing?.OfficeCommission), PlaceholderText = "0,00" };
            over = new TextBox { Text = Format(existing?.OverCommission), PlaceholderText = "0,00" };
            Add("Μικτά ασφάλιστρα (€) *", gross);
            Add("Καθαρά ασφάλιστρα (€) *", net);
            Add("Προμήθεια έδρας (€) *", office);
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
            method = new TextBox { Text = existing?.PaymentMethod ?? "",
                PlaceholderText = "Μετρητά, τραπεζική μεταφορά, κάρτα…" };
            Add("Τρόπος πληρωμής", method);
        }
        var reference = new TextBox { Text = existing?.Reference ?? "",
            PlaceholderText = "Παραστατικό ή αριθμός αναφοράς" };
        Add("Αναφορά", reference);
        var notes = new TextBox { Text = existing?.Notes ?? "", AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, MinHeight = 75 };
        Add("Σημειώσεις", notes);
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
                Description = description.Text,
                Category = category?.Text,
                Amount = _overcommissions ? 0 : Parse(amount!.Text, "ποσό"),
                PaymentMethod = method?.Text,
                Reference = reference.Text,
                Notes = notes.Text,
                PartnerId = (partner?.SelectedItem as Choice)?.Id,
                CompanyId = (company?.SelectedItem as Choice)?.Id,
                GrossPremium = gross is null ? null : Parse(gross.Text, "μικτά ασφάλιστρα"),
                NetPremium = net is null ? null : Parse(net.Text, "καθαρά ασφάλιστρα"),
                OfficeCommission = office is null ? null : Parse(office.Text, "προμήθεια έδρας"),
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

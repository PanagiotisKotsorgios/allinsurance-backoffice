using AllInsurance.Application.Filters;
using AllInsurance.Application.OfficeOperations;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

internal sealed record BulkFinancialTarget(Guid Id, string Name);

internal static class BulkFinancialMovementDialog
{
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    public static async Task<bool> ShowAsync(XamlRoot root, string section, string direction,
        IReadOnlyList<BulkFinancialTarget> targets)
    {
        if (targets.Count == 0)
        {
            await UiAlerts.ShowErrorAsync(root, "Επιλέξτε πρώτα καρτέλες με τα τετράγωνα επιλογής.");
            return false;
        }
        try
        {
            var filters = App.Services.GetRequiredService<IFilterOptionService>();
            var currency = await App.Services.GetRequiredService<ITransactionSettingsService>().GetCurrencyAsync();
            var methods = (await filters.ChoicesAsync("Τρόποι πληρωμής"))
                .Concat(["Μετρητά", "Τραπεζική μεταφορά", "Κάρτα", "Ταχυπληρωμή"])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
            var method = new ComboBox { Header = RequiredHeader.Create("Τρόπος πληρωμής"),
                ItemsSource = methods, SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            var commonAmount = new TextBox { Header = "Κοινό ποσό για όλες τις καρτέλες",
                PlaceholderText = "π.χ. 50,00", VerticalContentAlignment = VerticalAlignment.Center };
            var apply = new Button { Content = "Εφαρμογή ποσού σε όλες", MinHeight = 36 };
            var reference = new TextBox { Header = "Αριθμός συναλλαγής / κοινή αναφορά",
                PlaceholderText = "Αν μείνει κενό, δημιουργείται κωδικός μαζικής κίνησης" };
            var description = new TextBox { Header = "Αιτιολογία", AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, Height = 68 };
            var total = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var error = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var fields = new List<(BulkFinancialTarget Target, TextBox Amount)>();
            var rows = new StackPanel { Spacing = 5 };
            foreach (var target in targets)
            {
                var row = new Grid { ColumnSpacing = 10,
                    ColumnDefinitions = { new ColumnDefinition(),
                        new ColumnDefinition { Width = new GridLength(145) } } };
                row.Children.Add(new TextBlock { Text = target.Name, TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center });
                var amount = new TextBox { PlaceholderText = $"Ποσό ({currency})",
                    VerticalContentAlignment = VerticalAlignment.Center };
                Grid.SetColumn(amount, 1); row.Children.Add(amount); rows.Children.Add(row);
                fields.Add((target, amount));
                amount.TextChanged += (_, _) => UpdateTotal();
            }
            apply.Click += (_, _) =>
            {
                foreach (var (_, amount) in fields) amount.Text = commonAmount.Text;
                UpdateTotal();
            };
            var form = new StackPanel { Spacing = 9, Width = 520 };
            form.Children.Add(new TextBlock { Text = $"{targets.Count} επιλεγμένες καρτέλες · {currency}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            form.Children.Add(method); form.Children.Add(commonAmount); form.Children.Add(apply);
            form.Children.Add(reference); form.Children.Add(description);
            form.Children.Add(new TextBlock { Text = "Ποσό ανά καρτέλα", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            form.Children.Add(rows); form.Children.Add(total); form.Children.Add(error);
            UpdateTotal();
            BulkMovementItem[] validated = [];
            var dialog = new ContentDialog { XamlRoot = root,
                Title = direction == "receipt" ? "Μαζική είσπραξη" : "Μαζική πληρωμή",
                Content = new ScrollViewer { Content = form, MaxHeight = 520 },
                PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                if (method.SelectedItem is not string || fields.Any(field =>
                    !TryAmount(field.Amount.Text, out var parsedAmount)))
                {
                    error.Text = "Επιλέξτε τρόπο πληρωμής και συμπληρώστε θετικό ποσό σε κάθε καρτέλα.";
                    error.Visibility = Visibility.Visible;
                    args.Cancel = true;
                    return;
                }
                validated = fields.Select(field => new BulkMovementItem(field.Target.Id,
                    ParseAmount(field.Amount.Text))).ToArray();
            };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return false;
            var batch = await App.Services.GetRequiredService<BulkFinancialMovementService>()
                .PostAsync(section, direction, validated, (string)method.SelectedItem,
                    reference.Text, description.Text, currency);
            await UiAlerts.ShowSuccessAsync(root,
                $"Καταχωρήθηκαν {validated.Length} κινήσεις. Αναφορά: {batch.Reference}.");
            return true;

            void UpdateTotal()
            {
                var sum = fields.Sum(field => TryAmount(field.Amount.Text, out var value) ? value : 0m);
                total.Text = $"Σύνολο: {sum:N2} {currency}";
            }
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(root, exception.Message);
            return false;
        }
    }

    private static bool TryAmount(string text, out decimal value) =>
        decimal.TryParse(text.Trim(), NumberStyles.Number, Greek, out value) &&
        value > 0 && value <= 999_999_999m;

    private static decimal ParseAmount(string text) => decimal.Parse(text.Trim(), NumberStyles.Number, Greek);
}

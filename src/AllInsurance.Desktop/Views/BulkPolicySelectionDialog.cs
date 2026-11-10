using AllInsurance.Application.Abstractions;
using AllInsurance.Desktop.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

internal static class BulkPolicySelectionDialog
{
    // null means cancelled; an empty result means a general movement per customer.
    public static async Task<BulkFinancialTarget[]?> ShowAsync(XamlRoot root,
        IReadOnlyList<BulkFinancialTarget> customers)
    {
        var ids = customers.Select(item => item.Id).ToArray();
        var names = customers.ToDictionary(item => item.Id, item => item.Name);
        await using var db = await App.Services.GetRequiredService<IApplicationDbContextFactory>()
            .CreateDbContextAsync();
        var policies = await db.BusinessRecords.AsNoTracking()
            .Where(item => item.SectionKey == "policies" && item.CustomerId.HasValue &&
                ids.Contains(item.CustomerId.Value))
            .OrderBy(item => item.Title)
            .Select(item => new { item.Id, item.CustomerId, item.Title,
                item.Reference, item.DocumentType })
            .ToListAsync();

        var mode = new ComboBox { Header = "Τρόπος μαζικής κίνησης",
            ItemsSource = new[] { "Γενική κίνηση ανά πελάτη", "Κίνηση ανά επιλεγμένο ασφαλιστήριο" },
            SelectedIndex = policies.Count > 0 && customers.Count == 1 ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var search = new TextBox { PlaceholderText = "Αναζήτηση πελάτη ή ασφαλιστηρίου",
            VerticalContentAlignment = VerticalAlignment.Center };
        var rows = new StackPanel { Spacing = 0 };
        var checks = new List<(Guid Id, string Name, CheckBox Check, FrameworkElement Row)>();
        foreach (var policy in policies)
        {
            var customerId = policy.CustomerId!.Value;
            var customer = names[customerId];
            var number = string.IsNullOrWhiteSpace(policy.Reference) ? policy.Title : policy.Reference;
            var name = $"{customer} · {number} · {policy.DocumentType ?? "Ασφαλιστήριο"}";
            var check = new CheckBox { Content = name, MinHeight = 32,
                VerticalAlignment = VerticalAlignment.Center };
            var row = new Border { Child = check, BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"] };
            checks.Add((policy.Id, name, check, row));
            rows.Children.Add(row);
        }
        var list = new ScrollViewer { Content = rows, MaxHeight = 330,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed };
        void UpdateMode()
        {
            var perPolicy = mode.SelectedIndex == 1;
            search.Visibility = list.Visibility = perPolicy ? Visibility.Visible : Visibility.Collapsed;
            info.Text = perPolicy
                ? $"Επιλέξτε ασφαλιστήρια από τους {customers.Count} πελάτες. Κάθε ασφαλιστήριο θα συνδεθεί με τη δική του κίνηση."
                : $"Θα δημιουργηθεί μία γενική κίνηση για κάθε έναν από τους {customers.Count} πελάτες, με κοινή αναφορά.";
        }
        mode.SelectionChanged += (_, _) => UpdateMode();
        search.TextChanged += (_, _) =>
        {
            var term = search.Text.Trim();
            foreach (var entry in checks)
                entry.Row.Visibility = entry.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
        };
        if (policies.Count == 0) mode.IsEnabled = false;
        UpdateMode();
        var form = new StackPanel { Width = 520, Spacing = 8,
            Children = { mode, info, search, list, error } };
        var dialog = new ContentDialog { XamlRoot = root, Title = "Επιλογή μαζικής κίνησης",
            Content = form, PrimaryButtonText = "Συνέχεια", CloseButtonText = "Ακύρωση" };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (mode.SelectedIndex != 1 || checks.Any(entry => entry.Check.IsChecked == true)) return;
            error.Text = "Επιλέξτε τουλάχιστον ένα ασφαλιστήριο.";
            error.Visibility = Visibility.Visible;
            args.Cancel = true;
        };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
        return mode.SelectedIndex == 1
            ? checks.Where(entry => entry.Check.IsChecked == true)
                .Select(entry => new BulkFinancialTarget(entry.Id, entry.Name)).ToArray()
            : [];
    }
}

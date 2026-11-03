using AllInsurance.Application.Commissions;
using AllInsurance.Application.Filters;
using AllInsurance.Application.Business;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed class CommissionSettingsPage : Page
{
    private readonly ICommissionRuleService _service = App.Services.GetRequiredService<ICommissionRuleService>();
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly IFilterOptionService _filters = App.Services.GetRequiredService<IFilterOptionService>();
    private readonly ListView _list = new() { MinHeight = 250 };
    private readonly ListView _exposureList = new() { Height = 190 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private CommissionRule[] _rules = [];

    public CommissionSettingsPage()
    {
        var root = new Grid { Padding = new Thickness(22), RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(new TextBlock { Text = "Παραμετροποίηση προμηθειών",
            FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text =
            "Βασικοί κανόνες ανά εταιρεία, κλάδο, χρήση, κάλυψη και πακέτο. Τα ποσοστά υπολογίζονται επί των μικτών ασφαλίστρων. Η εκκαθάριση αποδόσεων προς εταιρείες θα εξειδικευτεί σε επόμενη φάση.",
            TextWrapping = TextWrapping.Wrap });
        root.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new ActionButton { Kind = "Add", Label = "Νέος κανόνας" };
        add.Click += async (_, _) => await EditAsync(null);
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        edit.Click += async (_, _) =>
        {
            if (Selected() is CommissionRule rule) await EditAsync(rule);
            else await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε κανόνα.");
        };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        delete.Click += async (_, _) => await DeleteAsync();
        actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete);
        Grid.SetRow(actions, 1); root.Children.Add(actions);
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(_status);
        content.Children.Add(_list);
        content.Children.Add(new TextBlock { Text = "Εκτίμηση προς απόδοση ανά ασφαλιστική",
            FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 0) });
        content.Children.Add(new TextBlock { Text =
            "Μικτά ασφάλιστρα μείον προμήθεια γραφείου, πριν από καταχωρήσεις αποδόσεων προς εταιρείες.",
            TextWrapping = TextWrapping.Wrap });
        content.Children.Add(_exposureList);
        var scroller = new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroller, 2); root.Children.Add(scroller);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private CommissionRule? Selected() => _list.SelectedIndex is >= 0 and var index &&
        index < _rules.Length ? _rules[index] : null;

    private async Task RefreshAsync()
    {
        try
        {
            _rules = (await _service.ListAsync()).ToArray();
            _list.ItemsSource = _rules.Select(rule =>
                $"{(rule.IsActive ? "●" : "○")}  {rule.Insurer}  ·  " +
                $"{rule.Branch ?? "Όλοι οι κλάδοι"}  ·  {rule.VehicleUse ?? "Όλες οι χρήσεις"}  ·  " +
                $"{rule.Coverage ?? "Όλες οι καλύψεις"}  ·  {rule.Package ?? "Όλα τα πακέτα"}  ·  " +
                $"Γραφείο {rule.OfficePercent:N2}%  ·  Συνεργάτης {rule.PartnerPercent:N2}%").ToArray();
            _status.Text = $"{_rules.Length} κανόνες. Οι ειδικότεροι ενεργοί κανόνες έχουν προτεραιότητα.";
            var policies = (await _records.SearchAsync("policies"))
                .Where(item => item.DocumentType is null or "Ασφαλιστήριο")
                .Select(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson))
                .Where(item => !string.IsNullOrWhiteSpace(item.Insurer)).ToArray();
            var exposure = policies.GroupBy(item => item.Insurer!,
                StringComparer.CurrentCultureIgnoreCase).OrderBy(group => group.Key)
                .Select(group =>
                {
                    var gross = group.Sum(item => item.GrossPremium ?? 0);
                    var office = group.Sum(item => decimal.Round((item.GrossPremium ?? 0) *
                        (item.OfficeCommissionPercent ?? 0) / 100m, 2));
                    return $"{group.Key}  ·  Μικτά {gross:N2} €  ·  Προμήθεια γραφείου {office:N2} €  ·  " +
                        $"Εκτιμώμενη απόδοση {gross - office:N2} €";
                }).ToArray();
            _exposureList.ItemsSource = exposure.Length == 0 ?
                ["Δεν υπάρχουν εκδομένα ασφαλιστήρια με ασφαλιστική εταιρεία."] : exposure;
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async Task EditAsync(CommissionRule? existing)
    {
        try
        {
            var companies = (await _records.SearchAsync("companies"))
                .Select(item => item.Title).Distinct().ToArray();
            var branches = (await _filters.ChoicesAsync("Κλάδοι")).ToArray();
            var uses = (await _filters.ChoicesAsync("Χρήσεις οχημάτων")).ToArray();
            var coverages = (await _filters.ChoicesAsync("Καλύψεις")).ToArray();
            var packages = (await _filters.ChoicesAsync("Πακέτα ασφαλίσεων")).ToArray();
            var insurer = Suggest("Ασφαλιστική εταιρεία *", existing?.Insurer, companies);
            var branch = Suggest("Κλάδος (κενό = όλοι)", existing?.Branch, branches);
            var use = Suggest("Χρήση οχήματος (κενό = όλες)", existing?.VehicleUse, uses);
            var coverage = Suggest("Κάλυψη (κενό = όλες)", existing?.Coverage, coverages);
            var package = Suggest("Πακέτο (κενό = όλα)", existing?.Package, packages);
            var office = new TextBox { Header = "Προμήθεια γραφείου επί μικτών (%) *",
                Text = existing?.OfficePercent.ToString("N2") ?? "0" };
            var partner = new TextBox { Header = "Προμήθεια συνεργάτη επί μικτών (%) *",
                Text = existing?.PartnerPercent.ToString("N2") ?? "0" };
            var active = new CheckBox { Content = "Ενεργός κανόνας",
                IsChecked = existing?.IsActive ?? true };
            var form = new StackPanel { Spacing = 8, Width = 460 };
            foreach (var control in new UIElement[]
                { insurer, branch, use, coverage, package, office, partner, active })
                form.Children.Add(control);
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = existing is null ? "Νέος κανόνας προμηθειών" : "Επεξεργασία κανόνα",
                Content = new ScrollViewer { Content = form, MaxHeight = 560 },
                PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            var rule = new CommissionRule
            {
                Id = existing?.Id ?? Guid.Empty, Insurer = insurer.Text,
                Branch = branch.Text, VehicleUse = use.Text, Coverage = coverage.Text,
                Package = package.Text, OfficePercent = Percentage(office.Text),
                PartnerPercent = Percentage(partner.Text), IsActive = active.IsChecked == true
            };
            await _service.SaveAsync(rule);
            await RefreshAsync();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private static AutoSuggestBox Suggest(string label, string? value, string[] options)
    {
        var box = new AutoSuggestBox { Header = label, Text = value ?? "",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        box.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            box.ItemsSource = options.Where(item => item.Contains(box.Text,
                StringComparison.CurrentCultureIgnoreCase)).Take(30).ToArray();
        };
        box.SuggestionChosen += (_, args) => box.Text = args.SelectedItem as string ?? box.Text;
        return box;
    }

    private static decimal Percentage(string text)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"),
            out var value)) return value;
        throw new ArgumentException("Συμπληρώστε έγκυρα ποσοστά προμήθειας.");
    }

    private async Task DeleteAsync()
    {
        var rule = Selected();
        if (rule is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε κανόνα."); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Διαγραφή κανόνα",
            Content = "Να διαγραφεί ο επιλεγμένος κανόνας προμηθειών; Τα ποσοστά που έχουν ήδη αποτυπωθεί σε ασφαλιστήρια παραμένουν.",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try { await _service.DeleteAsync(rule.Id); await RefreshAsync(); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }
}

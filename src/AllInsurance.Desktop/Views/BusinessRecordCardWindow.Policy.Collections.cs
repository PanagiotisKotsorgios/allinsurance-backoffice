using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow
{
    private UIElement BuildPolicyTable<T>(string title, List<T> items, string[] headers,
        Func<T, string[]> cells, Func<T?, Task<T?>> edit) where T : class
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη" };
        var change = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        var remove = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        _editControls.Add(add); _editControls.Add(change); _editControls.Add(remove);
        buttons.Children.Add(add); buttons.Children.Add(change); buttons.Children.Add(remove);
        panel.Children.Add(buttons);
        var width = Math.Max(150, (int)Math.Floor((_contentWidth - 52) / headers.Length));
        var total = width * headers.Length;
        var header = new Grid { Width = total,
            Background = new SolidColorBrush(IsDarkTheme() ? Color.FromArgb(255, 55, 65, 77) :
                Color.FromArgb(255, 224, 233, 241)) };
        foreach (var _ in headers) header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var index = 0; index < headers.Length; index++)
        {
            var cell = new Border { Child = new TextBlock { Text = headers[index],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis },
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = new Thickness(0, 0, index == headers.Length - 1 ? 0 : 1, 0),
                Padding = new Thickness(9, 7, 9, 7) };
            Grid.SetColumn(cell, index); header.Children.Add(cell);
        }
        panel.Children.Add(new Border { Child = header, Width = total + 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Width = total + 2, Height = 205, Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1) };
        TableRowStriping.Attach(list);
        panel.Children.Add(list);
        var pager = new TablePagination(list);
        panel.Children.Add(pager.Bar);
        void Refresh()
        {
            list.Items.Clear();
            foreach (var item in items)
            {
                var row = new Grid { Width = total, Height = 36 };
                foreach (var _ in headers)
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                var values = cells(item);
                for (var index = 0; index < headers.Length; index++)
                {
                    var cell = new Border { Child = new TextBlock { Text = index < values.Length ? values[index] : "—",
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center },
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = new Thickness(0, 0, index == headers.Length - 1 ? 0 : 1, 1),
                        Padding = new Thickness(9, 4, 9, 4) };
                    if (index == 0 && title is "Πράσινες κάρτες" or "Πρόσθετες πράξεις")
                    {
                        cell.Background = title == "Πράσινες κάρτες"
                            ? PolicyStatusColors.Background("Πράσινη κάρτα", null, null)
                            : PolicyStatusColors.Background("Πρόσθετη πράξη",
                                values.Length > 1 ? values[1] : null, null);
                        if (cell.Child is TextBlock label)
                            label.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    }
                    Grid.SetColumn(cell, index); row.Children.Add(cell);
                }
                list.Items.Add(new ListViewItem { Content = row, Tag = item,
                    Height = 36, Width = total, Padding = new Thickness(0), Margin = new Thickness(0) });
            }
            pager.Capture();
        }
        async Task EditSelected()
        {
            if (!_editing) return;
            if ((list.SelectedItem as ListViewItem)?.Tag is not T item)
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρώτα γραμμή."); return; }
            var updated = await edit(item);
            if (updated is null) return;
            var index = items.IndexOf(item);
            if (index >= 0) items[index] = updated;
            Refresh(); _status.Text = "Υπάρχουν μη αποθηκευμένες αλλαγές.";
        }
        add.Click += async (_, _) =>
        {
            var added = await edit(null);
            if (added is null) return;
            items.Add(added); Refresh(); _status.Text = "Υπάρχουν μη αποθηκευμένες αλλαγές.";
        };
        change.Click += async (_, _) => await EditSelected();
        list.DoubleTapped += async (_, _) => await EditSelected();
        remove.Click += async (_, _) =>
        {
            if ((list.SelectedItem as ListViewItem)?.Tag is not T item)
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρώτα γραμμή."); return; }
            var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
                Title = "Διαγραφή γραμμής", Content = "Να αφαιρεθεί η επιλεγμένη γραμμή από την καρτέλα;",
                PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            items.Remove(item); Refresh(); _status.Text = "Υπάρχουν μη αποθηκευμένες αλλαγές.";
        };
        Refresh();
        return panel;
    }

    private UIElement BuildPolicyObjectsEditor() => BuildPolicyTable("Ασφαλισμένα αντικείμενα",
        _policyData.InsuredObjects, ["Είδος", "Περιγραφή", "Ασφαλιστική αξία"], item =>
        [item.Kind, item.Description, item.InsuredValue?.ToString("N2") ?? "—"], EditObjectAsync);

    private UIElement BuildPolicyCoveragesEditor() => BuildPolicyTable("Αναλυτικές καλύψεις",
        _policyData.Coverages, ["Κάλυψη", "Ασφαλισμένο ποσό", "Απαλλαγή", "Ασφάλιστρο"], item =>
        [item.Name, item.SumInsured?.ToString("N2") ?? "—",
            item.Deductible?.ToString("N2") ?? "—", item.Premium?.ToString("N2") ?? "—"], EditCoverageAsync);

    private UIElement BuildPolicyEndorsementsEditor() => BuildPolicyTable("Πρόσθετες πράξεις",
        _policyData.Endorsements, ["Αριθμός", "Είδος", "Ισχύς", "Διαφορά ασφαλίστρου"], item =>
        [item.Number, item.Type, item.EffectiveDate?.ToString("dd/MM/yyyy") ?? "—",
            item.PremiumDifference?.ToString("N2") ?? "—"], EditEndorsementAsync);

    private UIElement BuildPolicyGreenCardsEditor() => BuildPolicyTable("Πράσινες κάρτες",
        _policyData.GreenCards, ["Αριθμός", "Έναρξη", "Λήξη", "Χώρες"], item =>
        [item.Number, item.StartDate?.ToString("dd/MM/yyyy") ?? "—",
            item.EndDate?.ToString("dd/MM/yyyy") ?? "—", item.Countries ?? "—"], EditGreenCardAsync);

    private UIElement BuildPolicyInstallmentsEditor() => BuildPolicyTable("Πρόγραμμα δόσεων",
        _policyData.Installments, ["Δόση", "Προθεσμία", "Ποσό", "Σημειώσεις"], item =>
        [item.Number.ToString(), item.DueDate?.ToString("dd/MM/yyyy") ?? "—",
            item.Amount.ToString("N2"), item.Notes ?? "—"], EditInstallmentAsync);

    private async Task<PolicyInsuredObject?> EditObjectAsync(PolicyInsuredObject? existing)
    {
        var kind = new ComboBox { Header = "Είδος", ItemsSource = new[]
            { "Όχημα", "Κατοικία", "Σκάφος", "Επιχείρηση", "Πρόσωπο", "Άλλο" },
            SelectedItem = existing?.Kind ?? "Άλλο" };
        var description = new TextBox { Header = RequiredHeader.Create("Περιγραφή"),
            Text = existing?.Description ?? "" };
        var value = new TextBox { Header = "Ασφαλιστική αξία (€)",
            Text = existing?.InsuredValue?.ToString("N2") ?? "" };
        if (!await ShowPolicyEditorAsync(existing is null ? "Νέο αντικείμενο" : "Επεξεργασία αντικειμένου",
            kind, description, value)) return null;
        if (string.IsNullOrWhiteSpace(description.Text))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Η περιγραφή είναι υποχρεωτική."); return null; }
        try { return new PolicyInsuredObject { Id = existing?.Id ?? Guid.NewGuid(),
            Kind = kind.SelectedItem as string ?? "Άλλο", Description = description.Text.Trim(),
            InsuredValue = DecimalValue(value.Text, "Ασφαλιστική αξία") }; }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
    }

    private async Task<PolicyCoverage?> EditCoverageAsync(PolicyCoverage? existing)
    {
        var configured = (await _policyFilters.ScopedChoicesAsync("Καλύψεις",
                _insurerSuggest?.Text, _text.GetValueOrDefault("branch")?.Text))
            .Concat(existing is null ? [] : [existing.Name])
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        var name = new AutoSuggestBox { Header = RequiredHeader.Create("Κάλυψη"),
            Text = existing?.Name ?? "", PlaceholderText = "Αναζήτηση παραμετρικής κάλυψης" };
        SearchSuggestions.CloseOnLostFocus(name);
        name.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                name.ItemsSource = configured.Where(value => value.Contains(name.Text,
                    StringComparison.CurrentCultureIgnoreCase)).Take(30).ToArray();
        };
        name.SuggestionChosen += (_, args) => name.Text = args.SelectedItem as string ?? name.Text;
        var sum = new TextBox { Header = "Ασφαλισμένο ποσό (€)", Text = existing?.SumInsured?.ToString("N2") ?? "" };
        var deductible = new TextBox { Header = "Απαλλαγή (€)", Text = existing?.Deductible?.ToString("N2") ?? "" };
        var premium = new TextBox { Header = "Ασφάλιστρο κάλυψης (€)", Text = existing?.Premium?.ToString("N2") ?? "" };
        var notes = new TextBox { Header = "Σημειώσεις", Text = existing?.Notes ?? "" };
        if (!await ShowPolicyEditorAsync(existing is null ? "Νέα κάλυψη" : "Επεξεργασία κάλυψης",
            name, sum, deductible, premium, notes)) return null;
        if (!configured.Contains(name.Text.Trim(), StringComparer.CurrentCultureIgnoreCase))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot,
            "Επιλέξτε κάλυψη από τις παραμετρικές τιμές της εφαρμογής."); return null; }
        try { return new PolicyCoverage { Id = existing?.Id ?? Guid.NewGuid(), Name = name.Text.Trim(),
            SumInsured = DecimalValue(sum.Text, "Ασφαλισμένο ποσό"),
            Deductible = DecimalValue(deductible.Text, "Απαλλαγή"),
            Premium = DecimalValue(premium.Text, "Ασφάλιστρο κάλυψης"), Notes = notes.Text.Trim() }; }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
    }

    private async Task<PolicyEndorsement?> EditEndorsementAsync(PolicyEndorsement? existing)
    {
        var number = new TextBox { Header = RequiredHeader.Create("Αριθμός πρόσθετης πράξης"),
            Text = existing?.Number ?? "" };
        var type = new ComboBox { Header = "Είδος", ItemsSource = new[]
            { "Πρόσθετη πράξη", "Πρόσθετη πράξη ακύρωσης" },
            SelectedItem = existing?.Type ?? "Πρόσθετη πράξη" };
        var date = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.EffectiveDate is DateTime day ? new DateTimeOffset(day) : null };
        var difference = new TextBox { Header = "Διαφορά ασφαλίστρου (€)",
            Text = existing?.PremiumDifference?.ToString("N2") ?? "",
            PlaceholderText = "Αρνητικό ποσό για επιστροφή" };
        var notes = new TextBox { Header = "Σημειώσεις", Text = existing?.Notes ?? "" };
        if (!await ShowPolicyEditorAsync(existing is null ? "Νέα πρόσθετη πράξη" : "Επεξεργασία πράξης",
            number, type, LabeledDate("Ημερομηνία ισχύος", date), difference, notes)) return null;
        if (string.IsNullOrWhiteSpace(number.Text))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ο αριθμός πράξης είναι υποχρεωτικός."); return null; }
        try { return new PolicyEndorsement { Id = existing?.Id ?? Guid.NewGuid(),
            Number = number.Text.Trim(), Type = type.SelectedItem as string ?? "Πρόσθετη πράξη",
            EffectiveDate = date.GetValidatedDate("Ημερομηνία ισχύος"),
            PremiumDifference = SignedDecimal(difference.Text),
            Notes = notes.Text.Trim() }; }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
    }

    private async Task<PolicyGreenCard?> EditGreenCardAsync(PolicyGreenCard? existing)
    {
        var number = new TextBox { Header = RequiredHeader.Create("Αριθμός πράσινης κάρτας"),
            Text = existing?.Number ?? "" };
        var start = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.StartDate is DateTime a ? new DateTimeOffset(a) : null };
        var end = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.EndDate is DateTime b ? new DateTimeOffset(b) : null };
        var countries = new TextBox { Header = "Χώρες ισχύος", Text = existing?.Countries ?? "" };
        if (!await ShowPolicyEditorAsync(existing is null ? "Νέα πράσινη κάρτα" : "Επεξεργασία πράσινης κάρτας",
            number, LabeledDate("Έναρξη", start), LabeledDate("Λήξη", end), countries)) return null;
        if (string.IsNullOrWhiteSpace(number.Text) || start.Date?.Date > end.Date?.Date)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ελέγξτε τον αριθμό και τις ημερομηνίες της πράσινης κάρτας."); return null; }
        try
        {
            return new PolicyGreenCard { Id = existing?.Id ?? Guid.NewGuid(), Number = number.Text.Trim(),
                StartDate = start.GetValidatedDate("Έναρξη"),
                EndDate = end.GetValidatedDate("Λήξη"), Countries = countries.Text.Trim() };
        }
        catch (Exception exception)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
    }

    private async Task<PolicyInstallment?> EditInstallmentAsync(PolicyInstallment? existing)
    {
        var number = new TextBox { Header = RequiredHeader.Create("Αριθμός δόσης"),
            Text = existing?.Number.ToString() ?? (_policyData.Installments.Count + 1).ToString() };
        var due = new EditableDateInput { PlaceholderText = "ηη/μμ/εεεε",
            Date = existing?.DueDate is DateTime day ? new DateTimeOffset(day) : null };
        var amount = new TextBox { Header = RequiredHeader.Create("Ποσό (€)"),
            Text = existing?.Amount.ToString("N2") ?? "" };
        var notes = new TextBox { Header = "Σημειώσεις", Text = existing?.Notes ?? "" };
        if (!await ShowPolicyEditorAsync(existing is null ? "Νέα δόση" : "Επεξεργασία δόσης",
            number, LabeledDate("Ημερομηνία πληρωμής", due), amount, notes)) return null;
        if (!int.TryParse(number.Text, out var index) || index < 1)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ο αριθμός δόσης πρέπει να είναι θετικός."); return null; }
        try
        {
            var value = DecimalValue(amount.Text, "Ποσό δόσης");
            if (value is null or <= 0) throw new ArgumentException("Το ποσό δόσης πρέπει να είναι θετικό.");
            return new PolicyInstallment { Id = existing?.Id ?? Guid.NewGuid(), Number = index,
                DueDate = due.GetValidatedDate("Ημερομηνία πληρωμής"),
                Amount = value.Value, Notes = notes.Text.Trim() };
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
    }

    private static UIElement LabeledDate(string label, EditableDateInput input)
    {
        var host = new StackPanel { Spacing = 4 };
        host.Children.Add(new TextBlock { Text = label });
        host.Children.Add(input);
        return host;
    }

    private async Task<bool> ShowPolicyEditorAsync(string title, params UIElement[] fields)
    {
        var panel = new StackPanel { Spacing = 9, Width = 430 };
        foreach (var field in fields) panel.Children.Add(field);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = title,
            Content = new ScrollViewer { Content = panel, MaxHeight = 560 },
            PrimaryButtonText = "Εφαρμογή", CloseButtonText = "Ακύρωση" };
        return await DialogButtonStyles.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private static decimal? SignedDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var value))
            return value;
        throw new ArgumentException("Συμπληρώστε έγκυρη διαφορά ασφαλίστρου.");
    }
}

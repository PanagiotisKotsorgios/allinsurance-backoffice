using AllInsurance.Application.Business;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspaceRecordCardWindow
{
    private void AppendToLastTab(UIElement extra)
    {
        var scroller = (ScrollViewer)_tabs[^1].Content;
        var form = (UIElement)scroller.Content;
        scroller.Content = null;
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(form);
        stack.Children.Add(extra);
        scroller.Content = stack;
    }

    private UIElement OrganizationContactsPanel()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(22, 4, 22, 20) };
        panel.Children.Add(new TextBlock { Text = "Πρόσωπα επικοινωνίας", FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη προσώπου" };
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete);
        panel.Children.Add(actions);
        var list = new ListView { Height = 156, SelectionMode = ListViewSelectionMode.Single };
        TableRowStriping.Attach(list);
        panel.Children.Add(list);
        void Render() => list.ItemsSource = _data.Contacts.Select(contact => new ContactRow(contact)).ToArray();
        add.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (await ContactDialogAsync(null) is { } contact) { _data.Contacts.Add(contact); Render(); }
        };
        edit.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (list.SelectedItem is not ContactRow row) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρόσωπο."); return; }
            if (await ContactDialogAsync(row.Contact) is { } updated)
            {
                var index = _data.Contacts.FindIndex(item => item.Id == row.Contact.Id);
                if (index >= 0) _data.Contacts[index] = updated;
                Render();
            }
        };
        delete.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (list.SelectedItem is not ContactRow row) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρόσωπο."); return; }
            var confirm = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή προσώπου επικοινωνίας",
                Content = $"Να αφαιρεθεί ο/η {row.Contact.Name};", PrimaryButtonText = "Διαγραφή",
                CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(confirm, destructive: true) != ContentDialogResult.Primary) return;
            _data.Contacts.RemoveAll(item => item.Id == row.Contact.Id);
            Render();
        };
        Render();
        return panel;
    }

    private async Task<OrganizationContact?> ContactDialogAsync(OrganizationContact? existing)
    {
        var name = new TextBox { Header = "Ονοματεπώνυμο *", Text = existing?.Name ?? "" };
        var position = new TextBox { Header = "Ιδιότητα / τμήμα", Text = existing?.Position ?? "" };
        var email = new TextBox { Header = "Email", Text = existing?.Email ?? "" };
        var mobile = new TextBox { Header = "Κινητό τηλέφωνο", Text = existing?.Mobile ?? "" };
        var phone = new TextBox { Header = "Σταθερό τηλέφωνο", Text = existing?.Phone ?? "" };
        var form = new StackPanel { Spacing = 8, Width = 420,
            Children = { name, position, email, mobile, phone } };
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Προσθήκη προσώπου επικοινωνίας" : "Επεξεργασία προσώπου",
            Content = new ScrollViewer { Content = form, MaxHeight = 500 },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(name.Text))
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Το ονοματεπώνυμο είναι υποχρεωτικό.");
            return null;
        }
        return new OrganizationContact { Id = existing?.Id ?? Guid.NewGuid(),
            Name = name.Text.Trim(), Position = position.Text.Trim(), Email = email.Text.Trim(),
            Mobile = mobile.Text.Trim(), Phone = phone.Text.Trim() };
    }

    private UIElement OrganizationSettlementsPanel()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(22, 4, 22, 20) };
        panel.Children.Add(new TextBlock { Text = "Οικονομική εικόνα", FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var balance = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var badge = new Border { Child = balance, Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(badge);
        panel.Children.Add(new TextBlock { Text = "Υπολογισμός: μικτά ασφάλιστρα μείον προμήθεια γραφείου επί των καθαρών και καταχωρημένες αποδόσεις.",
            TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new ActionButton { Kind = "Add", Label = "Καταχώριση απόδοσης" };
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete);
        panel.Children.Add(actions);
        var list = new ListView { Height = 140, SelectionMode = ListViewSelectionMode.Single };
        TableRowStriping.Attach(list);
        panel.Children.Add(list);
        void Render()
        {
            list.ItemsSource = _data.Settlements.OrderByDescending(item => item.Date)
                .Select(item => new SettlementRow(item)).ToArray();
            var amount = OrganizationBalance.Calculate(_record, _data, _all.GetValueOrDefault("policies") ?? []);
            balance.Text = amount > 0 ? $"Οφειλή προς {Singular()}: {amount:N2} €" :
                amount < 0 ? $"Πιστωτικό υπόλοιπο από {Singular()}: {-amount:N2} €" : "Μηδενικό υπόλοιπο: 0,00 €";
            badge.Background = new SolidColorBrush(amount == 0
                ? Color.FromArgb(255, 96, 105, 115) : Color.FromArgb(255, 198, 40, 40));
        }
        add.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (await SettlementDialogAsync(null) is { } item) { _data.Settlements.Add(item); Render(); }
        };
        edit.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (list.SelectedItem is not SettlementRow row) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε απόδοση."); return; }
            if (await SettlementDialogAsync(row.Item) is { } item)
            {
                var index = _data.Settlements.FindIndex(value => value.Id == row.Item.Id);
                if (index >= 0) _data.Settlements[index] = item;
                Render();
            }
        };
        delete.Click += async (_, _) =>
        {
            if (!await RequireEditAsync()) return;
            if (list.SelectedItem is not SettlementRow row) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε απόδοση."); return; }
            var confirm = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή απόδοσης",
                Content = "Να αφαιρεθεί η επιλεγμένη οικονομική κίνηση;",
                PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
            if (await DialogButtonStyles.ShowAsync(confirm, destructive: true) != ContentDialogResult.Primary) return;
            _data.Settlements.RemoveAll(item => item.Id == row.Item.Id);
            Render();
        };
        Render();
        return panel;
    }

    private async Task<OrganizationSettlement?> SettlementDialogAsync(OrganizationSettlement? existing)
    {
        var date = new EditableDateInput { Date = new DateTimeOffset(existing?.Date ?? DateTime.Today),
            Height = 42, PlaceholderText = "Ημερομηνία" };
        var amount = new TextBox { Header = "Ποσό (€) *", Text = existing?.Amount.ToString("N2") ?? "" };
        var note = new TextBox { Header = "Αιτιολογία / ασφαλιστήρια", Text = existing?.Note ?? "" };
        var form = new StackPanel { Spacing = 8, Width = 420,
            Children = { new TextBlock { Text = "Ημερομηνία" }, date, amount, note,
                new TextBlock { Text = "Θετικό ποσό: απόδοση προς φορέα. Αρνητικό: επιστροφή από φορέα.", TextWrapping = TextWrapping.Wrap } } };
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Απόδοση / επιστροφή",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return null;
        if (!decimal.TryParse(amount.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var value) || value == 0)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Συμπληρώστε έγκυρο μη μηδενικό ποσό.");
            return null;
        }
        DateTime? selected;
        try { selected = date.GetValidatedDate("Ημερομηνία"); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); return null; }
        return new OrganizationSettlement { Id = existing?.Id ?? Guid.NewGuid(),
            Date = selected ?? DateTime.Today, Amount = value, Note = note.Text.Trim() };
    }

    private async Task<bool> RequireEditAsync()
    {
        if (_editing) return true;
        await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Πατήστε πρώτα «Επεξεργασία» στην καρτέλα.");
        return false;
    }

    private sealed record ContactRow(OrganizationContact Contact)
    {
        public override string ToString() => $"{Contact.Name} · {Contact.Position} · {Contact.Email} · {Contact.Mobile} · {Contact.Phone}";
    }

    private sealed record SettlementRow(OrganizationSettlement Item)
    {
        public override string ToString() => $"{Item.Date:dd/MM/yyyy}  ·  {Item.Amount:N2} €  ·  {Item.Note}";
    }
}

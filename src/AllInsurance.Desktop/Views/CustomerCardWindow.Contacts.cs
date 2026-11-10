using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomerCardWindow
{
    private sealed record ContactEntry(string Title, string Value, CustomerDetail? Detail,
        Action<string?>? SetLegacyValue);

    private void AddCommunicationSection(Grid main)
    {
        while (main.RowDefinitions.Count < 3)
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var list = new ListView { MaxHeight = 235, SelectionMode = ListViewSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        TableRowStriping.Attach(list);
        void Add(string title, string? value, Action<string?> setter)
        {
            if (!string.IsNullOrWhiteSpace(value))
                AddContactRow(list, new ContactEntry(title, value, null, setter));
        }
        Add("Κύριο κινητό", _customer.PrimaryMobile, value => _customer.PrimaryMobile = value);
        Add("Κύριο email", _customer.PrimaryEmail, value => _customer.PrimaryEmail = value);
        Add("Σταθερό τηλέφωνο", _customer.HomePhone, value => _customer.HomePhone = value);
        Add("2ο σταθερό τηλέφωνο", _customer.SecondPhone, value => _customer.SecondPhone = value);
        Add("3ο σταθερό τηλέφωνο", _customer.ThirdPhone, value => _customer.ThirdPhone = value);
        Add("2ο κινητό τηλέφωνο", _customer.SecondMobile, value => _customer.SecondMobile = value);
        Add("3ο κινητό τηλέφωνο", _customer.ThirdMobile, value => _customer.ThirdMobile = value);
        Add("2ο email", _customer.SecondEmail, value => _customer.SecondEmail = value);
        Add("3ο email", _customer.ThirdEmail, value => _customer.ThirdEmail = value);
        Add("Τηλέφωνο επιχείρησης", _customer.BusinessPhone, value => _customer.BusinessPhone = value);
        Add("Email επιχείρησης", _customer.BusinessEmail, value => _customer.BusinessEmail = value);
        foreach (var detail in _customer.Details.Where(item => item.Category == "contacts")
                     .OrderBy(item => item.Title))
            AddContactRow(list, new ContactEntry(detail.Title, detail.Value ?? "", detail, null));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη στοιχείου" };
        add.Click += async (_, _) => await EditContactAsync(null);
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        edit.Click += async (_, _) =>
        {
            if (list.SelectedItem is ListViewItem { Tag: ContactEntry entry })
                await EditContactAsync(entry);
            else await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε στοιχείο επικοινωνίας.");
        };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        delete.Click += async (_, _) =>
        {
            if (list.SelectedItem is not ListViewItem { Tag: ContactEntry entry })
            {
                await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε στοιχείο επικοινωνίας.");
                return;
            }
            await DeleteContactAsync(entry);
        };
        actions.Children.Add(add);
        actions.Children.Add(edit);
        actions.Children.Add(delete);
        var content = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
        content.Children.Add(actions);
        var heading = new Grid { ColumnSpacing = 12, MinHeight = 34,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(45, 83, 124, 157)) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.Children.Add(new TextBlock { Text = "Τίτλος", Margin = new Thickness(8, 0, 8, 0),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center });
        var valueHeading = new TextBlock { Text = "Περιεχόμενο", Margin = new Thickness(8, 0, 8, 0),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(valueHeading, 1);
        heading.Children.Add(valueHeading);
        content.Children.Add(heading);
        content.Children.Add(list);
        var exportHost = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        TableExportActions.Attach(exportHost, _root, () => new TableExportData("Επικοινωνία πελάτη",
            ["Τίτλος", "Περιεχόμενο"], list.Items.OfType<ListViewItem>()
                .Where(item => item.Tag is ContactEntry)
                .Select(item => (ContactEntry)item.Tag)
                .Select(entry => new[] { entry.Title, entry.Value }).ToArray()));
        content.Children.Add(exportHost);
        content.Visibility = Visibility.Collapsed;
        var toggle = new Button { Content = "▸  Επικοινωνία", HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left, MinHeight = 36 };
        var section = new StackPanel { Spacing = 3, Margin = new Thickness(0, 8, 0, 0) };
        toggle.Click += (_, _) =>
        {
            content.Visibility = content.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            toggle.Content = content.Visibility == Visibility.Visible ? "▾  Επικοινωνία" : "▸  Επικοινωνία";
            if (content.Visibility == Visibility.Visible)
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_selectedTabIndex != 0 || _tabContents.FirstOrDefault() is not ScrollViewer scroller)
                        return;
                    scroller.UpdateLayout();
                    var top = section.TransformToVisual(main).TransformPoint(new Point(0, 0)).Y;
                    scroller.ChangeView(null, Math.Min(scroller.ScrollableHeight, Math.Max(0, top - 12)),
                        null, disableAnimation: false);
                });
        };
        section.Children.Add(toggle);
        section.Children.Add(content);
        Grid.SetRow(section, 1);
        Grid.SetColumnSpan(section, 2);
        main.Children.Add(section);
    }

    private static void AddContactRow(ListView list, ContactEntry entry)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 36 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = entry.Title, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis });
        var value = new TextBlock { Text = entry.Value, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        list.Items.Add(new ListViewItem { Content = row, Tag = entry,
            HorizontalContentAlignment = HorizontalAlignment.Stretch });
    }

    private async Task EditContactAsync(ContactEntry? existing)
    {
        if (!await EnsureSavedAsync()) return;
        var title = new TextBox { Header = "Τίτλος *", Text = existing?.Title ?? "",
            IsReadOnly = existing?.SetLegacyValue is not null, MinHeight = 36 };
        var value = new TextBox { Header = "Περιεχόμενο *", Text = existing?.Value ?? "",
            MinHeight = 36 };
        var form = new StackPanel { Spacing = 10, Width = 440 };
        form.Children.Add(title);
        form.Children.Add(value);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Νέο στοιχείο επικοινωνίας" : "Στοιχείο επικοινωνίας",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(value.Text))
                throw new ArgumentException("Συμπληρώστε τίτλο και περιεχόμενο.");
            if (existing?.SetLegacyValue is not null)
            {
                existing.SetLegacyValue(value.Text.Trim());
                await _service.SaveAsync(_customer);
            }
            else if (existing?.Detail is CustomerDetail detail)
                await _service.UpdateDetailAsync(_customer.Id, detail.Id, title.Text, value.Text,
                    detail.Notes, detail.EffectiveFrom, detail.EffectiveUntil, detail.IsHistorical);
            else
                await _service.AddDetailAsync(_customer.Id, "contacts", title.Text, value.Text,
                    null, null, null, false);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs(preserveSelectedTab: true);
            CustomerChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task DeleteContactAsync(ContactEntry entry)
    {
        if (!await EnsureSavedAsync()) return;
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή στοιχείου επικοινωνίας",
            Content = $"Να διαγραφεί το «{entry.Title}»;", PrimaryButtonText = "Διαγραφή",
            CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog, destructive: true) != ContentDialogResult.Primary) return;
        try
        {
            if (entry.SetLegacyValue is not null)
            {
                entry.SetLegacyValue(null);
                await _service.SaveAsync(_customer);
            }
            else if (entry.Detail is CustomerDetail detail)
                await _service.DeleteDetailAsync(_customer.Id, detail.Id);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs(preserveSelectedTab: true);
            CustomerChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }
}

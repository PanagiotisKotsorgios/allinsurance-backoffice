using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomerCardWindow
{
    private void BuildActivityHistoryTab()
    {
        var root = new StackPanel { Margin = new Thickness(20, 12, 20, 16), Spacing = 8 };
        var search = new TextBox { Height = 40, MinHeight = 40,
            PlaceholderText = "Αναζήτηση ενέργειας, περιγραφής ή χρήστη...",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        root.Children.Add(SearchFieldVisual.Wrap(search));
        var filters = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 205, ItemHeight = 46 };
        var action = new ComboBox { Width = 195, Height = 40, SelectedIndex = 0 };
        action.Items.Add("Όλες οι ενέργειες");
        var from = new EditableDateInput { Width = 195, Height = 40,
            PlaceholderText = "Από ημερομηνία" };
        var until = new EditableDateInput { Width = 195, Height = 40,
            PlaceholderText = "Έως ημερομηνία" };
        filters.Children.Add(action);
        filters.Children.Add(from);
        filters.Children.Add(until);
        root.Children.Add(filters);
        var count = new TextBlock { Text = "Φόρτωση ενεργειών...", FontSize = 13 };
        root.Children.Add(count);
        var header = new Grid { Width = 1120, Background =
            new SolidColorBrush(IsDarkTheme() ? Color.FromArgb(255, 55, 65, 77) :
                Color.FromArgb(255, 224, 233, 241)) };
        foreach (var width in new[] { 170, 190, 590, 170 })
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        var captions = new[] { "Ημερομηνία", "Ενέργεια", "Περιγραφή", "Χρήστης" };
        for (var index = 0; index < captions.Length; index++)
        {
            var cell = new Border { Child = new TextBlock { Text = captions[index],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == captions.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10, 8, 10, 8) };
            Grid.SetColumn(cell, index);
            header.Children.Add(cell);
        }
        root.Children.Add(new Border { Child = header, Width = 1122,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Height = 490, Width = 1122,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1), Padding = new Thickness(0) };
        root.Children.Add(list);
        list.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(async (_, args) =>
        {
            var row = FindCardRow(args.OriginalSource) ?? list.SelectedItem as ListViewItem;
            if (row?.Tag is CustomerActivity activity)
            {
                await ShowActivityAsync(activity);
                args.Handled = true;
            }
        }), true);
        var pager = new TablePagination(list);
        root.Children.Add(pager.Bar);
        var applyColumns = TableInteractions.Attach(header, list, _tabContent);
        IReadOnlyList<CustomerActivity> activities = [];
        void Refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            var selectedAction = action.SelectedItem as string;
            var entries = activities.Where(item => selectedAction is null or "Όλες οι ενέργειες" ||
                    item.Action == selectedAction)
                .Where(item => term.Length == 0 || item.Action.Contains(term,
                    StringComparison.CurrentCultureIgnoreCase) ||
                    item.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    item.ActorName.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .Where(item => from.Date is null || item.OccurredAtUtc.ToLocalTime().Date >= from.Date.Value.Date)
                .Where(item => until.Date is null || item.OccurredAtUtc.ToLocalTime().Date <= until.Date.Value.Date)
                .ToArray();
            foreach (var item in entries)
            {
                var row = new Grid { Width = 1120, Height = 39 };
                foreach (var width in new[] { 170, 190, 590, 170 })
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                var values = new[] { item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    item.Action, item.Description, item.ActorName };
                for (var index = 0; index < values.Length; index++)
                {
                    var cell = new Border { Child = new TextBlock { Text = values[index],
                        TextTrimming = TextTrimming.CharacterEllipsis },
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = index == values.Length - 1 ? new Thickness(0, 0, 0, 1) :
                            new Thickness(0, 0, 1, 1), Padding = new Thickness(10, 5, 10, 5) };
                    Grid.SetColumn(cell, index);
                    row.Children.Add(cell);
                }
                var container = new ListViewItem { Content = row, Tag = item, Height = 39, Width = 1120,
                    Padding = new Thickness(0), Margin = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch };
                TableRowResizer.Attach(row, container, "customer-activity", item.Id);
                list.Items.Add(container);
            }
            count.Text = $"{entries.Length} από {activities.Count} ενέργειες";
            pager.Capture();
            applyColumns();
        }
        search.TextChanged += (_, _) => Refresh();
        action.SelectionChanged += (_, _) => Refresh();
        from.DateChanged += (_, _) => Refresh();
        until.DateChanged += (_, _) => Refresh();
        _ = LoadAsync();
        async Task LoadAsync()
        {
            if (_customer.Id == Guid.Empty) { count.Text = "Αποθηκεύστε τον πελάτη για να αρχίσει το ιστορικό."; return; }
            try
            {
                activities = await _service.GetActivitiesAsync(_customer.Id);
                action.Items.Clear();
                action.Items.Add("Όλες οι ενέργειες");
                foreach (var name in activities.Select(item => item.Action).Distinct().OrderBy(value => value))
                    action.Items.Add(name);
                action.SelectedIndex = 0;
                Refresh();
            }
            catch (Exception exception) { count.Text = exception.Message; }
        }
        AddTab("Ιστορικό ενεργειών", root);
    }

    private async Task ShowActivityAsync(CustomerActivity activity)
    {
        var details = new StackPanel { Width = 440, Spacing = 10 };
        void Add(string label, string value)
        {
            details.Children.Add(new TextBlock { Text = label, FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        }
        Add("Ημερομηνία και ώρα", activity.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        Add("Ενέργεια", activity.Action);
        Add("Περιγραφή", activity.Description);
        Add("Χρήστης", activity.ActorName);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Καρτέλα ενέργειας",
            Content = details, CloseButtonText = "Κλείσιμο" };
        await DialogButtonStyles.ShowAsync(dialog);
    }
}

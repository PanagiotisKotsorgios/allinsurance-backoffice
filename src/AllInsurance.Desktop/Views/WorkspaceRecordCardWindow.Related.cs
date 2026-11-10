using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspaceRecordCardWindow
{
    private void AddTab(string title, UIElement content)
    {
        var index = _tabs.Count;
        var icon = title switch
        {
            "Γενικά" or "Καρτέλα εταιρείας" or "Καρτέλα πρακτορείου" or "Καρτέλα ζημιάς" or
                "Καρτέλα ενημέρωσης" or "Καρτέλα αναφοράς" or "Καρτέλα γέφυρας" => "file-text",
            "Επικοινωνία" => "phone", "Παραγωγή" or "Οικονομικά" or
                "Οικονομικά εταιρείας" or "Οικονομικά πρακτορείου" or "Δεδομένα" => "chart-no-axes-combined",
            "Όχημα & συμβόλαιο" => "car-front", "Εμπλεκόμενοι" => "user-round",
            "Ιστορικό ενεργειών" => "clock-3", _ => "settings-2"
        };
        var (button, label) = CardTabVisual.Create(title, icon, IsDarkTheme());
        button.Click += (_, _) => SelectTab(index);
        _tabStrip.Children.Add(button);
        _tabs.Add((button, label, new ScrollViewer { Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }));
        CardTabVisual.Layout(_tabStrip, _contentWidth);
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _selectedTab = index;
        _tabContent.Content = _tabs[index].Content;
        for (var i = 0; i < _tabs.Count; i++)
            CardTabVisual.SetSelected(_tabs[i].Button, _tabs[i].Label, i == index, IsDarkTheme());
    }

    private static StackPanel RelatedPanel() => new() { Spacing = 14,
        Margin = new Thickness(22, 14, 22, 20) };

    private UIElement LinkedSummary(string title, string key)
    {
        var id = LinkId(key);
        var name = key == "customer" ? _customers.FirstOrDefault(item => item.Id == id)?.FullName :
            _all.GetValueOrDefault(LinkSection(key))?.FirstOrDefault(item => item.Id == id)?.Title;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = $"{title}: {name ?? "Χωρίς σύνδεση"}",
            FontSize = 15, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 720 });
        if (id is Guid linkedId)
        {
            var open = new ActionButton { Kind = "Open", Label = "Άνοιγμα καρτέλας" };
            open.Click += async (_, _) => await OpenLinkAsync(key, linkedId);
            panel.Children.Add(open);
        }
        return panel;
    }

    private StackPanel RelatedTable(string title, string section,
        Func<BusinessRecord, bool> predicate)
    {
        var panel = new StackPanel { Spacing = 7 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (section == "policies" && (_section.Key is "companies" or "agencies") &&
            _record.Id != Guid.Empty)
        {
            var add = new ActionButton { Kind = "Add", Label = "Νέα αίτηση ασφάλισης",
                HorizontalAlignment = HorizontalAlignment.Left };
            add.Click += async (_, _) => await OpenNewPolicyAsync();
            panel.Children.Add(add);
        }
        if (_record.Id == Guid.Empty)
        {
            panel.Children.Add(new TextBlock { Text = "Αποθηκεύστε πρώτα την καρτέλα για να εμφανιστούν οι συνδέσεις." });
            return panel;
        }
        var source = _all.GetValueOrDefault(section) ?? [];
        var rows = source.Where(predicate).ToArray();
        var search = new TextBox { PlaceholderText = "Αναζήτηση αριθμού, τίτλου ή κατάστασης...",
            Height = 36, MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(SearchFieldVisual.Wrap(search));
        var widths = section == "policies"
            ? new[] { 190, 170, 205, 185, 120, 120, 150 }
            : new[] { 350, 210, 180, 170 };
        var captions = section == "policies"
            ? new[] { "Αριθμός συμβολαίου", "Κλάδος", "Πελάτης", "Ασφαλιστική",
                "Έναρξη", "Λήξη", "Κατάσταση" }
            : new[] { "Εγγραφή", "Κωδικός", "Κατάσταση", "Ημερομηνία" };
        var total = widths.Sum();
        var header = new Grid { Width = total,
            Background = new SolidColorBrush(IsDarkTheme() ? Color.FromArgb(255, 55, 65, 77) :
                Color.FromArgb(255, 224, 233, 241)) };
        foreach (var width in widths) header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var column = 0; column < captions.Length; column++)
        {
            var cell = new Border { Child = new TextBlock { Text = captions[column],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = new Thickness(0, 0, column == captions.Length - 1 ? 0 : 1, 0),
                Padding = new Thickness(10, 8, 10, 8) };
            Grid.SetColumn(cell, column); header.Children.Add(cell);
        }
        panel.Children.Add(new Border { Child = header, Width = total + 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Width = total + 2, Height = section == "policies" ? 330 : 250,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1) };
        TableRowStriping.Attach(list);
        panel.Children.Add(list);
        var pager = new TablePagination(list);
        panel.Children.Add(pager.Bar);
        void Refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            foreach (var record in rows.Where(item => term.Length == 0 ||
                new[] { item.Title, item.Reference, item.Status }.Any(value =>
                    value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true)))
            {
                var policy = section == "policies"
                    ? BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson) : null;
                var values = policy is null
                    ? new[] { record.Title, record.Reference ?? "—", record.Status ?? "—",
                        record.EventDate?.ToString("dd/MM/yyyy") ?? "—" }
                    : new[] { policy.PolicyNumber ?? record.Reference ?? record.Title,
                        policy.Branch ?? "—",
                        _customers.FirstOrDefault(item => item.Id == record.CustomerId)?.FullName ?? "—",
                        policy.Insurer ?? "—", policy.StartDate?.ToString("dd/MM/yyyy") ?? "—",
                        policy.EndDate?.ToString("dd/MM/yyyy") ?? "—", record.Status ?? "—" };
                var grid = new Grid { Width = total, Height = 36 };
                foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                for (var column = 0; column < values.Length; column++)
                {
                    UIElement content;
                    if (column == 0)
                    {
                        var link = new HyperlinkButton { Content = values[column],
                            Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
                        var linkedId = record.Id;
                        link.Click += async (_, _) => await OpenRecordAsync(section, linkedId);
                        content = link;
                    }
                    else content = new TextBlock { Text = values[column],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center };
                    var cell = new Border { Child = content,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = new Thickness(0, 0, column == values.Length - 1 ? 0 : 1, 1),
                        Padding = new Thickness(10, 4, 10, 4) };
                    Grid.SetColumn(cell, column); grid.Children.Add(cell);
                }
                var row = new ListViewItem { Content = grid, Tag = record.Id,
                    Width = total, Height = 36, Padding = new Thickness(0), Margin = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var menu = new MenuFlyout();
                var openItem = new MenuFlyoutItem { Text = "Άνοιγμα καρτέλας" };
                openItem.Click += async (_, _) => await OpenRecordAsync(section, record.Id);
                menu.Items.Add(openItem);
                row.ContextFlyout = menu;
                list.Items.Add(row);
            }
            pager.Capture();
        }
        search.TextChanged += (_, _) => Refresh();
        list.DoubleTapped += async (_, _) =>
        {
            if ((list.SelectedItem as ListViewItem)?.Tag is Guid id) await OpenRecordAsync(section, id);
        };
        Refresh();
        return panel;
    }

    private UIElement ReportDataPanel()
    {
        var type = _data.Fields.GetValueOrDefault("reportType") ?? "Ασφαλιστήρια";
        var section = type switch
        {
            "Ζημιές" => "claims", "Οχήματα" => "vehicles", "Συνεργάτες" => "partners",
            _ => "policies"
        };
        if (type == "Πελάτες")
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = $"Σύνολο πελατών: {_customers.Length}", FontSize = 18 });
            panel.Children.Add(new TextBlock { Text = $"Ενεργοί: {_customers.Count(item => item.IsActive)} · " +
                $"Ανενεργοί: {_customers.Count(item => !item.IsActive)}" });
            var search = new TextBox { PlaceholderText = "Αναζήτηση ονόματος ή ΑΦΜ...",
                Height = 36, MinHeight = 36 };
            panel.Children.Add(SearchFieldVisual.Wrap(search));
            var list = new ListView { Height = 320 };
            TableRowStriping.Attach(list);
            panel.Children.Add(list);
            var pager = new TablePagination(list);
            panel.Children.Add(pager.Bar);
            void RefreshCustomers()
            {
                list.Items.Clear();
                var term = search.Text.Trim();
                foreach (var customer in _customers.Where(item => term.Length == 0 ||
                    item.FullName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    item.TaxNumber?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true))
                {
                    var link = new HyperlinkButton { Content = $"{customer.FullName}  ·  {customer.TaxNumber ?? "—"}" };
                    link.Click += async (_, _) => await OpenLinkAsync("customer", customer.Id);
                    list.Items.Add(link);
                }
                pager.Capture();
            }
            search.TextChanged += (_, _) => RefreshCustomers();
            RefreshCustomers();
            return panel;
        }
        DateTime? from = DateTime.TryParse(_data.Fields.GetValueOrDefault("fromDate"), out var a) ? a : null;
        DateTime? until = DateTime.TryParse(_data.Fields.GetValueOrDefault("toDate"), out var b) ? b : null;
        var panel2 = new StackPanel { Spacing = 10 };
        var total = (_all.GetValueOrDefault(section) ?? []).Count(item =>
            (!from.HasValue || item.EventDate >= from) && (!until.HasValue || item.EventDate <= until));
        panel2.Children.Add(new TextBlock { Text = $"{type}: {total} εγγραφές στην επιλεγμένη περίοδο",
            FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel2.Children.Add(RelatedTable(type, section, item =>
            (!from.HasValue || item.EventDate >= from) && (!until.HasValue || item.EventDate <= until)));
        return panel2;
    }

    private void BuildHistoryTab()
    {
        var panel = RelatedPanel();
        var search = new TextBox { PlaceholderText = "Αναζήτηση στο ιστορικό", MinHeight = 36 };
        var action = new ComboBox { Header = "Ενέργεια", MinWidth = 180 };
        panel.Children.Add(search);
        panel.Children.Add(action);
        var list = new ListView { Height = 410, HorizontalAlignment = HorizontalAlignment.Stretch };
        list.Header = RecordHistoryTable.Header();
        TableRowStriping.Attach(list);
        list.DoubleTapped += async (_, _) =>
        {
            if (list.SelectedItem is not ListViewItem { Tag: BusinessRecordActivity activity }) return;
            var detail = new StackPanel { Spacing = 8, Width = 440 };
            foreach (var (label, value) in new[]
            {
                ("Ημερομηνία", activity.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
                ("Ενέργεια", activity.Action), ("Περιγραφή", activity.Description),
                ("Χρήστης", activity.ActorName)
            })
                detail.Children.Add(new TextBox { Header = label, Text = value ?? "",
                    IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                    MinHeight = label == "Περιγραφή" ? 80 : 40 });
            await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = _root.XamlRoot,
                Title = "Καρτέλα ενέργειας", Content = detail, CloseButtonText = "Κλείσιμο" });
        };
        panel.Children.Add(list);
        var pager = new TablePagination(list);
        panel.Children.Add(pager.Bar);
        AddTab("Ιστορικό ενεργειών", panel);
        BusinessRecordActivity[] activities = [];
        void Render()
        {
            var term = search.Text.Trim();
            var selected = action.SelectedIndex > 0 ? action.SelectedItem?.ToString() : null;
            list.Items.Clear();
            foreach (var activity in activities.Where(item =>
                (selected is null || item.Action == selected) &&
                (term.Length == 0 || new[] { item.Action, item.Description, item.ActorName }
                    .Any(value => value?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true))))
                list.Items.Add(RecordHistoryTable.Row(activity));
            pager.Capture();
        }
        search.TextChanged += (_, _) => Render();
        action.SelectionChanged += (_, _) => Render();
        if (_record.Id == Guid.Empty)
        {
            Render();
            return;
        }
        _ = LoadHistoryAsync();
        async Task LoadHistoryAsync()
        {
            try
            {
                activities = [.. await _records.GetActivitiesAsync(_record.Id)];
                action.Items.Clear();
                action.Items.Add("Όλες οι ενέργειες");
                foreach (var value in activities.Select(item => item.Action).Distinct().OrderBy(value => value))
                    action.Items.Add(value);
                action.SelectedIndex = 0;
                Render();
            }
            catch (Exception exception) { _status.Text = exception.Message; }
        }
    }

    private async Task OpenLinkAsync(string key, Guid? id)
    {
        if (id is not Guid linkedId)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρώτα συνδεδεμένη εγγραφή.");
            return;
        }
        if (key == "customer")
        {
            if (_child is CustomerCardWindow existing) { existing.BringToFront(); return; }
            var card = new CustomerCardWindow(linkedId);
            _child = card;
            card.Closed += (_, _) => { _child = null; BringToFront(); };
            card.BringToFront();
        }
        else await OpenRecordAsync(LinkSection(key), linkedId);
    }

    private async Task OpenNewPolicyAsync()
    {
        if (_child is not null) { BringChildToFront(); return; }
        var owner = WindowNative.GetWindowHandle(this);
        var card = new BusinessRecordCardWindow("policies", ownerHandle: owner,
            insurerName: _section.Key == "companies" ? _record.Title : null,
            agencyName: _section.Key == "agencies" ? _record.Title : null);
        _child = card;
        card.RecordSaved += async (_, _) =>
        {
            _all["policies"] = (await _records.SearchAsync("policies")).ToArray();
            BuildTabs(preserve: true);
            RecordSaved?.Invoke(this, EventArgs.Empty);
        };
        card.Closed += (_, _) => { _child = null; BringToFront(); };
        card.BringToFront();
        await Task.CompletedTask;
    }

    private async Task OpenRecordAsync(string section, Guid id)
    {
        if (_child is not null)
        {
            BringChildToFront();
            return;
        }
        var owner = WindowNative.GetWindowHandle(this);
        _child = section switch
        {
            "partners" => new PartnerCardWindow(id),
            "policies" or "vehicles" => new BusinessRecordCardWindow(section, recordId: id,
                ownerHandle: owner),
            "companies" or "agencies" or "claims" or "updates" or "reports" or "bridges" =>
                new WorkspaceRecordCardWindow(section, id, ownerHandle: owner),
            _ => null
        };
        if (_child is null)
        {
            await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Η καρτέλα δεν μπορεί να ανοίξει.");
            return;
        }
        _child.Closed += (_, _) => { _child = null; BringToFront(); };
        BringChildToFront();
    }

    private void BringChildToFront()
    {
        switch (_child)
        {
            case CustomerCardWindow card: card.BringToFront(); break;
            case PartnerCardWindow card: card.BringToFront(); break;
            case BusinessRecordCardWindow card: card.BringToFront(); break;
            case WorkspaceRecordCardWindow card: card.BringToFront(); break;
        }
    }
}

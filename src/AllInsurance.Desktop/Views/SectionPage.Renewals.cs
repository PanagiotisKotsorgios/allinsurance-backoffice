using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class SectionPage
{
    private async void Renewals_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var all = await _records.SearchAsync("policies");
            var renewedIds = all.Select(record =>
                BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson).PreviousPolicyId)
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var candidates = all.Select(record => new
                {
                    Record = record,
                    Data = BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson)
                })
                .Where(item => item.Data.EndDate.HasValue && !renewedIds.Contains(item.Record.Id) &&
                    (item.Record.DocumentType is null or "Ασφαλιστήριο") &&
                    item.Record.Status is not ("Ακυρωμένο" or "Ανανεωμένο"))
                .Select(item => new RenewalRow(item.Record, item.Data)).ToArray();

            var from = new DatePicker { Header = "Λήξη από", Date = DateTimeOffset.Now.Date };
            var until = new DatePicker { Header = "Λήξη έως", Date = DateTimeOffset.Now.Date.AddDays(30) };
            var search = new TextBox { Header = "Αναζήτηση", PlaceholderText = "Αριθμός, πελάτης ή ασφαλιστική" };
            var insurer = new ComboBox { Header = "Ασφαλιστική", MinWidth = 180 };
            insurer.Items.Add("Όλες οι ασφαλιστικές");
            foreach (var name in candidates.Select(row => row.Insurer).Where(name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(name => name))
                insurer.Items.Add(name);
            insurer.SelectedIndex = 0;
            var count = new TextBlock { Margin = new Thickness(0, 4, 0, 4) };
            var list = new ListView { Height = 360, SelectionMode = ListViewSelectionMode.Single };
            list.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <Grid ColumnDefinitions="155,190,170,110,95" Height="38">
                        <TextBlock Text="{Binding Number}" VerticalAlignment="Center" Margin="8,0" TextTrimming="CharacterEllipsis" />
                        <TextBlock Grid.Column="1" Text="{Binding Customer}" VerticalAlignment="Center" Margin="8,0" TextTrimming="CharacterEllipsis" />
                        <TextBlock Grid.Column="2" Text="{Binding Insurer}" VerticalAlignment="Center" Margin="8,0" TextTrimming="CharacterEllipsis" />
                        <TextBlock Grid.Column="3" Text="{Binding Expiry}" VerticalAlignment="Center" Margin="8,0" />
                        <TextBlock Grid.Column="4" Text="{Binding DaysLeft}" VerticalAlignment="Center" Margin="8,0" />
                    </Grid>
                </DataTemplate>
                """);
            void Refresh()
            {
                var first = from.Date.Date;
                var last = until.Date.Date;
                var query = search.Text.Trim();
                var selectedInsurer = insurer.SelectedIndex > 0 ? insurer.SelectedItem?.ToString() : null;
                var result = candidates.Where(row => row.EndDate.Date >= first && row.EndDate.Date <= last &&
                    (selectedInsurer is null || string.Equals(row.Insurer, selectedInsurer, StringComparison.CurrentCultureIgnoreCase)) &&
                    (query.Length == 0 || row.Number.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     row.Customer.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     row.Insurer.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
                    .OrderBy(row => row.EndDate).ToArray();
                list.ItemsSource = result;
                count.Text = first > last ? "Η αρχική ημερομηνία είναι μετά την τελική." : $"{result.Length} ασφαλιστήρια προς ανανέωση";
            }
            from.DateChanged += (_, _) => Refresh();
            until.DateChanged += (_, _) => Refresh();
            search.TextChanged += (_, _) => Refresh();
            insurer.SelectionChanged += (_, _) => Refresh();

            var filters = new Grid { ColumnSpacing = 10, ColumnDefinitions =
                { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                  new ColumnDefinition { Width = GridLength.Auto },
                  new ColumnDefinition { Width = GridLength.Auto },
                  new ColumnDefinition { Width = GridLength.Auto } } };
            filters.Children.Add(search);
            Grid.SetColumn(insurer, 1); filters.Children.Add(insurer);
            Grid.SetColumn(from, 2); filters.Children.Add(from);
            Grid.SetColumn(until, 3); filters.Children.Add(until);
            var header = new Grid { ColumnDefinitions =
                { new ColumnDefinition { Width = new GridLength(155) }, new ColumnDefinition { Width = new GridLength(190) },
                  new ColumnDefinition { Width = new GridLength(170) }, new ColumnDefinition { Width = new GridLength(110) },
                  new ColumnDefinition { Width = new GridLength(95) } } };
            var labels = new[] { "Αριθμός", "Πελάτης", "Ασφαλιστική", "Λήξη", "Ημέρες" };
            for (var index = 0; index < labels.Length; index++)
            {
                var label = new TextBlock { Text = labels[index], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(8, 6, 8, 6) };
                Grid.SetColumn(label, index);
                header.Children.Add(label);
            }
            var panel = new StackPanel { Width = 760, Spacing = 6 };
            panel.Children.Add(filters);
            panel.Children.Add(count);
            panel.Children.Add(header);
            panel.Children.Add(list);
            Refresh();
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Ανανεώσεις ασφαλιστηρίων",
                Content = panel, PrimaryButtonText = "Άνοιγμα καρτέλας", CloseButtonText = "Κλείσιμο" };
            if (await DialogButtonStyles.ShowAsync(dialog) == ContentDialogResult.Primary &&
                list.SelectedItem is RenewalRow selected)
                OpenBusinessRecordCard(selected.Record.Id);
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν ήταν δυνατή η εμφάνιση των ανανεώσεων: " + exception.Message);
        }
    }

    private sealed class RenewalRow(BusinessRecord record, PolicyCardData data)
    {
        public BusinessRecord Record { get; } = record;
        public DateTime EndDate { get; } = data.EndDate!.Value;
        public string Number { get; } = data.PolicyNumber ?? record.Reference ?? record.Title;
        public string Customer { get; } = record.Customer is { } customer
            ? $"{customer.LastName} {customer.FirstName}" : record.RelatedParty ?? "—";
        public string Insurer { get; } = data.Insurer ?? "—";
        public string Expiry => EndDate.ToString("dd/MM/yyyy");
        public string DaysLeft => (EndDate.Date - DateTime.Today).Days.ToString();
    }
}

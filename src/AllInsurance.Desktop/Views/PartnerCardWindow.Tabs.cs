using AllInsurance.Application.Partners;
using AllInsurance.Desktop.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace AllInsurance.Desktop.Views;

public sealed partial class PartnerCardWindow
{
    private BusinessRecordCardWindow? _policyCard;

    private void BuildTabs(bool preserve = false)
    {
        var index = preserve ? _selectedTab : 0;
        _tabContent.Content = null;
        _tabStrip.Children.Clear(); _tabs.Clear();
        _formGrids.Clear();
        _text.Clear(); _choice.Clear(); _date.Clear(); _check.Clear(); _suggest.Clear();
        BuildMainTab();
        BuildExtraTab();
        BuildCooperationTab();
        BuildProductionTab();
        BuildPoliciesTab();
        BuildFinanceTab();
        BuildCommissionsTab();
        BuildHistoryTab();
        SelectTab(Math.Clamp(index, 0, _tabs.Count - 1));
    }

    private void BuildMainTab()
    {
        var form = TwoColumns(out var left, out var right);
        Text(left, "last", "Επώνυμο *", _profile.LastName);
        Text(left, "first", "Όνομα *", _profile.FirstName);
        Text(left, "company", "Επωνυμία επιχείρησης", _profile.CompanyName);
        Text(left, "tax", "ΑΦΜ *", _profile.TaxNumber);
        Choice(left, "taxoffice", "ΔΟΥ", _taxOffices, _profile.TaxOffice);
        Choice(left, "person", "Τύπος προσώπου *", ["Φυσικό πρόσωπο", "Νομικό πρόσωπο"],
            _profile.PersonType ?? "Φυσικό πρόσωπο");
        void UpdateRequiredIdentity()
        {
            var legal = _choice["person"].SelectedItem as string == "Νομικό πρόσωπο";
            CardFieldLayout.SetRequired(left, _text["company"], "Επωνυμία επιχείρησης", legal);
            CardFieldLayout.SetRequired(left, _text["last"], "Επώνυμο", !legal);
            CardFieldLayout.SetRequired(left, _text["first"], "Όνομα", !legal);
        }
        _choice["person"].SelectionChanged += (_, _) => UpdateRequiredIdentity();
        UpdateRequiredIdentity();
        Choice(left, "business", "Τύπος επιχείρησης", _businessTypes, _profile.BusinessType);
        Choice(left, "capacity", "Επαγγελματική ιδιότητα", _capacities, _profile.ProfessionalCapacity);
        Text(left, "identity", "Αριθμός ταυτότητας", _profile.IdentityNumber);
        Text(left, "manager", "Υπεύθυνος συνεργασίας", _profile.CooperationManager);
        Choice(right, "status", "Κατάσταση *",
            ["Ενεργός", "Πιθανός", "Αναστολή", "Λήξη συνεργασίας"], _record.Status ?? "Ενεργός");
        Choice(right, "category", "Κατηγορία συνεργάτη", _categories, _profile.Category);
        Check(right, "contractactive", "Ενεργή σύμβαση", _profile.HasActiveContract);
        Text(right, "email", "Κύριο email", _profile.Email);
        Text(right, "phone", "Κύριο τηλέφωνο", _profile.Phone);
        Text(right, "address", "Κύρια διεύθυνση", _profile.Address);
        Suggest(right, "city", "Πόλη", _profile.City, _cities);
        Text(right, "postal", "Ταχυδρομικός κώδικας", _profile.PostalCode);
        AddTab("Καρτέλα συνεργάτη", form);
    }

    private void BuildExtraTab()
    {
        var form = TwoColumns(out var left, out var right);
        Text(left, "father", "Πατρώνυμο", _profile.FatherName);
        Text(left, "mother", "Μητρώνυμο", _profile.MotherName);
        Choice(left, "gender", "Φύλο", _genders, _profile.Gender);
        var birth = Date(left, "birth", "Ημερομηνία γέννησης", _profile.BirthDate);
        var age = new TextBox { IsReadOnly = true, MinHeight = 28, Height = 28, FontSize = 13 };
        void UpdateAge()
        {
            var born = birth.Date?.Date;
            if (born is null || born > DateTime.Today) { age.Text = "—"; return; }
            var years = DateTime.Today.Year - born.Value.Year;
            if (born.Value.Date > DateTime.Today.AddYears(-years)) years--;
            age.Text = $"{years} έτη";
        }
        birth.DateChanged += (_, _) => UpdateAge(); UpdateAge();
        left.Children.Add(CardFieldLayout.CreateRow("Ηλικία", age));
        Text(left, "passport", "Αριθμός διαβατηρίου", _profile.PassportNumber);
        Text(left, "education", "Εκπαίδευση / πιστοποιήσεις", _profile.Education);
        Text(left, "registry", "Αριθμός μητρώου διαμεσολαβητή", _profile.RegistryNumber);
        Date(left, "registryexpiry", "Λήξη εγγραφής μητρώου", _profile.RegistryExpiry);
        Text(left, "chamber", "Επιμελητήριο", _profile.Chamber);
        Text(right, "email2", "Δεύτερο email", _profile.SecondaryEmail);
        Text(right, "phone2", "Δεύτερο τηλέφωνο", _profile.SecondaryPhone);
        Text(right, "mobile", "Κινητό", _profile.Mobile);
        Text(right, "website", "Ιστότοπος", _profile.Website);
        Text(right, "workaddress", "Διεύθυνση εργασίας", _profile.WorkAddress);
        Suggest(right, "workcity", "Πόλη εργασίας", _profile.WorkCity, _cities);
        Text(right, "workpostal", "ΤΚ εργασίας", _profile.WorkPostalCode);
        Text(right, "workphone", "Τηλέφωνο εργασίας", _profile.WorkPhone);
        Text(right, "workemail", "Email εργασίας", _profile.WorkEmail);
        Text(right, "notes", "Σημειώσεις", _profile.Notes, multiline: true);
        AddTab("Πρόσθετα στοιχεία", form);
    }

    private void BuildCooperationTab()
    {
        var form = TwoColumns(out var left, out var right);
        Text(left, "contract", "Αριθμός σύμβασης", _profile.ContractNumber);
        Date(left, "start", "Έναρξη συνεργασίας", _profile.StartDate);
        Date(left, "end", "Λήξη συνεργασίας", _profile.EndDate);
        Text(left, "credit", "Πιστωτικό όριο (€)", _profile.CreditLimit?.ToString("N2"));
        Text(left, "rate", "Βασικό ποσοστό προμήθειας (%)",
            _profile.DefaultCommissionPercent?.ToString("N2"));
        Text(right, "bank", "Τράπεζα", _profile.BankName);
        Text(right, "iban", "IBAN", _profile.Iban);
        Choice(right, "paymentmethod", "Προτιμώμενος τρόπος πληρωμής",
            _paymentMethods, _profile.PaymentMethod);
        AddTab("Συνεργασία", form);
    }

    private void BuildProductionTab()
    {
        var panel = Panel();
        panel.Children.Add(Info($"Ασφαλιστήρια: {_snapshot.Production.Count}   ·   " +
            $"Μικτά ασφάλιστρα: {_snapshot.Production.Sum(item => item.GrossPremium):N2} €   ·   " +
            $"Προμήθειες: {_snapshot.Earned:N2} €"));
        var monthly = _snapshot.Production.GroupBy(item => item.StartDate?.ToString("yyyy-MM") ?? "Χωρίς ημερομηνία")
            .OrderByDescending(group => group.Key)
            .Select(group => new PartnerTableRow([
                group.Key, group.Count().ToString(), group.Sum(item => item.GrossPremium).ToString("N2") + " €",
                group.Sum(item => item.Commission).ToString("N2") + " €"])).ToArray();
        panel.Children.Add(Table(["Μήνας", "Ασφαλιστήρια", "Μικτά ασφάλιστρα", "Προμήθειες"],
            [180, 200, 280, 260], monthly, "Αναζήτηση μήνα παραγωγής..."));
        AddTab("Παραγωγή", panel);
    }

    private void BuildPoliciesTab()
    {
        var panel = Panel();
        panel.Children.Add(Info("Τα ασφαλιστήρια ανοίγουν με διπλό κλικ."));
        var rows = _snapshot.Production.Select(item => new PartnerTableRow([
            item.Number, item.CustomerName, item.StartDate?.ToString("dd/MM/yyyy") ?? "—",
            item.GrossPremium.ToString("N2") + " €", item.CommissionPercent.ToString("N2") + " %",
            item.Commission.ToString("N2") + " €"], item.PolicyId)).ToArray();
        panel.Children.Add(Table(["Ασφαλιστήριο", "Πελάτης", "Έναρξη", "Ασφάλιστρα", "Ποσοστό", "Προμήθεια"],
            [190, 240, 150, 170, 150, 170], rows,
            "Αναζήτηση αριθμού ασφαλιστηρίου, πελάτη ή ποσού...", async id => await OpenPolicyAsync(id)));
        AddTab("Ασφαλιστήρια", panel);
    }

    private void BuildFinanceTab()
    {
        var panel = Panel();
        var due = _snapshot.Due;
        var color = due > 0 ? Color.FromArgb(255, 198, 40, 40) :
            due < 0 ? Color.FromArgb(255, 25, 135, 75) : Color.FromArgb(255, 101, 111, 123);
        panel.Children.Add(new Border { Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 9, 14, 9),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = due > 0 ? $"Οφειλή γραφείου στον συνεργάτη: {due:N2} €" :
                due < 0 ? $"Προκαταβολή συνεργάτη: {-due:N2} €" : "Μηδενικό υπόλοιπο: 0,00 €",
                Foreground = new SolidColorBrush(Colors.White), FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } });
        panel.Children.Add(Info($"Δεδουλευμένες προμήθειες: {_snapshot.Earned:N2} €   ·   " +
            $"Καταβληθέντα: {_snapshot.Paid:N2} €"));
        var add = new ActionButton { Kind = "Payment", Label = "Καταχώριση πληρωμής προμήθειας",
            HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 290,
            IsEnabled = _record.Id != Guid.Empty };
        add.Click += async (_, _) => await AddPayoutAsync();
        panel.Children.Add(add);
        var rows = _snapshot.Payouts.Select(item => new PartnerTableRow([
            item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), item.Amount.ToString("N2") + " €",
            item.PaymentMethod, item.Reference ?? "—", item.Description ?? "—"])).ToArray();
        panel.Children.Add(Table(["Ημερομηνία", "Ποσό", "Τρόπος", "Αναφορά", "Αιτιολογία"],
            [195, 150, 230, 170, 320], rows, "Αναζήτηση πληρωμής ή αναφοράς..."));
        AddTab("Οικονομικά", panel);
    }

    private void BuildCommissionsTab()
    {
        var panel = Panel();
        panel.Children.Add(Info($"Βασικό ποσοστό: {_profile.DefaultCommissionPercent?.ToString("N2") ?? "0,00"} %   ·   " +
            $"Δεδουλευμένες προμήθειες: {_snapshot.Earned:N2} €"));
        var rows = _snapshot.Production.Select(item => new PartnerTableRow([
            item.Number, item.CustomerName, item.GrossPremium.ToString("N2") + " €",
            item.CommissionPercent.ToString("N2") + " %", item.Commission.ToString("N2") + " €"],
            item.PolicyId)).ToArray();
        panel.Children.Add(Table(["Ασφαλιστήριο", "Πελάτης", "Μικτά ασφάλιστρα", "Ποσοστό", "Προμήθεια"],
            [195, 265, 200, 170, 200], rows,
            "Αναζήτηση προμήθειας ή ασφαλιστηρίου...", async id => await OpenPolicyAsync(id)));
        AddTab("Προμήθειες", panel);
    }

    private void BuildHistoryTab()
    {
        var panel = Panel();
        var rows = _snapshot.Activities.Select(item => new PartnerTableRow([
            item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), item.Action,
            item.Description, item.ActorName])).ToArray();
        panel.Children.Add(Table(["Ημερομηνία", "Ενέργεια", "Περιγραφή", "Χρήστης"],
            [190, 200, 540, 150], rows, "Αναζήτηση ενέργειας, περιγραφής ή χρήστη..."));
        AddTab("Ιστορικό ενεργειών", panel);
    }

    private static StackPanel Panel() => new() { Margin = new Thickness(22, 14, 22, 20), Spacing = 12 };
    private static TextBlock Info(string value) => new() { Text = value, FontSize = 15,
        TextWrapping = TextWrapping.Wrap };
    private sealed record PartnerTableRow(string[] Cells, Guid? RecordId = null);

    private StackPanel Table(string[] captions, int[] widths, PartnerTableRow[] data,
        string hint, Func<Guid, Task>? open = null)
    {
        var panel = new StackPanel { Spacing = 0 };
        var search = new TextBox { PlaceholderText = hint, Height = 40, MinHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(search);
        var total = widths.Sum();
        var header = new Grid { Width = total,
            Background = new SolidColorBrush(IsDarkTheme() ? Color.FromArgb(255, 55, 65, 77)
                : Color.FromArgb(255, 224, 233, 241)) };
        foreach (var width in widths) header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var i = 0; i < captions.Length; i++)
        {
            var border = new Border { Child = new TextBlock { Text = captions[i],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = i == captions.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10, 9, 10, 9) };
            Grid.SetColumn(border, i); header.Children.Add(border);
        }
        panel.Children.Add(new Border { Child = header, Width = total + 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Width = total + 2, Height = 405,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1) };
        panel.Children.Add(list);
        var count = new TextBlock { FontSize = 13 };
        panel.Children.Add(count);
        var applyColumns = TableInteractions.Attach(header, list);
        void Refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            var matching = data.Where(row => term.Length == 0 || row.Cells.Any(cell =>
                cell.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
            foreach (var entry in matching)
            {
                var grid = new Grid { Width = total, Height = 40 };
                foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                for (var i = 0; i < entry.Cells.Length; i++)
                {
                    var border = new Border { Child = new TextBlock { Text = entry.Cells[i],
                        TextTrimming = TextTrimming.CharacterEllipsis },
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = i == entry.Cells.Length - 1 ? new Thickness(0, 0, 0, 1) :
                            new Thickness(0, 0, 1, 1), Padding = new Thickness(10, 6, 10, 6) };
                    Grid.SetColumn(border, i); grid.Children.Add(border);
                }
                list.Items.Add(new ListViewItem { Content = grid, Tag = entry.RecordId,
                    Width = total, Height = 40, Padding = new Thickness(0), Margin = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
            count.Text = $"{matching.Length} εγγραφές";
            applyColumns();
        }
        search.TextChanged += (_, _) => Refresh();
        if (open is not null)
            list.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(async (_, args) =>
            {
                var source = args.OriginalSource as DependencyObject;
                while (source is not null && source is not ListViewItem) source = VisualTreeHelper.GetParent(source);
                if (source is ListViewItem { Tag: Guid id }) await open(id);
            }), true);
        Refresh();
        return panel;
    }

    private async Task OpenPolicyAsync(Guid id)
    {
        if (_policyCard is not null) { _policyCard.BringToFront(); return; }
        _policyCard = new BusinessRecordCardWindow("policies", recordId: id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _policyCard.RecordSaved += async (_, _) =>
        {
            _snapshot = await _partners.GetSnapshotAsync(_record.Id);
            BuildTabs(preserve: true);
        };
        _policyCard.Closed += (_, _) => { _policyCard = null; BringToFront(); };
        _policyCard.BringToFront();
    }

    private async Task AddPayoutAsync()
    {
        if (_record.Id == Guid.Empty) return;
        var amount = new TextBox { Header = "Ποσό (€) *", PlaceholderText = "π.χ. 150,00" };
        var method = new ComboBox { Header = "Τρόπος πληρωμής *",
            ItemsSource = _paymentMethods, HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = _profile.PaymentMethod ?? _paymentMethods.FirstOrDefault() };
        var reference = new TextBox { Header = "Αριθμός αναφοράς" };
        var description = new TextBox { Header = "Αιτιολογία" };
        var form = new StackPanel { Spacing = 9, Width = 400 };
        form.Children.Add(amount); form.Children.Add(method); form.Children.Add(reference);
        form.Children.Add(description);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = "Πληρωμή προμήθειας συνεργάτη", Content = form,
            PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            var value = Money(amount.Text, "Ποσό", 999_999_999m);
            if (value is null or <= 0) throw new ArgumentException("Συμπληρώστε θετικό ποσό.");
            if (method.SelectedItem is not string selectedMethod)
                throw new ArgumentException("Επιλέξτε τρόπο πληρωμής.");
            await _partners.AddPayoutAsync(_record.Id, value.Value, selectedMethod,
                reference.Text, description.Text);
            _profile = ReadProfile();
            _record.Status = Selected("status") ?? _record.Status;
            _snapshot = await _partners.GetSnapshotAsync(_record.Id);
            BuildTabs(preserve: true);
            RecordSaved?.Invoke(this, EventArgs.Empty);
            _status.Text = "Η πληρωμή προμήθειας καταχωρίστηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }
}

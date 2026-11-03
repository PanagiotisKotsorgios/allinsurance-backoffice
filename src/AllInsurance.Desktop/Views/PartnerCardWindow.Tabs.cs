using AllInsurance.Application.Partners;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
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
        BuildPoliciesTab();
        BuildCustomersTab();
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
        Text(left, "gemi", "Αριθμός ΓΕΜΗ", _profile.GemiNumber);
        Text(left, "manager", "Υπεύθυνος συνεργασίας", _profile.CooperationManager);
        Choice(left, "status", "Κατάσταση *",
            ["Ενεργός", "Πιθανός", "Αναστολή", "Λήξη συνεργασίας"], _record.Status ?? "Ενεργός");
        Choice(left, "category", "Κατηγορία συνεργάτη", _categories, _profile.Category);
        Check(left, "contractactive", "Ενεργή σύμβαση", _profile.HasActiveContract);
        Text(right, "email", "Κύριο email", _profile.Email);
        Text(right, "phone", "Κύριο τηλέφωνο", _profile.Phone);
        Text(right, "address", "Κύρια διεύθυνση", _profile.Address);
        Suggest(right, "city", "Πόλη", _profile.City, _cities);
        Text(right, "postal", "Ταχυδρομικός κώδικας", _profile.PostalCode);
        var due = _snapshot.Due;
        var dueColor = due > 0 ? Color.FromArgb(255, 198, 40, 40) :
            due < 0 ? Color.FromArgb(255, 25, 135, 75) : Color.FromArgb(255, 101, 111, 123);
        right.Children.Add(CardFieldLayout.CreateRow("Υπόλοιπο συνεργάτη",
            new Border { Background = new SolidColorBrush(dueColor),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5),
                Child = new TextBlock { Text = due > 0 ? $"Οφειλή γραφείου: {due:N2} €" :
                    due < 0 ? $"Προκαταβολή: {-due:N2} €" : "0,00 €",
                    Foreground = new SolidColorBrush(Colors.White), FontSize = 14 } }));
        Text(right, "contract", "Αριθμός σύμβασης", _profile.ContractNumber);
        Date(right, "start", "Έναρξη συνεργασίας", _profile.StartDate);
        Date(right, "end", "Λήξη συνεργασίας", _profile.EndDate);
        Text(right, "credit", "Πιστωτικό όριο (€)", _profile.CreditLimit?.ToString("N2"));
        Choice(right, "paymentmethod", "Προτιμώμενος τρόπος πληρωμής",
            _paymentMethods, _profile.PaymentMethod);
        BuildBankAccounts(right);
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(form);
        body.Children.Add(new Expander { Header = "Πρόσθετα στοιχεία συνεργάτη",
            Content = BuildExtraFields(), Margin = new Thickness(20, 0, 20, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch });
        AddTab("Καρτέλα συνεργάτη", body);
    }

    private Grid BuildExtraFields()
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
        Date(left, "nameday", "Ονομαστική εορτή", _profile.NameDay);
        Text(left, "birthplace", "Τόπος γέννησης", _profile.BirthPlace);
        Text(left, "nationality", "Εθνικότητα", _profile.Nationality);
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
        return form;
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

    private CustomerCardWindow? _customerCard;

    private void BuildCustomersTab()
    {
        var panel = Panel();
        var rows = _linkedCustomers.Select(item => new PartnerTableRow([
            item.FullName, item.TaxNumber ?? "—", item.City ?? "—",
            item.IsActive ? "Ενεργός" : "Ανενεργός", item.BalanceDisplay], item.Id)).ToArray();
        panel.Children.Add(Table(["Πελάτης", "ΑΦΜ", "Πόλη", "Κατάσταση", "Υπόλοιπο"],
            [300, 180, 220, 190, 170], rows, "Αναζήτηση πελάτη, ΑΦΜ ή πόλης...", async id =>
            {
                if (_customerCard is not null) { _customerCard.BringToFront(); return; }
                _customerCard = new CustomerCardWindow(id);
                _customerCard.Closed += (_, _) => { _customerCard = null; BringToFront(); };
                _customerCard.BringToFront();
                await Task.CompletedTask;
            }));
        AddTab("Πελάτες", panel);
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
        var paid = _snapshot.Payouts.Where(item => item.Kind != "receipt").Sum(item => item.Amount);
        var received = _snapshot.Payouts.Where(item => item.Kind == "receipt").Sum(item => item.Amount);
        panel.Children.Add(Info($"Δεδουλευμένες προμήθειες: {_snapshot.Earned:N2} €   ·   " +
            $"Αποδόθηκαν: {paid:N2} €   ·   Εισπράχθηκαν: {received:N2} €"));
        var entries = _snapshot.Production.Select(item => new PartnerLedgerEntry(
            item.RecordedAtUtc, "Προμήθεια", item.Number,
            $"{item.CustomerName} · Ασφαλιστήριο {item.Number}", item.Commission, 0, 0,
            item.PolicyId)).Concat(_snapshot.Payouts.Select(item => new PartnerLedgerEntry(
            item.OccurredAtUtc, item.Kind == "receipt" ? "Είσπραξη" : "Πληρωμή",
            item.Reference ?? "—", $"{item.PaymentMethod} · {item.Description ?? "—"}", 0,
            item.Kind == "receipt" ? 0 : item.Amount,
            item.Kind == "receipt" ? item.Amount : 0, null)))
            .OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Type).ToArray();
        decimal balance = 0;
        var rows = entries.Select(item =>
        {
            balance += item.Earned - item.Payment + item.Receipt;
            return new PartnerTableRow([
                item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                item.Type, item.Reference, item.Description,
                item.Earned == 0 ? "—" : $"{item.Earned:N2} €",
                item.Payment == 0 ? "—" : $"{item.Payment:N2} €",
                item.Receipt == 0 ? "—" : $"{item.Receipt:N2} €",
                $"{balance:N2} €"], item.PolicyId);
        }).ToArray();
        panel.Children.Add(Table(["Ημερομηνία", "Κίνηση", "Αναφορά", "Περιγραφή",
                "Δεδουλευμένα", "Αποδόθηκαν", "Εισπράχθηκαν", "Υπόλοιπο"],
            [150, 125, 125, 235, 125, 115, 125, 120], rows,
            "Αναζήτηση ασφαλιστηρίου, αναφοράς ή οικονομικής κίνησης...",
            async id => await OpenPolicyAsync(id)));
        AddTab("Οικονομικά", panel);
    }

    private void BuildCommissionsTab()
    {
        var panel = Panel();
        panel.Children.Add(Info($"Δεδουλευμένες προμήθειες: {_snapshot.Earned:N2} €"));
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

    private void BuildBankAccounts(StackPanel panel)
    {
        var host = new Grid { ColumnSpacing = 4 };
        host.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 3; i++)
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _bankChoice = new ComboBox { PlaceholderText = "Επιλέξτε τράπεζα",
            Height = 32, MinHeight = 32, HorizontalAlignment = HorizontalAlignment.Stretch };
        host.Children.Add(_bankChoice);
        var add = BankIconButton("Προσθήκη τράπεζας", "\uE710", Color.FromArgb(255, 22, 128, 61));
        add.IsEnabled = _editing;
        add.Click += async (_, _) => await EditBankAsync(null);
        Grid.SetColumn(add, 1); host.Children.Add(add);
        var edit = BankIconButton("Επεξεργασία τράπεζας", "\uE70F", Color.FromArgb(255, 29, 100, 179));
        edit.IsEnabled = _editing;
        edit.Click += async (_, _) => await EditBankAsync(_bankChoice.SelectedIndex);
        Grid.SetColumn(edit, 2); host.Children.Add(edit);
        var delete = BankIconButton("Διαγραφή τράπεζας", "\uE74D", Color.FromArgb(255, 198, 40, 40));
        delete.IsEnabled = _editing;
        delete.Click += async (_, _) =>
        {
            if (_bankChoice.SelectedIndex < 0 || _bankChoice.SelectedIndex >= _bankAccounts.Count)
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε τράπεζα."); return; }
            _bankAccounts.RemoveAt(_bankChoice.SelectedIndex);
            RefreshBankList();
        };
        Grid.SetColumn(delete, 3); host.Children.Add(delete);
        panel.Children.Add(CardFieldLayout.CreateRow("Τράπεζες / IBAN", host));
        RefreshBankList();
    }

    private static Button BankIconButton(string name, string glyph, Color color)
    {
        var button = new Button { Width = 32, Height = 32, MinHeight = 32,
            Padding = new Thickness(0), Background = new SolidColorBrush(color),
            Foreground = new SolidColorBrush(Colors.White),
            Content = new FontIcon { Glyph = glyph, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 15 } };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        ButtonMotion.Attach(button);
        return button;
    }

    private void RefreshBankList()
    {
        if (_bankChoice is null) return;
        var previous = _bankChoice.SelectedIndex;
        _bankChoice.ItemsSource = _bankAccounts.Select(item =>
            $"{item.BankName}  ·  {item.Iban}" +
            (string.IsNullOrWhiteSpace(item.Bic) ? "" : $"  ·  {item.Bic}")).ToArray();
        _bankChoice.SelectedIndex = _bankAccounts.Count == 0 ? -1 :
            Math.Clamp(previous, 0, _bankAccounts.Count - 1);
    }

    private async Task EditBankAsync(int? index)
    {
        var existing = index is >= 0 && index < _bankAccounts.Count ? _bankAccounts[index.Value] : null;
        if (index is not null && existing is null)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε τράπεζα."); return; }
        var bank = new TextBox { Header = "Τράπεζα *", Text = existing?.BankName ?? "" };
        var iban = new TextBox { Header = "IBAN *", Text = existing?.Iban ?? "" };
        var bic = new TextBox { Header = "BIC / SWIFT", Text = existing?.Bic ?? "" };
        var form = new StackPanel { Spacing = 8, Width = 420 };
        form.Children.Add(bank); form.Children.Add(iban); form.Children.Add(bic);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Προσθήκη τραπεζικού λογαριασμού" : "Επεξεργασία τραπεζικού λογαριασμού",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        var normalizedIban = new string(iban.Text.Where(character => !char.IsWhiteSpace(character)).ToArray())
            .ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(bank.Text) || normalizedIban.Length is < 15 or > 34 ||
            !normalizedIban.All(char.IsLetterOrDigit))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Συμπληρώστε τράπεζα και έγκυρο IBAN."); return; }
        if (_bankAccounts.Where((_, accountIndex) => accountIndex != index).Any(item => item.Iban == normalizedIban))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Το IBAN υπάρχει ήδη στην καρτέλα."); return; }
        var account = new PartnerBankAccount { BankName = bank.Text.Trim(), Iban = normalizedIban,
            Bic = string.IsNullOrWhiteSpace(bic.Text) ? null : bic.Text.Trim() };
        if (existing is null) _bankAccounts.Add(account);
        else _bankAccounts[index!.Value] = account;
        RefreshBankList();
    }
    private sealed record PartnerTableRow(string[] Cells, Guid? RecordId = null);
    private sealed record PartnerLedgerEntry(DateTime OccurredAtUtc, string Type, string Reference,
        string Description, decimal Earned, decimal Payment, decimal Receipt, Guid? PolicyId);

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
        var count = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var pager = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var previous = new Button { Content = "‹ Προηγούμενη" };
        var next = new Button { Content = "Επόμενη ›" };
        pager.Children.Add(previous); pager.Children.Add(count); pager.Children.Add(next);
        panel.Children.Add(pager);
        var page = 1;
        var applyColumns = TableInteractions.Attach(header, list);
        void Refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            var matching = data.Where(row => term.Length == 0 || row.Cells.Any(cell =>
                cell.Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToArray();
            var pages = Math.Max(1, (int)Math.Ceiling(matching.Length / 25.0));
            page = Math.Clamp(page, 1, pages);
            foreach (var entry in matching.Skip((page - 1) * 25).Take(25))
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
            count.Text = $"{matching.Length} εγγραφές · Σελίδα {page}/{pages}";
            previous.IsEnabled = page > 1;
            next.IsEnabled = page < pages;
            applyColumns();
        }
        search.TextChanged += (_, _) => { page = 1; Refresh(); };
        previous.Click += (_, _) => { page--; Refresh(); };
        next.Click += (_, _) => { page++; Refresh(); };
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

    private async Task AddPayoutAsync(string kind)
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
            Title = kind == "receipt" ? "Γενική είσπραξη από συνεργάτη" : "Γενική πληρωμή σε συνεργάτη", Content = form,
            PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            var value = Money(amount.Text, "Ποσό", 999_999_999m);
            if (value is null or <= 0) throw new ArgumentException("Συμπληρώστε θετικό ποσό.");
            if (method.SelectedItem is not string selectedMethod)
                throw new ArgumentException("Επιλέξτε τρόπο πληρωμής.");
            await _partners.AddMovementAsync(_record.Id, kind, value.Value, selectedMethod,
                reference.Text, description.Text);
            _profile = ReadProfile();
            _record.Status = Selected("status") ?? _record.Status;
            _snapshot = await _partners.GetSnapshotAsync(_record.Id);
            BuildTabs(preserve: true);
            RecordSaved?.Invoke(this, EventArgs.Empty);
            _status.Text = kind == "receipt" ? "Η είσπραξη καταχωρίστηκε." : "Η πληρωμή καταχωρίστηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }
}

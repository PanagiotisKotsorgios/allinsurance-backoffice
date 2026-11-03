using AllInsurance.Application.Customers;
using AllInsurance.Application.Settings;
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
    private Grid CompactColumns(out StackPanel left, out StackPanel right)
    {
        var grid = new Grid { ColumnSpacing = 18, Margin = new Thickness(20, 10, 20, 12),
            MaxWidth = 1100, MinWidth = 600, Width = _contentWidth };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        left = new StackPanel { Spacing = 2 };
        right = new StackPanel { Spacing = 2 };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        _compactMain = grid;
        return grid;
    }

    private void BuildCompactMainTab()
    {
        var main = CompactColumns(out var left, out var right);
        AddHeading(left, "Ταυτότητα και επικοινωνία");
        AddText(left, "ΑΦΜ *", () => _customer.TaxNumber, value => _customer.TaxNumber = value);
        AddText(left, "Επώνυμο *", () => _customer.LastName, value => _customer.LastName = value ?? "");
        AddText(left, "Όνομα *", () => _customer.FirstName, value => _customer.FirstName = value ?? "");
        AddText(left, "Πατρώνυμο", () => _customer.FatherName, value => _customer.FatherName = value);
        AddCatalogChoice(left, "Φύλο", "Φύλα", () => _customer.Gender,
            value => _customer.Gender = value);
        var birthDate = AddDate(left, "Ημ. γέννησης", () => _customer.BirthDate,
            value => _customer.BirthDate = value);
        var ageField = AddReadOnly(left, "Ηλικία", "—");
        void UpdateAge()
        {
            if (birthDate.Date is not DateTimeOffset date) { ageField.Text = "—"; return; }
            var age = DateTime.Today.Year - date.Year;
            if (date.Date > DateTime.Today.AddYears(-age)) age--;
            ageField.Text = $"{age} έτη";
        }
        birthDate.DateChanged += (_, _) => UpdateAge();
        UpdateAge();
        AddText(left, "Κύριο κινητό", () => _customer.PrimaryMobile, value => _customer.PrimaryMobile = value);
        AddText(left, "Κύριο email", () => _customer.PrimaryEmail, value => _customer.PrimaryEmail = value);
        AddText(left, "Διεύθυνση", () => _customer.PrimaryAddress, value => _customer.PrimaryAddress = value);
        AddCitySuggest(left);
        _mainBalanceText = new TextBlock { FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        _mainBalanceBadge = new Border { Child = _mainBalanceText,
            CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left };
        left.Children.Add(CreateFieldRow("Υπόλοιπο πελάτη", _mainBalanceBadge));
        UpdateMainBalance();

        AddHeading(right, "Έγγραφα και οδήγηση");
        AddText(right, "Αρ. ταυτότητας", () => _customer.IdentityNumber, value => _customer.IdentityNumber = value);
        AddCatalogChoice(right, "Τύπος ταυτότητας", "Τύποι ταυτότητας",
            () => _customer.IdentityType, value => _customer.IdentityType = value);
        AddBool(right, "Είναι οδηγός", () => _customer.IsDriver, value => _customer.IsDriver = value);
        AddBool(right, "Έχει δίπλωμα", () => _customer.HasDrivingLicense ||
            !string.IsNullOrWhiteSpace(_customer.LicenseNumber),
            value => _customer.HasDrivingLicense = value);
        AddBool(right, "Νέος οδηγός", () => _customer.IsNewDriver ||
            (_customer.LicenseStartDate is DateTime start && start >= DateTime.Today.AddYears(-2)),
            value => _customer.IsNewDriver = value);
        AddText(right, "Αρ. διπλώματος", () => _customer.LicenseNumber, value => _customer.LicenseNumber = value);
        AddCatalogChoice(right, "Κατηγορία διπλώματος", "Κατηγορίες διπλώματος", () => _customer.LicenseCategory,
            value => _customer.LicenseCategory = value);
        AddDate(right, "Έναρξη διπλώματος", () => _customer.LicenseStartDate,
            value => _customer.LicenseStartDate = value);
        AddDate(right, "Λήξη διπλώματος", () => _customer.LicenseEndDate,
            value => _customer.LicenseEndDate = value);
        AddCatalogChoice(right, "Επάγγελμα", "Επαγγέλματα", () => _customer.Occupation,
            value => _customer.Occupation = value);
        AddCatalogChoice(right, "Ιθαγένεια", "Ιθαγένειες", () => _customer.Nationality,
            value => _customer.Nationality = value);
        AddText(right, "ΑΜΚΑ", () => _customer.SocialSecurityNumber,
            value => _customer.SocialSecurityNumber = value);
        AddChoice(right, "Τύπος πελάτη", () => _customer.CustomerKind,
            value => _customer.CustomerKind = value, ["Φυσικό πρόσωπο", "Επιχείρηση"]);
        AddBool(right, "Ενεργός πελάτης", () => _customer.IsActive, value => _customer.IsActive = value);
        AddTab("Καρτέλα πελάτη", main);
    }

    private static TextBox AddReadOnly(StackPanel panel, string label, string value)
    {
        var field = new TextBox { Text = value, IsReadOnly = true, MinHeight = 28, Height = 28, FontSize = 13 };
        panel.Children.Add(CreateFieldRow(label, field));
        return field;
    }

    private void AddCitySuggest(StackPanel panel)
    {
        if (!_editing)
        {
            AddText(panel, "Πόλη", () => _customer.City, value => _customer.City = value);
            return;
        }
        var input = new AutoSuggestBox { Text = _customer.City ?? "", MinHeight = 28, Height = 28,
            PlaceholderText = "Αναζήτηση πόλης από τον κατάλογο" };
        panel.Children.Add(CreateFieldRow("Πόλη", input));
        _readFields.Add(() =>
        {
            var city = input.Text.Trim();
            if (city.Length > 0 && input.Tag is string[] options &&
                !options.Contains(city, StringComparer.CurrentCultureIgnoreCase))
                throw new ArgumentException("Επιλέξτε πόλη από τον παραμετρικό κατάλογο.");
            _customer.City = city;
        });
        _ = LoadCitySuggestionsAsync(input);
    }

    private async Task LoadCitySuggestionsAsync(AutoSuggestBox input)
    {
        try
        {
            var options = await _filterService.ChoicesAsync("Πόλεις");
            input.Tag = options.ToArray();
            input.GotFocus += (_, _) =>
            {
                input.ItemsSource = options.Where(value => value.Contains(input.Text.Trim(),
                    StringComparison.CurrentCultureIgnoreCase)).Take(20).ToArray();
                input.IsSuggestionListOpen = true;
            };
            input.TextChanged += (_, args) =>
            {
                if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
                var term = input.Text.Trim();
                input.ItemsSource = options.Where(value =>
                    value.Contains(term, StringComparison.CurrentCultureIgnoreCase)).Take(20).ToArray();
            };
            input.SuggestionChosen += (_, args) => input.Text = args.SelectedItem as string ?? input.Text;
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task LoadPartnerChoicesAsync()
    {
        try
        {
            var options = await _filterService.ChoicesAsync("Συνεργάτες");
            if (_partnerChoice is null) return;
            var current = _customer.PartnerName;
            _partnerChoice.ItemsSource = string.IsNullOrWhiteSpace(current) || options.Contains(current)
                ? options : options.Concat([current]).OrderBy(value => value).ToArray();
            _partnerChoice.SelectedItem = current;
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private void BuildSupplementaryTab()
    {
        var root = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 350, ItemHeight = 510, Margin = new Thickness(18, 10, 18, 16) };
        var family = new StackPanel { Spacing = 3, Width = 330 };
        AddHeading(family, "Προσωπικά και οικογενειακά");
        AddText(family, "Μητρώνυμο", () => _customer.MotherName, value => _customer.MotherName = value);
        AddText(family, "Διαβατήριο", () => _customer.PassportNumber,
            value => _customer.PassportNumber = value);
        AddCatalogChoice(family, "Οικ. κατάσταση", "Οικογενειακές καταστάσεις",
            () => _customer.MaritalStatus, value => _customer.MaritalStatus = value);
        AddText(family, "Σύζυγος / σύντροφος", () => _customer.SpouseName, value => _customer.SpouseName = value);
        AddText(family, "Τόπος γέννησης", () => _customer.BirthPlace, value => _customer.BirthPlace = value);
        AddDate(family, "Ονομαστική εορτή", () => _customer.NameDay, value => _customer.NameDay = value);
        AddText(family, "Περιοχή", () => _customer.Region, value => _customer.Region = value);
        AddText(family, "Τηλέφωνο", () => _customer.HomePhone, value => _customer.HomePhone = value);
        AddText(family, "2ο τηλέφωνο", () => _customer.SecondPhone, value => _customer.SecondPhone = value);
        AddText(family, "3ο τηλέφωνο", () => _customer.ThirdPhone, value => _customer.ThirdPhone = value);
        AddText(family, "2ο κινητό", () => _customer.SecondMobile, value => _customer.SecondMobile = value);
        AddText(family, "2ο email", () => _customer.SecondEmail, value => _customer.SecondEmail = value);
        AddText(family, "3ο email", () => _customer.ThirdEmail, value => _customer.ThirdEmail = value);
        AddText(family, "Σημειώσεις", () => _customer.Notes, value => _customer.Notes = value, multiline: true);
        root.Children.Add(family);

        var business = new StackPanel { Spacing = 3, Width = 330 };
        AddHeading(business, "Επαγγελματικά και φορολογικά");
        AddCatalogChoice(business, "ΔΟΥ", "ΔΟΥ", () => _customer.TaxOffice,
            value => _customer.TaxOffice = value);
        AddText(business, "Ταχ. κώδικας", () => _customer.PostalCode,
            value => _customer.PostalCode = value);
        AddText(business, "ΓΕΜΗ / μητρώο", () => _customer.InsuranceRegistry,
            value => _customer.InsuranceRegistry = value);
        AddCatalogChoice(business, "Νομική μορφή", "Νομικές μορφές",
            () => _customer.LegalForm, value => _customer.LegalForm = value);
        AddText(business, "ΚΑΔ", () => _customer.BusinessActivityCode,
            value => _customer.BusinessActivityCode = value);
        AddText(business, "Εργοδότης", () => _customer.Employer, value => _customer.Employer = value);
        AddText(business, "Επωνυμία", () => _customer.CompanyName, value => _customer.CompanyName = value);
        AddText(business, "Τηλ. επιχείρησης", () => _customer.BusinessPhone,
            value => _customer.BusinessPhone = value);
        AddText(business, "Email επιχείρησης", () => _customer.BusinessEmail,
            value => _customer.BusinessEmail = value);
        AddCatalogChoice(business, "Πηγή", "Πηγές πελατών",
            () => _customer.Source, value => _customer.Source = value);
        AddText(business, "Ετικέτες", () => _customer.Tags, value => _customer.Tags = value);
        AddText(business, "URL φωτογραφίας", () => _customer.PhotoUrl,
            value => _customer.PhotoUrl = value);
        AddText(business, "Ζώνη", () => _customer.Zone, value => _customer.Zone = value);
        AddText(business, "Ομαδοποίηση", () => _customer.GroupName,
            value => _customer.GroupName = value);
        root.Children.Add(business);

        var status = new StackPanel { Spacing = 3, Width = 330 };
        AddHeading(status, "Σχέσεις, σύσταση και εκπτώσεις");
        AddBool(status, "Δικαιολογητικό έκπτωσης", () => _customer.HasDiscountDocument,
            value => _customer.HasDiscountDocument = value);
        AddText(status, "Περιγραφή έκπτωσης", () => _customer.DiscountDocumentDescription,
            value => _customer.DiscountDocumentDescription = value);
        var officeControl = AddBool(status, "Ανήκει στο γραφείο", () => _customer.BelongsToOffice,
            value => _customer.BelongsToOffice = value);
        if (_editing)
        {
            _partnerChoice = new ComboBox { MinHeight = 28, Height = 28,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = officeControl.IsChecked != true };
            officeControl.Checked += (_, _) => _partnerChoice.IsEnabled = false;
            officeControl.Unchecked += (_, _) => _partnerChoice.IsEnabled = true;
            status.Children.Add(CreateFieldRow("Συνεργάτης", _partnerChoice));
            _readFields.Add(() => _customer.PartnerName = _partnerChoice.SelectedItem as string);
            _ = LoadPartnerChoicesAsync();
        }
        else AddText(status, "Συνεργάτης", () => _customer.PartnerName,
            value => _customer.PartnerName = value);
        var referralCheck = AddBool(status, "Από σύσταση", () => _customer.IsReferral,
            value => _customer.IsReferral = value);
        _referralChoice = new ComboBox { PlaceholderText = "Επιλέξτε πελάτη που έκανε τη σύσταση",
            DisplayMemberPath = "FullName", SelectedValuePath = "Id", IsEnabled = _editing,
            MinHeight = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch };
        _referralChoice.IsEnabled = _editing && referralCheck.IsChecked == true;
        referralCheck.Checked += (_, _) => _referralChoice.IsEnabled = _editing;
        referralCheck.Unchecked += (_, _) => _referralChoice.IsEnabled = false;
        status.Children.Add(CreateFieldRow("Σύσταση από πελάτη", _referralChoice));
        _ = LoadReferrersAsync();
        status.Children.Add(new TextBlock { Text = _customer.Id == Guid.Empty ?
            "Η εγγραφή θα οριστεί στην αποθήκευση." :
            $"Εγγραφή: {_customer.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}", FontSize = 12 });
        status.Children.Add(new TextBlock { Text = _customer.Id == Guid.Empty ? "" :
            $"Ενημέρωση: {_customer.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}", FontSize = 12 });
        root.Children.Add(status);
        AddTab("Λοιπά", root);
    }

    private void BuildFinanceTab()
    {
        var root = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        var balance = CustomerService.Balance(_customer);
        var balanceTitle = balance > 0 ? "Οφειλή πελάτη προς το γραφείο" :
            balance < 0 ? "Πιστωτικό υπόλοιπο πελάτη" : "Μηδενικό υπόλοιπο";
        root.Children.Add(BalanceVisual.Create(balance, $"{balanceTitle}: {Math.Abs(balance):N2} {CurrencySymbol}", 20));
        var receipts = _customer.Transactions.Where(item => item.Type == "receipt").Sum(item => item.Amount);
        var payments = _customer.Transactions.Where(item => item.Type == "payment").Sum(item => item.Amount);
        root.Children.Add(new TextBlock { Text =
            $"Υπόλοιπο έναρξης: {_customer.OpeningBalance:N2} {CurrencySymbol}  ·  Εισπράξεις: {receipts:N2} {CurrencySymbol}  ·  Πληρωμές: {payments:N2} {CurrencySymbol}",
            TextWrapping = TextWrapping.Wrap });
        root.Children.Add(new TextBlock { Text =
            "Το υπόλοιπο έναρξης χρησιμοποιείται μόνο για μεταφορά παλαιού υπολοίπου. Οι νέες κινήσεις καταχωρίζονται από τα κουμπιά είσπραξης και πληρωμής.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var openingBalance = AddText(root, $"Υπόλοιπο έναρξης ({CurrencySymbol})", () => _customer.OpeningBalance.ToString("N2"),
            value => _customer.OpeningBalance = ParseAmount(value, allowNegative: true));
        var normalBackground = openingBalance.Background;
        var normalForeground = openingBalance.Foreground;
        void UpdateOpeningStyle()
        {
            var nonzero = decimal.TryParse(openingBalance.Text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.GetCultureInfo("el-GR"), out var amount) && amount != 0;
            openingBalance.Background = nonzero ? new SolidColorBrush(Color.FromArgb(255, 198, 40, 40)) : normalBackground;
            openingBalance.Foreground = nonzero ? new SolidColorBrush(Microsoft.UI.Colors.White) : normalForeground;
        }
        openingBalance.TextChanged += (_, _) => UpdateOpeningStyle();
        UpdateOpeningStyle();
        root.Children.Add(new TextBlock { Text = "Οικονομικές κινήσεις", FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var search = new TextBox { Width = 240, Height = 38, MinHeight = 38,
            PlaceholderText = "Αναζήτηση κίνησης..." };
        var from = new CalendarDatePicker { Width = 170, Height = 38,
            PlaceholderText = "Από ημερομηνία" };
        var until = new CalendarDatePicker { Width = 170, Height = 38,
            PlaceholderText = "Έως ημερομηνία" };
        var filters = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 210, ItemHeight = 42 };
        foreach (var field in new UIElement[] { search, from, until }) filters.Children.Add(field);
        root.Children.Add(filters);
        var header = new Grid { ColumnSpacing = 0, Width = 940,
            Background = new SolidColorBrush(Color.FromArgb(36, 77, 129, 174)) };
        foreach (var width in new[] { 180, 150, 130, 480 })
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        var captions = new[] { "Ημερομηνία", "Κίνηση", "Ποσό", "Αιτιολογία" };
        for (var index = 0; index < captions.Length; index++)
        {
            var caption = new TextBlock { Text = captions[index],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var cell = new Border { Child = caption,
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == captions.Length - 1 ? new Thickness(0) : new Thickness(0, 0, 1, 0),
                Padding = new Thickness(10, 8, 10, 8) };
            Grid.SetColumn(cell, index);
            header.Children.Add(cell);
        }
        root.Children.Add(new Border { Child = header, Width = 942, HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Height = 300, Width = 942, HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1), Padding = new Thickness(0) };
        root.Children.Add(list);
        list.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(async (_, args) =>
        {
            if (FindCardRow(args.OriginalSource)?.Tag is CustomerTransaction transaction)
                await ShowTransactionAsync(transaction);
        }), true);
        list.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, args) =>
        {
            if (FindCardRow(args.OriginalSource) is not ListViewItem { Tag: CustomerTransaction transaction } row)
                return;
            list.SelectedItem = row;
            var menu = new MenuFlyout();
            var open = new MenuFlyoutItem { Text = "Προβολή κίνησης" };
            open.Click += async (_, _) => await ShowTransactionAsync(transaction);
            menu.Items.Add(open);
            menu.ShowAt(row);
            args.Handled = true;
        }), true);
        var applyColumnLayout = TableInteractions.Attach(header, list);
        void refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            var start = from.Date?.Date;
            var end = until.Date?.Date;
            foreach (var item in _customer.Transactions.OrderByDescending(item => item.OccurredAtUtc)
                .Where(item => !start.HasValue || item.OccurredAtUtc.ToLocalTime().Date >= start)
                .Where(item => !end.HasValue || item.OccurredAtUtc.ToLocalTime().Date <= end)
                .Where(item => term.Length == 0 || item.Description?.Contains(term,
                    StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Amount.ToString("N2").Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            {
                var row = new Grid { ColumnSpacing = 0, Width = 940, Height = 39 };
                foreach (var width in new[] { 180, 150, 130, 480 })
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                var values = new[] { item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    item.Type == "receipt" ? "Είσπραξη" : "Πληρωμή",
                    $"{item.Amount:N2} {TransactionSettingsService.Symbol(item.Currency)}", item.Description ?? "—" };
                for (var index = 0; index < values.Length; index++)
                {
                    var text = new TextBlock { Text = values[index], TextTrimming = TextTrimming.CharacterEllipsis };
                    var cell = new Border { Child = text,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = index == values.Length - 1 ? new Thickness(0, 0, 0, 1) :
                            new Thickness(0, 0, 1, 1),
                        Padding = new Thickness(10, 5, 10, 5) };
                    Grid.SetColumn(cell, index);
                    row.Children.Add(cell);
                }
                var listItem = new ListViewItem { Content = row, Tag = item, Width = 940, Height = 39,
                    Padding = new Thickness(0), Margin = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch };
                list.Items.Add(listItem);
            }
            applyColumnLayout();
        }
        search.TextChanged += (_, _) => refresh();
        from.DateChanged += (_, _) => refresh();
        until.DateChanged += (_, _) => refresh();
        refresh();
        AddTab("Οικονομικά", root);
    }

    private async Task ShowTransactionAsync(CustomerTransaction transaction)
    {
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = transaction.Type == "receipt" ? "Γενική είσπραξη" : "Γενική πληρωμή",
            Content = $"Ημερομηνία: {transaction.OccurredAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                $"Ποσό: {transaction.Amount:N2} {TransactionSettingsService.Symbol(transaction.Currency)}\n" +
                $"Τρόπος πληρωμής: {transaction.PaymentMethod}\n" +
                $"Αριθμός αναφοράς: {transaction.Reference ?? "—"}\n" +
                $"Αιτιολογία: {transaction.Description ?? "—"}",
            CloseButtonText = "Κλείσιμο" };
        await DialogButtonStyles.ShowAsync(dialog);
    }

    private void BuildHistoryTab()
    {
        var root = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = "Ιστορικό και επιπλέον στοιχεία", FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "Ζημιές, σκάφη, επαφές, διευθύνσεις και αρχεία παραμένουν διαθέσιμα εδώ.",
            TextWrapping = TextWrapping.Wrap });
        var timeline = new List<(DateTime When, string Text)>();
        if (_customer.Id != Guid.Empty)
        {
            timeline.Add((_customer.CreatedAtUtc, "Δημιουργία καρτέλας πελάτη"));
            if (_customer.UpdatedAtUtc > _customer.CreatedAtUtc)
                timeline.Add((_customer.UpdatedAtUtc, "Τελευταία ενημέρωση καρτέλας"));
        }
        timeline.AddRange(_customer.Details.Select(item => (item.CreatedAtUtc,
            $"Στοιχείο: {item.Title}")));
        timeline.AddRange(_customer.Transactions.Select(item => (item.OccurredAtUtc,
            $"{(item.Type == "receipt" ? "Είσπραξη" : "Πληρωμή")}: {item.Amount:N2} {TransactionSettingsService.Symbol(item.Currency)}")));
        timeline.AddRange(_customer.BusinessRecords.Select(item => (item.CreatedAtUtc,
            $"Συνδεδεμένη εγγραφή: {item.Title}")));
        if (timeline.Count > 0)
            root.Children.Add(new ListView { ItemsSource = timeline.OrderByDescending(item => item.When)
                .Take(30).Select(item => $"{item.When.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {item.Text}")
                .ToArray(), Height = Math.Min(130, timeline.Count * 38 + 6) });
        else root.Children.Add(new TextBlock { Text = "Δεν υπάρχουν ακόμη κινήσεις ιστορικού.",
            FontSize = 12, Margin = new Thickness(0, 0, 0, 2) });
        _historyChoice = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        var categories = new (string Key, string Label)[] { ("claims", "Ζημιές"), ("boats", "Σκάφη"),
            ("phones", "Τηλέφωνα"), ("addresses", "Διευθύνσεις"), ("emails", "Email"),
            ("persons", "Πρόσωπα"), ("other", "Άλλα στοιχεία"), ("files", "Αρχεία") };
        foreach (var (key, label) in categories)
        {
            _historyPanels[key] = CreateDetailPanel(key);
            _historyChoice.Items.Add(new ComboBoxItem { Content = label, Tag = key });
        }
        root.Children.Add(_historyChoice);
        _historyHost = new ContentControl();
        root.Children.Add(_historyHost);
        _historyChoice.SelectionChanged += (_, _) =>
        {
            if (_historyChoice.SelectedItem is ComboBoxItem item && item.Tag is string key)
                _historyHost.Content = _historyPanels[key];
        };
        _historyChoice.SelectedIndex = 0;
        AddTab("Ιστορικό ενεργειών", root);
    }
}

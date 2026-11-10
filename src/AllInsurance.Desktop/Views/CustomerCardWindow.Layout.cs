using AllInsurance.Application.Customers;
using AllInsurance.Application.Business;
using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed partial class CustomerCardWindow
{
    private PartnerCardWindow? _linkedPartnerCard;

    private Button PartnerLinkButton()
    {
        var button = new ActionButton { Kind = "Open", Label = "Άνοιγμα συνεργάτη",
            IconOnly = true, Width = 38, Height = 36 };
        button.Click += async (_, _) =>
        {
            try
            {
                var name = _editing ? _partnerChoice?.SelectedItem as string : _customer.PartnerName;
                var partner = (await App.Services.GetRequiredService<IBusinessRecordService>()
                    .SearchAsync("partners")).FirstOrDefault(item =>
                    string.Equals(item.Title, name, StringComparison.CurrentCultureIgnoreCase));
                if (partner is null)
                {
                    await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε καταχωρημένο συνεργάτη.");
                    return;
                }
                if (_linkedPartnerCard is not null) { _linkedPartnerCard.BringToFront(); return; }
                _linkedPartnerCard = new PartnerCardWindow(partner.Id);
                _linkedPartnerCard.Closed += (_, _) => { _linkedPartnerCard = null; BringToFront(); };
                _linkedPartnerCard.BringToFront();
            }
            catch (Exception error) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, error.Message); }
        };
        return button;
    }

    private void AddPartnerRow(StackPanel panel, Control field)
    {
        var host = new Grid { ColumnSpacing = 4, ColumnDefinitions =
        { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        host.Children.Add(field);
        var link = PartnerLinkButton();
        Grid.SetColumn(link, 1); host.Children.Add(link);
        panel.Children.Add(CreateFieldRow("Συνεργάτης", host));
    }
    private Grid CompactColumns(out StackPanel left, out StackPanel right)
    {
        var grid = CardFieldLayout.TwoColumns(out left, out right, _contentWidth,
            preserveColumns: true);
        left.Spacing = 2;
        right.Spacing = 2;
        grid.MinWidth = 580;
        if (_tabs.ActualWidth > 0)
            grid.Width = Math.Min(1100, Math.Max(580, _tabs.ActualWidth - 50));
        _compactMain = grid;
        return grid;
    }

    private void BuildCompactMainTab()
    {
        var main = CompactColumns(out var left, out var right);
        var taxNumber = AddText(left, "ΑΦΜ", () => _customer.TaxNumber,
            value => _customer.TaxNumber = value);
        taxNumber.MaxLength = 9;
        AddText(left, "Επώνυμο *", () => _customer.LastName, value => _customer.LastName = value ?? "");
        AddText(left, "Όνομα *", () => _customer.FirstName, value => _customer.FirstName = value ?? "");
        AddText(left, "Πατρώνυμο", () => _customer.FatherName, value => _customer.FatherName = value);
        AddCatalogChoice(left, "Φύλο", "Φύλα", () => _customer.Gender,
            value => _customer.Gender = value);
        AddText(left, "Τόπος γέννησης", () => _customer.BirthPlace, value => _customer.BirthPlace = value);
        var birthDate = AddDate(left, "Ημ. γέννησης", () => _customer.BirthDate,
            value => _customer.BirthDate = value);
        var ageField = AddReadOnly(left, "Ηλικία", "—");
        ageField.Foreground = new SolidColorBrush(Color.FromArgb(255, 24, 135, 74));
        void UpdateAge()
        {
            if (birthDate.Date is not DateTimeOffset date) { ageField.Text = "—"; return; }
            var age = DateTime.Today.Year - date.Year;
            if (date.Date > DateTime.Today.AddYears(-age)) age--;
            ageField.Text = $"{age} έτη (αυτόματα)";
        }
        birthDate.DateChanged += (_, _) => UpdateAge();
        UpdateAge();
        AddText(left, "Κύριο κινητό", () => _customer.PrimaryMobile, value => _customer.PrimaryMobile = value);
        AddText(left, "Κύριο email", () => _customer.PrimaryEmail, value => _customer.PrimaryEmail = value);
        AddText(left, "Διεύθυνση", () => _customer.PrimaryAddress, value => _customer.PrimaryAddress = value);
        AddCitySuggest(left);
        if (_editing)
        {
            _partnerChoice = new ComboBox { MinHeight = 36, Height = 36,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = !_customer.BelongsToOffice };
            AddPartnerRow(left, _partnerChoice);
            _readFields.Add(() => _customer.PartnerName = _partnerChoice.SelectedItem as string);
            _ = LoadPartnerChoicesAsync();
        }
        else AddPartnerRow(left, new TextBox { Text = _customer.PartnerName ?? "",
            IsReadOnly = true, MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center });
        _mainBalanceText = new TextBlock { FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        _mainBalanceBadge = new Border { Child = _mainBalanceText,
            CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left };
        left.Children.Add(CreateFieldRow("Υπόλοιπο πελάτη", _mainBalanceBadge));
        UpdateMainBalance();
        AddCommunicationSection(main);

        var identityType = AddCatalogChoice(right, "Τύπος ταυτότητας", "Τύποι ταυτότητας",
            () => _customer.IdentityType, value => _customer.IdentityType = value);
        var identityNumber = AddText(right, "Αριθμός ταυτότητας",
            () => _customer.IdentityType == "Άδεια οδήγησης"
                ? _customer.LicenseNumber ?? _customer.IdentityNumber : _customer.IdentityNumber,
            value =>
            {
                if (identityType.SelectedItem as string == "Άδεια οδήγησης")
                    _customer.LicenseNumber = value;
                else _customer.IdentityNumber = value;
            });
        var identityNumberRow = (Grid)right.Children[^1];
        AddBool(right, "Είναι οδηγός", () => _customer.IsDriver, value => _customer.IsDriver = value);
        var hasLicense = AddBool(right, "Έχει δίπλωμα", () => _customer.HasDrivingLicense ||
            !string.IsNullOrWhiteSpace(_customer.LicenseNumber),
            value => _customer.HasDrivingLicense = value);
        AddBool(right, "Νέος οδηγός", () => _customer.IsNewDriver ||
            (_customer.LicenseStartDate is DateTime start && start >= DateTime.Today.AddYears(-2)),
            value => _customer.IsNewDriver = value);
        var licenseNumber = AddText(right, "Αρ. διπλώματος", () => _customer.LicenseNumber,
            value =>
            {
                if (identityType.SelectedItem as string != "Άδεια οδήγησης")
                    _customer.LicenseNumber = value;
            });
        var licenseNumberRow = (Grid)right.Children[^1];
        var licenseCategory = AddCatalogChoice(right, "Κατηγορία διπλώματος", "Κατηγορίες διπλώματος", () => _customer.LicenseCategory,
            value => _customer.LicenseCategory = value);
        var licenseStart = AddDate(right, "Έναρξη διπλώματος", () => _customer.LicenseStartDate,
            value => _customer.LicenseStartDate = value);
        var licenseEnd = AddDate(right, "Λήξη διπλώματος", () => _customer.LicenseEndDate,
            value => _customer.LicenseEndDate = value);
        void UpdateLicenseFields()
        {
            var enabled = _editing && hasLicense.IsChecked == true;
            licenseNumber.IsReadOnly = !enabled;
            licenseCategory.IsEnabled = enabled;
            licenseStart.IsEnabled = enabled;
            licenseEnd.IsEnabled = enabled;
            identityNumber.IsReadOnly = !_editing ||
                (identityType.SelectedItem as string == "Άδεια οδήγησης" && !enabled);
        }
        var previousIdentityType = _customer.IdentityType;
        void UpdateIdentityField()
        {
            var type = identityType.SelectedItem as string ?? _customer.IdentityType;
            if (_editing && previousIdentityType != type)
            {
                if (previousIdentityType == "Άδεια οδήγησης")
                    _customer.LicenseNumber = identityNumber.Text.Trim();
                else _customer.IdentityNumber = identityNumber.Text.Trim();
            }
            var label = type switch
            {
                "Άδεια οδήγησης" => "Αριθμός άδειας οδήγησης",
                "Άδεια διαμονής" => "Αριθμός άδειας διαμονής",
                "Διαβατήριο" => "Αριθμός διαβατηρίου",
                _ => "Αριθμός ταυτότητας"
            };
            CardFieldLayout.SetRequired(identityNumberRow, label, false);
            if (type == "Άδεια οδήγησης")
            {
                if (_editing) hasLicense.IsChecked = true;
                identityNumber.Text = _customer.LicenseNumber ?? _customer.IdentityNumber ?? "";
            }
            else if (previousIdentityType != type)
                identityNumber.Text = _customer.IdentityNumber ?? "";
            previousIdentityType = type;
            licenseNumberRow.Visibility = type == "Άδεια οδήγησης"
                ? Visibility.Collapsed : Visibility.Visible;
            UpdateLicenseFields();
        }
        identityType.SelectionChanged += (_, _) => UpdateIdentityField();
        hasLicense.Checked += (_, _) => UpdateLicenseFields();
        hasLicense.Unchecked += (_, _) => UpdateLicenseFields();
        UpdateIdentityField();
        UpdateLicenseFields();
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
        var field = new TextBox { Text = value, IsReadOnly = true, MinHeight = 36, Height = 36,
            FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
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
        var input = new AutoSuggestBox { Text = _customer.City ?? "", MinHeight = 36, Height = 36,
            PlaceholderText = "Αναζήτηση πόλης από τον κατάλογο" };
        SearchSuggestions.CloseOnLostFocus(input);
        input.AllowDrop = true;
        input.DragOver += (_, args) =>
        {
            if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        };
        input.Drop += async (_, args) => input.Text = await args.DataView.GetTextAsync();
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
                input.ItemsSource = SearchSuggestions.FirstHundred(options, "");
                input.IsSuggestionListOpen = true;
            };
            input.TextChanged += (_, args) =>
            {
                if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
                input.ItemsSource = SearchSuggestions.FirstHundred(options, input.Text);
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
        var root = new StackPanel { Width = _contentWidth,
            Margin = new Thickness(18, 10, 18, 16), Spacing = 10 };
        var columns = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 350, ItemHeight = 450 };
        var family = new StackPanel { Spacing = 5, Width = 330 };
        AddText(family, "Μητρώνυμο", () => _customer.MotherName, value => _customer.MotherName = value);
        AddText(family, "Διαβατήριο", () => _customer.PassportNumber,
            value => _customer.PassportNumber = value);
        AddCatalogChoice(family, "Οικ. κατάσταση", "Οικογενειακές καταστάσεις",
            () => _customer.MaritalStatus, value => _customer.MaritalStatus = value);
        AddText(family, "Σύζυγος / σύντροφος", () => _customer.SpouseName, value => _customer.SpouseName = value);
        AddDate(family, "Ονομαστική εορτή", () => _customer.NameDay, value => _customer.NameDay = value);
        AddText(family, "Περιοχή", () => _customer.Region, value => _customer.Region = value);
        columns.Children.Add(family);

        var business = new StackPanel { Spacing = 5, Width = 330 };
        AddCatalogChoice(business, "ΔΟΥ", "ΔΟΥ", () => _customer.TaxOffice,
            value => _customer.TaxOffice = value);
        AddText(business, "Ταχ. κώδικας", () => _customer.PostalCode,
            value => _customer.PostalCode = value);
        AddText(business, "ΓΕΜΗ / μητρώο", () => _customer.InsuranceRegistry,
            value => _customer.InsuranceRegistry = value);
        AddCatalogChoice(business, "Νομική μορφή", "Νομικές μορφές",
            () => _customer.LegalForm, value => _customer.LegalForm = value);
        AddText(business, "Εργοδότης", () => _customer.Employer, value => _customer.Employer = value);
        AddText(business, "Επωνυμία", () => _customer.CompanyName, value => _customer.CompanyName = value);
        AddText(business, "Τηλ. επιχείρησης", () => _customer.BusinessPhone,
            value => _customer.BusinessPhone = value);
        AddText(business, "Email επιχείρησης", () => _customer.BusinessEmail,
            value => _customer.BusinessEmail = value);
        var sourceChoice = AddCatalogChoice(business, "Πηγή", "Πηγές πελατών",
            () => _customer.Source, value => _customer.Source = value);
        _referralChoice = new ComboBox { PlaceholderText = "Επιλέξτε πελάτη",
            DisplayMemberPath = "FullName", SelectedValuePath = "Id",
            IsHitTestVisible = _editing, IsTabStop = _editing,
            MinHeight = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Stretch };
        var customerReferralRow = CreateFieldRow("Σύσταση από πελάτη", _referralChoice);
        business.Children.Add(customerReferralRow);
        _ = LoadReferrersAsync();
        _referralPartnerChoice = new ComboBox { PlaceholderText = "Επιλέξτε συνεργάτη",
            MinHeight = 36, Height = 36, IsHitTestVisible = _editing, IsTabStop = _editing,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var partnerReferralRow = CreateFieldRow("Σύσταση από συνεργάτη", _referralPartnerChoice);
        business.Children.Add(partnerReferralRow);
        _ = LoadPartnerReferrersAsync();
        void UpdateReferralRows()
        {
            var source = sourceChoice.SelectedItem as string;
            customerReferralRow.Visibility = source is "Σύσταση" or "Σύσταση από πελάτη"
                ? Visibility.Visible : Visibility.Collapsed;
            partnerReferralRow.Visibility = source == "Σύσταση από συνεργάτη"
                ? Visibility.Visible : Visibility.Collapsed;
        }
        sourceChoice.SelectionChanged += (_, _) => UpdateReferralRows();
        UpdateReferralRows();
        columns.Children.Add(business);

        var status = new StackPanel { Spacing = 5, Width = 330 };
        AddBool(status, "Δικαιολογητικό έκπτωσης", () => _customer.HasDiscountDocument,
            value => _customer.HasDiscountDocument = value);
        AddText(status, "Περιγραφή έκπτωσης", () => _customer.DiscountDocumentDescription,
            value => _customer.DiscountDocumentDescription = value);
        var officeControl = AddBool(status, "Ανήκει στο γραφείο", () => _customer.BelongsToOffice,
            value => _customer.BelongsToOffice = value);
        if (_editing && _partnerChoice is not null)
        {
            officeControl.Checked += (_, _) => _partnerChoice.IsEnabled = false;
            officeControl.Unchecked += (_, _) => _partnerChoice.IsEnabled = true;
        }
        status.Children.Add(new TextBlock { Text = _customer.Id == Guid.Empty ?
            "Η εγγραφή θα οριστεί στην αποθήκευση." :
            $"Εγγραφή: {_customer.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}", FontSize = 12 });
        status.Children.Add(new TextBlock { Text = _customer.Id == Guid.Empty ? "" :
            $"Ενημέρωση: {_customer.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}", FontSize = 12 });
        columns.Children.Add(status);
        root.Children.Add(columns);
        var notes = new TextBox { Text = _customer.Notes ?? "", MinHeight = 100, Height = 120,
            FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsReadOnly = !_editing };
        root.Children.Add(CreateFieldRow("Σημειώσεις", ResizableNoteField.Wrap(notes)));
        _readFields.Add(() => _customer.Notes = notes.Text.Trim());
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
        var transfersIn = _customer.Transactions.Where(item => item.Type == "credit_transfer_in").Sum(item => item.Amount);
        var transfersOut = _customer.Transactions.Where(item => item.Type == "credit_transfer_out").Sum(item => item.Amount);
        root.Children.Add(new TextBlock { Text =
            $"Παλαιό υπόλοιπο: {_customer.OpeningBalance:N2} {CurrencySymbol}  ·  Εισπράξεις: {receipts:N2} {CurrencySymbol}  ·  Πληρωμές: {payments:N2} {CurrencySymbol}  ·  Πιστωτικά από άλλους: {transfersIn:N2} {CurrencySymbol}  ·  Πιστωτικά προς άλλους: {transfersOut:N2} {CurrencySymbol}",
            TextWrapping = TextWrapping.Wrap });
        root.Children.Add(new TextBlock { Text =
            "Το υπόλοιπο έναρξης χρησιμοποιείται μόνο για μεταφορά παλαιού υπολοίπου. Οι νέες κινήσεις καταχωρίζονται από τα κουμπιά είσπραξης και πληρωμής.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var openingBalance = AddText(root, $"Παλαιό υπόλοιπο ({CurrencySymbol})", () => _customer.OpeningBalance.ToString("N2"),
            value => _customer.OpeningBalance = ParseAmount(value, allowNegative: true));
        void UpdateOpeningStyle()
        {
            var nonzero = decimal.TryParse(openingBalance.Text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.GetCultureInfo("el-GR"), out var amount) && amount != 0;
            openingBalance.Background = new SolidColorBrush(nonzero
                ? Color.FromArgb(255, 173, 78, 78)
                : IsDarkTheme() ? Color.FromArgb(255, 57, 64, 72) : Color.FromArgb(255, 235, 240, 245));
            openingBalance.Foreground = new SolidColorBrush(nonzero || IsDarkTheme()
                ? Microsoft.UI.Colors.White : Color.FromArgb(255, 37, 47, 59));
            openingBalance.Opacity = 1;
        }
        openingBalance.TextChanged += (_, _) => UpdateOpeningStyle();
        UpdateOpeningStyle();
        root.Children.Add(new TextBlock { Text = "Οικονομικές κινήσεις", FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var search = new TextBox { Height = 36, MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Αναζήτηση κίνησης..." };
        root.Children.Add(SearchFieldVisual.Wrap(search));
        var kind = new ComboBox { Width = 170, Height = 36,
            ItemsSource = new[] { "Όλες οι κινήσεις", "Εισπράξεις", "Πληρωμές", "Μεταφορές πιστωτικού" }, SelectedIndex = 0 };
        var from = new EditableDateInput { Width = 170, Height = 36,
            PlaceholderText = "Από ημερομηνία" };
        var until = new EditableDateInput { Width = 170, Height = 36,
            PlaceholderText = "Έως ημερομηνία" };
        var filters = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 210, ItemHeight = 42 };
        foreach (var field in new UIElement[] { kind, from, until }) filters.Children.Add(field);
        root.Children.Add(filters);
        var header = new Grid { ColumnSpacing = 0, Width = 940,
            Background = TableHeaderBrush() };
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
        TableRowStriping.Attach(list);
        root.Children.Add(list);
        var pager = new TablePagination(list);
        root.Children.Add(pager.Bar);
        list.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(async (_, args) =>
        {
            var row = FindCardRow(args.OriginalSource) ?? list.SelectedItem as ListViewItem;
            if (row?.Tag is CustomerTransaction transaction)
            {
                await ShowTransactionAsync(transaction);
                args.Handled = true;
            }
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
        var applyColumnLayout = TableInteractions.Attach(header, list, _tabContent);
        void refresh()
        {
            list.Items.Clear();
            var term = search.Text.Trim();
            var start = from.Date?.Date;
            var end = until.Date?.Date;
            foreach (var item in _customer.Transactions.OrderByDescending(item => item.OccurredAtUtc)
                .Where(item => kind.SelectedIndex == 0 ||
                    (kind.SelectedIndex == 1 ? item.Type == "receipt" :
                     kind.SelectedIndex == 2 ? item.Type == "payment" :
                     item.Type is "credit_transfer_in" or "credit_transfer_out"))
                .Where(item => !start.HasValue || item.OccurredAtUtc.ToLocalTime().Date >= start)
                .Where(item => !end.HasValue || item.OccurredAtUtc.ToLocalTime().Date <= end)
                .Where(item => term.Length == 0 || item.Description?.Contains(term,
                    StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Reference?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Amount.ToString("N2").Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            {
                var row = new Grid { ColumnSpacing = 0, Width = 940, Height = 35 };
                foreach (var width in new[] { 180, 150, 130, 480 })
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
                var values = new[] { item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    item.Type switch
                    {
                        "receipt" => "Είσπραξη", "payment" => "Πληρωμή",
                        "credit_transfer_in" => "Πιστωτικό από άλλον",
                        "credit_transfer_out" => "Πιστωτικό προς άλλον",
                        _ => "Κίνηση"
                    },
                    $"{item.Amount:N2} {TransactionSettingsService.Symbol(item.Currency)}",
                    (item.BusinessRecordId is Guid policyId
                        ? _customer.BusinessRecords.FirstOrDefault(record => record.Id == policyId)?.Title + " · " : "") +
                    (item.Description ?? "—") };
                for (var index = 0; index < values.Length; index++)
                {
                    var text = new TextBlock { Text = values[index], TextTrimming = TextTrimming.CharacterEllipsis };
                    var cell = new Border { Child = text,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                        BorderThickness = index == values.Length - 1 ? new Thickness(0, 0, 0, 1) :
                            new Thickness(0, 0, 1, 1),
                        Padding = new Thickness(10, 5, 10, 5) };
                    if (index == 1)
                    {
                        cell.Background = new SolidColorBrush(item.Type switch
                        {
                            "receipt" => StatusPalette.Positive,
                            "payment" => StatusPalette.Negative,
                            _ => StatusPalette.Informational
                        });
                        text.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    }
                    Grid.SetColumn(cell, index);
                    row.Children.Add(cell);
                }
                var listItem = new ListViewItem { Content = row, Tag = item, Width = 940, Height = 35,
                    Padding = new Thickness(0), Margin = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch };
                TableRowResizer.Attach(row, listItem, "customer-transactions", item.Id);
                list.Items.Add(listItem);
            }
            pager.Capture();
            applyColumnLayout();
        }
        search.TextChanged += (_, _) => refresh();
        kind.SelectionChanged += (_, _) => refresh();
        from.DateChanged += (_, _) => refresh();
        until.DateChanged += (_, _) => refresh();
        refresh();
        AddTab("Οικονομικά", root);
    }

    private Task ShowTransactionAsync(CustomerTransaction transaction)
    {
        ReceiptCardWindow.Open(transaction.Id, WinRT.Interop.WindowNative.GetWindowHandle(this));
        return Task.CompletedTask;
    }

}

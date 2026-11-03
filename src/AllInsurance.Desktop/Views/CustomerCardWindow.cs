using AllInsurance.Application.Customers;
using AllInsurance.Desktop.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Windows.Graphics;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Foundation;
using Windows.UI;

namespace AllInsurance.Desktop.Views;

public sealed class CustomerCardWindow : Window
{
    private readonly ICustomerService _service = App.Services.GetRequiredService<ICustomerService>();
    private readonly Guid? _customerId;
    private readonly Grid _root = new() { RowDefinitions = { new RowDefinition { Height = GridLength.Auto },
        new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
    private readonly TabView _tabs = new() { IsAddTabButtonVisible = false };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<Action> _readFields = [];
    private Customer _customer = new() { LastName = "", FirstName = "" };
    private ComboBox? _referralChoice;
    private bool _editing;
    private readonly TextBlock _headerTitle = new() { FontSize = 26,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerCode = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerAfm = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly TextBlock _headerState = new() { Foreground = new SolidColorBrush(Colors.White) };
    private readonly ActionButton _saveButton = new() { Kind = "Save", Label = "Αποθήκευση" };
    private readonly ActionButton _editButton = new() { Kind = "Edit", Label = "Επεξεργασία πελάτη" };

    public CustomerCardWindow(Guid? customerId)
    {
        _customerId = customerId;
        _editing = !customerId.HasValue;
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(18, 16, 18, 8) };
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 13, 47, 91), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 119, 170), Offset = 1 });
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        identity.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri("ms-appx:///Assets/Sections/customers.png")),
            Width = 54, Height = 54
        });
        var identityText = new StackPanel { Spacing = 3 };
        identityText.Children.Add(_headerTitle);
        identityText.Children.Add(_headerState);
        identity.Children.Add(identityText);
        var metadata = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 40,
            Margin = new Thickness(68, 2, 0, 0) };
        metadata.Children.Add(_headerCode);
        metadata.Children.Add(_headerAfm);
        var bannerContent = new StackPanel { Spacing = 8 };
        bannerContent.Children.Add(identity);
        bannerContent.Children.Add(metadata);
        header.Children.Add(new Border { Background = gradient, CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24, 18, 24, 18), Child = bannerContent });
        header.Children.Add(_status);
        _root.Children.Add(header);
        Grid.SetRow(_tabs, 1);
        _root.Children.Add(_tabs);
        var actions = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 190, ItemHeight = 48, Margin = new Thickness(20, 10, 20, 18) };
        var primaryAction = new Grid { Width = 180 };
        _saveButton.Click += async (_, _) => await SaveAsync();
        primaryAction.Children.Add(_saveButton);
        _editButton.Click += (_, _) =>
        {
            _editing = true;
            BuildTabs();
            UpdateActionState();
        };
        primaryAction.Children.Add(_editButton);
        actions.Children.Add(primaryAction);
        AddButton(actions, "Γενική είσπραξη", async () => await AddTransactionAsync("receipt"));
        AddButton(actions, "Γενική πληρωμή", async () => await AddTransactionAsync("payment"));
        AddButton(actions, "Στατιστικά πελάτη", ShowCustomerStatisticsAsync);
        AddDeferredButton(actions, "Υπεύθυνη δήλωση Ν. 105");
        AddDeferredButton(actions, "Ασφαλιστικός γονέας");
        AddDeferredButton(actions, "Έντυπο GDPR");
        AddButton(actions, "Κλείσιμο", () => { Close(); return Task.CompletedTask; });
        Grid.SetRow(actions, 2);
        _root.Children.Add(actions);
        _root.RequestedTheme = App.Services.GetRequiredService<WorkspacePreferences>().Theme;
        var dark = _root.RequestedTheme == ElementTheme.Dark ||
            (_root.RequestedTheme == ElementTheme.Default &&
             Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
        _root.Background = new SolidColorBrush(dark
            ? Color.FromArgb(255, 31, 31, 31) : Color.FromArgb(255, 247, 248, 250));
        Content = _root;

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var working = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1220, Math.Max(680, working.Width - 80));
        var height = Math.Min(880, Math.Max(520, working.Height - 80));
        appWindow.MoveAndResize(new RectInt32(working.X + (working.Width - width) / 2,
            working.Y + (working.Height - height) / 2, width, height));
        appWindow.Title = "Καρτέλα πελάτη — AllInsurance Backoffice Software";
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "allinsurance.ico"));
        UpdateHeader();
        UpdateActionState();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            if (_customerId is Guid id)
            {
                _customer = await _service.GetAsync(id) ??
                    throw new InvalidOperationException("Ο πελάτης δεν υπάρχει.");
            }
            BuildTabs();
            UpdateHeader();
            UpdateActionState();
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    private void BuildTabs()
    {
        _readFields.Clear();
        _tabs.TabItems.Clear();
        BuildMainTab();

        var other = CreateForm();
        AddText(other, "Δεύτερο τηλέφωνο", () => _customer.SecondPhone, value => _customer.SecondPhone = value);
        AddText(other, "Τρίτο τηλέφωνο", () => _customer.ThirdPhone, value => _customer.ThirdPhone = value);
        AddText(other, "Δεύτερο email", () => _customer.SecondEmail, value => _customer.SecondEmail = value);
        AddText(other, "Τρίτο email", () => _customer.ThirdEmail, value => _customer.ThirdEmail = value);
        AddText(other, "Email επιχείρησης", () => _customer.BusinessEmail, value => _customer.BusinessEmail = value);
        AddText(other, "Τηλέφωνο επιχείρησης", () => _customer.BusinessPhone, value => _customer.BusinessPhone = value);
        AddText(other, "Σημειώσεις", () => _customer.Notes, value => _customer.Notes = value, multiline: true);
        AddTab("Λοιπά", other);

        AddDetailTab("Ασφαλιστήρια", "policies");
        AddDetailTab("Οχήματα", "vehicles");
        AddDetailTab("Σκάφη", "boats");
        AddDetailTab("Ζημιές", "claims");
        AddDetailTab("Τηλέφωνα", "phones");
        AddDetailTab("Διευθύνσεις", "addresses");
        AddDetailTab("Email", "emails");
        AddDetailTab("Πρόσωπα", "persons");
        AddDetailTab("Άλλα στοιχεία", "other");
        AddDetailTab("Αρχεία", "files");
        var movements = new StackPanel { Margin = new Thickness(24), Spacing = 10 };
        movements.Children.Add(new TextBlock { Text = $"Τρέχον υπόλοιπο: {CustomerService.Balance(_customer):N2} €",
            FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var transaction in _customer.Transactions.OrderByDescending(item => item.OccurredAtUtc))
        {
            movements.Children.Add(new TextBlock
            {
                Text = $"{transaction.OccurredAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · " +
                    $"{(transaction.Type == "receipt" ? "Είσπραξη" : "Πληρωμή")} · " +
                    $"{transaction.Amount:N2} € · {transaction.Description}",
                TextWrapping = TextWrapping.Wrap
            });
        }
        AddTab("Κινήσεις", movements);
        _tabs.SelectedIndex = 0;
    }

    private void BuildMainTab()
    {
        var main = new Grid { Width = 1100, ColumnSpacing = 24,
            Margin = new Thickness(24, 14, 24, 24) };
        main.ColumnDefinitions.Add(new ColumnDefinition());
        main.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new StackPanel { Spacing = 8 };
        var right = new StackPanel { Spacing = 8 };
        Grid.SetColumn(right, 1);
        main.Children.Add(left);
        main.Children.Add(right);

        AddHeading(left, "Ταυτότητα και επικοινωνία");
        AddText(left, "Επώνυμο *", () => _customer.LastName, value => _customer.LastName = value ?? "");
        AddText(left, "Όνομα *", () => _customer.FirstName, value => _customer.FirstName = value ?? "");
        AddChoice(left, "Τύπος πελάτη", () => _customer.CustomerKind,
            value => _customer.CustomerKind = value, ["Φυσικό πρόσωπο", "Επιχείρηση"]);
        AddText(left, "Επωνυμία επιχείρησης", () => _customer.CompanyName,
            value => _customer.CompanyName = value);
        AddText(left, "Κύρια διεύθυνση", () => _customer.PrimaryAddress,
            value => _customer.PrimaryAddress = value);
        AddText(left, "Πόλη", () => _customer.City, value => _customer.City = value);
        AddText(left, "Περιοχή", () => _customer.Region, value => _customer.Region = value);
        AddText(left, "Ταχυδρομικός κώδικας", () => _customer.PostalCode,
            value => _customer.PostalCode = value);
        AddText(left, "Κύριο κινητό", () => _customer.PrimaryMobile,
            value => _customer.PrimaryMobile = value);
        AddText(left, "Κύριο email", () => _customer.PrimaryEmail,
            value => _customer.PrimaryEmail = value);
        AddText(left, "Επάγγελμα", () => _customer.Occupation, value => _customer.Occupation = value);
        AddText(left, "ΑΦΜ", () => _customer.TaxNumber, value => _customer.TaxNumber = value);
        AddText(left, "ΔΟΥ", () => _customer.TaxOffice, value => _customer.TaxOffice = value);
        AddHeading(left, "Γέννηση και οικογένεια");
        AddDate(left, "Ημερομηνία γέννησης", () => _customer.BirthDate,
            value => _customer.BirthDate = value);
        AddText(left, "Τόπος γέννησης", () => _customer.BirthPlace,
            value => _customer.BirthPlace = value);
        AddText(left, "Ιθαγένεια", () => _customer.Nationality,
            value => _customer.Nationality = value);
        AddText(left, "Φύλο", () => _customer.Gender, value => _customer.Gender = value);
        AddText(left, "Πατρώνυμο", () => _customer.FatherName,
            value => _customer.FatherName = value);
        AddText(left, "Μητρώνυμο", () => _customer.MotherName,
            value => _customer.MotherName = value);

        AddHeading(right, "Στοιχεία και κατάσταση");
        AddText(right, "Τύπος ταυτότητας", () => _customer.IdentityType,
            value => _customer.IdentityType = value);
        AddText(right, "Αριθμός ταυτότητας", () => _customer.IdentityNumber,
            value => _customer.IdentityNumber = value);
        AddText(right, "ΑΜΚΑ", () => _customer.SocialSecurityNumber,
            value => _customer.SocialSecurityNumber = value);
        AddBool(right, "Έχει προσωπικά ασφαλιστήρια", () => _customer.HasPersonalInsurance,
            value => _customer.HasPersonalInsurance = value);
        AddText(right, "Ομαδοποίηση", () => _customer.GroupName, value => _customer.GroupName = value);
        AddText(right, "Αρχικό υπόλοιπο (€)", () => _customer.OpeningBalance.ToString("N2"),
            value => _customer.OpeningBalance = ParseAmount(value, allowNegative: true));
        right.Children.Add(new TextBlock
        {
            Text = $"Τρέχον υπόλοιπο: {CustomerService.Balance(_customer):N2} €",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        AddBool(right, "Ενεργός πελάτης", () => _customer.IsActive, value => _customer.IsActive = value);
        AddBool(right, "Πελάτης από σύσταση", () => _customer.IsReferral,
            value => _customer.IsReferral = value);
        _referralChoice = new ComboBox { Header = "Σύσταση από πελάτη",
            DisplayMemberPath = "FullName", SelectedValuePath = "Id", IsEnabled = _editing };
        right.Children.Add(_referralChoice);
        _ = LoadReferrersAsync();
        AddBool(right, "Ανήκει στο γραφείο", () => _customer.BelongsToOffice,
            value => _customer.BelongsToOffice = value);
        AddText(right, "Συνεργάτης", () => _customer.PartnerName, value => _customer.PartnerName = value);
        AddHeading(right, "Οδήγηση και έγγραφα");
        AddBool(right, "Είναι οδηγός", () => _customer.IsDriver, value => _customer.IsDriver = value);
        AddDate(right, "Έναρξη διπλώματος", () => _customer.LicenseStartDate,
            value => _customer.LicenseStartDate = value);
        AddDate(right, "Λήξη διπλώματος", () => _customer.LicenseEndDate,
            value => _customer.LicenseEndDate = value);
        AddText(right, "Κατηγορία διπλώματος", () => _customer.LicenseCategory,
            value => _customer.LicenseCategory = value);
        AddText(right, "Αριθμός διπλώματος", () => _customer.LicenseNumber,
            value => _customer.LicenseNumber = value);
        AddText(right, "HL diploma", () => _customer.HLDiploma,
            value => _customer.HLDiploma = value);
        AddBool(right, "Έχει δικαιολογητικό έκπτωσης", () => _customer.HasDiscountDocument,
            value => _customer.HasDiscountDocument = value);
        AddText(right, "Δικαιολογητικό έκπτωσης", () => _customer.DiscountDocumentDescription,
            value => _customer.DiscountDocumentDescription = value);
        right.Children.Add(new TextBlock
        {
            Text = _customer.Id == Guid.Empty ? "Η εγγραφή θα οριστεί κατά την αποθήκευση." :
                $"Ημερομηνία εγγραφής: {_customer.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}"
        });
        AddTab("Καρτέλα πελάτη", main);
    }

    private void UpdateHeader()
    {
        _headerTitle.Text = _customer.Id == Guid.Empty ? "Νέος πελάτης" :
            $"{_customer.LastName} {_customer.FirstName}".ToUpperInvariant();
        _headerCode.Text = "ΚΩΔΙΚΟΣ  " + (_customer.Id == Guid.Empty ? "Νέος" :
            "C-" + _customer.Id.ToString("N")[..8].ToUpperInvariant());
        _headerAfm.Text = "ΑΦΜ  " + (_customer.TaxNumber ?? "—");
        _headerState.Text = _customer.IsActive ? "● Ενεργός πελάτης" : "● Ανενεργός πελάτης";
    }

    private void UpdateActionState()
    {
        _saveButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        _editButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task LoadReferrersAsync()
    {
        try
        {
            var items = (await _service.SearchAsync()).Where(item => item.Id != _customer.Id).ToArray();
            if (_referralChoice is null) return;
            _referralChoice.ItemsSource = items;
            if (_customer.ReferredByCustomerId.HasValue)
                _referralChoice.SelectedValue = _customer.ReferredByCustomerId.Value;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    private void AddDetailTab(string label, string category)
    {
        var root = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() },
            Margin = new Thickness(16) };
        var commands = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal,
            ItemWidth = 190, ItemHeight = 48 };
        if (category == "files")
        {
            AddButton(commands, "Επισύναψη αρχείου", AddFileAsync);
            AddButton(commands, "Άνοιγμα", OpenSelectedFileAsync);
        }
        else AddButton(commands, "Προσθήκη", async () => await EditDetailAsync(category, null));
        if (category != "files") AddButton(commands, "Επεξεργασία", async () =>
        {
            var selected = GetSelectedDetail(category);
            if (selected is null) { _status.Text = "Επιλέξτε στοιχείο."; return; }
            await EditDetailAsync(category, selected);
        });
        AddButton(commands, "Διαγραφή επιλεγμένου", async () => await DeleteDetailAsync(category));
        root.Children.Add(commands);
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single,
            Height = 460, Margin = new Thickness(0, 12, 0, 0),
            Tag = category };
        var details = _customer.Details.Where(detail => detail.Category == category)
            .OrderByDescending(detail => detail.CreatedAtUtc)
            .Select(detail => (object)new DetailRow(detail));
        var linked = _customer.BusinessRecords.Where(record => record.SectionKey == category)
            .OrderByDescending(record => record.CreatedAtUtc)
            .Select(record => (object)new LinkedRecordRow(record));
        list.ItemsSource = details.Concat(linked).ToArray();
        Grid.SetRow(list, 1);
        root.Children.Add(list);
        AddTab(label, root);
    }

    private async Task EditDetailAsync(string category, CustomerDetail? existing)
    {
        if (!await EnsureSavedAsync()) return;
        var title = new TextBox { Header = "Τίτλος *", PlaceholderText = category == "vehicles" ? "Πινακίδα" : "Περιγραφή",
            Text = existing?.Title ?? "" };
        var value = new TextBox { Header = "Τιμή / αριθμός / στοιχεία", Text = existing?.Value ?? "" };
        var notes = new TextBox { Header = "Σημειώσεις", AcceptsReturn = true, Height = 90,
            Text = existing?.Notes ?? "" };
        var from = new TextBox { Header = "Από (ηη/μμ/εεεε)",
            Text = existing?.EffectiveFrom?.ToString("dd/MM/yyyy") ?? "" };
        var until = new TextBox { Header = "Έως (ηη/μμ/εεεε)",
            Text = existing?.EffectiveUntil?.ToString("dd/MM/yyyy") ?? "" };
        var historical = new CheckBox { Content = "Ιστορική καταχώριση", IsChecked = existing?.IsHistorical ?? false };
        var form = new StackPanel { Spacing = 10, Width = 460 };
        foreach (var field in new UIElement[] { title, value, notes, from, until, historical }) form.Children.Add(field);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Προσθήκη στοιχείου" : "Επεξεργασία στοιχείου",
            Content = new ScrollViewer { Content = form, MaxHeight = 460 },
            PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            if (existing is null)
                await _service.AddDetailAsync(_customer.Id, category, title.Text, value.Text, notes.Text,
                    ParseDate(from.Text), ParseDate(until.Text), historical.IsChecked == true);
            else
                await _service.UpdateDetailAsync(_customer.Id, existing.Id, title.Text, value.Text, notes.Text,
                    ParseDate(from.Text), ParseDate(until.Text), historical.IsChecked == true);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab(category);
            _status.Text = "Το στοιχείο αποθηκεύτηκε.";
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task DeleteDetailAsync(string category)
    {
        var detail = GetSelectedDetail(category);
        if (detail is null)
        {
            _status.Text = "Επιλέξτε στοιχείο.";
            return;
        }
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή στοιχείου",
            Content = $"Να διαγραφεί το «{detail.Title}»;", PrimaryButtonText = "Διαγραφή",
            CloseButtonText = "Ακύρωση" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            await _service.DeleteDetailAsync(_customer.Id, detail.Id);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab(category);
            _status.Text = "Το στοιχείο διαγράφηκε.";
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task AddFileAsync()
    {
        if (!await EnsureSavedAsync()) return;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            await _service.AddFileAsync(_customer.Id, file.Path);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            SelectTab("files");
            _status.Text = "Το αρχείο επισυνάφθηκε.";
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private Task OpenSelectedFileAsync()
    {
        var file = GetSelectedDetail("files");
        if (file is null)
        {
            _status.Text = "Επιλέξτε αρχείο.";
            return Task.CompletedTask;
        }
        try
        {
            Process.Start(new ProcessStartInfo(_service.GetFilePath(file)) { UseShellExecute = true });
        }
        catch (Exception exception) { _status.Text = exception.Message; }
        return Task.CompletedTask;
    }

    private CustomerDetail? GetSelectedDetail(string category) =>
        _tabs.TabItems.OfType<TabViewItem>().Select(item => item.Content)
            .OfType<ScrollViewer>().Select(view => view.Content).OfType<Grid>()
            .SelectMany(grid => grid.Children.OfType<ListView>())
            .FirstOrDefault(view => view.Tag is string tag && tag == category)?
            .SelectedItem is DetailRow row ? row.Detail : null;

    private void SelectTab(string category)
    {
        var labels = new[] { "policies", "vehicles", "boats", "claims", "phones", "addresses",
            "emails", "persons", "other", "files" };
        _tabs.SelectedIndex = Array.IndexOf(labels, category) + 2;
    }

    private async Task AddTransactionAsync(string type)
    {
        if (!await EnsureSavedAsync()) return;
        var amount = new TextBox { Header = "Ποσό (€) *" };
        var description = new TextBox { Header = "Αιτιολογία" };
        var form = new StackPanel { Spacing = 12, Width = 360 };
        form.Children.Add(amount);
        form.Children.Add(description);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = type == "receipt" ? "Γενική είσπραξη" : "Γενική πληρωμή",
            Content = form, PrimaryButtonText = "Καταχώριση", CloseButtonText = "Ακύρωση" };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            await _service.AddTransactionAsync(_customer.Id, type, ParseAmount(amount.Text, false), description.Text);
            _customer = await _service.GetAsync(_customer.Id) ?? _customer;
            BuildTabs();
            _status.Text = $"Η κίνηση καταχωρίστηκε. Νέο υπόλοιπο: {CustomerService.Balance(_customer):N2} €";
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task ShowCustomerStatisticsAsync()
    {
        var details = _customer.Details.Count;
        var linked = _customer.BusinessRecords.Count;
        var receipts = _customer.Transactions.Where(item => item.Type == "receipt").Sum(item => item.Amount);
        var payments = _customer.Transactions.Where(item => item.Type == "payment").Sum(item => item.Amount);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = "Στατιστικά πελάτη",
            Content = $"Σχετικά στοιχεία: {details}\nΣυνδεδεμένες εγγραφές: {linked}\n" +
                $"Εισπράξεις: {receipts:N2} €\nΠληρωμές: {payments:N2} €\n" +
                $"Τρέχον υπόλοιπο: {CustomerService.Balance(_customer):N2} €",
            CloseButtonText = "Κλείσιμο" };
        await dialog.ShowAsync();
    }

    private async Task<bool> EnsureSavedAsync()
    {
        if (_customer.Id != Guid.Empty) return true;
        return await SaveAsync();
    }

    private async Task<bool> SaveAsync()
    {
        try
        {
            foreach (var read in _readFields) read();
            _customer.ReferredByCustomerId = _customer.IsReferral
                ? _referralChoice?.SelectedValue as Guid? : null;
            var id = await _service.SaveAsync(_customer);
            _customer = await _service.GetAsync(id) ?? _customer;
            _editing = false;
            BuildTabs();
            UpdateHeader();
            UpdateActionState();
            _status.Text = "Η καρτέλα πελάτη αποθηκεύτηκε.";
            return true;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
            return false;
        }
    }

    private void AddTab(string header, UIElement content)
    {
        _tabs.TabItems.Add(new TabViewItem { Header = header,
            Content = new ScrollViewer { Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }

    private static StackPanel CreateForm() => new() { Spacing = 12, Margin = new Thickness(24, 16, 24, 24),
        MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };

    private static void AddHeading(StackPanel form, string title) => form.Children.Add(new TextBlock
    {
        Text = title, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 12, 0, 2)
    });

    private void AddText(StackPanel form, string label, Func<string?> getter,
        Action<string?> setter, bool multiline = false)
    {
        var input = new TextBox { Text = getter() ?? "",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AcceptsReturn = multiline, Height = multiline ? 110 : double.NaN,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            IsReadOnly = !_editing };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        _readFields.Add(() => setter(input.Text.Trim()));
    }

    private void AddBool(StackPanel form, string label, Func<bool> getter, Action<bool> setter)
    {
        var input = new CheckBox { IsChecked = getter(), IsEnabled = _editing };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        _readFields.Add(() => setter(input.IsChecked == true));
    }

    private void AddChoice(StackPanel form, string label, Func<string> getter,
        Action<string> setter, string[] options)
    {
        var input = new ComboBox { ItemsSource = options, IsEnabled = _editing,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = options.Contains(getter()) ? getter() : options[0] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        _readFields.Add(() => setter(input.SelectedItem as string ?? options[0]));
    }

    private void AddDate(StackPanel form, string label, Func<DateTime?> getter, Action<DateTime?> setter)
    {
        var input = new TextBox { PlaceholderText = "ηη/μμ/εεεε",
            Text = getter()?.ToString("dd/MM/yyyy") ?? "",
            IsReadOnly = !_editing };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, label);
        form.Children.Add(CreateFieldRow(label, input));
        _readFields.Add(() => setter(ParseDate(input.Text)));
    }

    private static Grid CreateFieldRow(string label, FrameworkElement input)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = label, FontSize = 13,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        return row;
    }

    private static DateTime? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (DateTime.TryParseExact(input.Trim(), "dd/MM/yyyy", CultureInfo.GetCultureInfo("el-GR"),
            DateTimeStyles.None, out var date)) return date;
        throw new ArgumentException("Η ημερομηνία πρέπει να είναι στη μορφή ηη/μμ/εεεε.");
    }

    private static decimal ParseAmount(string? input, bool allowNegative)
    {
        if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var amount) ||
            (!allowNegative && amount <= 0))
            throw new ArgumentException("Συμπληρώστε έγκυρο ποσό.");
        return amount;
    }

    private static void AddButton(Panel panel, string label, Func<Task> action)
    {
        var button = new ActionButton { Label = label, Kind = ActionKind(label),
            Width = 180, MinHeight = 38 };
        button.Click += async (_, _) => await action();
        panel.Children.Add(button);
    }

    private static void AddDeferredButton(Panel panel, string label)
    {
        var button = new ActionButton { Label = label, Kind = "File", Width = 180,
            IsEnabled = false, Opacity = 0.6 };
        ToolTipService.SetToolTip(button, "Διαθέσιμο σε επόμενη φάση με το εγκεκριμένο υπόδειγμα.");
        panel.Children.Add(button);
    }

    private static string ActionKind(string label) => label switch
    {
        "Γενική είσπραξη" => "Receipt",
        "Γενική πληρωμή" => "Payment",
        "Στατιστικά πελάτη" => "Statistics",
        "Επισύναψη αρχείου" => "Upload",
        "Άνοιγμα" => "Open",
        "Προσθήκη" => "Add",
        "Επεξεργασία" => "Edit",
        "Διαγραφή επιλεγμένου" => "Delete",
        "Κλείσιμο" => "Close",
        _ => "Open"
    };

    private sealed record DetailRow(CustomerDetail Detail)
    {
        public override string ToString() => $"{Detail.Title} — {Detail.Value}" +
            (Detail.IsHistorical ? " · Ιστορικό" : "");
    }

    private sealed record LinkedRecordRow(BusinessRecord Record)
    {
        public override string ToString() => $"{Record.Title} — {Record.Reference} · Από την ενότητα";
    }
}

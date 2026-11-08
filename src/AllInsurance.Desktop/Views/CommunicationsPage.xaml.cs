using AllInsurance.Application.Business;
using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using AllInsurance.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text.Json;
using System.Net;
using System.Text;
using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace AllInsurance.Desktop.Views;

public sealed partial class CommunicationsPage : Page
{
    private readonly IBusinessRecordService _records = App.Services.GetRequiredService<IBusinessRecordService>();
    private readonly CommunicationGateway _gateway = new();
    private Candidate[] _candidates = [];
    private Candidate[] _shown = [];
    private readonly HashSet<Guid> _selected = [];
    private bool _syncingSelection;
    private BusinessRecordCardWindow? _policyCard;
    private readonly List<MessageTemplate> _templates = [];
    private readonly Dictionary<string, DateTime> _sent = [];
    private static readonly string CommunicationFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "Communications");
    private static readonly string TemplatesFile = Path.Combine(CommunicationFolder, "templates.json");
    private static readonly string SentFile = Path.Combine(CommunicationFolder, "sent.json");

    public CommunicationsPage()
    {
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Ενημέρωση πελατών",
            ["Πελάτης", "Email", "Τηλέφωνο", "Απεστάλη", "Ασφαλιστήριο", "Λήξη", "Είδος"],
            _shown.Select(item =>
            {
                var key = $"{item.Record.Id}:{(ChannelChoice.SelectedIndex == 1 ? "email" : "sms")}";
                return new[] { item.CustomerName, item.Email, item.Phone,
                    _sent.TryGetValue(key, out var date) ? date.ToLocalTime().ToString("dd/MM/yyyy") : "—",
                    item.Number, item.DueDate.ToString("dd/MM/yyyy"), item.Type };
            }).ToArray()));
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(RecipientsList);
        ChannelChoice.SelectedIndex = 0;
        DocumentFilter.SelectedIndex = 0;
        FromDate.Date = new DateTimeOffset(DateTime.Today);
        UntilDate.Date = new DateTimeOffset(DateTime.Today.AddDays(30));
        SmsBody.Text = "Αγαπητέ/ή {όνομα}, το ασφαλιστήριό σας {αριθμός} λήγει στις {λήξη}. Επικοινωνήστε μαζί μας για την ανανέωσή του.";
        EmailBody.Document.SetText(TextSetOptions.None,
            "Αγαπητέ/ή {όνομα},\n\nΤο ασφαλιστήριό σας {αριθμός} λήγει στις {λήξη}. Επικοινωνήστε μαζί μας για την ανανέωσή του.\n\nAllInsurance Backoffice");
        SubjectInput.Text = "Υπενθύμιση ανανέωσης ασφαλιστηρίου";
        ReadLocalData();
        RefreshTemplates();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var records = await _records.SearchAsync("policies");
            _candidates = records.Where(record => record.Customer is not null)
                .Select(record => (Record: record, Data: BusinessRecordDetails.Read<PolicyCardData>(record.DetailsJson)))
                .Where(item => item.Data.EndDate is not null || item.Record.EventDate is not null)
                .Select(item => new Candidate(item.Record, item.Data)).ToArray();
            CompanyFilter.Items.Clear();
            CompanyFilter.Items.Add("Όλες οι εταιρείες");
            foreach (var name in _candidates.Select(item => item.Company).Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(value => value))
                CompanyFilter.Items.Add(name);
            CompanyFilter.SelectedIndex = 0;
            ApplyFilters();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν φορτώθηκαν οι παραλήπτες: " + exception.Message);
        }
    }

    private void ChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmailHeader is null) return;
        var email = ChannelChoice.SelectedIndex == 1;
        EmailHeader.Visibility = email ? Visibility.Visible : Visibility.Collapsed;
        EmailEditorPanel.Visibility = email ? Visibility.Visible : Visibility.Collapsed;
        SmsBody.Visibility = email ? Visibility.Collapsed : Visibility.Visible;
        _selected.Clear();
        RefreshTemplates();
        ApplyFilters();
    }

    private void FiltersChanged(object sender, object args) => ApplyFilters();

    private void ApplyFilters()
    {
        if (RecipientsList is null || DocumentFilter is null || ChannelChoice is null || CompanyFilter is null ||
            FromDate is null || UntilDate is null || SearchInput is null) return;
        var from = FromDate.Date?.Date ?? DateTime.Today;
        var until = UntilDate.Date?.Date ?? from.AddDays(30);
        var term = SearchInput.Text.Trim();
        var company = CompanyFilter.SelectedIndex > 0 ? CompanyFilter.SelectedItem?.ToString() : null;
        _shown = _candidates.Where(item => item.DueDate.Date >= from && item.DueDate.Date <= until)
            .Where(item => DocumentFilter.SelectedIndex == 0 || DocumentFilter.SelectedIndex == 1 && item.Type == "Ασφαλιστήριο" ||
                DocumentFilter.SelectedIndex == 2 && item.Type == "Αίτηση" ||
                DocumentFilter.SelectedIndex == 3 && item.Type == "Ειδοποιητήριο")
            .Where(item => company is null || item.Company == company)
            .Where(item => term.Length == 0 || new[] { item.CustomerName, item.Number, item.Company,
                item.Branch, item.Email, item.Phone }
                .Any(value => value.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(item => item.DueDate).ToArray();
        RecipientsList.Items.Clear();
        foreach (var candidate in _shown) RecipientsList.Items.Add(BuildRow(candidate));
        _syncingSelection = true;
        SelectAll.IsChecked = _shown.Length > 0 && _shown.All(item => _selected.Contains(item.Record.Id));
        _syncingSelection = false;
        UpdateStatus();
    }

    private ListViewItem BuildRow(Candidate candidate)
    {
        var grid = new Grid { Height = 40, ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(42) },
            new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        } };
        var box = new AllInsurance.Desktop.Controls.VisibleSelectionCheckBox
        { Tag = candidate.Record.Id, IsChecked = _selected.Contains(candidate.Record.Id),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        box.Checked += RecipientCheckChanged;
        box.Unchecked += RecipientCheckChanged;
        var selectionCell = new Border { Child = box,
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
            BorderThickness = new Thickness(0, 0, 1, 1) };
        grid.Children.Add(selectionCell);
        var sentKey = $"{candidate.Record.Id}:{(ChannelChoice.SelectedIndex == 1 ? "email" : "sms")}";
        var values = new[] { candidate.CustomerName, candidate.Email, candidate.Phone,
            _sent.TryGetValue(sentKey, out var sentAt) ? sentAt.ToLocalTime().ToString("dd/MM/yyyy") : "—",
            candidate.Number, candidate.DueDate.ToString("dd/MM/yyyy"), candidate.Type };
        for (var index = 0; index < values.Length; index++)
        {
            var cell = new Border { BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TableBorderBrush"],
                BorderThickness = new Thickness(0, 0, index == values.Length - 1 ? 0 : 1, 1),
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock { Text = values[index], VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis } };
            Grid.SetColumn(cell, index + 1); grid.Children.Add(cell);
        }
        return new ListViewItem { Content = grid, Tag = candidate, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0) };
    }

    private void RecipientCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingSelection || sender is not AllInsurance.Desktop.Controls.VisibleSelectionCheckBox
            { Tag: Guid id } box) return;
        if (box.IsChecked == true) _selected.Add(id); else _selected.Remove(id);
        _syncingSelection = true;
        SelectAll.IsChecked = _shown.Length > 0 && _shown.All(item => _selected.Contains(item.Record.Id));
        _syncingSelection = false;
        UpdateStatus();
    }

    private void SelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingSelection || RecipientsList is null) return;
        if (SelectAll.IsChecked == true)
            foreach (var item in _shown) _selected.Add(item.Record.Id);
        else foreach (var item in _shown) _selected.Remove(item.Record.Id);
        ApplyFilters();
    }

    private void UpdateStatus()
    {
        if (RecipientsText is null || ComposeStatus is null) return;
        RecipientsText.Text = $"{_shown.Length} αποτελέσματα · {_shown.Count(item => _selected.Contains(item.Record.Id))} επιλεγμένοι";
        var channel = ChannelChoice.SelectedIndex == 1 ? "email" : "SMS";
        var missing = _shown.Count(item => _selected.Contains(item.Record.Id) &&
            string.IsNullOrWhiteSpace(channel == "email" ? item.Email : item.Phone));
        ComposeStatus.Text = $"Κανάλι: {channel.ToUpperInvariant()} · {missing} επιλεγμένοι χωρίς στοιχεία επικοινωνίας";
    }

    private void SmsBody_Changed(object sender, TextChangedEventArgs e)
    {
        if (ComposeStatus is null) return;
        var length = SmsBody.Text.Length;
        var segments = length <= 70 ? 1 : (int)Math.Ceiling(length / 67d);
        ComposeStatus.Text = $"SMS: {length} χαρακτήρες · περίπου {segments} τμήμα(τα)";
    }

    private void EditorHeight_Changed(object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (EmailBody is not null) EmailBody.Height = e.NewValue;
    }

    private void Bold_Click(object sender, RoutedEventArgs e) => ToggleFormat("bold");
    private void Italic_Click(object sender, RoutedEventArgs e) => ToggleFormat("italic");
    private void Underline_Click(object sender, RoutedEventArgs e) => ToggleFormat("underline");
    private void Font_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EmailBody is null || FontChoice.SelectedItem is not ComboBoxItem { Content: string name }) return;
        EmailBody.Document.Selection.CharacterFormat.Name = name;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private void FontSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EmailBody is null || FontSizeChoice.SelectedItem is not ComboBoxItem item ||
            !float.TryParse(item.Content?.ToString(), CultureInfo.InvariantCulture, out var size)) return;
        EmailBody.Document.Selection.CharacterFormat.Size = size;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private void TextColor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (EmailBody is null || TextColorChoice.SelectedItem is not ComboBoxItem item) return;
        var code = item.Tag?.ToString();
        Windows.UI.Color color;
        if (code == "default")
            color = EmailBody.ActualTheme == ElementTheme.Dark
                ? Microsoft.UI.Colors.White : Windows.UI.Color.FromArgb(255, 32, 38, 45);
        else if (code is { Length: 7 } && code.StartsWith('#') &&
            byte.TryParse(code.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) &&
            byte.TryParse(code.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) &&
            byte.TryParse(code.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            color = Windows.UI.Color.FromArgb(255, red, green, blue);
        else return;
        EmailBody.Document.Selection.CharacterFormat.ForegroundColor = color;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e)
    {
        EmailBody.Document.Selection.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private void AlignCenter_Click(object sender, RoutedEventArgs e)
    {
        EmailBody.Document.Selection.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private void Bullet_Click(object sender, RoutedEventArgs e)
    {
        EmailBody.Document.Selection.SetText(TextSetOptions.None, "• ");
        EmailBody.Focus(FocusState.Programmatic);
    }
    private void ToggleFormat(string kind)
    {
        var format = EmailBody.Document.Selection.CharacterFormat;
        if (kind == "bold") format.Bold = format.Bold.Equals(FormatEffect.On) ? FormatEffect.Off : FormatEffect.On;
        if (kind == "italic") format.Italic = format.Italic.Equals(FormatEffect.On) ? FormatEffect.Off : FormatEffect.On;
        if (kind == "underline") format.Underline = format.Underline.Equals(UnderlineType.Single) ? UnderlineType.None : UnderlineType.Single;
        EmailBody.Focus(FocusState.Programmatic);
    }

    private async void Credits_Click(object sender, RoutedEventArgs e) => await DialogButtonStyles.ShowAsync(
        new ContentDialog { XamlRoot = XamlRoot, Title = "SMS credits",
            Content = "Η προβολή και αγορά credits θα ενεργοποιηθεί όταν προστεθεί η υπηρεσία λογαριασμού Bulker. Η ρύθμιση αποστολής SMS είναι ήδη διαθέσιμη στις Ρυθμίσεις αποστολής.", CloseButtonText = "Κλείσιμο" });

    private sealed record MessageTemplate(string Name, string Channel, string Subject,
        string Body, string RichBody);

    private void ReadLocalData()
    {
        try
        {
            if (File.Exists(TemplatesFile))
                _templates.AddRange(JsonSerializer.Deserialize<List<MessageTemplate>>(
                    File.ReadAllText(TemplatesFile)) ?? []);
            if (File.Exists(SentFile))
                foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, DateTime>>(
                    File.ReadAllText(SentFile)) ?? []) _sent[pair.Key] = pair.Value;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        { ComposeStatus.Text = "Δεν διαβάστηκαν τα τοπικά πρότυπα/ιστορικό: " + exception.Message; }
    }

    private void RefreshTemplates()
    {
        if (TemplateChoice is null || ChannelChoice is null) return;
        var channel = ChannelChoice.SelectedIndex == 1 ? "email" : "sms";
        TemplateChoice.ItemsSource = new[] { "Χωρίς πρότυπο" }
            .Concat(_templates.Where(item => item.Channel == channel)
                .OrderBy(item => item.Name).Select(item => item.Name)).ToArray();
        TemplateChoice.SelectedIndex = 0;
    }

    private void Template_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateChoice is null || TemplateChoice.SelectedIndex <= 0 || SmsBody is null) return;
        var channel = ChannelChoice.SelectedIndex == 1 ? "email" : "sms";
        var template = _templates.FirstOrDefault(item => item.Channel == channel &&
            item.Name == TemplateChoice.SelectedItem?.ToString());
        if (template is null) return;
        SubjectInput.Text = template.Subject;
        if (channel == "sms") SmsBody.Text = template.Body;
        else EmailBody.Document.SetText(TextSetOptions.FormatRtf,
            string.IsNullOrWhiteSpace(template.RichBody) ? template.Body : template.RichBody);
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Header = "Όνομα προτύπου *", MaxLength = 80 };
        var result = await DialogButtonStyles.ShowAsync(new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Αποθήκευση προτύπου",
            Content = name, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση"
        });
        if (result != ContentDialogResult.Primary) return;
        var title = name.Text.Trim();
        if (title.Length == 0) { await UiAlerts.ShowErrorAsync(XamlRoot, "Δώστε όνομα προτύπου."); return; }
        var channel = ChannelChoice.SelectedIndex == 1 ? "email" : "sms";
        EmailBody.Document.GetText(TextGetOptions.FormatRtf, out var rtf);
        _templates.RemoveAll(item => item.Channel == channel &&
            string.Equals(item.Name, title, StringComparison.CurrentCultureIgnoreCase));
        _templates.Add(new MessageTemplate(title, channel, SubjectInput.Text, BodyText(), rtf));
        try
        {
            Directory.CreateDirectory(CommunicationFolder);
            await File.WriteAllTextAsync(TemplatesFile, JsonSerializer.Serialize(_templates));
            RefreshTemplates();
            TemplateChoice.SelectedItem = title;
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateChoice.SelectedIndex <= 0) return;
        var channel = ChannelChoice.SelectedIndex == 1 ? "email" : "sms";
        var title = TemplateChoice.SelectedItem?.ToString();
        _templates.RemoveAll(item => item.Channel == channel && item.Name == title);
        try
        {
            Directory.CreateDirectory(CommunicationFolder);
            await File.WriteAllTextAsync(TemplatesFile, JsonSerializer.Serialize(_templates));
            RefreshTemplates();
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Recipient_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null && current is not ListViewItem)
            current = VisualTreeHelper.GetParent(current);
        if (current is not ListViewItem { Tag: Candidate recipient }) return;
        var panel = new StackPanel { Width = 390, Spacing = 7 };
        foreach (var (label, value) in new[]
        {
            ("Πελάτης", recipient.CustomerName), ("Email", recipient.Email),
            ("Τηλέφωνο", recipient.Phone), ("Ασφαλιστήριο", recipient.Number),
            ("Ασφαλιστική", recipient.Company), ("Λήξη", recipient.DueDate.ToString("dd/MM/yyyy")),
            ("Είδος", recipient.Type)
        })
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 5 };
            row.Children.Add(new TextBox { Header = label, Text = value, IsReadOnly = true });
            var copy = new Button { Content = "Αντιγραφή", VerticalAlignment = VerticalAlignment.Bottom };
            copy.Click += (_, _) => { var data = new DataPackage(); data.SetText(value);
                Clipboard.SetContent(data); };
            Grid.SetColumn(copy, 1); row.Children.Add(copy);
            panel.Children.Add(row);
        }
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Στοιχεία παραλήπτη", Content = panel, CloseButtonText = "Κλείσιμο" });
        e.Handled = true;
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Μόνο διαχειριστής μπορεί να αλλάξει τις ρυθμίσεις αποστολής."); return; }
        var channel = await DialogButtonStyles.ShowAsync(new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Ρυθμίσεις αποστολής",
            Content = "Επιλέξτε ξεχωριστά τις ρυθμίσεις για το κανάλι αποστολής.",
            PrimaryButtonText = "Email · Brevo", SecondaryButtonText = "SMS · Bulker",
            CloseButtonText = "Κλείσιμο"
        });
        if (channel == ContentDialogResult.None) return;
        var emailChannel = channel == ContentDialogResult.Primary;
        var settings = _gateway.ReadSettings();
        var email = new TextBox { Header = "Email αποστολέα Brevo", Text = settings.EmailSender };
        var name = new TextBox { Header = "Όνομα αποστολέα", Text = settings.EmailSenderName };
        var sms = new TextBox { Header = "Αποστολέας Bulker SMS", Text = settings.SmsSender };
        var brevo = new PasswordBox { Header = "Νέο API key Brevo", PlaceholderText = "Κενό = διατήρηση υπάρχοντος" };
        var bulker = new PasswordBox { Header = "Νέο auth key Bulker", PlaceholderText = "Κενό = διατήρηση υπάρχοντος" };
        var content = new StackPanel { Spacing = 8 };
        if (emailChannel)
        { content.Children.Add(email); content.Children.Add(name); content.Children.Add(brevo); }
        else { content.Children.Add(sms); content.Children.Add(bulker); }
        content.Children.Add(new TextBlock { Text = "Τα κλειδιά φυλάσσονται στον Windows Credential Manager για τον τρέχοντα χρήστη.",
            TextWrapping = TextWrapping.Wrap });
        var result = await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = emailChannel ? "Ρυθμίσεις email · Brevo" : "Ρυθμίσεις SMS · Bulker",
            Content = content, PrimaryButtonText = "Αποθήκευση",
            CloseButtonText = "Άκυρο" });
        if (result != ContentDialogResult.Primary) return;
        try
        {
            _gateway.SaveSettings(new CommunicationGateway.Settings
            {
                EmailSender = emailChannel ? email.Text.Trim() : settings.EmailSender,
                EmailSenderName = emailChannel ? name.Text.Trim() : settings.EmailSenderName,
                SmsSender = emailChannel ? settings.SmsSender : sms.Text.Trim()
            }, emailChannel ? brevo.Password : "", emailChannel ? "" : bulker.Password);
            ComposeStatus.Text = "Οι ρυθμίσεις αποστολής αποθηκεύτηκαν.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void OpenPolicy_Click(object sender, RoutedEventArgs e)
    {
        if (RecipientsList.SelectedItem is not ListViewItem { Tag: Candidate candidate })
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε ασφαλιστήριο από τον πίνακα."); return; }
        if (_policyCard is not null) { _policyCard.BringToFront(); return; }
        _policyCard = new BusinessRecordCardWindow("policies", recordId: candidate.Record.Id);
        _policyCard.Closed += (_, _) => { _policyCard = null; _ = LoadAsync(); };
        _policyCard.BringToFront();
    }

    private string BodyText()
    {
        if (ChannelChoice.SelectedIndex != 1) return SmsBody.Text;
        EmailBody.Document.GetText(TextGetOptions.None, out var value);
        return value.Trim();
    }

    private string EmailHtml(Candidate recipient)
    {
        EmailBody.Document.GetText(TextGetOptions.None, out var value);
        var html = new StringBuilder("<div style=\"white-space:pre-wrap\">");
        string? previousStyle = null;
        void OpenParagraph(int index)
        {
            var alignment = EmailBody.Document.GetRange(index, index + 1).ParagraphFormat.Alignment;
            var css = alignment == ParagraphAlignment.Center ? "center" :
                alignment == ParagraphAlignment.Right ? "right" : "left";
            html.Append("<p style=\"margin:0;text-align:").Append(css).Append("\">");
        }
        OpenParagraph(0);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] is '\r' or '\n')
            {
                if (previousStyle is not null) html.Append("</span>");
                previousStyle = null;
                html.Append("</p>");
                if (value[index] == '\r' && index + 1 < value.Length && value[index + 1] == '\n') index++;
                if (index + 1 < value.Length) OpenParagraph(index + 1);
                continue;
            }
            var format = EmailBody.Document.GetRange(index, index + 1).CharacterFormat;
            var color = format.ForegroundColor;
            var css = new StringBuilder();
            if (format.Bold.Equals(FormatEffect.On)) css.Append("font-weight:700;");
            if (format.Italic.Equals(FormatEffect.On)) css.Append("font-style:italic;");
            if (!format.Underline.Equals(UnderlineType.None)) css.Append("text-decoration:underline;");
            if (color.A > 0) css.Append($"color:#{color.R:X2}{color.G:X2}{color.B:X2};");
            if (format.Size > 0) css.Append("font-size:")
                .Append(format.Size.ToString("0.##", CultureInfo.InvariantCulture)).Append("pt;");
            if (!string.IsNullOrWhiteSpace(format.Name)) css.Append("font-family:'")
                .Append(WebUtility.HtmlEncode(format.Name.Replace("'", ""))).Append("';");
            var style = css.ToString();
            if (style != previousStyle)
            {
                if (previousStyle is not null) html.Append("</span>");
                html.Append("<span style=\"").Append(style).Append("\">");
                previousStyle = style;
            }
            string rendered;
            var remaining = value.AsSpan(index);
            if (remaining.StartsWith("{όνομα}".AsSpan(), StringComparison.CurrentCultureIgnoreCase))
            { rendered = recipient.CustomerName; index += "{όνομα}".Length - 1; }
            else if (remaining.StartsWith("{αριθμός}".AsSpan(), StringComparison.CurrentCultureIgnoreCase))
            { rendered = recipient.Number; index += "{αριθμός}".Length - 1; }
            else if (remaining.StartsWith("{λήξη}".AsSpan(), StringComparison.CurrentCultureIgnoreCase))
            { rendered = recipient.DueDate.ToString("dd/MM/yyyy"); index += "{λήξη}".Length - 1; }
            else rendered = value[index].ToString();
            html.Append(WebUtility.HtmlEncode(rendered));
        }
        if (previousStyle is not null) html.Append("</span>");
        if (value.Length == 0 || value[^1] is not ('\r' or '\n')) html.Append("</p>");
        html.Append("</div>");
        return html.ToString();
    }

    private string Personalize(string value, Candidate candidate) => value
        .Replace("{όνομα}", candidate.CustomerName, StringComparison.CurrentCultureIgnoreCase)
        .Replace("{αριθμός}", candidate.Number, StringComparison.CurrentCultureIgnoreCase)
        .Replace("{λήξη}", candidate.DueDate.ToString("dd/MM/yyyy"), StringComparison.CurrentCultureIgnoreCase);

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var selected = _shown.FirstOrDefault(item => _selected.Contains(item.Record.Id)) ?? _shown.FirstOrDefault();
        if (selected is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν υπάρχουν παραλήπτες για προεπισκόπηση."); return; }
        UIElement preview;
        if (ChannelChoice.SelectedIndex == 1)
        {
            EmailBody.Document.GetText(TextGetOptions.FormatRtf, out var rtf);
            var rich = new RichEditBox { IsReadOnly = true, MinHeight = 280, Width = 470 };
            rich.Document.SetText(TextSetOptions.FormatRtf, rtf);
            preview = new StackPanel { Spacing = 8, Children =
            {
                new TextBlock { Text = $"Προς: {selected.CustomerName} <{selected.Email}>" },
                new TextBlock { Text = "Θέμα: " + Personalize(SubjectInput.Text, selected),
                    FontWeight = FontWeights.SemiBold },
                rich,
                new TextBlock { Text = "Οι μεταβλητές {όνομα}, {αριθμός}, {λήξη} συμπληρώνονται κατά την αποστολή.",
                    TextWrapping = TextWrapping.Wrap }
            } };
        }
        else preview = new TextBlock { Text = Personalize(SmsBody.Text, selected),
            TextWrapping = TextWrapping.Wrap, Width = 420 };
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = $"Προεπισκόπηση για {selected.CustomerName}",
            Content = new ScrollViewer { Content = preview, MaxHeight = 450 },
            CloseButtonText = "Κλείσιμο" });
    }

    private async void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AllInsurance", "Drafts");
            Directory.CreateDirectory(folder);
            EmailBody.Document.GetText(TextGetOptions.FormatRtf, out var rtf);
            var draft = new { Channel = ChannelChoice.SelectedIndex == 1 ? "email" : "sms",
                Subject = SubjectInput.Text, Body = BodyText(), RichBody = rtf,
                PolicyIds = _selected.ToArray(), SavedAtUtc = DateTime.UtcNow };
            await File.WriteAllTextAsync(Path.Combine(folder, "communication.json"), JsonSerializer.Serialize(draft));
            ComposeStatus.Text = "Το πρόχειρο αποθηκεύτηκε τοπικά.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(XamlRoot, exception.Message); }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var email = ChannelChoice.SelectedIndex == 1;
        var recipients = _shown.Where(item => _selected.Contains(item.Record.Id)).ToArray();
        if (recipients.Length == 0) { await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε παραλήπτες από τη λίστα."); return; }
        if (!_gateway.IsConfigured(email))
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Συμπληρώστε πρώτα τις ρυθμίσεις αποστολής Brevo ή Bulker SMS."); return; }
        if (string.IsNullOrWhiteSpace(BodyText()) || email && string.IsNullOrWhiteSpace(SubjectInput.Text))
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Συμπληρώστε θέμα και κείμενο μηνύματος."); return; }
        var missing = recipients.Count(item => string.IsNullOrWhiteSpace(email ? item.Email : item.Phone));
        if (missing > 0) { await UiAlerts.ShowErrorAsync(XamlRoot, $"{missing} παραλήπτες δεν έχουν {(email ? "email" : "κινητό")}. Διορθώστε τις καρτέλες τους ή αποεπιλέξτε τους."); return; }
        var confirmation = await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Επιβεβαίωση αποστολής", Content = $"Θα σταλούν {recipients.Length} { (email ? "email μέσω Brevo" : "SMS μέσω Bulker") } σε πραγματικούς παραλήπτες. Συνεχίζετε;",
            PrimaryButtonText = "Αποστολή", CloseButtonText = "Άκυρο" });
        if (confirmation != ContentDialogResult.Primary) return;
        var success = 0;
        var errors = new List<string>();
        SendButton.IsEnabled = false;
        try
        {
            foreach (var recipient in recipients)
            {
                try
                {
                    var body = Personalize(BodyText(), recipient);
                    if (email)
                    {
                        var html = EmailHtml(recipient);
                        await _gateway.SendEmailAsync(recipient.Email, recipient.CustomerName,
                            Personalize(SubjectInput.Text, recipient), html);
                    }
                    else await _gateway.SendSmsAsync(recipient.Phone, body);
                    _sent[$"{recipient.Record.Id}:{(email ? "email" : "sms")}"] = DateTime.UtcNow;
                    success++;
                }
                catch (Exception exception) { errors.Add($"{recipient.CustomerName}: {exception.Message}"); }
                ComposeStatus.Text = $"Αποστολή: {success + errors.Count}/{recipients.Length}";
            }
        }
        finally
        {
            SendButton.IsEnabled = true;
            try
            {
                Directory.CreateDirectory(CommunicationFolder);
                await File.WriteAllTextAsync(SentFile, JsonSerializer.Serialize(_sent));
            }
            catch (Exception exception) { errors.Add("Ιστορικό αποστολών: " + exception.Message); }
            ApplyFilters();
        }
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = "Αποτέλεσμα αποστολής",
            Content = $"Επιτυχείς αιτήσεις: {success}\nΑποτυχίες: {errors.Count}" +
                (errors.Count > 0 ? "\n\n" + string.Join("\n", errors.Take(5)) : ""), CloseButtonText = "Κλείσιμο" });
    }

    private sealed class Candidate(BusinessRecord record, PolicyCardData data)
    {
        public BusinessRecord Record { get; } = record;
        public string CustomerName { get; } = record.Customer is { } customer
            ? $"{customer.LastName} {customer.FirstName}" : "—";
        public string Email { get; } = record.Customer?.PrimaryEmail ?? "—";
        public string Phone { get; } = record.Customer?.PrimaryMobile ?? "—";
        public string Number { get; } = data.PolicyNumber ?? record.Reference ?? record.Title;
        public string Company { get; } = data.Insurer ?? "—";
        public string Branch { get; } = data.Branch ?? "—";
        public string Type { get; } = record.DocumentType ?? "Ασφαλιστήριο";
        public DateTime DueDate { get; } = data.EndDate ?? record.EventDate ?? record.CreatedAtUtc;
    }
}

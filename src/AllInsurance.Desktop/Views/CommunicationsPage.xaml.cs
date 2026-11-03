using AllInsurance.Application.Business;
using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text.Json;
using System.Net;
using System.Text;
using Microsoft.UI.Text;

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

    public CommunicationsPage()
    {
        InitializeComponent();
        ChannelChoice.SelectedIndex = 0;
        DocumentFilter.SelectedIndex = 0;
        FromDate.Date = new DateTimeOffset(DateTime.Today);
        UntilDate.Date = new DateTimeOffset(DateTime.Today.AddDays(30));
        SmsBody.Text = "Αγαπητέ/ή {όνομα}, το ασφαλιστήριό σας {αριθμός} λήγει στις {λήξη}. Επικοινωνήστε μαζί μας για την ανανέωσή του.";
        EmailBody.Document.SetText(TextSetOptions.None,
            "Αγαπητέ/ή {όνομα},\n\nΤο ασφαλιστήριό σας {αριθμός} λήγει στις {λήξη}. Επικοινωνήστε μαζί μας για την ανανέωσή του.\n\nAllInsurance Backoffice");
        SubjectInput.Text = "Υπενθύμιση ανανέωσης ασφαλιστηρίου";
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
        EmailBody.Visibility = email ? Visibility.Visible : Visibility.Collapsed;
        SmsBody.Visibility = email ? Visibility.Collapsed : Visibility.Visible;
        _selected.Clear();
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
            .Where(item => term.Length == 0 || new[] { item.CustomerName, item.Number, item.Company, item.Branch }
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
            new ColumnDefinition { Width = new GridLength(150) },
            new ColumnDefinition { Width = new GridLength(165) },
            new ColumnDefinition { Width = new GridLength(120) },
            new ColumnDefinition { Width = new GridLength(130) },
            new ColumnDefinition { Width = new GridLength(110) }
        } };
        var box = new CheckBox { Tag = candidate.Record.Id, IsChecked = _selected.Contains(candidate.Record.Id),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        box.Checked += RecipientCheckChanged;
        box.Unchecked += RecipientCheckChanged;
        grid.Children.Add(box);
        var values = new[] { candidate.CustomerName, ChannelChoice.SelectedIndex == 1 ? candidate.Email : candidate.Phone,
            candidate.Number, candidate.Company, candidate.DueDate.ToString("dd/MM/yyyy"), candidate.Type };
        for (var index = 0; index < values.Length; index++)
        {
            var label = new TextBlock { Text = values[index], Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(label, index + 1); grid.Children.Add(label);
        }
        return new ListViewItem { Content = grid, Tag = candidate, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0) };
    }

    private void RecipientCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingSelection || sender is not CheckBox { Tag: Guid id } box) return;
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

    private void Bold_Click(object sender, RoutedEventArgs e) => ToggleFormat("bold");
    private void Italic_Click(object sender, RoutedEventArgs e) => ToggleFormat("italic");
    private void Underline_Click(object sender, RoutedEventArgs e) => ToggleFormat("underline");
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

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<ISessionService>();
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
        { await UiAlerts.ShowErrorAsync(XamlRoot, "Μόνο διαχειριστής μπορεί να αλλάξει τις ρυθμίσεις αποστολής."); return; }
        var settings = _gateway.ReadSettings();
        var email = new TextBox { Header = "Email αποστολέα Brevo", Text = settings.EmailSender };
        var name = new TextBox { Header = "Όνομα αποστολέα", Text = settings.EmailSenderName };
        var sms = new TextBox { Header = "Αποστολέας Bulker SMS", Text = settings.SmsSender };
        var brevo = new PasswordBox { Header = "Νέο API key Brevo", PlaceholderText = "Κενό = διατήρηση υπάρχοντος" };
        var bulker = new PasswordBox { Header = "Νέο auth key Bulker", PlaceholderText = "Κενό = διατήρηση υπάρχοντος" };
        var content = new StackPanel { Spacing = 8, Children = { email, name, sms, brevo, bulker,
            new TextBlock { Text = "Τα κλειδιά φυλάσσονται στον Windows Credential Manager για τον τρέχοντα χρήστη.", TextWrapping = TextWrapping.Wrap } } };
        var result = await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = "Ρυθμίσεις αποστολής", Content = content, PrimaryButtonText = "Αποθήκευση",
            CloseButtonText = "Άκυρο" });
        if (result != ContentDialogResult.Primary) return;
        try
        {
            _gateway.SaveSettings(new CommunicationGateway.Settings { EmailSender = email.Text.Trim(),
                EmailSenderName = name.Text.Trim(), SmsSender = sms.Text.Trim() }, brevo.Password, bulker.Password);
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
        var previous = (Bold: false, Italic: false, Underline: false);
        for (var index = 0; index < value.Length; index++)
        {
            var format = EmailBody.Document.GetRange(index, index + 1).CharacterFormat;
            var current = (Bold: format.Bold.Equals(FormatEffect.On),
                Italic: format.Italic.Equals(FormatEffect.On),
                Underline: !format.Underline.Equals(UnderlineType.None));
            if (current != previous)
            {
                if (previous.Underline) html.Append("</u>");
                if (previous.Italic) html.Append("</i>");
                if (previous.Bold) html.Append("</b>");
                if (current.Bold) html.Append("<b>");
                if (current.Italic) html.Append("<i>");
                if (current.Underline) html.Append("<u>");
                previous = current;
            }
            html.Append(WebUtility.HtmlEncode(value[index].ToString()));
        }
        if (previous.Underline) html.Append("</u>");
        if (previous.Italic) html.Append("</i>");
        if (previous.Bold) html.Append("</b>");
        html.Append("</div>");
        return html.ToString()
            .Replace("{όνομα}", WebUtility.HtmlEncode(recipient.CustomerName), StringComparison.CurrentCultureIgnoreCase)
            .Replace("{αριθμός}", WebUtility.HtmlEncode(recipient.Number), StringComparison.CurrentCultureIgnoreCase)
            .Replace("{λήξη}", recipient.DueDate.ToString("dd/MM/yyyy"), StringComparison.CurrentCultureIgnoreCase);
    }

    private string Personalize(string value, Candidate candidate) => value
        .Replace("{όνομα}", candidate.CustomerName, StringComparison.CurrentCultureIgnoreCase)
        .Replace("{αριθμός}", candidate.Number, StringComparison.CurrentCultureIgnoreCase)
        .Replace("{λήξη}", candidate.DueDate.ToString("dd/MM/yyyy"), StringComparison.CurrentCultureIgnoreCase);

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var selected = _shown.FirstOrDefault(item => _selected.Contains(item.Record.Id)) ?? _shown.FirstOrDefault();
        if (selected is null) { await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν υπάρχουν παραλήπτες για προεπισκόπηση."); return; }
        await DialogButtonStyles.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = $"Προεπισκόπηση για {selected.CustomerName}",
            Content = new ScrollViewer { Content = new TextBlock { Text =
                (ChannelChoice.SelectedIndex == 1 ? $"Θέμα: {SubjectInput.Text}\n\n" : "") +
                Personalize(BodyText(), selected), TextWrapping = TextWrapping.Wrap }, MaxHeight = 450 },
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
                    success++;
                }
                catch (Exception exception) { errors.Add($"{recipient.CustomerName}: {exception.Message}"); }
                ComposeStatus.Text = $"Αποστολή: {success + errors.Count}/{recipients.Length}";
            }
        }
        finally { SendButton.IsEnabled = true; }
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

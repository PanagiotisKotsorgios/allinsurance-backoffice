using AllInsurance.Application.Settings;
using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow
{
    private BusinessRecordCardWindow? _policyVehicleCard;
    private BusinessRecordCardWindow? _policyRenewalCard;
    private ListView? _policyAttachmentList;
    private TablePagination? _policyAttachmentPager;
    private ListView? _policyHistoryList;

    private void BuildPolicyFinanceTab(PolicyCardData data)
    {
        var panel = PolicyPanel();
        _policyFinanceSummary = new TextBlock { FontSize = 16, TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _policyCommissionSummary = new TextBlock { FontSize = 15, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(_policyFinanceSummary);
        panel.Children.Add(_policyCommissionSummary);
        _payInInstallments = new CheckBox { IsChecked = data.PayInInstallments,
            MinHeight = 32 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_payInInstallments,
            "Πληρωμή με δόσεις");
        _payInInstallmentsBadge = new Border { Width = 22, Height = 22,
            CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center } };
        var installmentsHost = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        installmentsHost.Children.Add(_payInInstallments);
        installmentsHost.Children.Add(_payInInstallmentsBadge);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { new TextBlock { Text = "Πληρωμή με δόσεις",
                VerticalAlignment = VerticalAlignment.Center }, installmentsHost } });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _policyReceiptButton = new ActionButton { Kind = "Receipt", Label = "Είσπραξη για ασφαλιστήριο" };
        _policyPaymentButton = new ActionButton { Kind = "Payment", Label = "Πληρωμή προς πελάτη" };
        _policyReceiptButton.Click += async (_, _) => await AddPolicyTransactionAsync("receipt");
        _policyPaymentButton.Click += async (_, _) => await AddPolicyTransactionAsync("payment");
        actions.Children.Add(_policyReceiptButton); actions.Children.Add(_policyPaymentButton);
        panel.Children.Add(actions);
        UpdatePolicyTransactionButtons();
        panel.Children.Add(new TextBlock { Text = "Συνδεδεμένες οικονομικές κινήσεις",
            FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _policyMovementsList = new ListView { Height = 210,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) };
        TableRowStriping.Attach(_policyMovementsList);
        panel.Children.Add(_policyMovementsList);
        _policyMovementsPager = new TablePagination(_policyMovementsList);
        panel.Children.Add(_policyMovementsPager.Bar);
        RefreshPolicyMovementRows();
        panel.Children.Add(BuildPolicyInstallmentsEditor());
        UpdatePolicyFinanceSummary(data);
        AddTab("Οικονομικά", panel);
    }

    private void UpdatePolicyTransactionButtons()
    {
        var enabled = _record.Id != Guid.Empty && _record.DocumentType is null or "Ασφαλιστήριο";
        if (_policyReceiptButton is not null) _policyReceiptButton.IsEnabled = enabled;
        if (_policyPaymentButton is not null) _policyPaymentButton.IsEnabled = enabled;
    }

    private void RefreshPolicyMovementRows()
    {
        if (_policyMovementsList is null || _policyMovementsPager is null) return;
        _policyMovementsList.Items.Clear();
        foreach (var item in _policyTransactions.OrderByDescending(value => value.OccurredAtUtc))
        {
            var label = item.Type == "receipt" ? "Είσπραξη" : "Πληρωμή προς πελάτη";
            _policyMovementsList.Items.Add(new TextBlock { Text =
                $"{item.OccurredAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {label}  ·  " +
                $"{item.Amount:N2} {TransactionSettingsService.Symbol(item.Currency)}  ·  " +
                $"{item.PaymentMethod}  ·  {item.Reference ?? "—"}",
                Margin = new Thickness(9, 7, 9, 7), TextWrapping = TextWrapping.Wrap });
        }
        _policyMovementsPager.Capture();
    }

    private async Task AddPolicyTransactionAsync(string type)
    {
        if (_record.Id == Guid.Empty || _record.CustomerId is not Guid customerId ||
            _record.DocumentType is not (null or "Ασφαλιστήριο"))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα ένα εκδομένο ασφαλιστήριο."); return; }
        try
        {
            _policyCurrency = await _policySettings.GetCurrencyAsync();
            var methods = await _policyFilters.ChoicesAsync("Τρόποι πληρωμής");
            var amount = new TextBox { Header = RequiredHeader.Create("Ποσό"),
                PlaceholderText = "π.χ. 125,50" };
            var method = new ComboBox { Header = RequiredHeader.Create("Τρόπος πληρωμής"),
                ItemsSource = methods, SelectedItem = methods.FirstOrDefault(value => value == "Μετρητά")
                    ?? methods.FirstOrDefault() };
            var currency = new TextBox { Header = "Νόμισμα", IsReadOnly = true,
                Text = $"{_policyCurrency} ({TransactionSettingsService.Symbol(_policyCurrency)})" };
            var reference = new TextBox { Header = "Αριθμός συναλλαγής / αναφορά" };
            var description = new TextBox { Header = "Αιτιολογία", AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, Height = 76,
                Text = $"{(type == "receipt" ? "Είσπραξη" : "Πληρωμή")} για {_record.Title}" };
            if (!await ShowPolicyEditorAsync(type == "receipt" ? "Είσπραξη ασφαλιστηρίου" :
                "Πληρωμή προς πελάτη", amount, method, currency, reference, description)) return;
            var value = DecimalValue(amount.Text, "Ποσό");
            if (value is null or <= 0 || method.SelectedItem is not string selectedMethod)
                throw new ArgumentException("Συμπληρώστε θετικό ποσό και τρόπο πληρωμής.");
            await _customers.AddTransactionAsync(customerId, type, value.Value,
                description.Text.Trim(), selectedMethod, reference.Text.Trim(), _record.Id);
            var customer = await _customers.GetAsync(customerId);
            _policyTransactions = customer?.Transactions.Where(item => item.BusinessRecordId == _record.Id)
                .ToArray() ?? [];
            RefreshPolicyMovementRows();
            UpdatePolicyFinanceSummary(_policyData);
            _status.Text = "Η οικονομική κίνηση καταχωρίστηκε.";
            RecordSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private void BuildPolicyDocumentsTab()
    {
        var panel = PolicyPanel();
        panel.Children.Add(BuildPolicyEndorsementsEditor());
        panel.Children.Add(BuildPolicyGreenCardsEditor());
        panel.Children.Add(new TextBlock { Text = "PDF ασφαλιστηρίου και λοιπά αρχεία",
            FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var attach = new ActionButton { Kind = "Upload", Label = "Επισύναψη" };
        var open = new ActionButton { Kind = "Open", Label = "Άνοιγμα" };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        attach.Click += async (_, _) => await AttachPolicyFileAsync();
        open.Click += async (_, _) => await OpenPolicyFileAsync();
        delete.Click += async (_, _) => await DeletePolicyFileAsync();
        actions.Children.Add(attach); actions.Children.Add(open); actions.Children.Add(delete);
        panel.Children.Add(actions);
        _policyAttachmentList = new ListView { Height = 160,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) };
        TableRowStriping.Attach(_policyAttachmentList);
        _policyAttachmentList.DoubleTapped += async (_, _) => await OpenPolicyFileAsync();
        panel.Children.Add(_policyAttachmentList);
        _policyAttachmentPager = new TablePagination(_policyAttachmentList);
        panel.Children.Add(_policyAttachmentPager.Bar);
        AddTab("Έγγραφα & πράξεις", panel);
        _ = RefreshPolicyAttachmentsAsync();
    }

    private async Task RefreshPolicyAttachmentsAsync()
    {
        if (_policyAttachmentList is null || _policyAttachmentPager is null) return;
        try
        {
            var files = _record.Id == Guid.Empty ? [] : await _records.GetAttachmentsAsync(_record.Id);
            _policyAttachmentList.Items.Clear();
            foreach (var file in files)
                _policyAttachmentList.Items.Add(new ListViewItem { Tag = file,
                    Content = $"{file.FileName}  ·  {file.UploadedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {file.UploadedBy}" });
            _policyAttachmentPager.Capture();
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task AttachPolicyFileAsync()
    {
        if (_record.Id == Guid.Empty)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα την καρτέλα."); return; }
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var extension in new[] { ".pdf", ".png", ".jpg", ".jpeg", ".docx" })
                picker.FileTypeFilter.Add(extension);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            await _records.AddAttachmentAsync(_record.Id, file.Path);
            await RefreshPolicyAttachmentsAsync();
            _status.Text = "Το αρχείο επισυνάφθηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private BusinessRecordAttachment? SelectedPolicyFile() =>
        (_policyAttachmentList?.SelectedItem as ListViewItem)?.Tag as BusinessRecordAttachment;

    private async Task OpenPolicyFileAsync()
    {
        if (SelectedPolicyFile() is not BusinessRecordAttachment file)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε αρχείο."); return; }
        try
        {
            var stored = await StorageFile.GetFileFromPathAsync(_records.GetAttachmentPath(file));
            await Windows.System.Launcher.LaunchFileAsync(stored);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task DeletePolicyFileAsync()
    {
        if (SelectedPolicyFile() is not BusinessRecordAttachment file)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε αρχείο."); return; }
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Διαγραφή αρχείου",
            Content = $"Να διαγραφεί το {file.FileName};",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try { await _records.DeleteAttachmentAsync(_record.Id, file.Id);
            await RefreshPolicyAttachmentsAsync(); _status.Text = "Το αρχείο διαγράφηκε."; }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private void BuildPolicyClaimsTab()
    {
        var panel = PolicyPanel();
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη ζημιάς",
            HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += async (_, _) => await OpenPolicyClaimAsync(null);
        panel.Children.Add(add);
        _policyClaimsList = new ListView { Height = 320 };
        TableRowStriping.Attach(_policyClaimsList);
        panel.Children.Add(_policyClaimsList);
        var pager = new TablePagination(_policyClaimsList);
        _policyClaimsPager = pager;
        panel.Children.Add(pager.Bar);
        _policyClaimsList.DoubleTapped += async (_, _) =>
        {
            if ((_policyClaimsList.SelectedItem as ListViewItem)?.Tag is Guid id)
                await OpenPolicyClaimAsync(id);
        };
        AddTab("Ζημιές", panel);
        _ = RefreshPolicyClaimsAsync(pager);
    }

    private async Task RefreshPolicyClaimsAsync(TablePagination pager)
    {
        if (_policyClaimsList is null) return;
        try
        {
            var claims = _record.Id == Guid.Empty ? [] :
                (await _records.SearchAsync("claims")).Where(item =>
                    WorkspaceCardData.Read(item.DetailsJson).PolicyId == _record.Id).ToArray();
            _policyClaimsList.Items.Clear();
            foreach (var claim in claims)
            {
                var link = new HyperlinkButton { Content =
                    $"{claim.Title}  ·  {claim.Status ?? "—"}  ·  {claim.EventDate?.ToString("dd/MM/yyyy") ?? "—"}" };
                link.Click += async (_, _) => await OpenPolicyClaimAsync(claim.Id);
                _policyClaimsList.Items.Add(new ListViewItem { Content = link, Tag = claim.Id });
            }
            pager.Capture();
        }
        catch (Exception exception) { _status.Text = exception.Message; }
    }

    private async Task OpenPolicyClaimAsync(Guid? id)
    {
        if (_record.Id == Guid.Empty || _record.CustomerId is not Guid customerId)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα το ασφαλιστήριο."); return; }
        if (_policyClaimCard is not null) { _policyClaimCard.BringToFront(); return; }
        _policyClaimCard = new WorkspaceRecordCardWindow("claims", id,
            customerId: customerId, vehicleId: _policyData.VehicleId, policyId: _record.Id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _policyClaimCard.RecordSaved += async (_, _) =>
        {
            if (_policyClaimsList is not null && _policyClaimsPager is not null)
                await RefreshPolicyClaimsAsync(_policyClaimsPager);
            RecordSaved?.Invoke(this, EventArgs.Empty);
        };
        _policyClaimCard.Closed += (_, _) => { _policyClaimCard = null; BringToFront(); };
        _policyClaimCard.BringToFront();
    }

    private TablePagination? _policyClaimsPager;

    private void BuildPolicyHistoryTab()
    {
        var panel = PolicyPanel();
        _historyText = new TextBlock { Text = _record.Id == Guid.Empty ?
            "Το ιστορικό θα ξεκινήσει μετά την αποθήκευση." :
            $"Δημιουργία: {_record.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  " +
            $"Τελευταία ενημέρωση: {_record.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
            FontSize = 15 };
        panel.Children.Add(_historyText);
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        if (_policyData.PreviousPolicyId is Guid previousId)
        {
            var previous = new ActionButton { Kind = "Previous", Label = "Προηγούμενο ασφαλιστήριο" };
            previous.Click += async (_, _) => await OpenRelatedPolicyAsync(previousId);
            links.Children.Add(previous);
        }
        var nextRecord = _allPolicyRecords.FirstOrDefault(item =>
            BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).PreviousPolicyId == _record.Id &&
            _record.Id != Guid.Empty);
        if (nextRecord is not null)
        {
            var next = new ActionButton { Kind = "Next", Label = "Επόμενο ασφαλιστήριο" };
            next.Click += async (_, _) => await OpenRelatedPolicyAsync(nextRecord.Id);
            links.Children.Add(next);
        }
        var renew = new ActionButton { Kind = "Add", Label = "Νέα αίτηση ανανέωσης",
            IsEnabled = _record.Id != Guid.Empty && _record.DocumentType is null or "Ασφαλιστήριο" };
        renew.Click += async (_, _) => await OpenNewRenewalAsync();
        links.Children.Add(renew);
        panel.Children.Add(links);
        _policyHistoryList = new ListView { Height = 350 };
        TableRowStriping.Attach(_policyHistoryList);
        panel.Children.Add(_policyHistoryList);
        var pager = new TablePagination(_policyHistoryList);
        panel.Children.Add(pager.Bar);
        AddTab("Ιστορικό ενεργειών", panel);
        _ = RefreshPolicyHistoryAsync(pager);
    }

    private async Task RefreshPolicyHistoryAsync(TablePagination pager)
    {
        if (_policyHistoryList is null) return;
        var activities = _record.Id == Guid.Empty ? [] : await _records.GetActivitiesAsync(_record.Id);
        _policyHistoryList.Items.Clear();
        foreach (var item in activities)
            _policyHistoryList.Items.Add(new TextBlock { Text =
                $"{item.OccurredAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {item.Action}  ·  " +
                $"{item.Description}  ·  {item.ActorName}", TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 5, 8, 5) });
        pager.Capture();
    }

    private async Task OpenRelatedPolicyAsync(Guid id)
    {
        if (_relatedPolicyCard is not null) { _relatedPolicyCard.BringToFront(); return; }
        _relatedPolicyCard = new BusinessRecordCardWindow("policies", recordId: id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _relatedPolicyCard.Closed += (_, _) => { _relatedPolicyCard = null; BringToFront(); };
        _relatedPolicyCard.BringToFront();
        await Task.CompletedTask;
    }

    private async Task OpenRelatedVehicleAsync(Guid id)
    {
        if (_policyVehicleCard is not null) { _policyVehicleCard.BringToFront(); return; }
        _policyVehicleCard = new BusinessRecordCardWindow("vehicles", recordId: id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _policyVehicleCard.Closed += (_, _) => { _policyVehicleCard = null; BringToFront(); };
        _policyVehicleCard.BringToFront();
        await Task.CompletedTask;
    }

    private async Task OpenNewRenewalAsync()
    {
        if (_record.Id == Guid.Empty || _record.CustomerId is not Guid ownerId)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα το ασφαλιστήριο."); return; }
        if (_policyRenewalCard is not null) { _policyRenewalCard.BringToFront(); return; }
        _policyRenewalCard = new BusinessRecordCardWindow("policies", customerId: ownerId,
            vehicleId: _policyData.VehicleId, insurerName: _policyData.Insurer,
            agencyName: _policyData.Agency, previousPolicyId: _record.Id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _policyRenewalCard.RecordSaved += async (_, _) =>
        { _allPolicyRecords = await _records.SearchAsync("policies"); RecordSaved?.Invoke(this, EventArgs.Empty); };
        _policyRenewalCard.Closed += (_, _) => { _policyRenewalCard = null; BringToFront(); };
        _policyRenewalCard.BringToFront();
    }
}

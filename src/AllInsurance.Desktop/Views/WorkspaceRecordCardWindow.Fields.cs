using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Globalization;

namespace AllInsurance.Desktop.Views;

public sealed partial class WorkspaceRecordCardWindow
{
    private sealed record FieldSpec(string Key, string Label, string Kind = "text",
        bool Required = false, string[]? Options = null);
    private sealed record RecordOption(Guid? Id, string Name);

    private static FieldSpec T(string key, string label, bool required = false) =>
        new(key, label, "text", required);
    private static FieldSpec N(string key, string label) => new(key, label, "number");
    private static FieldSpec M(string key, string label) => new(key, label, "memo");
    private static FieldSpec D(string key, string label) => new(key, label, "date");
    private static FieldSpec B(string key, string label) => new(key, label, "bool");
    private static FieldSpec L(string key, string label, bool required = false) =>
        new(key, label, "link", required);
    private static FieldSpec S(string key, string label, params string[] options) =>
        new(key, label, "select", false, options);

    private void BuildTabs(bool preserve = false)
    {
        var tab = preserve ? _selectedTab : 0;
        _building = true;
        _tabContent.Content = null;
        _tabStrip.Children.Clear(); _tabs.Clear(); _forms.Clear();
        _inputs.Clear(); _specs.Clear();
        switch (_section.Key)
        {
            case "companies": BuildCompanyTabs(); break;
            case "agencies": BuildAgencyTabs(); break;
            case "claims": BuildClaimTabs(); break;
            case "updates": BuildUpdateTabs(); break;
            case "reports": BuildReportTabs(); break;
            case "bridges": BuildBridgeTabs(); break;
        }
        BuildHistoryTab();
        _building = false;
        WireDependencies();
        SelectTab(Math.Clamp(tab, 0, _tabs.Count - 1));
        ApplyEditMode();
    }

    private void BuildCompanyTabs()
    {
        FormTab("Γενικά",
            [T("title", "Επωνυμία εταιρείας", true), T("reference", "Κωδικός"),
             S("status", "Κατάσταση", "Ενεργό", "Ανενεργό", "Σε αναστολή"),
             S("companyType", "Είδος φορέα", "Ασφαλιστική εταιρεία", "Μεσίτης", "Άλλο"),
             S("origin", "Προέλευση", "Εσωτερικό", "Εξωτερικό"),
             T("taxNumber", "ΑΦΜ"), T("taxOffice", "ΔΟΥ"),
             T("legalName", "Νομική επωνυμία")],
            [T("contractNumber", "Αριθμός συνεργασίας"), D("startDate", "Έναρξη συνεργασίας"),
             D("endDate", "Λήξη συνεργασίας"), T("manager", "Υπεύθυνος συνεργασίας"),
             T("licenseNumber", "Αριθμός άδειας"), T("gemi", "ΓΕΜΗ"),
             M("notes", "Σημειώσεις")]);
        FormTab("Επικοινωνία",
            [T("contact", "Πρόσωπο επικοινωνίας"), T("phone", "Τηλέφωνο"),
             T("mobile", "Κινητό"), T("email", "Email"), T("website", "Ιστότοπος")],
            [T("address", "Διεύθυνση"), T("city", "Πόλη"), T("postalCode", "Τ.Κ."),
             T("claimsEmail", "Email ζημιών"), T("accountingEmail", "Email λογιστηρίου")]);
        var production = RelatedPanel();
        production.Children.Add(RelatedTable("Ασφαλιστήρια εταιρείας", "policies",
            item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson) is var policy &&
                (policy.CompanyId == _record.Id || policy.Insurer == _record.Title)));
        production.Children.Add(RelatedTable("Πρακτορεία", "agencies",
            item => WorkspaceCardData.Read(item.DetailsJson).CompanyId == _record.Id));
        production.Children.Add(RelatedTable("Ζημιές", "claims",
            item => WorkspaceCardData.Read(item.DetailsJson).CompanyId == _record.Id));
        AddTab("Παραγωγή", production);
        FormTab("Παραμετρικά",
            [S("payoutMethod", "Τρόπος απόδοσης", "Τραπεζική μεταφορά", "Συμψηφισμός", "Μετρητά"),
             S("payoutPeriod", "Περίοδος απόδοσης", "Μηνιαία", "Εβδομαδιαία", "Ανά ασφαλιστήριο"),
             B("taxWithholding", "Παρακράτηση φόρου"), N("creditLimit", "Πιστωτικό όριο (€)"),
             N("creditDays", "Πιστωτικό όριο ημερών")],
            [T("branchCode", "Κωδικός υποκαταστήματος"),
             T("productionCode", "Κωδικός ομαδοποίησης"),
             N("printDeadline", "Εκτύπωση εντός (ημέρες)"),
             N("renewalDays", "Ημέρες ανανέωσης"), B("splitTax", "Split φόρου")]);
    }

    private void BuildAgencyTabs()
    {
        FormTab("Καρτέλα πρακτορείου",
            [T("title", "Επωνυμία πρακτορείου", true), T("reference", "Κωδικός"),
             S("status", "Κατάσταση", "Ενεργό", "Ανενεργό", "Σε αναστολή"),
             S("agencyType", "Είδος φορέα", "Πρακτορείο", "Υποκατάστημα", "Συνεργαζόμενο δίκτυο"),
             S("origin", "Προέλευση", "Εσωτερικό", "Εξωτερικό"),
             L("company", "Ασφαλιστική εταιρεία"), T("area", "Περιοχή"),
             T("manager", "Υπεύθυνος"), T("taxNumber", "ΑΦΜ")],
            [T("contractNumber", "Αριθμός σύμβασης"), D("startDate", "Έναρξη συνεργασίας"),
             D("endDate", "Λήξη συνεργασίας"), N("creditLimit", "Πιστωτικό όριο (€)"),
             T("branchCode", "Κωδικός υποκαταστήματος"), M("notes", "Σημειώσεις")]);
        FormTab("Επικοινωνία",
            [T("phone", "Τηλέφωνο"), T("mobile", "Κινητό"), T("email", "Email"),
             T("website", "Ιστότοπος")],
            [T("address", "Διεύθυνση"), T("city", "Πόλη"), T("postalCode", "Τ.Κ."),
             T("contact", "Πρόσωπο επικοινωνίας")]);
        var policies = RelatedPanel();
        policies.Children.Add(RelatedTable("Ασφαλιστήρια πρακτορείου", "policies",
            item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson) is var policy &&
                (policy.AgencyId == _record.Id || policy.Agency == _record.Title)));
        AddTab("Ασφαλιστήρια", policies);
    }

    private void BuildClaimTabs()
    {
        FormTab("Καρτέλα ζημιάς",
            [T("title", "Αριθμός ζημιάς", true), T("reference", "Αρ. φακέλου ασφαλιστικής"),
             S("status", "Κατάσταση", "Δηλωμένη", "Σε αναμονή", "Σε εξέλιξη", "Εγκρίθηκε",
                 "Πληρωμένη", "Κλειστή", "Απορρίφθηκε"),
             S("claimType", "Είδος ζημιάς", "Ζημιά οχήματος", "Ζημιά τρίτου", "Κλοπή",
                 "Πλημμύρα", "Υγεία", "Κατοικία", "Σκάφος", "Αστική ευθύνη", "Άλλο"),
             D("eventDate", "Ημερομηνία συμβάντος"), T("eventTime", "Ώρα συμβάντος"),
             T("incidentLocation", "Τόπος / διεύθυνση"), T("incidentCity", "Πόλη συμβάντος"),
             D("reportedDate", "Ημερομηνία δήλωσης"), M("notes", "Περιγραφή")],
            [L("customer", "Πελάτης / ασφαλισμένος", true), L("policy", "Ασφαλιστήριο"),
             L("vehicle", "Όχημα"), L("company", "Ασφαλιστική εταιρεία"),
             L("agency", "Πρακτορείο"), L("partner", "Συνεργάτης")]);
        FormTab("Πρόσθετα στοιχεία",
            [T("externalFile", "Εξωτερικός φάκελος"), T("assessor", "Πραγματογνώμονας"),
             T("policeReport", "Αριθμός συμβάντος Αστυνομίας"),
             D("assessmentDate", "Ημερομηνία εκτίμησης")],
            [S("liability", "Υπαιτιότητα", "Άγνωστη", "Ναι", "Όχι", "Συνυπαιτιότητα"),
             N("deductible", "Απαλλαγή (€)"), T("contactPerson", "Υπεύθυνος χειρισμού"),
             M("internalNotes", "Εσωτερικές σημειώσεις")]);
        var vehicle = RelatedPanel();
        vehicle.Children.Add(LinkedSummary("Όχημα ζημιάς", "vehicle"));
        vehicle.Children.Add(LinkedSummary("Ασφαλιστήριο ζημιάς", "policy"));
        AddTab("Όχημα & συμβόλαιο", vehicle);
        FormTab("Οικονομικά",
            [N("reserve", "Εκτίμηση ζημιάς (€)"), N("settlement", "Εγκριθείσα αποζημίωση (€)"),
             N("paid", "Καταβληθέν ποσό (€)"), D("paymentDate", "Ημερομηνία πληρωμής")],
            [N("customerShare", "Συμμετοχή πελάτη (€)"),
             S("paymentStatus", "Κατάσταση πληρωμής", "Εκκρεμεί", "Μερική", "Εξοφλήθηκε"),
             M("paymentNotes", "Σημειώσεις αποζημίωσης")]);
        FormTab("Εμπλεκόμενοι",
            [T("thirdParty", "Τρίτο πρόσωπο"), T("thirdPartyPhone", "Τηλέφωνο τρίτου"),
             T("thirdPartyVehicle", "Όχημα τρίτου"), T("thirdPartyInsurer", "Ασφαλιστική τρίτου")],
            [T("driver", "Οδηγός"), T("driverPhone", "Τηλέφωνο οδηγού"),
             T("witnesses", "Μάρτυρες"), M("involvedNotes", "Λοιπά στοιχεία")]);
    }

    private void BuildUpdateTabs()
    {
        FormTab("Καρτέλα ενημέρωσης",
            [T("title", "Θέμα", true), T("reference", "Κωδικός"),
             S("status", "Κατάσταση", "Πρόχειρο", "Ενεργό", "Ολοκληρωμένο"),
             S("category", "Κατηγορία", "Ανακοίνωση", "Υπενθύμιση", "Εργασία", "Ανανέωση"),
             D("eventDate", "Ημερομηνία"), M("notes", "Περιεχόμενο")],
            [L("customer", "Πελάτης"), L("partner", "Συνεργάτης"),
             L("company", "Εταιρεία"), L("policy", "Ασφαλιστήριο"),
             L("claim", "Ζημιά")]);
        var related = RelatedPanel();
        foreach (var key in new[] { "customer", "partner", "company", "policy", "claim" })
            related.Children.Add(LinkedSummary("Συνδεδεμένο: " + LinkLabel(key), key));
        AddTab("Σχετικές εγγραφές", related);
    }

    private void BuildReportTabs()
    {
        FormTab("Καρτέλα αναφοράς",
            [T("title", "Τίτλος αναφοράς", true), T("reference", "Κωδικός"),
             S("status", "Κατάσταση", "Ενεργό", "Ανενεργό"),
             S("reportType", "Είδος αναφοράς", "Ασφαλιστήρια", "Ζημιές", "Πελάτες", "Οχήματα", "Συνεργάτες"),
             D("fromDate", "Από ημερομηνία"), D("toDate", "Έως ημερομηνία")],
            [T("owner", "Υπεύθυνος"), M("notes", "Σημειώσεις")]);
        var data = RelatedPanel();
        data.Children.Add(ReportDataPanel());
        AddTab("Δεδομένα", data);
    }

    private void BuildBridgeTabs()
    {
        FormTab("Καρτέλα γέφυρας",
            [T("title", "Όνομα γέφυρας", true), T("reference", "Κωδικός"),
             S("status", "Κατάσταση", "Ανενεργό", "Ενεργό", "Σε δοκιμή"),
             L("company", "Ασφαλιστική εταιρεία", true),
             S("bridgeType", "Τύπος σύνδεσης", "Αρχείο", "API", "Χειροκίνητη εισαγωγή"),
             M("notes", "Σημειώσεις")],
            [T("contact", "Υπεύθυνος"), T("supportEmail", "Email υποστήριξης"),
             D("eventDate", "Τελευταίος συγχρονισμός")]);
        FormTab("Ρυθμίσεις σύνδεσης",
            [T("endpoint", "Διεύθυνση υπηρεσίας"), T("protocol", "Πρωτόκολλο"),
             T("fileFormat", "Μορφή αρχείου"), T("externalCode", "Εξωτερικός κωδικός")],
            [S("direction", "Κατεύθυνση", "Εισαγωγή", "Εξαγωγή", "Αμφίδρομη"),
             T("schedule", "Πρόγραμμα συγχρονισμού"),
             M("mapping", "Σημειώσεις αντιστοίχισης πεδίων")]);
        var related = RelatedPanel();
        related.Children.Add(LinkedSummary("Ασφαλιστική εταιρεία", "company"));
        AddTab("Συνδεδεμένη εταιρεία", related);
    }

    private void FormTab(string title, FieldSpec[] leftFields, FieldSpec[] rightFields)
    {
        var grid = CardFieldLayout.TwoColumns(out var left, out var right, _contentWidth);
        left.Spacing = 5; right.Spacing = 5;
        _forms.Add(grid);
        foreach (var field in leftFields) AddField(left, field);
        foreach (var field in rightFields) AddField(right, field);
        AddTab(title, grid);
    }

    private void AddField(StackPanel panel, FieldSpec spec)
    {
        _specs[spec.Key] = spec;
        FrameworkElement control;
        FrameworkElement display;
        if (spec.Kind == "link")
        {
            var combo = new ComboBox { MinHeight = 34, Height = 34,
                DisplayMemberPath = nameof(RecordOption.Name),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = OptionsFor(spec.Key) };
            combo.SelectedItem = (combo.ItemsSource as IEnumerable<RecordOption>)?
                .FirstOrDefault(item => item.Id == LinkId(spec.Key));
            var host = new Grid { ColumnSpacing = 4,
                ColumnDefinitions = { new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto } } };
            host.Children.Add(combo);
            var open = new Button { Content = new FontIcon { Glyph = "\uE8A7",
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets") },
                Width = 34, Height = 34, Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(255, 23, 111, 171)),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            ToolTipService.SetToolTip(open, "Άνοιγμα συνδεδεμένης καρτέλας");
            ButtonMotion.Attach(open);
            open.Click += async (_, _) => await OpenLinkAsync(spec.Key,
                (combo.SelectedItem as RecordOption)?.Id);
            Grid.SetColumn(open, 1); host.Children.Add(open);
            control = combo; display = host;
        }
        else if (spec.Kind == "date")
        {
            DateTime? value = spec.Key == "eventDate" ? _record.EventDate :
                DateTime.TryParse(GetText(spec.Key), out var parsed) ? parsed : null;
            var picker = new CalendarDatePicker { Date = value is DateTime date ?
                    new DateTimeOffset(date) : null,
                MinHeight = 36, Height = 36, PlaceholderText = "Επιλέξτε ημερομηνία",
                HorizontalAlignment = HorizontalAlignment.Stretch };
            control = picker; display = picker;
        }
        else if (spec.Kind == "select")
        {
            var options = spec.Options ?? [];
            var selected = GetText(spec.Key);
            var values = options.Concat(string.IsNullOrWhiteSpace(selected) ? [] : [selected])
                .Distinct().ToArray();
            var combo = new ComboBox { ItemsSource = values, SelectedItem = selected,
                MinHeight = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Stretch };
            control = combo; display = combo;
        }
        else if (spec.Kind == "bool")
        {
            var box = new CheckBox { IsChecked = GetText(spec.Key) == "true",
                MinHeight = 34, Height = 34 };
            control = box; display = box;
        }
        else
        {
            var memo = spec.Kind == "memo";
            var input = new TextBox { Text = GetText(spec.Key) ?? "", MinHeight = memo ? 72 : 34,
                Height = memo ? 110 : 34, FontSize = 13,
                AcceptsReturn = memo, TextWrapping = memo ? TextWrapping.Wrap : TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            control = input; display = memo ? ResizableNoteField.Wrap(input) : input;
        }
        panel.Children.Add(CardFieldLayout.CreateRow(spec.Label + (spec.Required ? " *" : ""), display));
        _inputs[spec.Key] = control;
    }

    private RecordOption[] OptionsFor(string key)
    {
        if (key == "customer") return [new(null, "Χωρίς πελάτη"),
            .. _customers.Select(item => new RecordOption(item.Id, item.FullName))];
        var section = LinkSection(key);
        return [new(null, "Χωρίς σύνδεση"),
            .. (_all.TryGetValue(section, out var items) ? items : [])
                .Select(item => new RecordOption(item.Id, item.Title))];
    }

    private string GetText(string key) => key switch
    {
        "title" => _record.Title, "reference" => _record.Reference ?? "",
        "status" => _record.Status ?? "", "notes" => _record.Notes ?? "",
        _ => _data.Fields.GetValueOrDefault(key) ?? ""
    };

    private Guid? LinkId(string key) => key switch
    {
        "customer" => _record.CustomerId, "company" => _data.CompanyId,
        "agency" => _data.AgencyId, "policy" => _data.PolicyId,
        "vehicle" => _data.VehicleId, "partner" => _data.PartnerId,
        "claim" => _data.ClaimId, _ => null
    };

    private void SetLinkId(string key, Guid? id)
    {
        switch (key)
        {
            case "customer": _record.CustomerId = id; break;
            case "company": _data.CompanyId = id; break;
            case "agency": _data.AgencyId = id; break;
            case "policy": _data.PolicyId = id; break;
            case "vehicle": _data.VehicleId = id; break;
            case "partner": _data.PartnerId = id; break;
            case "claim": _data.ClaimId = id; break;
        }
    }

    private static string LinkSection(string key) => key switch
    {
        "customer" => "customers", "company" => "companies", "agency" => "agencies",
        "policy" => "policies", "vehicle" => "vehicles", "partner" => "partners",
        "claim" => "claims", _ => throw new ArgumentException("Άγνωστος σύνδεσμος.")
    };

    private static string LinkLabel(string key) => key switch
    {
        "customer" => "Πελάτης", "company" => "Εταιρεία", "agency" => "Πρακτορείο",
        "policy" => "Ασφαλιστήριο", "vehicle" => "Όχημα", "partner" => "Συνεργάτης",
        "claim" => "Ζημιά", _ => "Εγγραφή"
    };

    private void WireDependencies()
    {
        if (_section.Key != "claims" || _inputs.GetValueOrDefault("customer") is not ComboBox customer)
            return;
        customer.SelectionChanged += (_, _) =>
        {
            if (!_editing || _building) return;
            FilterByCustomer("policy", (customer.SelectedItem as RecordOption)?.Id);
            FilterByCustomer("vehicle", (customer.SelectedItem as RecordOption)?.Id);
        };
        if (_inputs.GetValueOrDefault("policy") is ComboBox policy)
            policy.SelectionChanged += (_, _) =>
            {
                if (!_editing || _building || policy.SelectedItem is not RecordOption { Id: Guid id }) return;
                var linked = _all["policies"].FirstOrDefault(item => item.Id == id);
                if (linked is null) return;
                if (customer.SelectedItem is not RecordOption { Id: Guid })
                    customer.SelectedItem = (customer.ItemsSource as IEnumerable<RecordOption>)?
                        .FirstOrDefault(item => item.Id == linked.CustomerId);
                var details = BusinessRecordDetails.Read<PolicyCardData>(linked.DetailsJson);
                SelectLink("vehicle", details.VehicleId);
                var company = _all["companies"].FirstOrDefault(item => item.Title == details.Insurer);
                SelectLink("company", company?.Id);
                var agency = _all["agencies"].FirstOrDefault(item => item.Title == details.Agency);
                SelectLink("agency", agency?.Id);
            };
        FilterByCustomer("policy", (customer.SelectedItem as RecordOption)?.Id);
        FilterByCustomer("vehicle", (customer.SelectedItem as RecordOption)?.Id);
    }

    private void FilterByCustomer(string key, Guid? customerId)
    {
        if (_inputs.GetValueOrDefault(key) is not ComboBox combo) return;
        var selected = (combo.SelectedItem as RecordOption)?.Id;
        var all = _all[LinkSection(key)];
        var visible = customerId is Guid id ? all.Where(item => item.CustomerId == id) : all;
        var values = new[] { new RecordOption(null, "Χωρίς σύνδεση") }
            .Concat(visible.Select(item => new RecordOption(item.Id, item.Title))).ToArray();
        combo.ItemsSource = values;
        combo.SelectedItem = values.FirstOrDefault(item => item.Id == selected) ?? values[0];
    }

    private void SelectLink(string key, Guid? id)
    {
        if (_inputs.GetValueOrDefault(key) is ComboBox combo)
            combo.SelectedItem = (combo.ItemsSource as IEnumerable<RecordOption>)?
                .FirstOrDefault(item => item.Id == id);
    }

    private void ApplyEditMode()
    {
        if (_editButton is not null) _editButton.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        if (_saveButton is not null) _saveButton.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        foreach (var input in _inputs.Values)
        {
            if (input is TextBox text) text.IsReadOnly = !_editing;
            else if (input is Control control) control.IsEnabled = _editing;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            foreach (var (key, input) in _inputs)
            {
                var spec = _specs[key];
                if (spec.Kind == "link")
                {
                    var linkedId = ((input as ComboBox)?.SelectedItem as RecordOption)?.Id;
                    if (spec.Required && linkedId is null)
                        throw new ArgumentException($"Το πεδίο «{spec.Label}» είναι υποχρεωτικό.");
                    SetLinkId(key, linkedId);
                    continue;
                }
                if (spec.Kind == "date")
                {
                    var date = (input as CalendarDatePicker)?.Date?.Date;
                    if (key == "eventDate") _record.EventDate = date;
                    else _data.Fields[key] = date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    continue;
                }
                var value = input switch
                {
                    TextBox text => text.Text.Trim(),
                    ComboBox combo => combo.SelectedItem as string ?? "",
                    CheckBox box => box.IsChecked == true ? "true" : "false",
                    _ => ""
                };
                if (spec.Required && string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"Το πεδίο «{spec.Label}» είναι υποχρεωτικό.");
                if (spec.Kind == "number" && value.Length > 0 &&
                    (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("el-GR"), out var number) ||
                     number < 0))
                    throw new ArgumentException($"Το πεδίο «{spec.Label}» πρέπει να έχει έγκυρο μη αρνητικό αριθμό.");
                switch (key)
                {
                    case "title": _record.Title = value; break;
                    case "reference": _record.Reference = value; break;
                    case "status": _record.Status = value; break;
                    case "notes": _record.Notes = value; break;
                    default: _data.Fields[key] = value; break;
                }
            }
            if (_section.Key == "claims" && _data.PolicyId is Guid policyId &&
                _all["policies"].FirstOrDefault(item => item.Id == policyId) is BusinessRecord policy)
            {
                if (policy.CustomerId != _record.CustomerId)
                    throw new ArgumentException("Το ασφαλιστήριο πρέπει να ανήκει στον επιλεγμένο πελάτη.");
                var linkedVehicle = BusinessRecordDetails.Read<PolicyCardData>(policy.DetailsJson).VehicleId;
                if (_data.VehicleId is Guid vehicleId && linkedVehicle is Guid expected && vehicleId != expected)
                    throw new ArgumentException("Το όχημα δεν αντιστοιχεί στο επιλεγμένο ασφαλιστήριο.");
            }
            _record.RelatedParty = _section.Key switch
            {
                "claims" => _customers.FirstOrDefault(item => item.Id == _record.CustomerId)?.FullName,
                "agencies" or "bridges" => _all["companies"].FirstOrDefault(item => item.Id == _data.CompanyId)?.Title,
                "companies" => _data.Fields.GetValueOrDefault("phone"),
                "updates" => _data.Fields.GetValueOrDefault("category"),
                "reports" => _data.Fields.GetValueOrDefault("reportType"),
                _ => null
            };
            _record.DetailsJson = _data.Write();
            var id = await _records.SaveAsync(_record);
            _record = await _records.GetAsync(id) ?? _record;
            _data = WorkspaceCardData.Read(_record.DetailsJson);
            _editing = false;
            _all[_section.Key] = (await _records.SearchAsync(_section.Key)).ToArray();
            UpdateHeader();
            BuildTabs(preserve: true);
            RecordSaved?.Invoke(this, EventArgs.Empty);
            _status.Text = "Η καρτέλα αποθηκεύτηκε.";
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }
}

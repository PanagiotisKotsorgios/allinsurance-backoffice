using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AllInsurance.Desktop.Views;

public sealed partial class SectionPage
{
    private int _visibleFilterCount;

    private void ConfigureFilterBar()
    {
        CustomerFilter.Title = _section.Key switch
        {
            "partners" => "Κατηγορία", "vehicles" => "Χρήση",
            "companies" => "Είδος φορέα", "agencies" => "Είδος φορέα",
            "policies" => "Κλάδος", "claims" => "Είδος ζημιάς", _ => "Πελάτης"
        };
        CityFilter.Title = _section.Key switch
        {
            "partners" => "Πόλη",
            "companies" or "agencies" => "Προέλευση", "policies" => "Εταιρεία", _ => "Πόλη"
        };
        PersonTypeFilter.Title = _section.Key switch
        {
            "vehicles" => "Μάρκα", "policies" => "Πρακτορείο", _ => "Τύπος προσώπου"
        };
        var visible = _section.Key switch
        {
            "partners" => new FrameworkElement[] { StatusFilter, CustomerFilter, CityFilter, PersonTypeFilter },
            "vehicles" => [StatusFilter, CustomerFilter, PersonTypeFilter, DateRangeFilter],
            "companies" or "agencies" => [StatusFilter, CustomerFilter, CityFilter],
            "policies" => [StatusFilter, PolicyDocumentFilter, CustomerFilter, CityFilter, PersonTypeFilter,
                PolicyCustomerFilter, PolicyPartnerFilter, DateRangeFilter],
            "claims" => [StatusFilter, CustomerFilter, DateRangeFilter],
            _ => [StatusFilter, CustomerFilter, DateRangeFilter]
        };
        foreach (var element in new FrameworkElement[] { StatusFilter, CustomerFilter, CityFilter,
            PersonTypeFilter, PolicyCustomerFilter, PolicyPartnerFilter, PolicyDocumentFilter, DateRangeFilter })
            element.Visibility = visible.Contains(element) ? Visibility.Visible : Visibility.Collapsed;
        DateBasisFilter.Visibility = _section.Key == "policies" ? Visibility.Visible : Visibility.Collapsed;
        _visibleFilterCount = visible.Length;
        FilterBar.Children.Clear();
        foreach (var element in visible)
        {
            element.Width = 180;
            element.HorizontalAlignment = HorizontalAlignment.Stretch;
            element.Margin = new Thickness(0);
            FilterBar.Children.Add(element);
        }
        if (_section.Key is "vehicles")
        {
            DateRangeFilter.Label = "Πρώτη άδεια";
            FromFilter.PlaceholderText = "Από 1η άδεια";
            UntilFilter.PlaceholderText = "Έως 1η άδεια";
        }
        else if (_section.Key is "claims")
        {
            DateRangeFilter.Label = "Συμβάν";
            FromFilter.PlaceholderText = "Από συμβάν";
            UntilFilter.PlaceholderText = "Έως συμβάν";
        }
        else if (_section.Key is "policies")
        {
            DateBasisFilter.ItemsSource = new[] { "Ημ. έναρξης", "Ημ. έκδοσης" };
            DateBasisFilter.SelectedIndex = 0;
            FromFilter.PlaceholderText = "Από ημερομηνία";
            UntilFilter.PlaceholderText = "Έως ημερομηνία";
        }
        UpdateDateRangeLabel();
    }

    private void UpdateDateRangeLabel()
    {
        var label = _section.Key switch
        {
            "vehicles" => "Πρώτη άδεια", "claims" => "Συμβάν",
            "policies" => DateBasisFilter.SelectedItem as string ?? "Ημ. έναρξης",
            _ => "Ημερομηνία"
        };
        var from = FromFilter.Date?.ToString("dd/MM/yyyy");
        var until = UntilFilter.Date?.ToString("dd/MM/yyyy");
        DateRangeFilter.Label = from is null && until is null ? label : $"{label} ✓";
        ToolTipService.SetToolTip(DateRangeFilter,
            from is null && until is null ? "Επιλογή εύρους ημερομηνιών" :
            $"{label}: {from ?? "χωρίς αρχικό όριο"} – {until ?? "χωρίς τελικό όριο"}");
    }

    private void ConfigureFilterOptions(IReadOnlyList<BusinessRecord> records)
    {
        IEnumerable<string> statuses = _section.Key == "vehicles"
            ? records.Select(VehicleDisplayStatus) : records.Select(StatusKey);
        if (_section.Key == "policies")
            statuses = statuses.Concat(["Ενεργό", "Πιθανό", "Ληγμένο", "Ακυρωμένο", "Ανανεωμένο",
                "Σε εξέλιξη", "Πρόσθετη πράξη", "Πρόσθετη πράξη ακύρωσης"]);
        else if (_section.Key == "claims")
            statuses = statuses.Concat(["Δηλωμένη", "Σε αναμονή", "Σε εξέλιξη", "Εγκρίθηκε",
                "Πληρωμένη", "Κλειστή", "Απορρίφθηκε"]);
        else if (_section.Key is "companies" or "agencies")
            statuses = statuses.Concat(["Ενεργό", "Ανενεργό"]);
        else if (_section.Key == "vehicles")
            statuses = statuses.Concat(["Ασφαλισμένο", "Σε εξέλιξη",
                "Ανασφάλιστο", "Ανενεργό"]);
        StatusFilter.SetOptions(statuses);
        if (_section.Key == "policies")
            PolicyDocumentFilter.SetOptions(records.Select(item => item.DocumentType ?? "Ασφαλιστήριο")
                .Concat(["Προσφορά", "Αίτηση", "Ειδοποιητήριο", "Ασφαλιστήριο",
                    "Πρόσθετη πράξη", "Πράσινη κάρτα"]));
        switch (_section.Key)
        {
            case "partners":
                CustomerFilter.SetOptions(_partnerCategories.Concat(records.Select(item =>
                    PartnerProfileData.Read(item.DetailsJson).Category ?? "Χωρίς κατηγορία")));
                CityFilter.SetOptions(records.Select(item =>
                    PartnerProfileData.Read(item.DetailsJson).City ?? "Χωρίς πόλη"));
                PersonTypeFilter.SetOptions(records.Select(item =>
                    PartnerProfileData.Read(item.DetailsJson).PersonType ?? "Χωρίς τύπο"));
                break;
            case "vehicles":
                CustomerFilter.SetOptions(_catalogVehicleUses.Concat(records.Select(item =>
                    Vehicle(item).Use ?? "Χωρίς χρήση")));
                PersonTypeFilter.SetOptions(records.Select(item => Vehicle(item).Make ?? "Χωρίς μάρκα"));
                break;
            case "companies":
                CustomerFilter.SetOptions(records.Select(item => Workspace(item, "companyType", "Χωρίς είδος")));
                CityFilter.SetOptions(records.Select(item => Workspace(item, "origin", "Χωρίς προέλευση")));
                break;
            case "agencies":
                CustomerFilter.SetOptions(records.Select(item => Workspace(item, "agencyType", "Χωρίς είδος")));
                CityFilter.SetOptions(records.Select(item => Workspace(item, "origin", "Χωρίς προέλευση")));
                break;
            case "policies":
                CustomerFilter.SetOptions(_catalogBranches.Concat(records.Select(item =>
                    Policy(item).Branch ?? "Χωρίς κλάδο")));
                CityFilter.SetOptions(records.Select(item => Policy(item).Insurer ?? "Χωρίς εταιρεία"));
                PersonTypeFilter.SetOptions(records.Select(item => Policy(item).Agency ?? "Χωρίς πρακτορείο"));
                PolicyCustomerFilter.SetOptions(records.Select(item => item.Customer is null
                    ? "Χωρίς πελάτη" : $"{item.Customer.LastName} {item.Customer.FirstName}".Trim()));
                PolicyPartnerFilter.SetOptions(records.Select(PolicyPartnerName));
                break;
            case "claims":
                CustomerFilter.SetOptions(records.Select(item => Workspace(item, "claimType", "Χωρίς είδος"))
                    .Concat(["Ζημιά οχήματος", "Ζημιά τρίτου", "Κλοπή", "Πλημμύρα",
                        "Υγεία", "Κατοικία", "Σκάφος", "Αστική ευθύνη", "Άλλο"]));
                break;
            default:
                CustomerFilter.SetOptions(_customerChoices.Select(item => item.Name));
                break;
        }
    }

    private bool PassesSectionFilters(BusinessRecord item)
    {
        var displayedStatus = _section.Key == "vehicles"
            ? VehicleDisplayStatus(item) : StatusKey(item);
        if (!StatusFilter.SelectedValues.Contains(displayedStatus)) return false;
        if (_section.Key == "policies" &&
            !PolicyDocumentFilter.SelectedValues.Contains(item.DocumentType ?? "Ασφαλιστήριο")) return false;
        var second = _section.Key switch
        {
            "partners" => PartnerProfileData.Read(item.DetailsJson).Category ?? "Χωρίς κατηγορία",
            "vehicles" => Vehicle(item).Use ?? "Χωρίς χρήση",
            "companies" => Workspace(item, "companyType", "Χωρίς είδος"),
            "agencies" => Workspace(item, "agencyType", "Χωρίς είδος"),
            "policies" => Policy(item).Branch ?? "Χωρίς κλάδο",
            "claims" => Workspace(item, "claimType", "Χωρίς είδος"),
            _ => item.Customer is null ? "Χωρίς πελάτη" :
                $"{item.Customer.LastName} {item.Customer.FirstName}".Trim()
        };
        if (!CustomerFilter.SelectedValues.Contains(second)) return false;
        var third = _section.Key switch
        {
            "partners" => PartnerProfileData.Read(item.DetailsJson).City ?? "Χωρίς πόλη",
            "companies" or "agencies" => Workspace(item, "origin", "Χωρίς προέλευση"),
            "policies" => Policy(item).Insurer ?? "Χωρίς εταιρεία",
            _ => null
        };
        if (third is not null && !CityFilter.SelectedValues.Contains(third)) return false;
        var fourth = _section.Key switch
        {
            "partners" => PartnerProfileData.Read(item.DetailsJson).PersonType ?? "Χωρίς τύπο",
            "vehicles" => Vehicle(item).Make ?? "Χωρίς μάρκα",
            "policies" => Policy(item).Agency ?? "Χωρίς πρακτορείο",
            _ => null
        };
        if (fourth is not null && !PersonTypeFilter.SelectedValues.Contains(fourth)) return false;
        if (_section.Key == "policies")
        {
            var customer = item.Customer is null ? "Χωρίς πελάτη" :
                $"{item.Customer.LastName} {item.Customer.FirstName}".Trim();
            return PolicyCustomerFilter.SelectedValues.Contains(customer) &&
                PolicyPartnerFilter.SelectedValues.Contains(PolicyPartnerName(item));
        }
        return true;
    }

    private string PolicyPartnerName(BusinessRecord item)
    {
        var partnerId = Policy(item).PartnerId;
        return partnerId is Guid id && _policyPartnerNames.TryGetValue(id, out var name)
            ? name : "Χωρίς συνεργάτη";
    }

    private DateTime? FilterDate(BusinessRecord item) => _section.Key switch
    {
        "vehicles" => Vehicle(item).FirstRegistrationDate,
        "policies" => DateBasisFilter.SelectedItem as string == "Ημ. έκδοσης"
            ? Policy(item).IssueDate : Policy(item).StartDate,
        _ => item.EventDate
    };

    private static bool IsActivePolicy(BusinessRecord item)
    {
        if (item.DocumentType is not (null or "Ασφαλιστήριο") ||
            item.Status is "Ακυρωμένο" or "Ληγμένο" or "Ανενεργό") return false;
        var policy = Policy(item);
        var today = DateTime.Today;
        return (item.Status is "Ενεργό" or "Ανανεωμένο" ||
                policy.StartDate is not null && policy.EndDate is not null) &&
            (policy.StartDate is null || policy.StartDate.Value.Date <= today) &&
            (policy.EndDate is null || policy.EndDate.Value.Date >= today);
    }

    private string VehicleDisplayStatus(BusinessRecord item) =>
        item.Status == "Ανενεργό" ? "Ανενεργό" :
        _vehicleInsuranceStates.GetValueOrDefault(item.Id) ?? "Ανασφάλιστο";

    private static string StatusKey(BusinessRecord item) =>
        string.IsNullOrWhiteSpace(item.Status) ? "Χωρίς κατάσταση" : item.Status;

    private static VehicleCardData Vehicle(BusinessRecord item) =>
        BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson);
    private static PolicyCardData Policy(BusinessRecord item) =>
        BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson);
    private static string Workspace(BusinessRecord item, string key, string missing) =>
        WorkspaceCardData.Read(item.DetailsJson).Fields.GetValueOrDefault(key) is { Length: > 0 } value
            ? value : missing;
}

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
            "partners" => "Πόλη", "vehicles" => "Συμβόλαιο",
            "companies" or "agencies" => "Προέλευση", "policies" => "Εταιρεία", _ => "Πόλη"
        };
        PersonTypeFilter.Title = _section.Key switch
        {
            "vehicles" => "Μάρκα", "policies" => "Πρακτορείο", _ => "Τύπος προσώπου"
        };
        var visible = _section.Key switch
        {
            "partners" => new FrameworkElement[] { StatusFilter, CustomerFilter, CityFilter, PersonTypeFilter },
            "vehicles" => [StatusFilter, CustomerFilter, CityFilter, PersonTypeFilter, DateRangeFilter],
            "companies" or "agencies" => [StatusFilter, CustomerFilter, CityFilter],
            "policies" => [StatusFilter, CustomerFilter, CityFilter, PersonTypeFilter, DateRangeFilter],
            "claims" => [StatusFilter, CustomerFilter, DateRangeFilter],
            _ => [StatusFilter, CustomerFilter, DateRangeFilter]
        };
        foreach (var element in new FrameworkElement[] { StatusFilter, CustomerFilter, CityFilter,
            PersonTypeFilter, DateRangeFilter })
            element.Visibility = visible.Contains(element) ? Visibility.Visible : Visibility.Collapsed;
        DateBasisFilter.Visibility = _section.Key == "policies" ? Visibility.Visible : Visibility.Collapsed;
        _visibleFilterCount = visible.Length + 1;
        FilterBar.Children.Clear();
        foreach (var element in visible)
        {
            element.Width = double.NaN;
            element.HorizontalAlignment = HorizontalAlignment.Stretch;
            element.Margin = new Thickness(0, 0, 14, 0);
            FilterBar.Children.Add(element);
        }
        ClearFiltersButton.Margin = new Thickness(0, 0, 14, 0);
        ClearFiltersButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        FilterBar.Children.Add(ClearFiltersButton);
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

    private void FilterBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_visibleFilterCount == 0 || e.NewSize.Width <= 0) return;
        FilterBar.ItemWidth = Math.Max(170, Math.Floor(e.NewSize.Width / _visibleFilterCount));
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
        IEnumerable<string> statuses = records.Select(StatusKey);
        if (_section.Key == "policies")
            statuses = statuses.Concat(["Ενεργό", "Πιθανό", "Ληγμένο", "Ακυρωμένο", "Ανανεωμένο",
                "Αίτηση", "Ειδοποιητήριο", "Πρόσθετη πράξη", "Πρόσθετη πράξη ακύρωσης"]);
        else if (_section.Key == "claims")
            statuses = statuses.Concat(["Δηλωμένη", "Σε αναμονή", "Σε εξέλιξη", "Εγκρίθηκε",
                "Πληρωμένη", "Κλειστή", "Απορρίφθηκε"]);
        else if (_section.Key is "companies" or "agencies")
            statuses = statuses.Concat(["Ενεργό", "Ανενεργό"]);
        else if (_section.Key == "vehicles")
            statuses = statuses.Concat(["Ενεργό", "Ανενεργό"]);
        StatusFilter.SetOptions(statuses);
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
                CustomerFilter.SetOptions(records.Select(item => Vehicle(item).Use ?? "Χωρίς χρήση"));
                CityFilter.SetOptions(["Ενεργό συμβόλαιο", "Χωρίς ενεργό συμβόλαιο"]);
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
                CustomerFilter.SetOptions(records.Select(item => Policy(item).Branch ?? "Χωρίς κλάδο"));
                CityFilter.SetOptions(records.Select(item => Policy(item).Insurer ?? "Χωρίς εταιρεία"));
                PersonTypeFilter.SetOptions(records.Select(item => Policy(item).Agency ?? "Χωρίς πρακτορείο"));
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
        if (!StatusFilter.SelectedValues.Contains(StatusKey(item))) return false;
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
            "vehicles" => _vehiclesWithActivePolicy.Contains(item.Id)
                ? "Ενεργό συμβόλαιο" : "Χωρίς ενεργό συμβόλαιο",
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
        return fourth is null || PersonTypeFilter.SelectedValues.Contains(fourth);
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

    private static string StatusKey(BusinessRecord item) => item.SectionKey == "policies" &&
        item.DocumentType is "Αίτηση" or "Ειδοποιητήριο"
            ? item.DocumentType : string.IsNullOrWhiteSpace(item.Status) ? "Χωρίς κατάσταση" : item.Status;

    private static VehicleCardData Vehicle(BusinessRecord item) =>
        BusinessRecordDetails.Read<VehicleCardData>(item.DetailsJson);
    private static PolicyCardData Policy(BusinessRecord item) =>
        BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson);
    private static string Workspace(BusinessRecord item, string key, string missing) =>
        WorkspaceCardData.Read(item.DetailsJson).Fields.GetValueOrDefault(key) is { Length: > 0 } value
            ? value : missing;
}

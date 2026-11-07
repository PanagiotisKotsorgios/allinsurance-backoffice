using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using AllInsurance.Desktop.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AllInsurance.Desktop.Views;

public sealed partial class NameDaysPage : Page
{
    private readonly IApplicationDbContextFactory _factory = App.Services.GetRequiredService<IApplicationDbContextFactory>();
    private Person[] _people = [];
    private CustomerCardWindow? _customerCard;
    private PartnerCardWindow? _partnerCard;

    public NameDaysPage()
    {
        InitializeComponent();
        TableExportActions.Attach(ExportHost, this, () => new TableExportData("Εορτολόγιο",
            ["Πρόσωπο", "Είδος", "Εορτή", "Σε ημέρες", "Τηλέφωνο", "Email", "Κατάσταση"],
            (PeopleList.ItemsSource as IEnumerable<NameDayRow> ?? [])
                .Select(row => new[] { row.Name, row.Kind, row.DayDisplay, row.DaysDisplay,
                    row.Phone, row.Email, row.Status }).ToArray()));
        AllInsurance.Desktop.Controls.TableRowStriping.Attach(PeopleList);
        TypeFilter.SelectedIndex = 0;
        ModeFilter.SelectedIndex = 0;
        FromDate.Date = new DateTimeOffset(DateTime.Today);
        UntilDate.Date = new DateTimeOffset(DateTime.Today.AddDays(30));
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            var customers = await db.Customers.AsNoTracking().ToListAsync();
            var partners = await db.BusinessRecords.AsNoTracking()
                .Where(item => item.SectionKey == "partners").ToListAsync();
            _people = customers.Select(item => new Person(item.Id, "Πελάτης",
                    $"{item.LastName} {item.FirstName}".Trim(), item.NameDay,
                    item.PrimaryMobile ?? item.HomePhone, item.PrimaryEmail,
                    item.IsActive ? "Ενεργός" : "Ανενεργός"))
                .Concat(partners.Select(item =>
                {
                    var data = PartnerProfileData.Read(item.DetailsJson);
                    return new Person(item.Id, "Συνεργάτης", item.Title, data.NameDay,
                        data.Mobile ?? data.Phone, data.Email, item.Status ?? "—");
                })).ToArray();
            ApplyFilters();
        }
        catch (Exception exception)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Δεν φορτώθηκε το εορτολόγιο: " + exception.Message);
        }
    }

    private void FilterChanged(object sender, object args) => ApplyFilters();

    private void ApplyFilters()
    {
        if (PeopleList is null || TypeFilter is null || ModeFilter is null || CountText is null) return;
        var query = SearchInput?.Text.Trim() ?? "";
        var from = FromDate?.Date?.Date ?? DateTime.Today;
        var until = UntilDate?.Date?.Date ?? from.AddDays(30);
        var type = TypeFilter.SelectedIndex;
        var mode = ModeFilter.SelectedIndex;
        var rows = _people.Where(person => type == 0 || type == 1 && person.Kind == "Πελάτης" ||
                type == 2 && person.Kind == "Συνεργάτης")
            .Where(person => query.Length == 0 || person.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                (person.Phone?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (person.Email?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .Where(person => mode == 2 ? person.NameDay is null : person.NameDay is not null)
            .Select(person => new NameDayRow(person, person.NameDay is DateTime day
                ? NextDate(day, mode == 0 ? from : DateTime.Today) : null))
            .Where(row => mode != 0 || row.NextDate >= from && row.NextDate <= until)
            .OrderBy(row => row.NextDate ?? DateTime.MaxValue).ThenBy(row => row.Name).ToArray();
        PeopleList.ItemsSource = rows;
        CountText.Text = from > until && mode == 0 ? "Η ημερομηνία «Από» είναι μετά το «Έως»." :
            $"{rows.Length} πρόσωπα · {_people.Count(person => person.NameDay is null)} χωρίς καταχωρισμένη εορτή";
    }

    private static DateTime NextDate(DateTime day, DateTime from)
    {
        for (var year = from.Year; year <= from.Year + 4; year++)
            if (DateTime.DaysInMonth(year, day.Month) >= day.Day)
            {
                var candidate = new DateTime(year, day.Month, day.Day);
                if (candidate >= from.Date) return candidate;
            }
        return from.Date;
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenSelectedAsync();
    private async void PeopleList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => await OpenSelectedAsync();

    private async Task OpenSelectedAsync()
    {
        if (PeopleList.SelectedItem is not NameDayRow row)
        {
            await UiAlerts.ShowErrorAsync(XamlRoot, "Επιλέξτε πρόσωπο από τον πίνακα.");
            return;
        }
        if (row.Kind == "Πελάτης")
        {
            if (_customerCard is not null) { _customerCard.BringToFront(); return; }
            _customerCard = new CustomerCardWindow(row.Id);
            _customerCard.Closed += (_, _) => { _customerCard = null; _ = LoadAsync(); };
            _customerCard.BringToFront();
        }
        else
        {
            if (_partnerCard is not null) { _partnerCard.BringToFront(); return; }
            _partnerCard = new PartnerCardWindow(row.Id);
            _partnerCard.Closed += (_, _) => { _partnerCard = null; _ = LoadAsync(); };
            _partnerCard.BringToFront();
        }
    }

    private sealed record Person(Guid Id, string Kind, string Name, DateTime? NameDay,
        string? Phone, string? Email, string Status);

    private sealed class NameDayRow(Person person, DateTime? nextDate)
    {
        public Guid Id => person.Id;
        public string Kind => person.Kind;
        public string Name => person.Name;
        public string Phone => person.Phone ?? "—";
        public string Email => person.Email ?? "—";
        public string Status => person.Status;
        public DateTime? NextDate { get; } = nextDate;
        public string DayDisplay => NextDate?.ToString("dd/MM/yyyy") ?? "—";
        public string DaysDisplay => NextDate is DateTime date ? (date.Date - DateTime.Today).Days.ToString() : "—";
    }
}

using AllInsurance.Application.Business;
using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace AllInsurance.Desktop.Views;

internal static class DemoDataSeeder
{
    private static readonly string MarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "demo-data-v1.done");

    public static async Task SeedOnceAsync(IServiceProvider services)
    {
        var customers = services.GetRequiredService<ICustomerService>();
        var records = services.GetRequiredService<IBusinessRecordService>();
        var existingCustomers = await customers.SearchAsync();
        foreach (var (email, taxNumber) in new[] {
            ("demo@example.invalid", "900000001"),
            ("company@example.invalid", "900000002") })
        {
            var sample = existingCustomers.FirstOrDefault(item => item.PrimaryEmail == email &&
                string.IsNullOrWhiteSpace(item.TaxNumber));
            if (sample is null || existingCustomers.Any(item => item.TaxNumber == taxNumber)) continue;
            var record = await customers.GetAsync(sample.Id);
            if (record is null) continue;
            record.TaxNumber = taxNumber;
            await customers.SaveAsync(record);
        }
        if (File.Exists(MarkerPath)) return;
        Guid? sampleCustomerId = null;
        if (existingCustomers.Count == 0)
        {
            sampleCustomerId = await customers.SaveAsync(new Customer
            {
                LastName = "ΔΕΙΓΜΑ", FirstName = "Πελάτη", City = "Αθήνα",
                TaxNumber = "900000001",
                PrimaryMobile = "6900000000", PrimaryEmail = "demo@example.invalid",
                Notes = "Δοκιμαστική καταχώριση για έλεγχο της εφαρμογής.",
                OpeningBalance = 0m
            });
            await customers.SaveAsync(new Customer
            {
                LastName = "ΔΟΚΙΜΑΣΤΙΚΗ", FirstName = "Εταιρεία", City = "Πάτρα",
                TaxNumber = "900000002",
                CompanyName = "ΔΟΚΙΜΑΣΤΙΚΗ ΕΤΑΙΡΕΙΑ", CustomerKind = "Επιχείρηση",
                PrimaryMobile = "6900000001", PrimaryEmail = "company@example.invalid",
                Notes = "Δοκιμαστική καταχώριση για έλεγχο της εφαρμογής.",
                OpeningBalance = 120m, IsActive = false
            });
        }
        foreach (var section in WorkspaceSections.All.Where(item =>
            item.Key is not ("customers" or "renewals")))
        {
            if ((await records.SearchAsync(section.Key)).Count != 0) continue;
            for (var index = 1; index <= 2; index++)
                await records.SaveAsync(new BusinessRecord
                {
                    SectionKey = section.Key,
                    Title = index == 1 ? $"ΔΕΙΓΜΑ · {section.Columns[0]}" :
                        $"ΔΟΚΙΜΗ · {section.Columns[0]}",
                    Reference = $"DEMO-{index:000}",
                    RelatedParty = index == 1 ? "Δοκιμαστική καταχώριση" : "Δοκιμαστικό στοιχείο",
                    Status = index == 1 ? "Ενεργό" : "Σε εξέλιξη",
                    EventDate = DateTime.Today.AddDays(index == 1 ? 0 : -7),
                    CustomerId = section.Key is "vehicles" or "policies" or "claims" ? sampleCustomerId : null,
                    Notes = "Δοκιμαστική εγγραφή για έλεγχο της εμφάνισης και των ενεργειών."
                });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
    }
}

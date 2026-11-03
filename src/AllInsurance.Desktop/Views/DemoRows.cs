using AllInsurance.Application.Customers;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Desktop.Views;

// Visual fixtures for empty lists. These are never written to the database.
internal static class DemoRows
{
    public static CustomerListItem[] Customers() =>
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Δείγμα Πελάτη", null,
            "6900000000", "demo@example.invalid", true, 0m, DateTime.UtcNow,
            null, null, true, "Δείγμα", "Πελάτη", null, "Φυσικό πρόσωπο", "Αθήνα")
            { SearchText = "Δείγμα Πελάτη Αθήνα demo@example.invalid" },
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Δοκιμαστική Εταιρεία", null,
            "6900000001", "company@example.invalid", false, 120m, DateTime.UtcNow.AddDays(-10),
            null, null, true, "Δοκιμαστική", "Εταιρεία", "Δοκιμαστική Εταιρεία", "Επιχείρηση", "Πάτρα")
            { SearchText = "Δοκιμαστική Εταιρεία Πάτρα company@example.invalid" }
    ];

    public static BusinessRecord[] Section(WorkspaceSection section) =>
    [
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SectionKey = section.Key,
            Title = $"Δείγμα · {section.Columns[0]}",
            Reference = "DEMO-001",
            RelatedParty = section.Columns.Length > 1 ? $"Δείγμα · {section.Columns[1]}" : "Δοκιμαστικό στοιχείο",
            Status = "Ενεργό",
            EventDate = DateTime.Today,
            CreatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            SectionKey = section.Key,
            Title = $"Δοκιμαστικό · {section.Columns[0]}",
            Reference = "DEMO-002",
            RelatedParty = "Δοκιμαστική καταχώριση",
            Status = "Σε εξέλιξη",
            EventDate = DateTime.Today.AddDays(-7),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-7)
        }
    ];
}

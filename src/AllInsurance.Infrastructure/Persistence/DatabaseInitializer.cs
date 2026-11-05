using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<AllInsuranceDbContext> contextFactory,
    string databasePath) : IStartupInitializer
{
    private static readonly Guid AdministratorRoleId = Guid.Parse("a34f3189-90c9-4cec-9f7d-1a8cd4d19001");
    private static readonly Guid EmployeeRoleId = Guid.Parse("a34f3189-90c9-4cec-9f7d-1a8cd4d19002");
    private const string InitializationMutexName = @"Local\AllInsurance.Database.Initialization";

    public StartupState Initialize(CancellationToken cancellationToken = default) =>
        InitializeUnderMutex(cancellationToken);

    private StartupState InitializeUnderMutex(CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(false, InitializationMutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException("Timed out waiting for another application instance to initialize the database.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var databaseExisted = File.Exists(databasePath);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            using var db = contextFactory.CreateDbContext();

            var pendingMigrations = db.Database.GetPendingMigrations().ToArray();
            if (databaseExisted && pendingMigrations.Length > 0)
            {
                CreateMigrationBackup(db.Database.GetConnectionString()!);
            }

            db.Database.Migrate();
            EnableSqlitePragmas(db);
            EnsureRoleSeeds(db);
            EnsureCustomerCatalogSeeds(db);
            EnsurePartnerCatalogSeeds(db);
            EnsurePartnerGradeSeeds(db);
            EnsureInsuranceCatalogSeeds(db);

            if (!databaseExisted)
            {
                db.AppMetadata.Add(new AppMetadata { Key = "first_run_pending", Value = "true" });
                db.AppMetadata.Add(new AppMetadata
                {
                    Key = "installation_id",
                    Value = Guid.NewGuid().ToString("D")
                });
                db.SaveChanges();
                return StartupState.AdministratorSetupRequired;
            }

            var hasActiveAdministrator = db.Users.Any(user =>
                user.IsActive &&
                user.UserRoles.Any(membership => membership.Role.NormalizedName == "ADMINISTRATOR"));
            if (hasActiveAdministrator)
            {
                return StartupState.LoginRequired;
            }

            var firstRunMarker = db.AppMetadata.SingleOrDefault(entry => entry.Key == "first_run_pending");
            if (firstRunMarker?.Value == "true" && !db.Users.Any())
            {
                return StartupState.AdministratorSetupRequired;
            }

            if (firstRunMarker is not null)
            {
                firstRunMarker.Value = "false";
            }

            if (!db.AppMetadata.Any(entry => entry.Key == "administrator_repair_audited"))
            {
                db.SecurityEvents.Add(new SecurityEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = "ADMINISTRATOR_REPAIR_REQUIRED",
                    Outcome = "FAILURE",
                    CreatedAtUtc = DateTime.UtcNow,
                    Details = "No active administrator is available."
                });
                db.AppMetadata.Add(new AppMetadata
                {
                    Key = "administrator_repair_audited",
                    Value = "true"
                });
            }

            db.SaveChanges();
            return StartupState.RepairRequired;
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static void EnableSqlitePragmas(AllInsuranceDbContext db)
    {
        db.Database.OpenConnection();
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
            command.ExecuteNonQuery();
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static void EnsureRoleSeeds(AllInsuranceDbContext db)
    {
        var expectedRoles = new[]
        {
            new Role
            {
                Id = AdministratorRoleId,
                Name = "Administrator",
                NormalizedName = "ADMINISTRATOR",
                Description = "Full application administration"
            },
            new Role
            {
                Id = EmployeeRoleId,
                Name = "Employee",
                NormalizedName = "EMPLOYEE",
                Description = "Standard application user"
            }
        };
        foreach (var role in expectedRoles)
        {
            if (!db.Roles.Any(existing => existing.Id == role.Id))
            {
                db.Roles.Add(role);
            }
        }

        db.SaveChanges();
    }

    private static void EnsureCustomerCatalogSeeds(AllInsuranceDbContext db)
    {
        const string marker = "customer_catalog_defaults_v3";
        if (db.AppMetadata.Any(item => item.Key == marker)) return;
        var defaults = new Dictionary<string, string[]>
        {
            ["Φύλα"] = ["Άνδρας", "Γυναίκα", "Άλλο", "Δεν δηλώνεται"],
            ["Νομικές μορφές"] = ["Ατομική επιχείρηση", "Ο.Ε.", "Ε.Ε.", "Ε.Π.Ε.", "Ι.Κ.Ε.",
                "Α.Ε.", "Συνεταιρισμός", "Σωματείο", "Μη κερδοσκοπικός οργανισμός"],
            ["Οικογενειακές καταστάσεις"] = ["Άγαμος/η", "Έγγαμος/η", "Διαζευγμένος/η",
                "Χήρος/α", "Σύμφωνο συμβίωσης"],
            ["Πηγές πελατών"] = ["Σύσταση", "Ιστότοπος", "Τηλεφωνική επικοινωνία",
                "Επίσκεψη στο γραφείο", "Συνεργάτης", "Άλλο"],
            ["Τύποι ταυτότητας"] = ["Αστυνομική ταυτότητα", "Διαβατήριο", "Άδεια διαμονής",
                "Άδεια οδήγησης", "Άλλο"],
            ["Κατηγορίες διπλώματος"] = ["AM", "A1", "A2", "A", "B", "BE", "C1", "C",
                "CE", "D1", "D", "DE"],
            ["Ιθαγένειες"] = ["Ελληνική", "Κυπριακή", "Αλβανική", "Βουλγαρική",
                "Ρουμανική", "Άλλη"],
            ["ΔΟΥ"] = ["Α' Αθηνών", "ΙΓ' Αθηνών", "Α' Θεσσαλονίκης", "Πατρών",
                "Μεσολογγίου", "Αγρινίου", "Ηρακλείου", "Λάρισας", "Ιωαννίνων"],
            ["Τρόποι πληρωμής"] = ["Μετρητά", "Τραπεζική μεταφορά", "Κατάθεση σε τράπεζα",
                "Χρεωστική κάρτα (POS)", "Πιστωτική κάρτα (POS)", "Ηλεκτρονική πληρωμή",
                "IRIS", "Πάγια εντολή", "Επιταγή", "Ταχυπληρωμή", "Άλλο"],
            ["Επαγγέλματα"] = ["Ελεύθερος επαγγελματίας", "Ιδιωτικός υπάλληλος",
                "Δημόσιος υπάλληλος", "Συνταξιούχος", "Αγρότης", "Επιχειρηματίας",
                "Άνεργος/η", "Φοιτητής/τρια", "Άλλο"],
            ["Πόλεις"] = ["Αθήνα", "Θεσσαλονίκη", "Πάτρα", "Ηράκλειο", "Λάρισα",
                "Βόλος", "Ιωάννινα", "Μεσολόγγι", "Αγρίνιο"]
        };
        var cityFile = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "greek-settlements.txt");
        if (File.Exists(cityFile))
            defaults["Πόλεις"] = defaults["Πόλεις"].Concat(File.ReadLines(cityFile))
                .Select(value => value.Trim()).Where(value => value.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        var existing = db.FilterOptions.AsNoTracking()
            .Select(item => new { item.Category, item.Value }).ToList()
            .Select(item => (item.Category, item.Value))
            .ToHashSet();
        foreach (var (category, values) in defaults)
        {
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (existing.Contains((category, value))) continue;
                db.FilterOptions.Add(new FilterOption { Id = Guid.NewGuid(), Category = category,
                    Value = value, SortOrder = index });
            }
        }
        db.AppMetadata.Add(new AppMetadata { Key = marker, Value = "true" });
        db.SaveChanges();
    }

    private static void EnsurePartnerCatalogSeeds(AllInsuranceDbContext db)
    {
        const string marker = "partner_catalog_defaults_v1";
        if (db.AppMetadata.Any(item => item.Key == marker)) return;
        var defaults = new Dictionary<string, string[]>
        {
            ["Κατηγορίες συνεργατών"] = ["Ασφαλιστικός διαμεσολαβητής", "Πράκτορας",
                "Μεσίτης", "Σύμβουλος", "Συνεργαζόμενο γραφείο", "Άλλο"],
            ["Τύποι επιχειρήσεων"] = ["Ατομική επιχείρηση", "Ο.Ε.", "Ε.Ε.",
                "Ι.Κ.Ε.", "Ε.Π.Ε.", "Α.Ε.", "Άλλο"],
            ["Επαγγελματικές ιδιότητες"] = ["Ασφαλιστικός πράκτορας", "Μεσίτης ασφαλίσεων",
                "Συντονιστής ασφαλιστικών πρακτόρων", "Σύμβουλος", "Άλλο"]
        };
        var existing = db.FilterOptions.AsNoTracking()
            .Select(item => new { item.Category, item.Value }).ToList()
            .Select(item => (item.Category, item.Value)).ToHashSet();
        foreach (var (category, values) in defaults)
            for (var index = 0; index < values.Length; index++)
                if (!existing.Contains((category, values[index])))
                    db.FilterOptions.Add(new FilterOption { Id = Guid.NewGuid(), Category = category,
                        Value = values[index], SortOrder = index });
        db.AppMetadata.Add(new AppMetadata { Key = marker, Value = "true" });
        db.SaveChanges();
    }

    private static void EnsurePartnerGradeSeeds(AllInsuranceDbContext db)
    {
        const string marker = "partner_grades_v1";
        if (db.AppMetadata.Any(item => item.Key == marker)) return;
        const string category = "Κατηγορίες συνεργατών";
        var existing = db.FilterOptions.AsNoTracking().Where(item => item.Category == category)
            .Select(item => item.Value).ToHashSet();
        var grades = new[] { "Α", "Β", "Γ", "Δ", "Ε" };
        for (var index = 0; index < grades.Length; index++)
            if (!existing.Contains(grades[index]))
                db.FilterOptions.Add(new FilterOption { Id = Guid.NewGuid(), Category = category,
                    Value = grades[index], SortOrder = index });
        db.AppMetadata.Add(new AppMetadata { Key = marker, Value = "true" });
        db.SaveChanges();
    }

    private static void EnsureInsuranceCatalogSeeds(AllInsuranceDbContext db)
    {
        const string marker = "insurance_catalog_defaults_v1";
        if (db.AppMetadata.Any(item => item.Key == marker)) return;
        var categories = new Dictionary<string, string[]>
        {
            ["Κλάδοι"] = ["Αυτοκίνητο", "Πυρός / περιουσίας", "Υγεία", "Ζωή",
                "Ατυχήματα", "Αστική ευθύνη", "Σκάφη", "Πλοία", "Αεροσκάφη",
                "Μεταφορές", "Ταξιδιωτική ασφάλιση", "Νομική προστασία"],
            ["Χρήσεις οχημάτων"] = ["Ε.Ι.Χ. (επιβατικό ιδιωτικής χρήσης)",
                "Φ.Ι.Χ. (φορτηγό ιδιωτικής χρήσης)", "Φ.Δ.Χ. (φορτηγό δημόσιας χρήσης)",
                "Ταξί", "Δίκυκλο", "Αγροτικό", "Λεωφορείο", "Ενοικιαζόμενο",
                "Όχημα σχολής οδηγών", "Μηχάνημα έργου"],
            ["Καλύψεις"] = ["Αστική ευθύνη για σωματικές βλάβες τρίτων",
                "Αστική ευθύνη για υλικές ζημιές τρίτων", "Οδική βοήθεια",
                "Φροντίδα ατυχήματος", "Θραύση κρυστάλλων", "Πυρκαγιά",
                "Κλοπή ολική", "Κλοπή μερική", "Φυσικά φαινόμενα", "Πλημμύρα",
                "Σεισμός", "Ίδιες ζημιές", "Νομική προστασία", "Προσωπικό ατύχημα οδηγού",
                "Ζημιές από ανασφάλιστο όχημα", "Αστική ευθύνη από πυρκαγιά"]
        };
        var saved = db.FilterOptions.AsNoTracking().Select(item => new { item.Category, item.Value })
            .ToList().Select(item => (item.Category, item.Value)).ToHashSet();
        foreach (var (category, values) in categories)
            for (var index = 0; index < values.Length; index++)
                if (!saved.Contains((category, values[index])))
                    db.FilterOptions.Add(new FilterOption { Id = Guid.NewGuid(), Category = category,
                        Value = values[index], SortOrder = index });

        // Ενδεικτικός κατάλογος, όχι δήλωση ενεργής συνεργασίας. EAEE member list (2026)
        // https://www.eaee.gr/wp-content/uploads/2026/02/Παράρτημα-7.pdf
        var insurers = new[] { "Allianz", "Εθνική Ασφαλιστική", "INTERAMERICAN",
            "Generali Hellas", "ERGO Ασφαλιστική", "Eurolife FFH", "INTERLIFE",
            "Groupama", "Ατλαντική Ένωση", "NP Ασφαλιστική" };
        // Επίσημες σελίδες των φορέων: brokins.gr, insurancemarket.gr,
        // pricefox.gr, brokersunion.gr, megabrokers.gr.
        var agencies = new[] { "BROKINS", "Money Market / insurancemarket",
            "CompareGreece / Pricefox", "Brokers Union", "Mega Brokers" };
        SeedOrganizations(db, "companies", insurers, "companyType", "Ασφαλιστική εταιρεία", "ΑΣΦ");
        SeedOrganizations(db, "agencies", agencies, "agencyType", "Πρακτορείο", "ΠΡΑ");
        db.AppMetadata.Add(new AppMetadata { Key = marker, Value = "true" });
        db.SaveChanges();
    }

    private static void SeedOrganizations(AllInsuranceDbContext db, string section,
        string[] names, string typeField, string type, string prefix)
    {
        var existing = db.BusinessRecords.AsNoTracking().Where(item => item.SectionKey == section)
            .Select(item => item.Title).ToList().ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index];
            if (existing.Contains(name)) continue;
            db.BusinessRecords.Add(new BusinessRecord
            {
                Id = Guid.NewGuid(), SectionKey = section, Title = name,
                Reference = $"{prefix}-{index + 1:000}", Status = "Ανενεργό",
                Notes = "Ενδεικτικός κατάλογος. Επιβεβαιώστε τα στοιχεία και τη συνεργασία πριν από χρήση.",
                DetailsJson = BusinessRecordDetails.Write(new WorkspaceCardData
                {
                    Fields = new Dictionary<string, string?>
                    {
                        ["title"] = name, ["reference"] = $"{prefix}-{index + 1:000}",
                        ["status"] = "Ανενεργό", [typeField] = type,
                        ["origin"] = "Εσωτερικό"
                    }
                }),
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
        }
    }

    private static void CreateMigrationBackup(string connectionString)
    {
        var backupPath = Path.Combine(
            Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource)!,
            $"allinsurance.{DateTime.UtcNow:yyyyMMddHHmmssfff}.backup.db");
        var sourceConnection = new SqliteConnection(connectionString);
        var destinationConnection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString());
        sourceConnection.Open();
        try
        {
            destinationConnection.Open();
            sourceConnection.BackupDatabase(destinationConnection);
        }
        finally
        {
            destinationConnection.Dispose();
            sourceConnection.Dispose();
        }
    }
}

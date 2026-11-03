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
                "IRIS", "Πάγια εντολή", "Επιταγή", "Άλλο"],
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

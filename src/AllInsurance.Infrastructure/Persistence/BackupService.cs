using AllInsurance.Application.Authorization;
using AllInsurance.Application.Backups;
using Microsoft.Data.Sqlite;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class BackupService(string databasePath, ISessionService session) : IBackupService
{
    public string BackupFolder { get; } = Path.Combine(Path.GetDirectoryName(databasePath)!, "Backups");

    public Task<IReadOnlyList<BackupFile>> ListAsync(CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(BackupFolder)) return Task.FromResult<IReadOnlyList<BackupFile>>([]);
        IReadOnlyList<BackupFile> files = Directory.EnumerateFiles(BackupFolder, "allinsurance-*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file => new BackupFile(file.Name, file.FullName, file.Length, file.CreationTimeUtc))
            .ToArray();
        return Task.FromResult(files);
    }

    public async Task<BackupFile> CreateAsync(CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        return await Task.Run(() => CreateCore(cancellationToken), cancellationToken);
    }

    public async Task RestoreAsync(string fileName, CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
            !fileName.StartsWith("allinsurance-", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Μη έγκυρο αντίγραφο ασφαλείας.");
        var sourcePath = Path.GetFullPath(Path.Combine(BackupFolder, fileName));
        if (!string.Equals(Path.GetDirectoryName(sourcePath), Path.GetFullPath(BackupFolder),
            StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Το αντίγραφο ασφαλείας δεν βρέθηκε.");
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verify(sourcePath);
            CreateCore(cancellationToken); // Preserves the current database before replacement.
            SqliteConnection.ClearAllPools();
            using (var source = Open(sourcePath, SqliteOpenMode.ReadOnly))
            using (var destination = Open(databasePath, SqliteOpenMode.ReadWrite))
                source.BackupDatabase(destination);
            SqliteConnection.ClearAllPools();
            Record("BACKUP_RESTORED", fileName);
        }, cancellationToken);
    }

    public async Task<BackupContents> InspectAsync(string fileName, CancellationToken cancellationToken = default)
    {
        RequireAdministrator();
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
            !fileName.StartsWith("allinsurance-", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Μη έγκυρο αντίγραφο ασφαλείας.");
        var path = Path.GetFullPath(Path.Combine(BackupFolder, fileName));
        if (!string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(BackupFolder),
            StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new FileNotFoundException("Το αντίγραφο ασφαλείας δεν βρέθηκε.");
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verify(path);
            var counts = new Dictionary<string, long>();
            using (var connection = Open(path, SqliteOpenMode.ReadOnly))
            {
            foreach (var table in new[] { "Customers", "BusinessRecords", "CustomerTransactions",
                "PartnerPayouts", "SecurityEvents", "Users", "Roles", "FilterOptions" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$table";
                command.Parameters.AddWithValue("$table", table);
                if (Convert.ToInt64(command.ExecuteScalar()) == 0) continue;
                command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
                command.Parameters.Clear();
                counts[table] = Convert.ToInt64(command.ExecuteScalar());
            }
            if (counts.ContainsKey("BusinessRecords"))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT SectionKey, COUNT(*) FROM BusinessRecords GROUP BY SectionKey";
                using var reader = command.ExecuteReader();
                while (reader.Read()) counts["BusinessRecords:" + reader.GetString(0)] = reader.GetInt64(1);
            }
            }
            Record("BACKUP_VIEWED", fileName);
            return new BackupContents(counts, true);
        }, cancellationToken);
    }

    private BackupFile CreateCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(BackupFolder);
        var name = $"allinsurance-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db";
        var path = Path.Combine(BackupFolder, name);
        try
        {
            using (var source = Open(databasePath, SqliteOpenMode.ReadOnly))
            using (var destination = Open(path, SqliteOpenMode.ReadWriteCreate))
                source.BackupDatabase(destination);
            Verify(path);
            var file = new FileInfo(path);
            Record("BACKUP_CREATED", file.Name);
            return new BackupFile(file.Name, file.FullName, file.Length, file.CreationTimeUtc);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, DefaultTimeout = 10
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Verify(string path)
    {
        using var connection = Open(path, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        if (command.ExecuteScalar() as string != "ok")
            throw new InvalidDataException("Το αντίγραφο ασφαλείας δεν πέρασε τον έλεγχο ακεραιότητας.");
    }

    private void RequireAdministrator()
    {
        if (session.Current?.Permissions.Contains(Permission.ManageUsers) != true)
            throw new UnauthorizedAccessException("Απαιτούνται δικαιώματα διαχειριστή.");
    }

    private void Record(string eventType, string fileName)
    {
        using var connection = Open(databasePath, SqliteOpenMode.ReadWrite);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO SecurityEvents (Id, UserId, EventType, Outcome, CreatedAtUtc, Details) " +
            "VALUES ($id, CASE WHEN EXISTS (SELECT 1 FROM Users WHERE Id=$user) THEN $user ELSE NULL END, " +
            "$type, 'SUCCESS', $created, $details)";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("$user", session.Current!.UserId.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$details", "file=" + fileName +
            "; actor_username=" + session.Current.Username);
        command.ExecuteNonQuery();
    }
}

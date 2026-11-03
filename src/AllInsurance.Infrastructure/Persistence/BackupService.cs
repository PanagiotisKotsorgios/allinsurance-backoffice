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
            using var source = Open(sourcePath, SqliteOpenMode.ReadOnly);
            using var destination = Open(databasePath, SqliteOpenMode.ReadWrite);
            source.BackupDatabase(destination);
            SqliteConnection.ClearAllPools();
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
            using var source = Open(databasePath, SqliteOpenMode.ReadOnly);
            using var destination = Open(path, SqliteOpenMode.ReadWriteCreate);
            source.BackupDatabase(destination);
            Verify(path);
            var file = new FileInfo(path);
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
}

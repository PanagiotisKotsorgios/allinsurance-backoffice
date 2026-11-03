namespace AllInsurance.Application.Backups;

public sealed record BackupFile(string FileName, string FullPath, long SizeBytes, DateTime CreatedUtc);

public interface IBackupService
{
    string BackupFolder { get; }
    Task<IReadOnlyList<BackupFile>> ListAsync(CancellationToken cancellationToken = default);
    Task<BackupFile> CreateAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string fileName, CancellationToken cancellationToken = default);
}

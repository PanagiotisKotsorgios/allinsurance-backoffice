using AllInsurance.Application.Backups;
using Microsoft.UI.Xaml;
using System.Text.Json;

namespace AllInsurance.Desktop.Logging;

internal sealed record BackupScheduleSettings(int IntervalDays, DateTime? LastRunUtc)
{
    public static string PathOnDisk => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "backup-schedule.json");

    public static BackupScheduleSettings Load()
    {
        try { return File.Exists(PathOnDisk)
            ? JsonSerializer.Deserialize<BackupScheduleSettings>(File.ReadAllText(PathOnDisk)) ?? new(0, null)
            : new(0, null); }
        catch (Exception) { return new(0, null); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
        File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(this));
    }
}

internal sealed class BackupScheduler : IDisposable
{
    private readonly IBackupService _backups;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _running;

    public BackupScheduler(IBackupService backups)
    {
        _backups = backups;
        _timer.Tick += Tick;
        _timer.Start();
        _ = CheckAsync();
    }

    private void Tick(object? sender, object e) => _ = CheckAsync();

    private async Task CheckAsync()
    {
        if (_running) return;
        var settings = BackupScheduleSettings.Load();
        if (settings.IntervalDays < 1 ||
            settings.LastRunUtc is DateTime last && DateTime.UtcNow - last < TimeSpan.FromDays(settings.IntervalDays)) return;
        _running = true;
        try
        {
            await _backups.CreateAsync();
            (settings with { LastRunUtc = DateTime.UtcNow }).Save();
        }
        catch (Exception) { /* Retry on the next timer tick; the existing backup is never replaced. */ }
        finally { _running = false; }
    }

    public void Dispose() => _timer.Stop();
}

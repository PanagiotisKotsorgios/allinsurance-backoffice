using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace AllInsurance.Desktop.Logging;

public sealed record LocalErrorEntry(DateTime TimestampUtc, string Category,
    string Message, string? Exception);

public static class LocalErrorLogStore
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "Logs");
    public static string PathOnDisk => Path.Combine(Folder, "errors.jsonl");

    public static IReadOnlyList<LocalErrorEntry> Recent(int limit = 300)
    {
        var paths = new[] { PathOnDisk + ".1", PathOnDisk }.Where(File.Exists).ToArray();
        if (paths.Length == 0) return [];
        return paths.SelectMany(File.ReadLines).TakeLast(limit)
            .Select(line =>
            {
                try { return JsonSerializer.Deserialize<LocalErrorEntry>(line); }
                catch (JsonException) { return null; }
            })
            .Where(entry => entry is not null).Cast<LocalErrorEntry>()
            .OrderByDescending(entry => entry.TimestampUtc).ToArray();
    }
}

public sealed class LocalErrorLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    public ILogger CreateLogger(string categoryName) => new LocalErrorLogger(this, categoryName);
    public void Dispose() { }

    private void Write(LocalErrorEntry entry)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(LocalErrorLogStore.Folder);
                var path = LocalErrorLogStore.PathOnDisk;
                if (File.Exists(path) && new FileInfo(path).Length > 5_000_000)
                    File.Move(path, path + ".1", true);
                File.AppendAllText(path, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private sealed class LocalErrorLogger(LocalErrorLoggerProvider provider, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(new LocalErrorEntry(DateTime.UtcNow, category,
                formatter(state, exception), exception?.ToString()));
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}

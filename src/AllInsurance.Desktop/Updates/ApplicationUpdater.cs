using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace AllInsurance.Desktop.Updates;

internal static class ApplicationUpdater
{
    private const string ReleaseUrl = "https://api.github.com/repos/PanagiotisKotsorgios/allinsurance-backoffice/releases/latest";
    private const string AssetName = "allinsurance-backoffice-win-x64.zip";
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllInsurance");

    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    internal static async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var release = await client.GetFromJsonAsync<GitHubRelease>(ReleaseUrl, cancellationToken)
            ?? throw new InvalidOperationException("Δεν ήταν δυνατή η ανάγνωση της νέας έκδοσης.");
        var tag = release.TagName.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var version))
            throw new InvalidOperationException("Η δημοσιευμένη έκδοση έχει μη έγκυρο αριθμό.");
        if (version <= CurrentVersion) return null;
        var asset = release.Assets.FirstOrDefault(item => item.Name == AssetName);
        if (asset is null || asset.Size <= 0 || asset.Size > 500_000_000 ||
            !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com" ||
            !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
            asset.Digest.Length != 71)
            throw new InvalidOperationException("Το πακέτο αναβάθμισης ή το αποτύπωμα SHA-256 λείπει από το GitHub Release.");
        return new AvailableUpdate(version, url, asset.Digest[7..].ToLowerInvariant(), release.HtmlUrl);
    }

    internal static async Task<string> DownloadAndInstallAsync(AvailableUpdate update,
        CancellationToken cancellationToken = default)
    {
        var versionName = update.Version.ToString();
        var versionsDirectory = Path.Combine(Root, "AppVersions");
        var target = Path.Combine(versionsDirectory, versionName);
        var executable = Path.Combine(target, "AllInsurance.Desktop.exe");
        if (File.Exists(executable)) return executable;

        var updatesDirectory = Path.Combine(Root, "Updates");
        Directory.CreateDirectory(updatesDirectory);
        Directory.CreateDirectory(versionsDirectory);
        var archivePath = Path.Combine(updatesDirectory, versionName + ".zip");
        var staging = Path.Combine(versionsDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var client = CreateClient())
            using (var response = await client.GetAsync(update.DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 500_000_000)
                    throw new InvalidDataException("Το πακέτο αναβάθμισης είναι υπερβολικά μεγάλο.");
                await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var local = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1024 * 1024];
                long total = 0;
                int read;
                while ((read = await remote.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > 500_000_000) throw new InvalidDataException("Το πακέτο αναβάθμισης είναι υπερβολικά μεγάλο.");
                    hash.AppendData(buffer, 0, read);
                    await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(actual), Convert.FromHexString(update.Sha256)))
                    throw new InvalidDataException("Ο έλεγχος ακεραιότητας της αναβάθμισης απέτυχε.");
            }

            Directory.CreateDirectory(staging);
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                long expanded = 0;
                if (zip.Entries.Count > 10_000) throw new InvalidDataException("Το πακέτο έχει υπερβολικά πολλά αρχεία.");
                foreach (var entry in zip.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = entry.FullName.Replace('\\', '/');
                    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (path.StartsWith('/') || parts.Length == 0 ||
                        parts.Any(part => part is "." or ".." ||
                            part.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
                            part.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
                            part.Equals("RecordFiles", StringComparison.OrdinalIgnoreCase)) ||
                        path.Contains(':') || Path.IsPathRooted(path) ||
                        (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000 ||
                        Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(path).Equals(".sqlite", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Το πακέτο περιέχει μη επιτρεπτή διαδρομή ή δεδομένα χρήστη.");
                    expanded += entry.Length;
                    if (expanded > 1_500_000_000) throw new InvalidDataException("Το πακέτο είναι υπερβολικά μεγάλο.");
                    var destination = Path.GetFullPath(Path.Combine(staging, path));
                    if (!destination.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Μη επιτρεπτή διαδρομή στο πακέτο.");
                    if (path.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination);
                }
            }
            if (!File.Exists(Path.Combine(staging, "AllInsurance.Desktop.exe")))
                throw new InvalidDataException("Το εκτελέσιμο αρχείο λείπει από το πακέτο.");
            if (Directory.Exists(target)) throw new IOException("Ο φάκελος της έκδοσης υπάρχει ήδη.");
            Directory.Move(staging, target);
            return executable;
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    internal static void SwitchShortcutAndRestart(string executable, Version previousVersion)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "AllInsurance Backoffice Software.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Δεν είναι διαθέσιμη η υπηρεσία συντομεύσεων των Windows.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        var priorTarget = shortcut.TargetPath;
        var priorWorkingDirectory = shortcut.WorkingDirectory;
        var priorIcon = shortcut.IconLocation;
        try
        {
            shortcut.TargetPath = executable;
            shortcut.WorkingDirectory = Path.GetDirectoryName(executable)!;
            shortcut.IconLocation = Path.Combine(Path.GetDirectoryName(executable)!, "Assets", "allinsurance.ico") + ",0";
            shortcut.Description = "AllInsurance Backoffice Software";
            shortcut.Save();
            Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                ArgumentList = { "--updated-from=" + previousVersion }
            });
        }
        catch
        {
            shortcut.TargetPath = priorTarget;
            shortcut.WorkingDirectory = priorWorkingDirectory;
            shortcut.IconLocation = priorIcon;
            shortcut.Save();
            throw;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AllInsuranceBackoffice", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string Digest { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}

internal sealed record AvailableUpdate(Version Version, Uri DownloadUrl, string Sha256, string ReleaseUrl);

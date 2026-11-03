using System.Net.Http.Json;
using System.Text.Json;
using Windows.Security.Credentials;

namespace AllInsurance.Desktop.Views;

internal sealed class CommunicationGateway
{
    private const string VaultResource = "AllInsurance.Backoffice.Communications";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "Settings", "communications.json");

    public sealed class Settings
    {
        public string EmailSender { get; set; } = "";
        public string EmailSenderName { get; set; } = "AllInsurance Backoffice";
        public string SmsSender { get; set; } = "";
    }

    public Settings ReadSettings()
    {
        if (!File.Exists(SettingsFile)) return new Settings();
        return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile)) ?? new Settings();
    }

    public void SaveSettings(Settings settings, string? brevoKey, string? bulkerKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings));
        if (!string.IsNullOrWhiteSpace(brevoKey)) SaveSecret("brevo", brevoKey.Trim());
        if (!string.IsNullOrWhiteSpace(bulkerKey)) SaveSecret("bulker", bulkerKey.Trim());
    }

    public bool IsConfigured(bool email)
    {
        var settings = ReadSettings();
        return GetSecret(email ? "brevo" : "bulker") is not null &&
            !string.IsNullOrWhiteSpace(email ? settings.EmailSender : settings.SmsSender);
    }

    public async Task<string> SendEmailAsync(string recipient, string name, string subject, string html)
    {
        var settings = ReadSettings();
        var key = GetSecret("brevo") ?? throw new InvalidOperationException("Δεν έχει οριστεί κλειδί Brevo.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", key);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = settings.EmailSender, name = settings.EmailSenderName },
            to = new[] { new { email = recipient, name } },
            subject, htmlContent = html
        });
        using var response = await Client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Brevo HTTP {(int)response.StatusCode}: {TrimResponse(result)}");
        return "Brevo: " + TrimResponse(result);
    }

    public async Task<string> SendSmsAsync(string recipient, string body)
    {
        var settings = ReadSettings();
        var key = GetSecret("bulker") ?? throw new InvalidOperationException("Δεν έχει οριστεί κλειδί Bulker SMS.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.bulker.gr/http/sms.php");
        var digits = new string(recipient.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "30" + digits;
        if (digits.Length < 11 || digits.Length > 15)
            throw new ArgumentException("Ο αριθμός κινητού δεν έχει έγκυρη διεθνή μορφή.");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["auth_key"] = key, ["from"] = settings.SmsSender,
            ["to"] = digits, ["text"] = body, ["id"] = "no"
        });
        using var response = await Client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Bulker HTTP {(int)response.StatusCode}: {TrimResponse(result)}");
        if (string.IsNullOrWhiteSpace(result) || result.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            result.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            result.Contains("failed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Το Bulker επέστρεψε αποτυχία: " + TrimResponse(result));
        return "Bulker: " + TrimResponse(result);
    }

    private static string TrimResponse(string response) => response.Length > 240 ? response[..240] : response;

    private static void SaveSecret(string name, string value)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(VaultResource, name)); }
        catch (Exception) { /* No previously saved credential. */ }
        vault.Add(new PasswordCredential(VaultResource, name, value));
    }

    private static string? GetSecret(string name)
    {
        try
        {
            var item = new PasswordVault().Retrieve(VaultResource, name);
            item.RetrievePassword();
            return item.Password;
        }
        catch (Exception) { return null; }
    }
}

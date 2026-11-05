using System.Text.Json;

namespace AllInsurance.Desktop.Settings;

public sealed class AgencyProfileSettings
{
    private static readonly string ProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "agency-profile.json");

    public string Name { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string LicenseNumber { get; set; } = "";

    public static AgencyProfileSettings Load()
    {
        if (!File.Exists(ProfilePath)) return new();
        return JsonSerializer.Deserialize<AgencyProfileSettings>(File.ReadAllText(ProfilePath)) ?? new();
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Συμπληρώστε την επωνυμία του πρακτορείου.");
        if (!string.IsNullOrWhiteSpace(TaxNumber) &&
            (TaxNumber.Length != 9 || !TaxNumber.All(char.IsAsciiDigit)))
            throw new ArgumentException("Το ΑΦΜ πρακτορείου πρέπει να έχει 9 ψηφία.");
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public void EnsureReceiptReady()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(TaxNumber) ||
            string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(City))
            throw new InvalidOperationException("Συμπληρώστε επωνυμία, ΑΦΜ, διεύθυνση και πόλη στις Ρυθμίσεις προφίλ πριν εξαγάγετε απόδειξη.");
    }
}

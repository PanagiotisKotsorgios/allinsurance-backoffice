using System.Security.Cryptography;
using System.Text;

namespace AllInsurance.Application.Exports;

/// <summary>Short, portable names; the descriptive title and filters remain inside the export.</summary>
public static class ExportFileName
{
    public static string Create(string title, DateTime timestamp)
    {
        var kind = Category(title);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title ?? "")))[..6];
        return $"KSS-{kind}-{timestamp:yyyyMMdd-HHmmss}-{digest}";
    }

    private static string Category(string? title)
    {
        var value = (title ?? "").ToUpperInvariant();
        if (value.Contains("ΠΑΡΑΓΩΓ")) return "PROD";
        if (value.Contains("ΑΝΑΦΟΡ")) return "RPT";
        if (value.Contains("ΠΕΛΑΤ")) return "CUST";
        if (value.Contains("ΣΥΝΕΡΓΑΤ")) return "PART";
        if (value.Contains("ΟΧΗΜ")) return "VEH";
        if (value.Contains("ΑΝΑΝΕΩ")) return "REN";
        if (value.Contains("ΑΣΦΑΛΙΣΤΗΡ")) return "POL";
        if (value.Contains("ΖΗΜΙ")) return "CLM";
        if (value.Contains("ΕΙΣΠΡΑ")) return "REC";
        if (value.Contains("ΠΛΗΡΩΜ")) return "PAY";
        return "DATA";
    }
}

using System.Globalization;
using System.Text;
using AllInsurance.Domain.Entities;

namespace AllInsurance.Application.Bridges;

public enum NativeBridgeSource { Ydrogeios, Orizon }

public sealed record NativeCoverage(string Code, decimal SumInsured, decimal Deductible,
    decimal Premium);

public sealed record NativePolicyRow(
    int Line, string PolicyNumber, string? PreviousPolicyNumber, string CustomerTaxNumber,
    string CustomerName, string? Registration, string? Make, string BranchCode,
    string UseCode, string PartnerCode, DateTime IssueDate, DateTime StartDate,
    DateTime EndDate, decimal NetPremium, decimal GrossPremium, bool IsCancellation,
    IReadOnlyList<NativeCoverage> Coverages);

/// <summary>Reader for the two supplied positional formats, not a generic insurer CSV.</summary>
public static class NativePolicyBridge
{
    public static PolicyCardData ToPolicyData(NativePolicyRow row, Guid companyId, string company,
        string branch, string use, IReadOnlyDictionary<string, string> coverageNames,
        Guid? partnerId, Guid? vehicleId, bool isRenewal, Guid? previousPolicyId)
    {
        if (row.Coverages.Any(item => !coverageNames.ContainsKey(item.Code)))
            throw new ArgumentException("Λείπει αντιστοίχιση κάλυψης.");
        return new PolicyCardData
        {
            CompanyId = companyId, Insurer = company, Branch = branch,
            PolicyNumber = row.PolicyNumber,
            RenewalNumber = isRenewal ? row.PolicyNumber : null,
            PreviousPolicyNumber = isRenewal ? row.PreviousPolicyNumber : null,
            PreviousPolicyId = isRenewal ? previousPolicyId : null,
            RenewalDate = isRenewal ? row.StartDate : null,
            IssueDate = row.IssueDate, StartDate = row.StartDate, EndDate = row.EndDate,
            NetPremium = Math.Abs(row.NetPremium), GrossPremium = Math.Abs(row.GrossPremium),
            CancellationDate = row.IsCancellation ? row.IssueDate : null,
            CancellationReason = row.IsCancellation ? "Ακύρωση από γέφυρα " + company : null,
            VehicleId = vehicleId, PartnerId = partnerId,
            CoverageNotes = row.Registration is null ? null :
                $"Πινακίδα αρχείου: {row.Registration} · Χρήση: {use}",
            Coverages = row.Coverages.Select(item => new PolicyCoverage
            {
                Name = coverageNames[item.Code], SumInsured = Math.Abs(item.SumInsured),
                Deductible = Math.Abs(item.Deductible), Premium = Math.Abs(item.Premium)
            }).ToList()
        };
    }

    public static IReadOnlyList<NativePolicyRow> Read(string path, NativeBridgeSource source)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = source == NativeBridgeSource.Ydrogeios
            ? Encoding.GetEncoding(737, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
            : new UTF8Encoding(false, true);
        var separator = source == NativeBridgeSource.Ydrogeios ? ',' : ';';
        var result = new List<NativePolicyRow>();
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: false);
        string? line;
        var number = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = Split(line, separator);
            if (fields.Count < 61 || fields[0].Length != 10 ||
                !fields[0].All(char.IsAsciiDigit) || fields[19].Length != 9 ||
                !fields[19].All(char.IsAsciiDigit) ||
                (source == NativeBridgeSource.Ydrogeios && fields[9] != "0000*") ||
                (source == NativeBridgeSource.Orizon &&
                    (fields[9].Length != 8 || !fields[9].All(char.IsAsciiDigit))))
                throw new FormatException($"Η γραμμή {number} δεν ταιριάζει στη γραμμογράφηση {source}.");
            var coverageStart = source == NativeBridgeSource.Ydrogeios ? 61 : 62;
            var coverages = new List<NativeCoverage>();
            for (var index = coverageStart; index + 3 < fields.Count; index += 4)
                if (fields[index].Length == 3 && fields[index].All(char.IsAsciiDigit) &&
                    fields[index] != "000") coverages.Add(new NativeCoverage(fields[index],
                    Amount(fields[index + 1], number), Amount(fields[index + 2], number),
                    Amount(fields[index + 3], number)));
            var previous = fields[1] is { } value && value.Any(character => character != '0')
                ? value : null;
            result.Add(new NativePolicyRow(number, fields[0], previous, fields[19],
                fields[12].Trim(), fields[27].Trim() is { Length: > 0 } plate ? plate : null,
                fields[34].Trim() is { Length: > 0 } make ? make : null,
                fields[16].Trim(), fields[28].Trim(), fields[10].Trim(),
                Date(fields[5], number), Date(fields[6], number), Date(fields[7], number),
                Amount(fields[52], number), Amount(fields[59], number),
                fields[8] == "99" || fields[3] == "2" && fields[59].StartsWith('-'),
                coverages.GroupBy(item => item.Code).Select(group => group.First()).ToArray()));
        }
        if (result.Count == 0) throw new FormatException("Το αρχείο δεν περιέχει ασφαλιστήρια.");
        return result;
    }

    private static DateTime Date(string text, int line) =>
        DateTime.TryParseExact(text, "ddMMyyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) ? value :
            throw new FormatException($"Μη έγκυρη ημερομηνία στη γραμμή {line}.");

    private static decimal Amount(string text, int line) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
            out var cents) ? cents / 100m :
            throw new FormatException($"Μη έγκυρο ποσό στη γραμμή {line}.");

    private static IReadOnlyList<string> Split(string line, char separator)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == separator && !quoted)
            { values.Add(value.ToString().Trim()); value.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new FormatException("Το αρχείο περιέχει μη κλεισμένα εισαγωγικά.");
        values.Add(value.ToString().Trim());
        return values;
    }
}

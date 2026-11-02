using System.Security.Cryptography;
using System.Text;
using AllInsurance.Application.Abstractions;

namespace AllInsurance.Infrastructure.Security;

public sealed class CryptographicRecoveryCodeGenerator : IRecoveryCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public GeneratedRecoveryCode Create()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(20);
        var canonical = EncodeBase32(randomBytes);
        var grouped = string.Join('-', Enumerable.Range(0, 8).Select(index => canonical.Substring(index * 4, 4)));
        return new GeneratedRecoveryCode(grouped, ComputeDigest(canonical));
    }

    public bool Verify(string code, string digest)
    {
        var canonical = Normalize(code);
        if (canonical.Length != 32 || canonical.Any(character => !Alphabet.Contains(character, StringComparison.Ordinal)))
        {
            return false;
        }

        byte[] expectedDigest;
        try
        {
            expectedDigest = Convert.FromHexString(digest);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedDigest.Length != 32)
        {
            return false;
        }

        var actualDigest = Convert.FromHexString(ComputeDigest(canonical));
        return CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest);
    }

    private static string ComputeDigest(string canonicalCode)
    {
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(canonicalCode));
        return Convert.ToHexString(digest);
    }

    private static string Normalize(string code) =>
        new(code.Where(character => character != '-' && !char.IsWhiteSpace(character))
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string EncodeBase32(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder(32);
        var buffer = 0;
        var bitsInBuffer = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                result.Append(Alphabet[(buffer >> (bitsInBuffer - 5)) & 31]);
                bitsInBuffer -= 5;
            }
        }

        return result.ToString();
    }
}

using AllInsurance.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AllInsurance.Infrastructure.Security;

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new(
        Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 210_000
        }));
    private static readonly object HashUser = new();

    public string Hash(string password) => _hasher.HashPassword(HashUser, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(HashUser, passwordHash, password) != PasswordVerificationResult.Failed;
}

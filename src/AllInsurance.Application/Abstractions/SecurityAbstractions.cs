namespace AllInsurance.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public sealed record GeneratedRecoveryCode(string Code, string Digest);

public interface IRecoveryCodeGenerator
{
    GeneratedRecoveryCode Create();
    bool Verify(string code, string digest);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

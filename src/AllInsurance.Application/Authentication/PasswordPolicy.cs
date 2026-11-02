namespace AllInsurance.Application.Authentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 1024;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return $"Use a password or passphrase with at least {MinimumLength} characters.";
        }

        if (password.Length > MaximumLength)
        {
            return $"Passwords cannot exceed {MaximumLength} characters.";
        }

        return null;
    }
}

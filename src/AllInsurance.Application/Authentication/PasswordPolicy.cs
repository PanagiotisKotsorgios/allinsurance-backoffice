namespace AllInsurance.Application.Authentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 1024;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return $"Χρησιμοποιήστε κωδικό πρόσβασης με τουλάχιστον {MinimumLength} χαρακτήρες.";
        }

        if (password.Length > MaximumLength)
        {
            return $"Ο κωδικός πρόσβασης δεν μπορεί να ξεπερνά τους {MaximumLength} χαρακτήρες.";
        }

        return null;
    }
}

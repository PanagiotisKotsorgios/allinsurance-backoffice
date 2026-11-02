namespace AllInsurance.Application.Authentication;

public sealed class AuthenticationPolicyOptions
{
    public int MaximumFailedAttempts { get; init; } = 5;
    public int LockoutDurationMinutes { get; init; } = 15;

    public TimeSpan GetLockoutDuration()
    {
        if (MaximumFailedAttempts < 1)
        {
            throw new InvalidOperationException("Maximum failed attempts must be at least one.");
        }

        if (LockoutDurationMinutes < 1)
        {
            throw new InvalidOperationException("Lockout duration must be at least one minute.");
        }

        return TimeSpan.FromMinutes(LockoutDurationMinutes);
    }
}

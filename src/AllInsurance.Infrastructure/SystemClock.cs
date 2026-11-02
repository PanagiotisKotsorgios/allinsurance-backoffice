using AllInsurance.Application.Abstractions;

namespace AllInsurance.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

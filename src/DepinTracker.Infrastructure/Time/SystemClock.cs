namespace DepinTracker.Infrastructure.Time;

using DepinTracker.Application.Abstractions;

/// <summary>The production <see cref="IClock"/> backed by the system UTC clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

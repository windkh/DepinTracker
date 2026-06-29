namespace DepinTracker.Tests.TestSupport;

using DepinTracker.Application.Abstractions;

/// <summary>A fixed, controllable clock so time-dependent logic is deterministic in tests.</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTimeOffset? now = null) =>
        UtcNow = now ?? new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; }
}

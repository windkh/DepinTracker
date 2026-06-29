namespace DepinTracker.Application.Abstractions;

/// <summary>
/// Abstraction over the current time. Injected everywhere a timestamp is needed so
/// calculations stay deterministic and testable rather than calling DateTime.Now.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

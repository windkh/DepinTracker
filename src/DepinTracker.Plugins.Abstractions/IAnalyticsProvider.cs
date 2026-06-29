namespace DepinTracker.Plugins.Abstractions;

using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Computes an analytics result from a set of rewards. Providers are pure
/// functions over the supplied <see cref="AnalyticsRequest"/> and contribute
/// named results the dashboard and reports can render.
/// </summary>
public interface IAnalyticsProvider
{
    string ProviderKey { get; }

    /// <summary>Stable key of the analytic this provider produces (e.g. "rewards-by-month").</summary>
    string AnalyticsKey { get; }

    Task<AnalyticsResult> ComputeAsync(AnalyticsRequest request, CancellationToken cancellationToken);
}

namespace DepinTracker.Plugins.Abstractions.Models;

/// <summary>A single labelled data point in an analytics series.</summary>
public sealed record AnalyticsPoint(string Label, decimal Value);

/// <summary>
/// The output of an analytics provider: a named series of points plus optional
/// scalar headline metrics. Deliberately generic so new analytics can be added
/// as plugins without changing the host.
/// </summary>
public sealed record AnalyticsResult(
    string Key,
    string Title,
    IReadOnlyList<AnalyticsPoint> Series,
    IReadOnlyDictionary<string, decimal> Metrics);

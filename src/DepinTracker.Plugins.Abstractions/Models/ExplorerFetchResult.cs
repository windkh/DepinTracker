namespace DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// The outcome of an explorer fetch: the parsed rewards plus the verbatim raw
/// payload and a description of the request, so the host can persist full
/// provenance (raw response + import session) for auditability.
/// <see cref="Truncated"/> is <c>true</c> when the explorer stopped before the
/// remote source was exhausted (e.g. hit a page cap); the host surfaces this to
/// the user so silently-incomplete imports are impossible.
/// </summary>
public sealed record ExplorerFetchResult(
    string RequestDescription,
    string RawBody,
    IReadOnlyList<ExplorerReward> Rewards,
    bool Truncated = false);

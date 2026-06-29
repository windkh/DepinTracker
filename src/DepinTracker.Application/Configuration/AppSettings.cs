namespace DepinTracker.Application.Configuration;

/// <summary>
/// User-facing application settings, loaded from <c>config/appsettings.json</c>.
/// Kept as a plain options object (no framework coupling) so the Application layer
/// stays infrastructure-agnostic.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Currency the user reads reports/portfolio in (e.g. "EUR").</summary>
    public string ReportingCurrency { get; set; } = "EUR";

    /// <summary>Currency token prices are sourced in before FX conversion (e.g. "USD").</summary>
    public string PriceCurrency { get; set; } = "USD";

    /// <summary>Default reward classification when no classifier has an opinion.</summary>
    public string DefaultRewardKind { get; set; } = "Reward";
}

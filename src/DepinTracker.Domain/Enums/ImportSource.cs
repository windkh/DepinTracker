namespace DepinTracker.Domain.Enums;

/// <summary>How the data in an import session was obtained.</summary>
public enum ImportSource
{
    Manual = 0,
    OnChain = 1,
    Csv = 2,
}

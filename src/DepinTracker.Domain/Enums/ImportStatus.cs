namespace DepinTracker.Domain.Enums;

/// <summary>Lifecycle state of an import session.</summary>
public enum ImportStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}

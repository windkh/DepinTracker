namespace DepinTracker.Application.Dtos;

/// <summary>How many imported rows a remove or clear operation deleted.</summary>
public sealed record RemovalResult(int Rewards, int Disposals)
{
    public int Total => Rewards + Disposals;
}

namespace DepinTracker.Application.Services;

using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// Single source of truth for "what counts as taxable reward income", shared by the
/// dashboard, the tax report and the FIFO cost-basis lots so they can never drift apart.
///
/// A reward is income when it is a genuine DePIN payout received into a wallet:
/// <list type="bullet">
/// <item>Spam, internal transfers and fees are never income.</item>
/// <item>When a project defines an allowed-source list, only transfers from those reward
/// distributor addresses are income. Transfers from any other sender (e.g. swap or sale
/// proceeds landing back in the wallet) are disposals/other movements — a swap is not
/// income — and are excluded. An empty list means "count every incoming transfer".</item>
/// </list>
/// The check is applied at read time, so editing the allowed-source list changes the
/// figures without deleting or re-importing any data.
/// </summary>
public static class IncomeClassifier
{
    public static bool IsIncome(
        RewardTransaction reward,
        IReadOnlyDictionary<Guid, Guid> walletToProject,
        IReadOnlyDictionary<Guid, HashSet<string>> allowedByProject)
    {
        if (reward.Kind is RewardKind.Spam or RewardKind.Transfer or RewardKind.Fee)
        {
            return false;
        }

        if (walletToProject.TryGetValue(reward.WalletId, out var projectId) &&
            allowedByProject.TryGetValue(projectId, out var allowed) && allowed.Count > 0)
        {
            return reward.FromAddress is { } from && allowed.Contains(from);
        }

        return true;
    }

    /// <summary>Filters <paramref name="rewards"/> to income only, given all wallets and projects.</summary>
    public static List<RewardTransaction> FilterIncome(
        IEnumerable<RewardTransaction> rewards,
        IEnumerable<Wallet> wallets,
        IEnumerable<Project> projects)
    {
        var (walletToProject, allowedByProject) = BuildMaps(wallets, projects);
        return rewards.Where(r => IsIncome(r, walletToProject, allowedByProject)).ToList();
    }

    public static (Dictionary<Guid, Guid> WalletToProject, Dictionary<Guid, HashSet<string>> AllowedByProject)
        BuildMaps(IEnumerable<Wallet> wallets, IEnumerable<Project> projects)
    {
        var walletToProject = wallets.ToDictionary(w => w.Id, w => w.ProjectId);
        var allowedByProject = projects.ToDictionary(
            p => p.Id,
            p => p.RewardSourceAddresses
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
        return (walletToProject, allowedByProject);
    }
}

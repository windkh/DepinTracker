namespace DepinTracker.Tests.Application;

using DepinTracker.Application.Dtos;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;
using DepinTracker.Infrastructure.Persistence.Repositories;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

public class RewardImportServiceTests
{
    [Fact]
    public async Task Manual_import_persists_a_reward_with_provenance()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);

        var result = await service.ImportManualAsync(
            walletId, "ETH", 1.5m, new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
            txHash: "0xabc", RewardKind.Reward, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Imported.Should().Be(1);

        var stored = await rewards.GetByWalletAsync(walletId, CancellationToken.None);
        stored.Should().ContainSingle();
        stored[0].ImportSessionId.Should().NotBe(Guid.Empty, "every reward records the import session that produced it");
        stored[0].ProviderKey.Should().Be("manual");
    }

    [Fact]
    public async Task Re_importing_the_same_reward_is_skipped_not_overwritten()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);

        var date = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var first = await service.ImportManualAsync(walletId, "ETH", 1.5m, date, "0xabc", RewardKind.Reward, CancellationToken.None);
        var second = await service.ImportManualAsync(walletId, "ETH", 1.5m, date, "0xabc", RewardKind.Reward, CancellationToken.None);

        first.Imported.Should().Be(1);
        second.Imported.Should().Be(0);
        second.Skipped.Should().Be(1);
        (await rewards.CountAsync(CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task Remove_deletes_only_the_selected_reward_and_disposal()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);
        var dispositions = new DispositionRepository(store.Factory);
        var date = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

        await service.ImportManualAsync(walletId, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);
        await service.ImportManualAsync(walletId, "ETH", 2m, date, "0x2", RewardKind.Reward, CancellationToken.None);
        await service.AddManualDispositionAsync(walletId, "ETH", 0.5m, date, 2000m, "EUR", "0x3", DispositionKind.Sale, null, CancellationToken.None);
        await service.AddManualDispositionAsync(walletId, "ETH", 0.25m, date, null, null, "0x4", DispositionKind.TransferOut, null, CancellationToken.None);

        var rewardToRemove = (await rewards.GetByWalletAsync(walletId, CancellationToken.None)).Single(r => r.Amount == 1m);
        var disposalToRemove = (await dispositions.GetAllAsync(CancellationToken.None)).Single(d => d.Amount == 0.5m);

        var removed = await service.RemoveAsync(new[] { rewardToRemove.Id }, new[] { disposalToRemove.Id }, CancellationToken.None);

        removed.Should().Be(new RemovalResult(1, 1));
        (await rewards.GetByWalletAsync(walletId, CancellationToken.None)).Should().ContainSingle(r => r.Amount == 2m);
        (await dispositions.GetAllAsync(CancellationToken.None)).Should().ContainSingle(d => d.Amount == 0.25m);
    }

    [Fact]
    public async Task Removed_reward_can_be_imported_again()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);
        var date = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

        await service.ImportManualAsync(walletId, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);
        var stored = (await rewards.GetByWalletAsync(walletId, CancellationToken.None)).Single();
        await service.RemoveAsync(new[] { stored.Id }, Array.Empty<Guid>(), CancellationToken.None);

        var again = await service.ImportManualAsync(walletId, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);

        again.Imported.Should().Be(1, "removing a row also frees its dedup key");
    }

    [Fact]
    public async Task Clear_for_a_project_keeps_other_projects_and_unassigned_disposals()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);
        var dispositions = new DispositionRepository(store.Factory);
        var projects = new ProjectRepository(store.Factory);
        var wallets = new WalletRepository(store.Factory);
        var other = new Project { Name = "Other" };
        await projects.AddAsync(other, CancellationToken.None);
        var otherWallet = new Wallet { ProjectId = other.Id, BlockchainKey = "polygon", Address = "0xOther" };
        await wallets.AddAsync(otherWallet, CancellationToken.None);
        var date = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

        await service.ImportManualAsync(walletId, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);
        await service.ImportManualAsync(otherWallet.Id, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);
        await service.AddManualDispositionAsync(walletId, "ETH", 0.5m, date, null, null, "0x2", DispositionKind.Sale, null, CancellationToken.None);
        await service.AddManualDispositionAsync(null, "ETH", 0.5m, date, null, null, "0x3", DispositionKind.Sale, null, CancellationToken.None);

        var projectId = (await wallets.GetAsync(walletId, CancellationToken.None))!.ProjectId;
        var cleared = await service.ClearAsync(projectId, CancellationToken.None);

        cleared.Should().Be(new RemovalResult(1, 1));
        (await rewards.CountAsync(CancellationToken.None)).Should().Be(1);
        (await dispositions.GetAllAsync(CancellationToken.None)).Should().ContainSingle(d => d.WalletId == null);
    }

    [Fact]
    public async Task Clear_for_all_projects_also_drops_unassigned_disposals()
    {
        using var store = new TempStore();
        var (service, rewards, walletId) = await BuildAsync(store);
        var dispositions = new DispositionRepository(store.Factory);
        var date = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

        await service.ImportManualAsync(walletId, "ETH", 1m, date, "0x1", RewardKind.Reward, CancellationToken.None);
        await service.AddManualDispositionAsync(walletId, "ETH", 0.5m, date, null, null, "0x2", DispositionKind.Sale, null, CancellationToken.None);
        await service.AddManualDispositionAsync(null, "ETH", 0.5m, date, null, null, "0x3", DispositionKind.Sale, null, CancellationToken.None);

        var cleared = await service.ClearAsync(null, CancellationToken.None);

        cleared.Should().Be(new RemovalResult(1, 2));
        (await rewards.CountAsync(CancellationToken.None)).Should().Be(0);
        (await dispositions.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    private static async Task<(RewardImportService Service, RewardRepository Rewards, Guid WalletId)> BuildAsync(TempStore store)
    {
        var projects = new ProjectRepository(store.Factory);
        var wallets = new WalletRepository(store.Factory);
        var rewards = new RewardRepository(store.Factory);
        var sessions = new ImportSessionRepository(store.Factory);
        var raw = new RawResponseRepository(store.Factory);
        var dispositions = new DispositionRepository(store.Factory);

        var project = new Project { Name = "Test" };
        await projects.AddAsync(project, CancellationToken.None);
        var wallet = new Wallet { ProjectId = project.Id, BlockchainKey = "polygon", Address = "0xWallet" };
        await wallets.AddAsync(wallet, CancellationToken.None);

        var service = new RewardImportService(
            rewards, sessions, raw, wallets, projects, dispositions,
            Array.Empty<IBlockchainExplorer>(), Array.Empty<IRewardClassifier>(),
            store.Clock, NullLogger<RewardImportService>.Instance);

        return (service, rewards, wallet.Id);
    }
}

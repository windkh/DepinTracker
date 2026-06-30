namespace DepinTracker.Tests.Application;

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

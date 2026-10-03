namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Dtos;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;
using DepinTracker.Domain.ValueObjects;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Imports reward transactions, manually or from an on-chain explorer. Every import
/// is wrapped in an <see cref="ImportSession"/> for provenance, on-chain payloads are
/// stored verbatim as <see cref="RawProviderResponse"/>, and writes are insert-or-ignore
/// by dedup key so previously imported financial data is never overwritten.
/// </summary>
public sealed class RewardImportService
{
    private const string ManualProviderKey = "manual";

    private readonly IRewardRepository _rewards;
    private readonly IImportSessionRepository _sessions;
    private readonly IRawResponseRepository _rawResponses;
    private readonly IWalletRepository _wallets;
    private readonly IProjectRepository _projects;
    private readonly IDispositionRepository _dispositions;
    private readonly IReadOnlyList<IBlockchainExplorer> _explorers;
    private readonly IReadOnlyList<IRewardClassifier> _classifiers;
    private readonly IClock _clock;
    private readonly ILogger<RewardImportService> _logger;

    public RewardImportService(
        IRewardRepository rewards,
        IImportSessionRepository sessions,
        IRawResponseRepository rawResponses,
        IWalletRepository wallets,
        IProjectRepository projects,
        IDispositionRepository dispositions,
        IEnumerable<IBlockchainExplorer> explorers,
        IEnumerable<IRewardClassifier> classifiers,
        IClock clock,
        ILogger<RewardImportService> logger)
    {
        _rewards = rewards;
        _sessions = sessions;
        _rawResponses = rawResponses;
        _wallets = wallets;
        _projects = projects;
        _dispositions = dispositions;
        _explorers = explorers.ToList();
        _classifiers = classifiers.ToList();
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Wipes every imported reward, raw response, and import session belonging to
    /// the given project's wallets. The project and wallets themselves are kept
    /// (use <c>ProjectService.DeleteAsync</c> for that). Returns the reward row count.
    /// </summary>
    public async Task<int> ClearForProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var cleared = await ClearAsync(projectId, cancellationToken).ConfigureAwait(false);
        return cleared.Rewards;
    }

    /// <summary>
    /// Wipes imported rewards and disposals for one project's wallets, or for every wallet
    /// when <paramref name="projectId"/> is null (which also drops disposals not linked to
    /// a wallet). Projects and wallets are kept.
    /// </summary>
    public async Task<RemovalResult> ClearAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var wallets = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var ids = wallets.Where(w => projectId is null || w.ProjectId == projectId.Value).Select(w => w.Id).ToList();

        var rewards = await _rewards.DeleteAllForWalletsAsync(ids, cancellationToken).ConfigureAwait(false);
        var disposals = await _dispositions.DeleteForWalletsAsync(ids, cancellationToken).ConfigureAwait(false);
        if (projectId is null)
        {
            disposals += await _dispositions.DeleteUnassignedAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Cleared imported data for {Scope}: {RewardCount} reward(s) + {DisposalCount} disposal(s) across {WalletCount} wallet(s)",
            projectId?.ToString() ?? "all projects", rewards, disposals, ids.Count);
        return new RemovalResult(rewards, disposals);
    }

    /// <summary>
    /// Removes individual rewards and disposals the user picked. On-chain rewards come
    /// back on the next import of their wallet, since their dedup key is gone with them.
    /// </summary>
    public async Task<RemovalResult> RemoveAsync(
        IEnumerable<Guid> rewardIds, IEnumerable<Guid> disposalIds, CancellationToken cancellationToken)
    {
        var rewards = await _rewards.DeleteByIdsAsync(rewardIds, cancellationToken).ConfigureAwait(false);
        var disposals = await _dispositions.DeleteByIdsAsync(disposalIds, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Removed {RewardCount} reward(s) and {DisposalCount} disposal(s) by user request", rewards, disposals);
        return new RemovalResult(rewards, disposals);
    }

    /// <summary>
    /// Persists a manually-entered disposal (sale / swap / spend / transfer-out / loss).
    /// Wrapped in its own import session so provenance is preserved; dedup is keyed on
    /// wallet/chain/txhash/symbol/amount/timestamp so re-entering the same row is skipped.
    /// </summary>
    public async Task<ImportResult> AddManualDispositionAsync(
        Guid? walletId,
        string tokenSymbol,
        decimal amount,
        DateTimeOffset timestampUtc,
        decimal? proceedsPerUnit,
        string? proceedsCurrency,
        string? txHash,
        DepinTracker.Domain.Enums.DispositionKind kind,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol)) throw new ArgumentException("Token symbol is required.", nameof(tokenSymbol));
        if (amount <= 0m) throw new ArgumentException("Disposal amount must be positive.", nameof(amount));

        string? chainKey = null;
        if (walletId is { } wid)
        {
            var wallet = await _wallets.GetAsync(wid, cancellationToken).ConfigureAwait(false);
            chainKey = wallet?.BlockchainKey;
        }

        var session = await StartSessionAsync("manual", ImportSource.Manual, walletId, cancellationToken).ConfigureAwait(false);

        var record = new DispositionRecord
        {
            WalletId = walletId,
            BlockchainKey = chainKey,
            TxHash = txHash?.Trim() ?? string.Empty,
            TimestampUtc = timestampUtc,
            TokenSymbol = tokenSymbol.Trim().ToUpperInvariant(),
            Amount = amount,
            ProceedsPerUnit = proceedsPerUnit,
            ProceedsCurrency = string.IsNullOrWhiteSpace(proceedsCurrency) ? null : proceedsCurrency!.Trim().ToUpperInvariant(),
            Kind = kind,
            ProviderKey = "manual",
            ImportSessionId = session.Id,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            CreatedUtc = _clock.UtcNow,
        };

        var inserted = await _dispositions.AddIgnoreDuplicatesAsync(record, cancellationToken).ConfigureAwait(false);
        return await CompleteSessionAsync(session, inserted, 1 - inserted, truncated: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Imports a single, manually-entered reward.</summary>
    public async Task<ImportResult> ImportManualAsync(
        Guid walletId,
        string tokenSymbol,
        decimal amount,
        DateTimeOffset timestampUtc,
        string? txHash,
        RewardKind kind,
        CancellationToken cancellationToken)
    {
        var wallet = await _wallets.GetAsync(walletId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Wallet {walletId} not found.");

        var session = await StartSessionAsync(ManualProviderKey, ImportSource.Manual, walletId, cancellationToken)
            .ConfigureAwait(false);

        var reward = new RewardTransaction
        {
            WalletId = walletId,
            BlockchainKey = wallet.BlockchainKey,
            TxHash = txHash?.Trim() ?? string.Empty,
            TimestampUtc = timestampUtc,
            TokenSymbol = tokenSymbol.Trim().ToUpperInvariant(),
            Amount = amount,
            Kind = kind,
            ProviderKey = ManualProviderKey,
            ImportSessionId = session.Id,
            CreatedUtc = _clock.UtcNow,
        };

        var inserted = await _rewards
            .AddManyIgnoreDuplicatesAsync(new[] { reward }, cancellationToken)
            .ConfigureAwait(false);

        return await CompleteSessionAsync(session, inserted, 1 - inserted, truncated: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports rewards for every active wallet in turn, optionally restricted to a
    /// single project. Each wallet runs through the same single-wallet path so
    /// provenance and dedupe are preserved per session; failures on one wallet do
    /// not stop the others.
    /// </summary>
    public async Task<ImportResult> ImportAllActiveAsync(Guid? projectId, DateRange? range, CancellationToken cancellationToken)
    {
        var wallets = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var active = wallets
            .Where(w => w.IsActive && (projectId is null || w.ProjectId == projectId.Value))
            .ToList();
        if (active.Count == 0)
        {
            return new ImportResult(Guid.Empty, 0, 0, Success: true, "No active wallets.");
        }

        var imported = 0;
        var skipped = 0;
        var failures = new List<string>();

        foreach (var wallet in active)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ImportFromChainAsync(wallet.Id, range, cancellationToken).ConfigureAwait(false);
            imported += result.Imported;
            skipped += result.Skipped;
            if (!result.Success)
            {
                failures.Add($"{wallet.Label ?? wallet.Address}: {result.Message}");
            }
        }

        var ok = failures.Count == 0;
        var message = ok
            ? $"Imported {imported} across {active.Count} wallet(s), skipped {skipped} duplicate(s)."
            : $"Imported {imported} ({failures.Count}/{active.Count} wallet(s) failed): {string.Join("; ", failures)}";

        return new ImportResult(Guid.Empty, imported, skipped, ok, message);
    }

    /// <summary>Imports rewards for a wallet from the explorer that supports its chain.</summary>
    public async Task<ImportResult> ImportFromChainAsync(
        Guid walletId, DateRange? range, CancellationToken cancellationToken)
    {
        var wallet = await _wallets.GetAsync(walletId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Wallet {walletId} not found.");

        var explorer = _explorers.FirstOrDefault(e => e.Supports(wallet.BlockchainKey))
            ?? throw new InvalidOperationException(
                $"No explorer plugin supports blockchain '{wallet.BlockchainKey}'.");

        var session = await StartSessionAsync(
            explorer.ProviderKey, ImportSource.OnChain, walletId, cancellationToken).ConfigureAwait(false);

        try
        {
            var fetch = await explorer
                .FetchRewardsAsync(wallet.BlockchainKey, wallet.Address, range, cancellationToken)
                .ConfigureAwait(false);

            var rawResponse = new RawProviderResponse
            {
                ImportSessionId = session.Id,
                ProviderKey = explorer.ProviderKey,
                RequestDescription = fetch.RequestDescription,
                ResponseBody = fetch.RawBody,
                RetrievedUtc = _clock.UtcNow,
            };
            await _rawResponses.AddAsync(rawResponse, cancellationToken).ConfigureAwait(false);

            // Per-project allow-list of legitimate source addresses. Empty list = accept everything.
            // Case-insensitive: EVM addresses are case-insensitive by spec, and Solana base58
            // collisions across cases are astronomically improbable.
            var project = await _projects.GetAsync(wallet.ProjectId, cancellationToken).ConfigureAwait(false);
            var allowedSources = project?.RewardSourceAddresses
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rewards = new List<RewardTransaction>(fetch.Rewards.Count);
            var rejectedBySource = 0;
            var spamCount = 0;
            foreach (var item in fetch.Rewards)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (allowedSources.Count > 0)
                {
                    var from = item.FromAddress;
                    if (from is null || !allowedSources.Contains(from))
                    {
                        rejectedBySource++;
                        continue;
                    }
                }

                var kind = await ClassifyAsync(wallet.BlockchainKey, item, cancellationToken).ConfigureAwait(false);

                // Mark obvious scam-airdrop tokens so they are visually separable and can be
                // excluded downstream. Only override "plain" classifications, never a specific
                // one a classifier deliberately assigned.
                if ((kind == RewardKind.Reward || kind == RewardKind.Unknown) &&
                    SpamHeuristics.IsLikelySpam(item.TokenSymbol))
                {
                    kind = RewardKind.Spam;
                    spamCount++;
                }
                rewards.Add(new RewardTransaction
                {
                    WalletId = walletId,
                    BlockchainKey = wallet.BlockchainKey,
                    FromAddress = item.FromAddress,
                    TxHash = item.TxHash,
                    BlockNumber = item.BlockNumber,
                    TimestampUtc = item.TimestampUtc,
                    TokenSymbol = item.TokenSymbol.Trim().ToUpperInvariant(),
                    TokenContract = item.TokenContract,
                    Amount = item.Amount,
                    Kind = kind,
                    ProviderKey = explorer.ProviderKey,
                    ImportSessionId = session.Id,
                    RawResponseId = rawResponse.Id,
                    CreatedUtc = _clock.UtcNow,
                });
            }

            if (rejectedBySource > 0)
            {
                _logger.LogInformation(
                    "Import filter rejected {Count} transfer(s) not from an allowed source address for project {Project}",
                    rejectedBySource, project?.Name ?? wallet.ProjectId.ToString());
            }

            if (spamCount > 0)
            {
                _logger.LogInformation(
                    "Flagged {Count} imported transfer(s) as likely spam/scam airdrops for project {Project}",
                    spamCount, project?.Name ?? wallet.ProjectId.ToString());
            }

            var inserted = await _rewards
                .AddManyIgnoreDuplicatesAsync(rewards, cancellationToken)
                .ConfigureAwait(false);

            return await CompleteSessionAsync(
                session, inserted, rewards.Count - inserted, fetch.Truncated, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "On-chain import failed for wallet {WalletId}", walletId);
            session.Status = ImportStatus.Failed;
            session.CompletedUtc = _clock.UtcNow;
            session.Message = ex.Message;
            await _sessions.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            return new ImportResult(session.Id, 0, 0, Success: false, ex.Message);
        }
    }

    private async Task<RewardKind> ClassifyAsync(
        string blockchainKey, ExplorerReward reward, CancellationToken cancellationToken)
    {
        foreach (var classifier in _classifiers)
        {
            var kind = await classifier.ClassifyAsync(blockchainKey, reward, cancellationToken).ConfigureAwait(false);
            if (kind != RewardKind.Unknown)
            {
                return kind;
            }
        }

        return reward.Kind == RewardKind.Unknown ? RewardKind.Reward : reward.Kind;
    }

    private async Task<ImportSession> StartSessionAsync(
        string providerKey, ImportSource source, Guid? walletId, CancellationToken cancellationToken)
    {
        var session = new ImportSession
        {
            ProviderKey = providerKey,
            Source = source,
            WalletId = walletId,
            Status = ImportStatus.Running,
            StartedUtc = _clock.UtcNow,
        };
        await _sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    private async Task<ImportResult> CompleteSessionAsync(
        ImportSession session, int imported, int skipped, bool truncated, CancellationToken cancellationToken)
    {
        session.Status = ImportStatus.Completed;
        session.CompletedUtc = _clock.UtcNow;
        session.ItemsImported = imported;
        session.ItemsSkipped = skipped;
        session.Message = truncated
            ? $"Imported {imported}, skipped {skipped} duplicate(s). MORE DATA EXISTS — page cap hit (raise Providers.BlockscoutMaxPages)."
            : $"Imported {imported}, skipped {skipped} duplicate(s).";
        await _sessions.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        return new ImportResult(session.Id, imported, skipped, Success: true, session.Message);
    }
}

namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Dtos;
using DepinTracker.Domain.Enums;
using DepinTracker.Domain.ValueObjects;

/// <summary>
/// Assembles the dashboard summary: portfolio value, counts, missing-price count,
/// sync state, database health and the rewards-over-time series. Composes the
/// repositories, <see cref="ValuationService"/> and <see cref="AnalyticsService"/>.
/// </summary>
public sealed class DashboardService
{
    private readonly IProjectRepository _projects;
    private readonly IWalletRepository _wallets;
    private readonly IRewardRepository _rewards;
    private readonly IImportSessionRepository _sessions;
    private readonly ValuationService _valuation;
    private readonly AnalyticsService _analytics;
    private readonly AppSettings _settings;

    public DashboardService(
        IProjectRepository projects,
        IWalletRepository wallets,
        IRewardRepository rewards,
        IImportSessionRepository sessions,
        ValuationService valuation,
        AnalyticsService analytics,
        AppSettings settings)
    {
        _projects = projects;
        _wallets = wallets;
        _rewards = rewards;
        _sessions = sessions;
        _valuation = valuation;
        _analytics = analytics;
        _settings = settings;
    }

    public async Task<DashboardSummary> GetSummaryAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var projects = await _projects.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var wallets = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allRewards = await _rewards.GetAllAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Domain.Entities.Wallet> scopedWallets = wallets;
        IReadOnlyList<Domain.Entities.RewardTransaction> rewards = allRewards;
        if (projectId is { } pid)
        {
            scopedWallets = wallets.Where(w => w.ProjectId == pid).ToList();
            var walletIds = scopedWallets.Select(w => w.Id).ToHashSet();
            rewards = allRewards.Where(r => walletIds.Contains(r.WalletId)).ToList();
        }

        // Only DePIN reward *income* feeds the dashboard's figures — see IncomeClassifier
        // (shared with the tax report + FIFO). Applied at read time, so editing a project's
        // allowed-source list updates the dashboard without deleting or re-importing data.
        var incomeRewards = IncomeClassifier.FilterIncome(rewards, wallets, projects);
        var valuations = await _valuation.ValueManyAsync(incomeRewards, cancellationToken).ConfigureAwait(false);

        var currency = _settings.ReportingCurrency;
        var portfolio = valuations
            .Where(v => v.HasPrice)
            .Aggregate(Money.Zero(currency), (acc, v) => acc.Add(v.Value!.Value));

        var missingPrices = valuations.Count(v => !v.HasPrice);
        var byMonth = _analytics.ComputeRewardsByMonth(valuations);
        var byYear = _analytics.ComputeRewardsByYear(valuations);
        var tokensByMonth = _analytics.ComputeTokensByMonth(incomeRewards);
        var tokenBreakdown = _analytics.ComputeTokenBreakdown(valuations, currency);

        var recent = await _sessions.GetRecentAsync(1, cancellationToken).ConfigureAwait(false);
        var syncState = recent.Count == 0
            ? "No imports yet"
            : recent[0].Status == ImportStatus.Completed
                ? $"Last import {recent[0].CompletedUtc:yyyy-MM-dd HH:mm} UTC"
                : $"Last import: {recent[0].Status}";

        var dbHealth = missingPrices == 0 ? "Healthy" : $"{missingPrices} reward(s) missing prices";

        return new DashboardSummary(
            PortfolioValue: portfolio,
            RewardCount: incomeRewards.Count,
            WalletCount: projectId is null ? wallets.Count : scopedWallets.Count,
            ProjectCount: projectId is null ? projects.Count : 1,
            MissingPriceCount: missingPrices,
            SyncState: syncState,
            DatabaseHealth: dbHealth,
            RewardsOverTime: byMonth,
            RewardsByYear: byYear,
            TokensByMonth: tokensByMonth,
            TokenBreakdown: tokenBreakdown);
    }
}

namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Dtos;
using DepinTracker.Domain.Entities;

/// <summary>
/// Matches disposals against reward lots using First-In-First-Out order, per
/// token symbol. The reward stream is the lot stream: the first acquired
/// quantity is the first consumed when something is disposed. Each match
/// records per-unit cost basis (reward fiat value ÷ amount) and per-unit
/// proceeds (disposal sale price), so the report can show a realised gain
/// alongside a holding-period flag (DE §23 EStG: tax-free after one year).
///
/// The matcher is deterministic and pure: it does no I/O of its own; callers
/// pass already-loaded rewards + their valuations + the disposals. This keeps
/// it trivially unit-testable and lets the tax report compose it freely.
/// </summary>
public sealed class FifoMatcher
{
    private readonly AppSettings _settings;

    public FifoMatcher(AppSettings settings) => _settings = settings;

    /// <summary>
    /// Walks every <c>tokenSymbol</c> and pairs disposals against acquired lots
    /// in FIFO order. <paramref name="valuationsByReward"/> supplies the per-lot
    /// fiat value used as the cost basis; lots without a valuation contribute
    /// a null cost basis (the match still records the quantity consumed).
    /// </summary>
    public IReadOnlyList<DisposalMatch> Match(
        IEnumerable<RewardTransaction> rewards,
        IReadOnlyDictionary<Guid, RewardValuation> valuationsByReward,
        IEnumerable<DispositionRecord> dispositions)
    {
        var currency = _settings.ReportingCurrency;
        var matches = new List<DisposalMatch>();

        // Group both sides by token (case-insensitive) so disposals of token X
        // only consume lots of token X.
        var lotsByToken = rewards
            .OrderBy(r => r.TimestampUtc)
            .GroupBy(r => r.TokenSymbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new Queue<RemainingLot>(g.Select(r =>
            {
                valuationsByReward.TryGetValue(r.Id, out var v);
                return new RemainingLot(r, r.Amount, v);
            })), StringComparer.OrdinalIgnoreCase);

        var disposalsByToken = dispositions
            .OrderBy(d => d.TimestampUtc)
            .GroupBy(d => d.TokenSymbol, StringComparer.OrdinalIgnoreCase);

        foreach (var group in disposalsByToken)
        {
            if (!lotsByToken.TryGetValue(group.Key, out var queue))
            {
                // Disposal of a token we never acquired — emit a single
                // unmatched record so the report still surfaces the proceeds
                // (cost basis = null → gain = null).
                foreach (var d in group)
                {
                    matches.Add(BuildUnmatchedDisposal(d, currency));
                }
                continue;
            }

            foreach (var disposal in group)
            {
                var remaining = disposal.Amount;
                while (remaining > 0m && queue.Count > 0)
                {
                    var lot = queue.Peek();
                    var take = Math.Min(remaining, lot.RemainingQuantity);
                    var basisPerUnit = lot.CostBasisPerUnit;
                    var proceedsPerUnit = NormalisedProceedsPerUnit(disposal, currency);
                    var longTerm = (disposal.TimestampUtc - lot.Reward.TimestampUtc) >= TimeSpan.FromDays(365);
                    var gain = basisPerUnit is { } b && proceedsPerUnit is { } p
                        ? take * (p - b)
                        : (decimal?)null;

                    matches.Add(new DisposalMatch(
                        Disposal: disposal,
                        Lot: lot.Reward,
                        Quantity: take,
                        CostBasisPerUnit: basisPerUnit,
                        ProceedsPerUnit: proceedsPerUnit,
                        ReportingCurrency: currency,
                        LongTerm: longTerm,
                        RealisedGain: gain));

                    lot.RemainingQuantity -= take;
                    remaining -= take;
                    if (lot.RemainingQuantity <= 0m)
                    {
                        queue.Dequeue();
                    }
                }

                if (remaining > 0m)
                {
                    // Over-disposed: more was disposed than we have lots for. Emit
                    // a residual unmatched row so the user sees the discrepancy.
                    var leftover = new DispositionRecord
                    {
                        Id = disposal.Id,
                        TokenSymbol = disposal.TokenSymbol,
                        Amount = remaining,
                        TimestampUtc = disposal.TimestampUtc,
                        ProceedsPerUnit = disposal.ProceedsPerUnit,
                        ProceedsCurrency = disposal.ProceedsCurrency,
                        Kind = disposal.Kind,
                        WalletId = disposal.WalletId,
                        BlockchainKey = disposal.BlockchainKey,
                        TxHash = disposal.TxHash,
                        Notes = disposal.Notes,
                    };
                    matches.Add(BuildUnmatchedDisposal(leftover, currency));
                }
            }
        }

        return matches;
    }

    private static DisposalMatch BuildUnmatchedDisposal(DispositionRecord disposal, string currency)
    {
        // Synthesise a "phantom lot" so the result type stays homogeneous —
        // callers see Lot==phantom and Quantity == disposal.Amount with
        // CostBasis null. They can filter on that to surface the warning.
        var phantom = new RewardTransaction
        {
            Id = Guid.Empty,
            WalletId = disposal.WalletId ?? Guid.Empty,
            BlockchainKey = disposal.BlockchainKey ?? string.Empty,
            TokenSymbol = disposal.TokenSymbol,
            Amount = 0m,
            TimestampUtc = disposal.TimestampUtc, // not really "acquired" — but the field must be set
        };
        return new DisposalMatch(
            Disposal: disposal,
            Lot: phantom,
            Quantity: disposal.Amount,
            CostBasisPerUnit: null,
            ProceedsPerUnit: NormalisedProceedsPerUnit(disposal, currency),
            ReportingCurrency: currency,
            LongTerm: false,
            RealisedGain: null);
    }

    /// <summary>
    /// Returns the disposal's per-unit proceeds expressed in the reporting currency.
    /// When the proceeds currency differs (e.g. swap recorded as USD vs report EUR)
    /// we currently keep the raw value — historical FX conversion of proceeds is
    /// future work; <c>null</c> is returned for disposals that don't record a price.
    /// </summary>
    private static decimal? NormalisedProceedsPerUnit(DispositionRecord disposal, string reportingCurrency)
    {
        if (disposal.ProceedsPerUnit is not { } pu) return null;

        if (string.IsNullOrWhiteSpace(disposal.ProceedsCurrency) ||
            string.Equals(disposal.ProceedsCurrency, reportingCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return pu;
        }

        // Different currency than the report: leave it raw and let the caller
        // decide. The detail row still shows the currency code.
        return pu;
    }

    private sealed class RemainingLot
    {
        public RemainingLot(RewardTransaction reward, decimal remaining, RewardValuation? valuation)
        {
            Reward = reward;
            RemainingQuantity = remaining;
            // Cost basis per unit = (reward fiat value) / (reward amount). Guard
            // against division by zero on degenerate rows.
            CostBasisPerUnit = valuation is { Value: { } money } && reward.Amount > 0m
                ? money.Amount / reward.Amount
                : (decimal?)null;
        }

        public RewardTransaction Reward { get; }
        public decimal RemainingQuantity { get; set; }
        public decimal? CostBasisPerUnit { get; }
    }
}

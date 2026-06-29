namespace DepinTracker.Application.Services;

using DepinTracker.Application.Configuration;
using DepinTracker.Application.Dtos;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.ValueObjects;

/// <summary>
/// Values reward transactions into the user's reporting currency. The math is
/// deterministic and exact (decimal): <c>fiat = amount × unitPrice × fxRate</c>,
/// where the price is sourced in <see cref="AppSettings.PriceCurrency"/> and then
/// converted to <see cref="AppSettings.ReportingCurrency"/>. A missing price or FX
/// rate yields a valuation with no <see cref="RewardValuation.Value"/> rather than
/// a fabricated zero, so gaps stay visible.
/// </summary>
public sealed class ValuationService
{
    private readonly PriceEngine _priceEngine;
    private readonly ExchangeRateEngine _fxEngine;
    private readonly AppSettings _settings;

    public ValuationService(PriceEngine priceEngine, ExchangeRateEngine fxEngine, AppSettings settings)
    {
        _priceEngine = priceEngine;
        _fxEngine = fxEngine;
        _settings = settings;
    }

    public async Task<RewardValuation> ValueAsync(RewardTransaction reward, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(reward.TimestampUtc.UtcDateTime);
        var priceCurrency = _settings.PriceCurrency;
        var reportingCurrency = _settings.ReportingCurrency;

        var unitPrice = await _priceEngine
            .GetPriceAsync(reward.TokenSymbol, reward.BlockchainKey, reward.TokenContract, date, priceCurrency, cancellationToken)
            .ConfigureAwait(false);

        if (unitPrice is not { } price)
        {
            return new RewardValuation(reward, Value: null, UnitPrice: null, PriceCurrency: priceCurrency, ExchangeRate: null);
        }

        var fxRate = await _fxEngine
            .GetRateAsync(priceCurrency, reportingCurrency, date, cancellationToken)
            .ConfigureAwait(false);

        if (fxRate is not { } rate)
        {
            // We have a price but cannot convert to the reporting currency.
            return new RewardValuation(reward, Value: null, UnitPrice: price, PriceCurrency: priceCurrency, ExchangeRate: null);
        }

        var fiat = new Money(reward.Amount * price * rate, reportingCurrency);
        return new RewardValuation(reward, fiat, price, priceCurrency, rate);
    }

    public async Task<IReadOnlyList<RewardValuation>> ValueManyAsync(
        IEnumerable<RewardTransaction> rewards, CancellationToken cancellationToken)
    {
        var results = new List<RewardValuation>();
        foreach (var reward in rewards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ValueAsync(reward, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}

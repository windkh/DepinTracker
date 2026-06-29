namespace DepinTracker.Tests.Application;

using DepinTracker.Application.Configuration;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class ValuationServiceTests
{
    private static readonly AppSettings Settings = new() { PriceCurrency = "USD", ReportingCurrency = "EUR" };
    private static readonly DateOnly Date = new(2025, 1, 2);

    [Fact]
    public async Task Values_reward_as_amount_times_price_times_fx()
    {
        var service = BuildService(price: 2m, fx: 0.9m);
        var reward = Reward(amount: 10m);

        var result = await service.ValueAsync(reward, CancellationToken.None);

        result.HasPrice.Should().BeTrue();
        result.Value!.Value.Currency.Should().Be("EUR");
        result.Value!.Value.Amount.Should().Be(18m); // 10 * 2 * 0.9
        result.UnitPrice.Should().Be(2m);
        result.ExchangeRate.Should().Be(0.9m);
    }

    [Fact]
    public async Task Missing_price_yields_no_value_rather_than_zero()
    {
        var service = BuildService(price: null, fx: 0.9m);

        var result = await service.ValueAsync(Reward(10m), CancellationToken.None);

        result.HasPrice.Should().BeFalse();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Price_present_but_missing_fx_yields_no_value()
    {
        var service = BuildService(price: 2m, fx: null);

        var result = await service.ValueAsync(Reward(10m), CancellationToken.None);

        result.HasPrice.Should().BeFalse();
        result.UnitPrice.Should().Be(2m); // price was found…
        result.Value.Should().BeNull();    // …but no conversion was possible
    }

    private static RewardTransaction Reward(decimal amount) => new()
    {
        TokenSymbol = "ETH",
        Amount = amount,
        TimestampUtc = new DateTimeOffset(Date, TimeOnly.MinValue, TimeSpan.Zero),
    };

    private static ValuationService BuildService(decimal? price, decimal? fx)
    {
        var priceProvider = new Mock<IPriceProvider>();
        priceProvider.SetupGet(p => p.Priority).Returns(1);
        priceProvider.Setup(p => p.CanResolve(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(true);
        priceProvider.Setup(p => p.GetHistoricalPriceAsync(
                "ETH", It.IsAny<string?>(), It.IsAny<string?>(), Date, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(price);

        var fxProvider = new Mock<IExchangeRateProvider>();
        fxProvider.SetupGet(p => p.Priority).Returns(1);
        fxProvider.Setup(p => p.GetRateAsync("USD", "EUR", Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fx);

        var clock = new TestClock();
        var priceEngine = new PriceEngine(
            new[] { priceProvider.Object }, new InMemoryPriceCache(), clock, NullLogger<PriceEngine>.Instance);
        var fxEngine = new ExchangeRateEngine(
            new[] { fxProvider.Object }, new InMemoryExchangeRateCache(), clock, NullLogger<ExchangeRateEngine>.Instance);

        return new ValuationService(priceEngine, fxEngine, Settings);
    }
}

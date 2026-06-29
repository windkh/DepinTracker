namespace DepinTracker.Tests.Domain;

using DepinTracker.Domain.ValueObjects;
using FluentAssertions;

public class ValueObjectTests
{
    [Fact]
    public void Money_Add_same_currency_sums_amounts()
    {
        var result = new Money(10m, "EUR").Add(new Money(5.5m, "EUR"));
        result.Amount.Should().Be(15.5m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Money_Add_different_currency_throws()
    {
        var act = () => new Money(10m, "EUR").Add(new Money(5m, "USD"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TokenAmount_ValueAt_multiplies_by_unit_price()
    {
        var money = new TokenAmount(3m, "ETH").ValueAt(2000m, "USD");
        money.Amount.Should().Be(6000m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void DateRange_Year_covers_whole_year_and_Contains_works()
    {
        var year = DateRange.Year(2025);
        year.Start.Should().Be(new DateOnly(2025, 1, 1));
        year.End.Should().Be(new DateOnly(2025, 12, 31));
        year.Contains(new DateOnly(2025, 6, 15)).Should().BeTrue();
        year.Contains(new DateOnly(2026, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public void DateRange_rejects_end_before_start()
    {
        var act = () => new DateRange(new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 1));
        act.Should().Throw<ArgumentException>();
    }
}

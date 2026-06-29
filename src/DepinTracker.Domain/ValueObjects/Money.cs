namespace DepinTracker.Domain.ValueObjects;

/// <summary>
/// An immutable fiat money amount in a specific currency. Uses <see cref="decimal"/>
/// for exact financial arithmetic (never floating point). Arithmetic across
/// different currencies is rejected to keep valuations deterministic and auditable.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount - other.Amount };
    }

    public Money Multiply(decimal factor) => this with { Amount = Amount * factor };

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot combine money of different currencies: '{Currency}' and '{other.Currency}'.");
        }
    }

    public override string ToString() => $"{Amount:0.########} {Currency}";
}

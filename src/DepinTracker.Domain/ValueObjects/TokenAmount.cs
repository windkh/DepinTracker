namespace DepinTracker.Domain.ValueObjects;

/// <summary>
/// An immutable quantity of a token, identified by its symbol. Uses
/// <see cref="decimal"/> for exact arithmetic. A <see cref="TokenAmount"/> can be
/// valued into <see cref="Money"/> by multiplying by a unit price.
/// </summary>
public readonly record struct TokenAmount(decimal Value, string Symbol)
{
    public static TokenAmount Zero(string symbol) => new(0m, symbol);

    public TokenAmount Add(TokenAmount other)
    {
        EnsureSameSymbol(other);
        return this with { Value = Value + other.Value };
    }

    /// <summary>Values this token amount at a unit price expressed in <paramref name="currency"/>.</summary>
    public Money ValueAt(decimal unitPrice, string currency) => new(Value * unitPrice, currency);

    private void EnsureSameSymbol(TokenAmount other)
    {
        if (!string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot combine token amounts of different symbols: '{Symbol}' and '{other.Symbol}'.");
        }
    }

    public override string ToString() => $"{Value:0.########} {Symbol}";
}

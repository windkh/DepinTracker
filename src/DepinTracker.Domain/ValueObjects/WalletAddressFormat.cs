namespace DepinTracker.Domain.ValueObjects;

using DepinTracker.Domain.Enums;

/// <summary>
/// Syntactic address checks per chain family, so a wallet saved on the wrong chain
/// (e.g. a Solana address on Polygon) is caught at entry instead of surfacing later as
/// an opaque explorer error. Only the shape is checked — no checksum validation.
/// </summary>
public static class WalletAddressFormat
{
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary><c>0x</c> followed by 40 hex digits.</summary>
    public static bool IsEvm(string address) =>
        address.Length == 42 &&
        address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        !address.AsSpan(2).ContainsAnyExcept("0123456789abcdefABCDEF");

    /// <summary>A base58 public key, 32–44 characters.</summary>
    public static bool IsSolana(string address) =>
        address.Length is >= 32 and <= 44 &&
        !address.AsSpan().ContainsAnyExcept(Base58Alphabet);

    /// <summary>
    /// Returns <c>null</c> when <paramref name="address"/> fits <paramref name="chainType"/>,
    /// otherwise a user-facing explanation. Families without a check here always pass.
    /// </summary>
    public static string? Validate(ChainType chainType, string address)
    {
        var trimmed = address.Trim();
        return chainType switch
        {
            ChainType.Evm when !IsEvm(trimmed) => IsSolana(trimmed)
                ? $"'{trimmed}' looks like a Solana address, but the wallet's chain is EVM. Pick the Solana chain instead."
                : $"'{trimmed}' is not a valid EVM address (expected 0x followed by 40 hex characters).",
            ChainType.Solana when !IsSolana(trimmed) => IsEvm(trimmed)
                ? $"'{trimmed}' looks like an EVM address, but the wallet's chain is Solana. Pick an EVM chain instead."
                : $"'{trimmed}' is not a valid Solana address (expected 32–44 base58 characters).",
            _ => null,
        };
    }
}

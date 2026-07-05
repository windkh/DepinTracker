namespace DepinTracker.Application.Services;

/// <summary>
/// Heuristic detector for scam / phishing "airdrop" tokens. Legitimate tickers are
/// short, single-word and alphanumeric; scam airdrops instead pack a URL, marketing
/// slogan or instruction into the token <em>symbol</em> itself (e.g.
/// <c>"SHIB - [ T.LY/USHIB ] *REDEEM WITHIN 7 DAYS"</c>). This is intentionally a
/// symbol-shape heuristic — cheap, deterministic, and independent of any allow-list —
/// so it can mark rows both at import time and in the UI. It is conservative: it can
/// miss a plausibly-named junk token (which the per-source summary lets the user catch
/// manually), but should not flag a normal ticker.
/// </summary>
public static class SpamHeuristics
{
    // Substrings that essentially never appear in a real token ticker but are common
    // in scam-airdrop "symbols" (URLs, TLDs, calls to action, promo punctuation).
    private static readonly string[] Markers =
    {
        "http", "www.", ".io", ".com", ".app", ".net", ".org", ".xyz", ".finance",
        ".site", ".vip", ".pro", ".link", "t.ly", "bit.ly", "claim", "redeem", "visit",
        "voucher", "airdrop", "bonus", "reward", "free", "giveaway", "casino", "winner",
        "$", "[", "]", "!", "%", "👉", "🎁",
    };

    /// <summary>Maximum plausible length for a genuine ticker symbol.</summary>
    private const int MaxRealisticSymbolLength = 15;

    public static bool IsLikelySpam(string? tokenSymbol)
    {
        if (string.IsNullOrWhiteSpace(tokenSymbol))
        {
            return false;
        }

        var symbol = tokenSymbol.Trim();

        // Real tickers don't contain whitespace and aren't long sentences.
        if (symbol.Length > MaxRealisticSymbolLength || symbol.Any(char.IsWhiteSpace))
        {
            return true;
        }

        var lower = symbol.ToLowerInvariant();
        return Markers.Any(marker => lower.Contains(marker));
    }
}

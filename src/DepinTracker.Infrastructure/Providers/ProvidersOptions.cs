namespace DepinTracker.Infrastructure.Providers;

using DepinTracker.Domain.Enums;

/// <summary>
/// Configuration for the built-in HTTP providers, bound from the <c>Providers</c>
/// section of <c>config/appsettings.json</c>. Endpoints, chain registrations and the
/// symbol→CoinGecko-id map are data, not code, so users can add chains/tokens
/// without recompiling.
/// </summary>
public sealed class ProvidersOptions
{
    public const string SectionName = "Providers";

    public string CoinGeckoBaseUrl { get; set; } = "https://api.coingecko.com/api/v3";

    public string FrankfurterBaseUrl { get; set; } = "https://api.frankfurter.app";

    /// <summary>
    /// Base URL of the DeFiLlama coins API. The historical-price endpoint shape is
    /// <c>/prices/historical/{unixTs}/{chain}:{contract}?searchWidth={window}</c>.
    /// </summary>
    public string DefiLlamaBaseUrl { get; set; } = "https://coins.llama.fi";

    /// <summary>
    /// How wide a window DeFiLlama may search around the requested timestamp.
    /// 4h matches DeFiLlama's documented default and is tolerant of off-block timestamps.
    /// </summary>
    public string DefiLlamaSearchWidth { get; set; } = "4h";

    /// <summary>
    /// Maps an internal blockchain key (e.g. <c>"gnosis"</c>) to the chain identifier
    /// DeFiLlama uses on the wire (e.g. <c>"xdai"</c>). Keys that match DeFiLlama's
    /// identifier verbatim do not need an entry.
    /// </summary>
    public Dictionary<string, string> ChainKeyToDefiLlamaKey { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gnosis"] = "xdai",
    };

    /// <summary>Maps uppercase token symbols to CoinGecko coin ids.</summary>
    public Dictionary<string, string> SymbolToCoinGeckoId { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ETH"] = "ethereum",
        ["WETH"] = "weth",
        ["MATIC"] = "matic-network",
        ["POL"] = "matic-network",
        ["SOL"] = "solana",
        ["ATOM"] = "cosmos",
        ["BNB"] = "binancecoin",
        ["ARB"] = "arbitrum",
        ["OP"] = "optimism",
        ["USDC"] = "usd-coin",
        ["USDT"] = "tether",
        ["DAI"] = "dai",
        ["XDAI"] = "xdai",
    };

    /// <summary>
    /// Base URL of the Etherscan v2 unified API. A single key + endpoint serves every
    /// supported chain via the <c>chainid</c> query parameter.
    /// </summary>
    public string EtherscanV2BaseUrl { get; set; } = "https://api.etherscan.io/v2/api";

    /// <summary>Rows requested per Etherscan page. Etherscan caps page×offset at 10 000.</summary>
    public int EtherscanV2PageSize { get; set; } = 1000;

    /// <summary>
    /// Maximum number of pages to walk per wallet before stopping. 10 × 1000 covers
    /// 10 000 transfers which matches Etherscan's hard ceiling for offset pagination
    /// (beyond that we'd need block-range slicing). Set to 0 for unlimited.
    /// </summary>
    public int EtherscanV2MaxPages { get; set; } = 10;

    /// <summary>
    /// Base URL of the Helius API. The free tier covers personal-scale tax tracking
    /// (~100k credits/month) and includes the enhanced-transactions endpoint we use.
    /// </summary>
    public string HeliusBaseUrl { get; set; } = "https://api.helius.xyz";

    /// <summary>How many transactions Helius returns per page (cap is 100).</summary>
    public int HeliusPageSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of pages to walk per Solana wallet before stopping. 100 × 100
    /// covers 10 000 transactions. Set to 0 for unlimited.
    /// </summary>
    public int HeliusMaxPages { get; set; } = 100;

    /// <summary>Solana chains served by the Helius explorer, keyed by blockchain key.</summary>
    public Dictionary<string, SolanaChainOptions> SolanaChains { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["solana"] = new("Solana", "SOL"),
    };

    /// <summary>EVM chains served via the Etherscan v2 unified API, keyed by blockchain key.</summary>
    public Dictionary<string, EvmChainOptions> EvmChains { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ethereum"] = new("Ethereum", "ETH", 1),
        ["polygon"] = new("Polygon", "POL", 137),
        ["optimism"] = new("Optimism", "ETH", 10),
        ["base"] = new("Base", "ETH", 8453),
        ["arbitrum"] = new("Arbitrum One", "ETH", 42161),
        ["bnb"] = new("BNB Smart Chain", "BNB", 56),
        ["gnosis"] = new("Gnosis", "XDAI", 100),
    };
}

/// <summary>A single EVM chain definition served by the Etherscan v2 unified API.</summary>
public sealed record EvmChainOptions(string Name, string NativeSymbol, int ChainId)
{
    public EvmChainOptions() : this(string.Empty, string.Empty, 0) { }

    public ChainType ChainType => ChainType.Evm;
}

/// <summary>A single Solana-family chain definition (mainnet/devnet/etc.) served by Helius.</summary>
public sealed record SolanaChainOptions(string Name, string NativeSymbol)
{
    public SolanaChainOptions() : this(string.Empty, string.Empty) { }

    public ChainType ChainType => ChainType.Solana;
}

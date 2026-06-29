# Plugin Guide

DePIN Tracker is extended through plugins. A plugin is a .NET assembly that references
`DepinTracker.Plugins.Abstractions`, exposes one or more `IPlugin` implementations, and
is dropped into the portable `plugins/` folder. The host discovers it at startup, reads
its `PluginManifest` (shown on the Settings page for auditing), and calls `Register` so
the plugin can contribute services.

## Extension points

| Interface | Purpose |
| --- | --- |
| `IBlockchainExplorer` | Declare supported chains and fetch on-chain reward transactions. |
| `IPriceProvider` | Supply historical token prices (priority-ordered). |
| `IExchangeRateProvider` | Supply historical fiat exchange rates. |
| `IRewardClassifier` | Assign a `RewardKind` to a parsed transaction. |
| `IAnalyticsProvider` | Compute a named analytics result from rewards. |
| `IReportExporter` | Render a `ReportDocument` to a file format (e.g. add PDF). |

All methods are asynchronous and receive a `CancellationToken`. Providers should return
`null` ("no data") rather than throwing or guessing, so the engines can fall through.

`IPriceProvider` calls also carry the originating reward's `blockchainKey` and
`tokenContract` (both nullable) so contract-aware providers (DeFiLlama, DexScreener, …)
can resolve long-tail DePIN tokens without a hand-maintained symbol map. Symbol-only
providers should ignore those parameters.

## Minimal example

The bundled reference plugin (`samples/DepinTracker.Plugins.SampleCsvPrice`) registers a
CSV-backed `IPriceProvider`:

```csharp
public sealed class MyPlugin : IPlugin
{
    public PluginManifest Manifest { get; } = new(
        Id: "vendor.myplugin", Name: "My Plugin", Version: "1.0.0",
        Author: "you", Description: "What it does.");

    public void Register(IServiceCollection services)
        => services.AddSingleton<IPriceProvider, MyPriceProvider>();
}
```

## Loading model

Each plugin assembly is loaded into its own collectible `AssemblyLoadContext`. Shared
contract assemblies (the abstractions, anything the host already loaded) are resolved
from the host's context so type identity is preserved — without this, the plugin's
`IPlugin` would not match the host's. A plugin that fails to load is logged and skipped;
it can never take the application down.

## Deploying a plugin

1. Build your plugin project.
2. Copy its `.dll` (and any private dependencies + data files) into `plugins/`.
3. Restart the app — it appears under **Settings → Plugins** and its providers become active.

The reference plugin is copied into `plugins/` automatically by the app's build.

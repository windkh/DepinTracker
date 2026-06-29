namespace DepinTracker.Plugins.SampleCsvPrice;

using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Reference plugin demonstrating the extension model end-to-end: it ships a
/// manifest and registers an <see cref="IPriceProvider"/> that reads prices from a
/// local CSV file. Drop the built assembly into the <c>plugins/</c> folder and the
/// host discovers and loads it on startup — no host changes required.
/// </summary>
public sealed class SampleCsvPricePlugin : IPlugin
{
    public PluginManifest Manifest { get; } = new(
        Id: "depintracker.sample.csvprice",
        Name: "Sample CSV Price Provider",
        Version: "1.0.0",
        Author: "DePIN Tracker contributors",
        Description: "Reads historical prices from a local prices.csv file (offline reference plugin).");

    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IPriceProvider, CsvPriceProvider>();
    }
}

namespace DepinTracker.Tests.Infrastructure;

using DepinTracker.Infrastructure.Plugins;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.SampleCsvPrice;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class PluginLoaderTests
{
    [Fact]
    public void Discovers_sample_plugin_and_registers_its_provider()
    {
        // Copy just the sample plugin assembly into an isolated folder to load from.
        var pluginDll = typeof(SampleCsvPricePlugin).Assembly.Location;
        var tempDir = Path.Combine(Path.GetTempPath(), "depintracker-plugin-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.Copy(pluginDll, Path.Combine(tempDir, Path.GetFileName(pluginDll)), overwrite: true);

        try
        {
            var services = new ServiceCollection();
            var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);

            var manifests = loader.LoadInto(services, tempDir);

            manifests.Should().ContainSingle();
            manifests[0].Id.Should().Be("depintracker.sample.csvprice");
            services.Should().Contain(d => d.ServiceType == typeof(IPriceProvider),
                "the plugin registers an IPriceProvider implementation");
        }
        finally
        {
            // The plugin assembly stays loaded in its (collectible) context, so the file
            // handle may still be held here; a failed cleanup must not fail the test.
            try { Directory.Delete(tempDir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}

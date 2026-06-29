namespace DepinTracker.Plugins.Abstractions;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Entry point every plugin assembly exposes. The host discovers implementations
/// in the <c>plugins/</c> folder, reads their <see cref="Manifest"/> for auditing,
/// and calls <see cref="Register"/> so the plugin can contribute its providers
/// (explorers, price/FX providers, classifiers, analytics, exporters) to DI.
/// </summary>
public interface IPlugin
{
    PluginManifest Manifest { get; }

    /// <summary>
    /// Register the plugin's services into the host container. Implementations
    /// should only add their own provider implementations and must not assume any
    /// particular host service is present.
    /// </summary>
    void Register(IServiceCollection services);
}

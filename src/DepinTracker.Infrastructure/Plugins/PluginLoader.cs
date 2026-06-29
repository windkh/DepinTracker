namespace DepinTracker.Infrastructure.Plugins;

using System.Reflection;
using System.Runtime.Loader;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Discovers and loads plugins from the portable <c>plugins/</c> folder. Each plugin
/// assembly is loaded into its own collectible <see cref="AssemblyLoadContext"/>,
/// its <see cref="IPlugin"/> implementations are instantiated, and each is given the
/// chance to register its services. Discovery never throws: a bad plugin is logged
/// and skipped so it cannot take the whole app down.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger) => _logger = logger;

    /// <summary>
    /// Loads every plugin in <paramref name="pluginsDirectory"/>, registers their
    /// services into <paramref name="services"/>, and returns the discovered manifests.
    /// </summary>
    public IReadOnlyList<PluginManifest> LoadInto(IServiceCollection services, string pluginsDirectory)
    {
        var manifests = new List<PluginManifest>();
        if (!Directory.Exists(pluginsDirectory))
        {
            return manifests;
        }

        foreach (var dll in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var context = new PluginLoadContext(dll);
                var assembly = context.LoadFromAssemblyPath(dll);

                foreach (var type in GetPluginTypes(assembly))
                {
                    if (Activator.CreateInstance(type) is not IPlugin plugin)
                    {
                        continue;
                    }

                    plugin.Register(services);
                    manifests.Add(plugin.Manifest);
                    _logger.LogInformation("Loaded plugin {Name} v{Version} ({Id}) from {File}",
                        plugin.Manifest.Name, plugin.Manifest.Version, plugin.Manifest.Id, Path.GetFileName(dll));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {File}", Path.GetFileName(dll));
            }
        }

        return manifests;
    }

    private static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        return types.Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
    }

    /// <summary>Per-plugin load context that resolves the plugin's own dependencies.</summary>
    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true) =>
            _resolver = new AssemblyDependencyResolver(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Shared contract assemblies (e.g. the plugin abstractions, anything the host
            // already loaded) must come from the default context, otherwise a second copy
            // would break type identity and the plugin's IPlugin would not match the host's.
            if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
            {
                return null;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}

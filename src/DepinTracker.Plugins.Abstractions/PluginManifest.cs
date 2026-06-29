namespace DepinTracker.Plugins.Abstractions;

/// <summary>
/// Static metadata describing a plugin. Surfaced in the UI and logs so users can
/// audit exactly which extensions are loaded and from where.
/// </summary>
public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description);

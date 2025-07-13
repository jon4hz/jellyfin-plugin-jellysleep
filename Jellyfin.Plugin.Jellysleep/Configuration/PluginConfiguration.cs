using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellysleep.Configuration;

/// <summary>
/// Plugin configuration for Jellysleep.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

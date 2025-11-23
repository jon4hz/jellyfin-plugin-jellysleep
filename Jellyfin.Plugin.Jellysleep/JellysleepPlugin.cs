using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.Jellysleep.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Jellysleep;

/// <summary>
/// The main plugin.
/// </summary>
public class JellysleepPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JellysleepPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    /// <param name="configurationManager">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
    public JellysleepPlugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<JellysleepPlugin> logger,
        IServerConfigurationManager configurationManager)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
    }

    /// <summary>
    /// Registers the JavaScript with the JavaScript Injector plugin.
    /// </summary>
    public void RegisterJavascript()
    {
        try
        {
            // Find the JavaScript Injector assembly
            Assembly? jsInjectorAssembly = AssemblyLoadContext.All
                .SelectMany(x => x.Assemblies)
                .FirstOrDefault(x => x.FullName?.Contains("Jellyfin.Plugin.JavaScriptInjector", StringComparison.Ordinal) ?? false);

            if (jsInjectorAssembly != null)
            {
                var customScriptPath = $"{Assembly.GetExecutingAssembly().GetName().Name}.Web.jellysleep.js";
                var scriptStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(customScriptPath);
                if (scriptStream == null)
                {
                    _logger.LogError("Could not find embedded Jellysleep script at path: {Path}", customScriptPath);
                    return;
                }

                string scriptContent;
                using (var reader = new StreamReader(scriptStream))
                {
                    scriptContent = reader.ReadToEnd();
                }

                // Get the PluginInterface type
                Type? pluginInterfaceType = jsInjectorAssembly.GetType("Jellyfin.Plugin.JavaScriptInjector.PluginInterface");
                if (pluginInterfaceType == null)
                {
                    _logger.LogError("Could not find PluginInterface type in JavaScript Injector assembly.");
                    return;
                }

                // Create the registration payload
                var scriptRegistration = new JObject
                {
                            { "id", $"{Id}-script" },
                            { "name", "Jellysleep Client Script" },
                            { "script", scriptContent },
                            { "enabled", true },
                            { "requiresAuthentication", true },
                            { "pluginId", Id.ToString() },
                            { "pluginName", Name },
                            { "pluginVersion", Version.ToString() }
                        };

                // Register the script
                var registerResult = pluginInterfaceType.GetMethod("RegisterScript")?.Invoke(null, new object[] { scriptRegistration });

                // Validate the return value
                if (registerResult is bool success && success)
                {
                    _logger.LogInformation("Successfully registered JavaScript with JavaScript Injector plugin.");
                }
                else
                {
                    _logger.LogWarning("Failed to register JavaScript with JavaScript Injector plugin. RegisterScript returned false.");
                }

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register JavaScript with JavaScript Injector plugin.");
        }
    }

    /// <inheritdoc />
    public override void OnUninstalling()
    {
        try
        {
            // Find the JavaScript Injector assembly
            Assembly? jsInjectorAssembly = AssemblyLoadContext.All
                .SelectMany(x => x.Assemblies)
                .FirstOrDefault(x => x.FullName?.Contains("Jellyfin.Plugin.JavaScriptInjector", StringComparison.Ordinal) ?? false);

            if (jsInjectorAssembly != null)
            {
                Type? pluginInterfaceType = jsInjectorAssembly.GetType("Jellyfin.Plugin.JavaScriptInjector.PluginInterface");

                if (pluginInterfaceType != null)
                {
                    // Unregister all scripts from your plugin
                    var unregisterResult = pluginInterfaceType.GetMethod("UnregisterAllScriptsFromPlugin")?.Invoke(null, new object[] { Id.ToString() });

                    // Validate the return value
                    if (unregisterResult is int removedCount)
                    {
                        _logger?.LogInformation("Successfully unregistered {Count} script(s) from JavaScript Injector plugin.", removedCount);
                    }
                    else
                    {
                        _logger?.LogWarning("Failed to unregister scripts from JavaScript Injector plugin. Method returned unexpected value.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unregister JavaScript scripts.");
        }

        base.OnUninstalling();
    }

    private readonly ILogger<JellysleepPlugin> _logger;

    /// <inheritdoc />
    public override string Name => "Jellysleep";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a760bbb5-7b7b-4fda-951c-f3c39d689a8f");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static JellysleepPlugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Jellysleep",
                EnableInMainMenu = true,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        ];
    }
}

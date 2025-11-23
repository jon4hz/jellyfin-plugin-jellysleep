using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep;

/// <summary>
/// Plugin entry point that runs when the server starts.
/// </summary>
public sealed class PluginEntryPoint : IHostedService
{
    private readonly ILogger<PluginEntryPoint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginEntryPoint"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PluginEntryPoint(ILogger<PluginEntryPoint> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Jellysleep plugin is starting");

        JellysleepPlugin.Instance?.RegisterJavascript();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

using Jellyfin.Plugin.Jellysleep.EventHandlers;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Jellysleep;

/// <summary>
/// Register jellysleep services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ISleepTimerService, SleepTimerService>();
        serviceCollection.AddHostedService<SleepTimerService>(provider =>
            (SleepTimerService)provider.GetRequiredService<ISleepTimerService>());

        // Register event handlers
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopConsumer>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartConsumer>();
    }
}

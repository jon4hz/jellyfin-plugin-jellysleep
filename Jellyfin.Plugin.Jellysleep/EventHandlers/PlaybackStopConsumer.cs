using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles playback events for sleep timer functionality.
/// </summary>
public class PlaybackStopConsumer : IEventConsumer<SessionEndedEventArgs>
{
    private readonly ILogger<PlaybackStopConsumer> _logger;
    private readonly ISleepTimerService _sleepTimerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStopConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sleepTimerService">The sleep timer service.</param>
    public PlaybackStopConsumer(ILogger<PlaybackStopConsumer> logger, ISleepTimerService sleepTimerService)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
    }

    /// <inheritdoc />
    public async Task OnEvent(SessionEndedEventArgs eventArgs)
    {
        try
        {
            var session = eventArgs.Argument;

            if (session?.UserId == null || session.UserId == Guid.Empty)
            {
                return;
            }

            _logger.LogDebug(
                "Session ended for user {UserId} in session {SessionId}",
                session.UserId,
                session.Id);

            // Handle potential episode timer trigger
            var handled = await _sleepTimerService.HandlePlaybackStopAsync(session.UserId, session.Id).ConfigureAwait(false);
            if (handled)
            {
                _logger.LogInformation(
                    "Episode sleep timer triggered for user {UserId} in session {SessionId}",
                    session.UserId,
                    session.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session ended event");
        }
    }
}

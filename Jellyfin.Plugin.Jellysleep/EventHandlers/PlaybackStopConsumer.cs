using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles playback stop events for episode-based sleep timer functionality.
/// </summary>
public class PlaybackStopConsumer : IEventConsumer<PlaybackStopEventArgs>
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
    public async Task OnEvent(PlaybackStopEventArgs eventArgs)
    {
        try
        {
            var session = eventArgs.Session;

            if (session?.UserId == null || session.UserId == Guid.Empty)
            {
                return;
            }

            // check if there is an active sleep timer for this user and device
            var timerStatus = await _sleepTimerService.GetTimerStatusAsync(session.UserId, session.DeviceId).ConfigureAwait(false);
            if (timerStatus == null || !timerStatus.IsActive)
            {
                return;
            }

            _logger.LogInformation(
                "Playback stopped for user {UserId} in session {SessionId}, item: {ItemName}, PlayedToCompletion: {PlayedToCompletion}",
                session.UserId,
                session.Id,
                eventArgs.Item?.Name ?? "Unknown",
                eventArgs.PlayedToCompletion);

            if (eventArgs.PlayedToCompletion)
            {
                // Increment episode count and check if target was reached
                var targetReached = await _sleepTimerService.IncrementEpisodeCountAsync(session.UserId, session.DeviceId).ConfigureAwait(false);

                if (targetReached)
                {
                    _logger.LogInformation(
                        "Episode timer target reached after completion for user {UserId} in session {SessionId}",
                        session.UserId,
                        session.Id);

                    // For simple episode timers (no count), complete the timer now
                    await _sleepTimerService.HandlePlaybackStopAsync(session.UserId, session.Id).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation(
                        "Episode completed for user {UserId} in session {SessionId}, episode count incremented",
                        session.UserId,
                        session.Id);
                }
            }
            else
            {
                // Handle user interruption - this should cancel episode-count timers but not simple episode timers
                var handled = await _sleepTimerService.HandleUserInterruptionAsync(session.UserId, session.DeviceId).ConfigureAwait(false);
                if (handled)
                {
                    _logger.LogInformation(
                        "Episode-count timer cancelled due to user interruption for user {UserId} in session {SessionId}",
                        session.UserId,
                        session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback stop event");
        }
    }
}

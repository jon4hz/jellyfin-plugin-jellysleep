using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles playback start events to prevent new playback when episode timer is active.
/// </summary>
public class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ILogger<PlaybackStartConsumer> _logger;
    private readonly ISleepTimerService _sleepTimerService;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStartConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sleepTimerService">The sleep timer service.</param>
    /// <param name="sessionManager">The session manager.</param>
    public PlaybackStartConsumer(
        ILogger<PlaybackStartConsumer> logger,
        ISleepTimerService sleepTimerService,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStartEventArgs eventArgs)
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
                "Playback started for user {UserId} in session {SessionId}, item: {ItemName}",
                session.UserId,
                session.Id,
                eventArgs.Item?.Name ?? "Unknown");

            if (timerStatus.Type == "episode")
            {
                // Check if we've reached the target episode count for multi-episode timers
                if (timerStatus.EpisodeCount >= 1 && timerStatus.EpisodesPlayed >= timerStatus.EpisodeCount)
                {
                    _logger.LogInformation(
                        "Stopping playback in session {SessionId} for user {UserId} - episode timer target reached. Episodes: {EpisodesPlayed}/{EpisodeCount}, Item: {ItemName}",
                        session.Id,
                        session.UserId,
                        timerStatus.EpisodesPlayed,
                        timerStatus.EpisodeCount,
                        eventArgs.Item?.Name ?? "Unknown");

                    // Stop this playback immediately
                    await _sessionManager.SendPlaystateCommand(
                        session.Id,
                        session.Id,
                        new MediaBrowser.Model.Session.PlaystateRequest
                        {
                            Command = MediaBrowser.Model.Session.PlaystateCommand.Stop
                        },
                        CancellationToken.None).ConfigureAwait(false);

                    // Complete the timer
                    await _sleepTimerService.HandlePlaybackStopAsync(session.UserId, session.Id).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation(
                        "Allowing new playback in session {SessionId} for user {UserId} - episode timer still has episodes remaining. Episodes: {EpisodesPlayed}/{EpisodeCount}, Item: {ItemName}",
                        session.Id,
                        session.UserId,
                        timerStatus.EpisodesPlayed,
                        timerStatus.EpisodeCount,
                        eventArgs.Item?.Name ?? "Unknown");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback started event");
        }
    }
}

using System;
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

            _logger.LogDebug(
                "Playback started for user {UserId} in session {SessionId}, item: {ItemName}",
                session.UserId,
                session.Id,
                eventArgs.Item?.Name ?? "Unknown");

            // Check if this user has an active episode timer
            var timerStatus = await _sleepTimerService.GetTimerStatusAsync(session.UserId, session.DeviceId).ConfigureAwait(false);

            if (timerStatus.IsActive && timerStatus.Type == "episode")
            {
                _logger.LogInformation(
                    "Blocking new playback in session {SessionId} for user {UserId} due to active episode timer {TimerId}. Item: {ItemName}",
                    session.Id,
                    session.UserId,
                    timerStatus.TimerId,
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

                // Show a message about the sleep timer being active
                await _sessionManager.SendMessageCommand(
                    session.Id,
                    session.Id,
                    new MediaBrowser.Model.Session.MessageCommand
                    {
                        Header = "Sleep Timer Active",
                        Text = $"Sleep timer is set to activate after the current episode. Playback of '{eventArgs.Item?.Name ?? "this item"}' has been blocked.",
                        TimeoutMs = 5000
                    },
                    CancellationToken.None).ConfigureAwait(false);

                // Trigger the episode timer completion now since the episode has ended
                // and the user tried to start something new
                await _sleepTimerService.HandlePlaybackStopAsync(session.UserId, session.Id).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback started event");
        }
    }
}

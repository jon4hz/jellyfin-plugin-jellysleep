using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.EventHandlers;

/// <summary>
/// Handles session start events to prevent new playback when episode timer is active.
/// </summary>
public class SessionStartConsumer : IEventConsumer<SessionStartedEventArgs>
{
    private readonly ILogger<SessionStartConsumer> _logger;
    private readonly ISleepTimerService _sleepTimerService;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStartConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sleepTimerService">The sleep timer service.</param>
    /// <param name="sessionManager">The session manager.</param>
    public SessionStartConsumer(
        ILogger<SessionStartConsumer> logger,
        ISleepTimerService sleepTimerService,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public async Task OnEvent(SessionStartedEventArgs eventArgs)
    {
        try
        {
            var session = eventArgs.Argument;

            if (session?.UserId == null || session.UserId == Guid.Empty)
            {
                return;
            }

            _logger.LogDebug(
                "Session started for user {UserId} in session {SessionId}",
                session.UserId,
                session.Id);

            // Check if this user has an active episode timer
            var timerStatus = await _sleepTimerService.GetTimerStatusAsync(session.UserId, session.DeviceId).ConfigureAwait(false);

            if (timerStatus.IsActive && timerStatus.Type == "episode")
            {
                _logger.LogInformation(
                    "Blocking new session {SessionId} for user {UserId} due to active episode timer {TimerId}",
                    session.Id,
                    session.UserId,
                    timerStatus.TimerId);

                // Stop this new session immediately
                await _sessionManager.SendPlaystateCommand(
                    session.Id,
                    session.Id,
                    new MediaBrowser.Model.Session.PlaystateRequest
                    {
                        Command = MediaBrowser.Model.Session.PlaystateCommand.Stop
                    },
                    System.Threading.CancellationToken.None).ConfigureAwait(false);

                // Show a message about the sleep timer being active
                await _sessionManager.SendMessageCommand(
                    session.Id,
                    session.Id,
                    new MediaBrowser.Model.Session.MessageCommand
                    {
                        Header = "Sleep Timer Active",
                        Text = "Sleep timer is set to activate after the current episode. New playback has been blocked.",
                        TimeoutMs = 5000
                    },
                    System.Threading.CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session started event");
        }
    }
}

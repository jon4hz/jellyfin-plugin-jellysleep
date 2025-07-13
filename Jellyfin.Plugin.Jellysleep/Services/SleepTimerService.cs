using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellysleep.Models;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.Services;

/// <summary>
/// Service for managing sleep timers.
/// </summary>
public class SleepTimerService : BackgroundService, ISleepTimerService
{
    private readonly ILogger<SleepTimerService> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly ConcurrentDictionary<UserDeviceKey, ActiveSleepTimer> _activeTimers;
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SleepTimerService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sessionManager">The session manager.</param>
    public SleepTimerService(ILogger<SleepTimerService> logger, ISessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _activeTimers = new ConcurrentDictionary<UserDeviceKey, ActiveSleepTimer>();

        // Setup cleanup timer to run every 30 seconds
        _cleanupTimer = new Timer(async _ => await CleanupTimersAsync().ConfigureAwait(false), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Starts a new sleep timer for the specified user and device.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="request">The timer request.</param>
    /// <returns>The timer response.</returns>
    public async Task<SleepTimerResponse> StartTimerAsync(Guid userId, string? deviceId, SleepTimerRequest request)
    {
        if (string.IsNullOrEmpty(request.Type))
        {
            throw new ArgumentException("Timer type is required", nameof(request));
        }

        if (request.Type != "duration" && request.Type != "episode")
        {
            throw new ArgumentException("Timer type must be 'duration' or 'episode'", nameof(request));
        }

        if (request.Type == "duration" && (!request.Duration.HasValue || request.Duration.Value <= 0))
        {
            throw new ArgumentException("Duration is required for duration-based timers", nameof(request));
        }

        // Cancel any existing timer for this user and device
        await CancelTimerAsync(userId, deviceId).ConfigureAwait(false);

        var userDeviceKey = new UserDeviceKey(userId, deviceId);
        var timerId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        DateTime? endTime = null;

        if (request.Type == "duration")
        {
            endTime = request.EndTime ?? startTime.AddMinutes(request.Duration!.Value);
        }

        // for testing purposes, set the timer to 15 seconds
        // endTime = startTime.AddSeconds(15); // TODO: remove

        var timer = new ActiveSleepTimer
        {
            Id = timerId,
            UserId = userId,
            DeviceId = deviceId,
            Type = request.Type,
            Duration = request.Duration,
            StartTime = startTime,
            EndTime = endTime,
            Label = request.Label,
            IsActive = true
        };

        // Note: We don't store session information in the timer
        // Sessions will be looked up dynamically when needed

        _activeTimers.TryAdd(userDeviceKey, timer);

        _logger.LogInformation(
            "Started sleep timer {TimerId} for user {UserId}: Type={Type}, Duration={Duration}, EndTime={EndTime}",
            timerId,
            userId,
            request.Type,
            request.Duration,
            endTime);

        return new SleepTimerResponse
        {
            Success = true,
            TimerId = timerId,
            Type = request.Type,
            Duration = request.Duration,
            EndTime = endTime,
            Label = request.Label,
            Message = $"Sleep timer started: {request.Label ?? request.Type}"
        };
    }

    /// <summary>
    /// Cancels the sleep timer for the specified user and device.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>True if a timer was cancelled, false otherwise.</returns>
    public Task<bool> CancelTimerAsync(Guid userId, string? deviceId)
    {
        var userDeviceKey = new UserDeviceKey(userId, deviceId);
        if (_activeTimers.TryRemove(userDeviceKey, out var timer))
        {
            _logger.LogInformation(
                "Cancelled sleep timer {TimerId} for user {UserId} on device {DeviceId}",
                timer.Id,
                userId,
                deviceId ?? "unknown");
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the status of the sleep timer for the specified user and device.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The timer status.</returns>
    public Task<SleepTimerStatusResponse> GetTimerStatusAsync(Guid userId, string? deviceId)
    {
        var userDeviceKey = new UserDeviceKey(userId, deviceId);
        if (_activeTimers.TryGetValue(userDeviceKey, out var timer) && timer.IsActive)
        {
            return Task.FromResult(new SleepTimerStatusResponse
            {
                IsActive = true,
                TimerId = timer.Id,
                Type = timer.Type,
                Duration = timer.Duration,
                EndTime = timer.EndTime,
                RemainingMinutes = timer.GetRemainingMinutes(),
                Label = timer.Label
            });
        }

        return Task.FromResult(new SleepTimerStatusResponse
        {
            IsActive = false
        });
    }

    /// <inheritdoc />
    public async Task<bool> HandlePlaybackStopAsync(Guid userId, string? sessionId)
    {
        // Get the session to find the device ID
        string? deviceId = null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            var session = _sessionManager.Sessions.FirstOrDefault(s => s.Id == sessionId);
            deviceId = session?.DeviceId;
        }

        // Find episode timers for this user and device
        var userTimers = _activeTimers.Where(kvp => kvp.Key.UserId == userId &&
            kvp.Value.IsActive &&
            kvp.Value.Type == "episode" &&
            (string.IsNullOrEmpty(deviceId) || kvp.Key.DeviceId == deviceId || string.IsNullOrEmpty(kvp.Key.DeviceId)))
            .ToList();

        bool handled = false;
        foreach (var timerKvp in userTimers)
        {
            var timer = timerKvp.Value;
            _logger.LogInformation(
                "Episode finished for user {UserId} on device {DeviceId}, triggering episode-based sleep timer {TimerId}",
                userId,
                deviceId ?? "unknown",
                timer.Id);

            // This episode has finished, now we need to stop all playback for this user/device
            await StopPlaybackForUserAsync(timer.UserId, timer.DeviceId).ConfigureAwait(false);

            // Remove the timer since it has been triggered
            _activeTimers.TryRemove(timerKvp.Key, out _);

            handled = true;
        }

        return handled;
    }

    /// <inheritdoc />
    public async Task CleanupTimersAsync()
    {
        var expiredTimers = new List<KeyValuePair<UserDeviceKey, ActiveSleepTimer>>();

        foreach (var kvp in _activeTimers)
        {
            var timer = kvp.Value;

            // Check if timer has expired
            if (timer.IsExpired())
            {
                expiredTimers.Add(kvp);
            }

            // Check if user session is still active for this specific device
            else if (!IsUserSessionActive(timer.UserId, timer.DeviceId))
            {
                _logger.LogInformation(
                    "User {UserId} on device {DeviceId} session is no longer active, removing timer {TimerId}",
                    timer.UserId,
                    kvp.Key.DeviceId,
                    timer.Id);
                expiredTimers.Add(kvp);
            }
        }

        // Process expired timers
        foreach (var kvp in expiredTimers)
        {
            var userDeviceKey = kvp.Key;
            var timer = kvp.Value;

            if (timer.Type == "duration" && timer.IsExpired())
            {
                _logger.LogInformation(
                    "Duration-based timer {TimerId} expired for user {UserId} on device {DeviceId}, stopping playback",
                    timer.Id,
                    timer.UserId,
                    userDeviceKey.DeviceId);

                // Stop playback for this user/device combination
                await StopPlaybackForUserAsync(timer.UserId, timer.DeviceId).ConfigureAwait(false);
            }

            // Remove the timer
            _activeTimers.TryRemove(userDeviceKey, out _);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sleep timer service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupTimersAsync().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sleep timer service background task");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Sleep timer service stopped");
    }

    /// <summary>
    /// Stop playback for a specific user and device combination.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID (optional).</param>
    /// <returns>A task representing the async operation.</returns>
    private async Task StopPlaybackForUserAsync(Guid userId, string? deviceId)
    {
        try
        {
            // Dynamically find sessions based on userId and deviceId
            var sessions = _sessionManager.Sessions
                .Where(s => s.UserId == userId &&
                           (string.IsNullOrEmpty(deviceId) || s.DeviceId == deviceId))
                .ToList();

            _logger.LogInformation(
                "Found {SessionCount} active sessions for user {UserId} on device {DeviceId}",
                sessions.Count,
                userId,
                deviceId ?? "any");

            foreach (var session in sessions)
            {
                if (session.NowPlayingItem != null)
                {
                    _logger.LogInformation(
                        "Stopping playback for user {UserId} in session {SessionId} on device {DeviceId}",
                        userId,
                        session.Id,
                        session.DeviceId ?? "unknown");

                    await _sessionManager.SendPlaystateCommand(
                        session.Id,
                        session.Id,
                        new MediaBrowser.Model.Session.PlaystateRequest
                        {
                            Command = MediaBrowser.Model.Session.PlaystateCommand.Stop
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogDebug(
                        "Session {SessionId} for user {UserId} has no active playback to stop",
                        session.Id,
                        userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping playback for user {UserId} on device {DeviceId}", userId, deviceId);
        }
    }

    /// <summary>
    /// Check if a user has an active session, optionally for a specific device.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID (optional).</param>
    /// <returns>True if the user has an active session, false otherwise.</returns>
    private bool IsUserSessionActive(Guid userId, string? deviceId = null)
    {
        return _sessionManager.Sessions.Any(s => s.UserId == userId &&
            (string.IsNullOrEmpty(deviceId) || s.DeviceId == deviceId));
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _cleanupTimer?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

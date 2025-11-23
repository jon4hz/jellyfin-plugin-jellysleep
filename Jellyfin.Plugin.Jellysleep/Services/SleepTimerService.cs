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
    private readonly ConcurrentDictionary<UserDeviceKey, SemaphoreSlim> _timerLocks;
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
        _timerLocks = new ConcurrentDictionary<UserDeviceKey, SemaphoreSlim>();

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

        if (request.Type == "episode" && request.EpisodeCount.HasValue && request.EpisodeCount.Value <= 0)
        {
            throw new ArgumentException("Episode count must be greater than 0", nameof(request));
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
            EpisodeCount = request.EpisodeCount,
            EpisodesPlayed = 0,
            StartTime = startTime,
            EndTime = endTime,
            Label = request.Label,
            IsActive = true
        };

        // Note: We don't store session information in the timer
        // Sessions will be looked up dynamically when needed

        _activeTimers.TryAdd(userDeviceKey, timer);

        _logger.LogInformation(
            "Started sleep timer {TimerId} for user {UserId}: Type={Type}, Duration={Duration}, EpisodeCount={EpisodeCount}, EndTime={EndTime}",
            timerId,
            userId,
            request.Type,
            request.Duration,
            request.EpisodeCount,
            endTime);

        return new SleepTimerResponse
        {
            Success = true,
            TimerId = timerId,
            Type = request.Type,
            Duration = request.Duration,
            EpisodeCount = request.EpisodeCount,
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

            // Clean up the lock
            if (_timerLocks.TryRemove(userDeviceKey, out var semaphore))
            {
                semaphore.Dispose();
            }

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
                EpisodeCount = timer.EpisodeCount,
                EpisodesPlayed = timer.Type == "episode" ? timer.EpisodesPlayed : null,
                EndTime = timer.EndTime,
                RemainingMinutes = timer.GetRemainingMinutes(),
                RemainingEpisodes = timer.GetRemainingEpisodes(),
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

        var userDeviceKey = new UserDeviceKey(userId, deviceId);

        // Find episode timers for this user and device
        if (!_activeTimers.TryGetValue(userDeviceKey, out var timer) ||
            !timer.IsActive ||
            timer.Type != "episode")
        {
            return false;
        }

        if (timer.IsEpisodeCountTimer())
        {
            // For multi-episode timers, only complete when target is reached
            if (timer.EpisodesPlayed >= timer.EpisodeCount)
            {
                _logger.LogInformation(
                    "Multi-episode timer completed for user {UserId} on device {DeviceId}, episodes: {EpisodesPlayed}/{EpisodeCount}, timer {TimerId}",
                    userId,
                    deviceId ?? "unknown",
                    timer.EpisodesPlayed,
                    timer.EpisodeCount,
                    timer.Id);

                // Stop all playback for this user/device
                await StopPlaybackForUserAsync(timer.UserId, timer.DeviceId).ConfigureAwait(false);

                // Remove the timer since it has been triggered
                _activeTimers.TryRemove(userDeviceKey, out _);

                return true;
            }

            // Timer is still active, target not reached yet
            return false;
        }
        else
        {
            // This is a simple "after current episode" timer
            _logger.LogInformation(
                "Simple episode timer completed for user {UserId} on device {DeviceId}, timer {TimerId}",
                userId,
                deviceId ?? "unknown",
                timer.Id);

            // This episode has finished, now we need to stop all playback for this user/device
            await StopPlaybackForUserAsync(timer.UserId, timer.DeviceId).ConfigureAwait(false);

            // Remove the timer since it has been triggered
            _activeTimers.TryRemove(userDeviceKey, out _);

            return true;
        }
    }

    /// <inheritdoc />
    public Task<bool> HandleUserInterruptionAsync(Guid userId, string? deviceId)
    {
        var userDeviceKey = new UserDeviceKey(userId, deviceId);

        // Find episode timers for this user and device
        if (!_activeTimers.TryGetValue(userDeviceKey, out var timer) ||
            !timer.IsActive ||
            timer.Type != "episode")
        {
            return Task.FromResult(false);
        }

        // Only cancel episode-count timers on user interruption
        // Simple "after current episode" timers should not be cancelled
        if (timer.IsEpisodeCountTimer())
        {
            _logger.LogInformation(
                "User interrupted playback for episode-count timer {TimerId} (user {UserId}, device {DeviceId}). " +
                "Progress was {EpisodesPlayed}/{EpisodeCount}. Cancelling timer.",
                timer.Id,
                userId,
                deviceId ?? "unknown",
                timer.EpisodesPlayed,
                timer.EpisodeCount);

            // Cancel the timer
            _activeTimers.TryRemove(userDeviceKey, out _);
            return Task.FromResult(true);
        }

        // Don't cancel simple episode timers on user interruption
        _logger.LogDebug(
            "User interrupted playback but timer {TimerId} is simple episode timer, keeping it active",
            timer.Id);

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> IncrementEpisodeCountAsync(Guid userId, string? deviceId)
    {
        var userDeviceKey = new UserDeviceKey(userId, deviceId);

        // Get or create a lock for this user/device combination
        var semaphore = _timerLocks.GetOrAdd(userDeviceKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_activeTimers.TryGetValue(userDeviceKey, out var timer) ||
                !timer.IsActive ||
                timer.Type != "episode")
            {
                _logger.LogDebug(
                    "No active episode timer found to increment for user {UserId}, device {DeviceId}",
                    userId,
                    deviceId ?? "unknown");
                return false;
            }

            timer.EpisodesPlayed++;

            _logger.LogInformation(
                "Incremented episode count for timer {TimerId} (user {UserId}, device {DeviceId}): {EpisodesPlayed}/{EpisodeCount}",
                timer.Id,
                userId,
                deviceId ?? "unknown",
                timer.EpisodesPlayed,
                timer.EpisodeCount ?? 1);

            // Check if we've reached the target for multi-episode timers
            if (timer.IsEpisodeCountTimer() && timer.EpisodesPlayed >= timer.EpisodeCount)
            {
                _logger.LogInformation(
                    "Episode timer target reached for timer {TimerId} (user {UserId}, device {DeviceId}): {EpisodesPlayed}/{EpisodeCount}",
                    timer.Id,
                    userId,
                    deviceId ?? "unknown",
                    timer.EpisodesPlayed,
                    timer.EpisodeCount);

                return true; // Indicates target reached
            }

            return false; // Target not yet reached
        }
        finally
        {
            semaphore.Release();
        }
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

            // Remove the timer and its lock
            _activeTimers.TryRemove(userDeviceKey, out _);
            if (_timerLocks.TryRemove(userDeviceKey, out var semaphore))
            {
                semaphore.Dispose();
            }
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

        // Dispose all semaphores
        foreach (var semaphore in _timerLocks.Values)
        {
            semaphore.Dispose();
        }
        _timerLocks.Clear();

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

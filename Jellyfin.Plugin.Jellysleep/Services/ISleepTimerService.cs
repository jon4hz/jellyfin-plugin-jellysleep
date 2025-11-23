using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellysleep.Models;

namespace Jellyfin.Plugin.Jellysleep.Services;

/// <summary>
/// Interface for sleep timer service.
/// </summary>
public interface ISleepTimerService
{
    /// <summary>
    /// Start a sleep timer for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="request">The sleep timer request.</param>
    /// <returns>The sleep timer response.</returns>
    Task<SleepTimerResponse> StartTimerAsync(Guid userId, string? deviceId, SleepTimerRequest request);

    /// <summary>
    /// Cancel a user's active sleep timer.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>True if a timer was cancelled, false if no active timer was found.</returns>
    Task<bool> CancelTimerAsync(Guid userId, string? deviceId);

    /// <summary>
    /// Get the status of a user's sleep timer.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The sleep timer status.</returns>
    Task<SleepTimerStatusResponse> GetTimerStatusAsync(Guid userId, string? deviceId);

    /// <summary>
    /// Handle playback stop event for episode-based timers.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>True if a timer was triggered, false otherwise.</returns>
    Task<bool> HandlePlaybackStopAsync(Guid userId, string? sessionId);

    /// <summary>
    /// Handle user interruption (stopping playback early) - cancels episode-count timers.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>True if a timer was cancelled due to interruption, false otherwise.</returns>
    Task<bool> HandleUserInterruptionAsync(Guid userId, string? deviceId);

    /// <summary>
    /// Increments the episode count for episode-based timers when an episode completes.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>True if the target episode count was reached, false otherwise.</returns>
    Task<bool> IncrementEpisodeCountAsync(Guid userId, string? deviceId);

    /// <summary>
    /// Clean up expired timers and inactive sessions.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    Task CleanupTimersAsync();
}

using System;

namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Represents an active sleep timer.
/// </summary>
public class ActiveSleepTimer
{
    /// <summary>
    /// Gets or sets the timer ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the type of timer (duration or episode).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration in minutes (for duration-based timers).
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time (for duration-based timers).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the timer is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets the remaining time in minutes for duration-based timers.
    /// </summary>
    /// <returns>The remaining minutes if active, null otherwise.</returns>
    public int? GetRemainingMinutes()
    {
        if (Type != "duration" || EndTime == null)
        {
            return null;
        }

        var remaining = EndTime.Value - DateTime.UtcNow;
        return remaining.TotalSeconds > 0 ? (int)Math.Ceiling(remaining.TotalMinutes) : 0;
    }

    /// <summary>
    /// Gets a value indicating whether the timer has expired.
    /// </summary>
    /// <returns>True if the timer has expired, false otherwise.</returns>
    public bool IsExpired()
    {
        if (Type == "episode")
        {
            return false; // Episode timers don't expire on their own
        }

        return EndTime.HasValue && DateTime.UtcNow >= EndTime.Value;
    }
}

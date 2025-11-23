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
    /// Gets or sets the target episode count (for episode-based timers).
    /// </summary>
    public int? EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the current number of episodes played (for episode-based timers).
    /// </summary>
    public int EpisodesPlayed { get; set; }

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
            // For episode timers, check if we've reached the target episode count
            return EpisodeCount.HasValue && EpisodesPlayed >= EpisodeCount.Value;
        }

        return EndTime.HasValue && DateTime.UtcNow >= EndTime.Value;
    }

    /// <summary>
    /// Gets a value indicating whether this is an episode-count based timer (not just "after current episode").
    /// </summary>
    /// <returns>True if this is an episode-count timer, false otherwise.</returns>
    public bool IsEpisodeCountTimer()
    {
        return Type == "episode" && EpisodeCount.HasValue && EpisodeCount.Value > 0;
    }

    /// <summary>
    /// Gets the remaining episodes for episode-count timers.
    /// </summary>
    /// <returns>The remaining episodes if applicable, null otherwise.</returns>
    public int? GetRemainingEpisodes()
    {
        if (!IsEpisodeCountTimer())
        {
            return null;
        }

        var remaining = EpisodeCount!.Value - EpisodesPlayed;
        return Math.Max(0, remaining);
    }
}

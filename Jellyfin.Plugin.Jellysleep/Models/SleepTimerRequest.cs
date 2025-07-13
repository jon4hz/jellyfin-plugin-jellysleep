using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Request model for starting a sleep timer.
/// </summary>
public class SleepTimerRequest
{
    /// <summary>
    /// Gets or sets the type of sleep timer (e.g., "duration", "episode", "movie").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration in minutes (for duration-based timers).
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes (for episode-based timers).
    /// </summary>
    [JsonPropertyName("episodeCount")]
    public int? EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets a custom label for the timer.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the end time for time-based timers.
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }
}

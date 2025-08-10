using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Status response model for sleep timer status queries.
/// </summary>
public class SleepTimerStatusResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether a timer is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the timer ID.
    /// </summary>
    [JsonPropertyName("timerId")]
    public string? TimerId { get; set; }

    /// <summary>
    /// Gets or sets the type of sleep timer.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the duration in minutes.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    /// <summary>
    /// Gets or sets the target episode count (for episode-based timers).
    /// </summary>
    [JsonPropertyName("episodeCount")]
    public int? EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes played so far (for episode-based timers).
    /// </summary>
    [JsonPropertyName("episodesPlayed")]
    public int? EpisodesPlayed { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the remaining minutes.
    /// </summary>
    [JsonPropertyName("remainingMinutes")]
    public int? RemainingMinutes { get; set; }

    /// <summary>
    /// Gets or sets the remaining episodes (for episode-count timers).
    /// </summary>
    [JsonPropertyName("remainingEpisodes")]
    public int? RemainingEpisodes { get; set; }

    /// <summary>
    /// Gets or sets the timer label.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

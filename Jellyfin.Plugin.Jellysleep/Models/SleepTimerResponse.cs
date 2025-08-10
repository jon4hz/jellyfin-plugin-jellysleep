using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Response model for sleep timer operations.
/// </summary>
public class SleepTimerResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

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
    /// Gets or sets the episode count (for episode-based timers).
    /// </summary>
    [JsonPropertyName("episodeCount")]
    public int? EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the timer label.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets any error details.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

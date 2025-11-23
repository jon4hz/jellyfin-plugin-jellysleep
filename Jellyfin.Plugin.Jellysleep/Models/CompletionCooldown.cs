namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Represents a cooldown period after a timer completes.
/// </summary>
public class CompletionCooldown
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the time when the cooldown expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the cooldown is still active.
    /// </summary>
    /// <returns>True if the cooldown is active, false otherwise.</returns>
    public bool IsActive() => DateTime.UtcNow < ExpiresAt;
}

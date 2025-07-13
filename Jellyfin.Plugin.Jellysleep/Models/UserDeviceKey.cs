namespace Jellyfin.Plugin.Jellysleep.Models;

/// <summary>
/// Composite key for user and device identification.
/// </summary>
public class UserDeviceKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeviceKey"/> class.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="deviceId">The device ID.</param>
    public UserDeviceKey(Guid userId, string? deviceId)
    {
        UserId = userId;
        DeviceId = deviceId ?? string.Empty;
    }

    /// <summary>
    /// Gets the user ID.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the device ID.
    /// </summary>
    public string DeviceId { get; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is UserDeviceKey other && UserId == other.UserId && DeviceId == other.DeviceId;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(UserId, DeviceId);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{UserId}:{DeviceId}";
    }
}

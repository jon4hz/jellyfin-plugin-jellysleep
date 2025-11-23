using System.Security.Claims;
using Jellyfin.Plugin.Jellysleep.Models;
using Jellyfin.Plugin.Jellysleep.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellysleep.Api;

/// <summary>
/// Jellysleep API controller.
/// </summary>
[ApiController]
[Authorize]
[Route("Plugin/Jellysleep")]
public class JellysleepController : ControllerBase
{
    private readonly ILogger<JellysleepController> _logger;
    private readonly ISleepTimerService _sleepTimerService;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellysleepController"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{JellysleepController}"/> interface.</param>
    /// <param name="sleepTimerService">Instance of the <see cref="ISleepTimerService"/> interface.</param>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    public JellysleepController(ILogger<JellysleepController> logger, ISleepTimerService sleepTimerService, ISessionManager sessionManager)
    {
        _logger = logger;
        _sleepTimerService = sleepTimerService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Start a sleep timer for the current user session.
    /// </summary>
    /// <param name="request">The sleep timer request.</param>
    /// <returns>The sleep timer response.</returns>
    [HttpPost("StartTimer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SleepTimerResponse>> StartTimer([FromBody] SleepTimerRequest request)
    {
        try
        {
            var userId = GetUserId();
            var deviceId = GetDeviceId();

            // Debug logging to help troubleshoot authentication
            _logger.LogInformation("Authentication debug info:");
            _logger.LogInformation("User.Identity.IsAuthenticated: {IsAuthenticated}", User.Identity?.IsAuthenticated);
            _logger.LogInformation("User.Identity.Name: {Name}", User.Identity?.Name);
            _logger.LogInformation("User.Claims count: {ClaimCount}", User.Claims.Count());

            if (userId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Invalid user ID in sleep timer request. Available claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return BadRequest("Invalid user session");
            }

            _logger.LogInformation(
                "Starting sleep timer for user {UserId} on device {DeviceId}: {Type}, Duration: {Duration}",
                userId,
                deviceId ?? "unknown",
                request.Type,
                request.Duration);

            var response = await _sleepTimerService.StartTimerAsync(userId, deviceId, request).ConfigureAwait(false);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid sleep timer request");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sleep timer");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Cancel the active sleep timer for the current user session.
    /// </summary>
    /// <returns>Success response.</returns>
    [HttpPost("CancelTimer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelTimer()
    {
        try
        {
            var userId = GetUserId();
            var deviceId = GetDeviceId();
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID in cancel timer request");
                return BadRequest("Invalid user session");
            }

            _logger.LogInformation(
                "Cancelling sleep timer for user {UserId} on device {DeviceId}",
                userId,
                deviceId ?? "unknown");

            var success = await _sleepTimerService.CancelTimerAsync(userId, deviceId).ConfigureAwait(false);
            if (!success)
            {
                return NotFound("No active timer found");
            }

            return Ok(new { message = "Timer cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling sleep timer");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get the status of the current user's sleep timer.
    /// </summary>
    /// <returns>The sleep timer status.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SleepTimerStatusResponse>> GetStatus()
    {
        try
        {
            var userId = GetUserId();
            var deviceId = GetDeviceId();
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Invalid user ID in status request");
                return BadRequest("Invalid user session");
            }

            var status = await _sleepTimerService.GetTimerStatusAsync(userId, deviceId).ConfigureAwait(false);
            _logger.LogInformation(
                "Retrieved sleep timer status for user {UserId} on device {DeviceId}: {Status}",
                userId,
                deviceId ?? "unknown",
                status);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sleep timer status");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// List all active sleep timers across all users. Requires elevated privileges.
    /// </summary>
    /// <returns>List of active sleep timers.</returns>
    [Authorize(Policy = Policies.RequiresElevation)]
    [HttpGet("ListTimers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActiveSleepTimer>>> ListAllTimers()
    {
        try
        {
            var timers = await _sleepTimerService.ListAllActiveTimersAsync().ConfigureAwait(false);
            return Ok(timers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all active sleep timers");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get the user ID from the current user claims.
    /// </summary>
    /// <returns>The user ID.</returns>
    private Guid GetUserId()
    {
        if (HttpContext.User.Identity is ClaimsIdentity identity)
        {
            var claim = identity.FindFirst("Jellyfin-UserId");
            if (claim != null && !string.IsNullOrEmpty(claim.Value))
            {
                if (Guid.TryParse(claim.Value, out var userId))
                {
                    return userId;
                }
            }
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Get the device ID from the current user claims.
    /// </summary>
    /// <returns>The device ID.</returns>
    private string? GetDeviceId()
    {
        if (HttpContext.User.Identity is ClaimsIdentity identity)
        {
            var claim = identity.FindFirst("Jellyfin-DeviceId");
            return claim?.Value;
        }

        return null;
    }
}

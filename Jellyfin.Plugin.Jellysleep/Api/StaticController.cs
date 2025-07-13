using System.Net.Mime;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jellysleep.Api;

/// <summary>
/// API controller to serve static files for the Jellysleep plugin.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StaticController"/> class.
/// </remarks>
[ApiController]
[Route("Plugins/Jellysleep/Static")]
[Produces(MediaTypeNames.Application.Json)]
public class StaticController : ControllerBase
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly string _jellysleepScriptPath = $"{Assembly.GetExecutingAssembly().GetName().Name}.Web.jellysleep.js";

    /// <summary>
    /// Get the javascript file for the Jellysleep plugin.
    /// </summary>
    /// <response code="200">Javascript file successfully returned.</response>
    /// <response code="404">File not found.</response>
    /// <returns>The "jellysleep.js" embedded file.</returns>
    [HttpGet("ClientScript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var scriptStream = _assembly.GetManifestResourceStream(_jellysleepScriptPath);

        if (scriptStream != null)
        {
            return File(scriptStream, "application/javascript");
        }

        return NotFound();
    }
}

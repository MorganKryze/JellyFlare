using System.IO;
using System.Reflection;
using Jellyfin.Plugin.JellyFlare.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyFlare.Api;

/// <summary>
/// Exposes the plugin configuration as JSON for the banner script.
/// All endpoints require authentication — the banner is intended for registered users only.
/// </summary>
[ApiController]
[Route("JellyFlare")]
public class BannerController : ControllerBase
{
    /// <summary>Returns the current plugin configuration.</summary>
    [HttpGet("config")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfiguration> GetConfig()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        return Ok(config);
    }

    /// <summary>Serves the banner client script.</summary>
    [HttpGet("banner.js")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetBannerScript()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Jellyfin.Plugin.JellyFlare.Resources.banner.js");
        if (stream is null)
            return NotFound();
        return File(stream, "application/javascript");
    }

    /// <summary>Saves the plugin configuration.</summary>
    [HttpPost("config")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SaveConfig([FromBody] PluginConfiguration config)
    {
        if (Plugin.Instance is null)
            return NotFound();

        Plugin.Instance.UpdateConfiguration(config);
        Plugin.Instance.SaveConfiguration();
        return NoContent();
    }
}

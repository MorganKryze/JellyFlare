using System;
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SaveConfig([FromBody] PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (Plugin.Instance is null)
            return NotFound();

        // Clamp numeric fields to valid ranges
        config.DisplayDuration = Math.Max(1, config.DisplayDuration);
        config.PauseDuration = Math.Max(0, config.PauseDuration);
        config.BannerHeight = Math.Clamp(config.BannerHeight, 24, 80);

        // Normalise enum-like string fields to prevent unknown tokens being stored
        string[] validTextAlign = ["center", "left"];
        if (!string.IsNullOrEmpty(config.TextAlign) && !Array.Exists(validTextAlign, v => v == config.TextAlign))
            config.TextAlign = "center";

        string[] validTransitionSpeed = ["none", "fast", "normal", "slow"];
        if (!string.IsNullOrEmpty(config.TransitionSpeed) && !Array.Exists(validTransitionSpeed, v => v == config.TransitionSpeed))
            config.TransitionSpeed = "normal";

        // Validate URL schemes — reject javascript: and other non-http(s) schemes
        if (config.PermanentOverride?.Entries is not null)
        {
            foreach (var entry in config.PermanentOverride.Entries)
            {
                if (!IsUrlSafe(entry.Url))
                    return BadRequest($"Invalid URL in permanent entry: only http://, https://, and relative URLs are permitted.");
            }

            // Clamp active index to valid range
            var entryCount = config.PermanentOverride.Entries.Count;
            config.PermanentOverride.ActiveIndex = entryCount == 0
                ? -1
                : Math.Clamp(config.PermanentOverride.ActiveIndex, -1, entryCount - 1);
        }

        if (config.RotationMessages is not null)
        {
            foreach (var msg in config.RotationMessages)
            {
                if (!IsUrlSafe(msg.Url))
                    return BadRequest($"Invalid URL in rotation message: only http://, https://, and relative URLs are permitted.");
            }
        }

        // Validate schedule types
        if (config.PermanentOverride?.Entries is not null)
        {
            var err = ValidateSchedules(
                System.Linq.Enumerable.Select(config.PermanentOverride.Entries, e => e.Schedule),
                "permanent entry");
            if (err is not null) return BadRequest(err);
        }
        if (config.RotationMessages is not null)
        {
            var err = ValidateSchedules(
                System.Linq.Enumerable.Select(config.RotationMessages, m => m.Schedule),
                "rotation message");
            if (err is not null) return BadRequest(err);
        }

        config.LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Plugin.Instance.UpdateConfiguration(config);
        Plugin.Instance.SaveConfiguration();
        return NoContent();
    }

    private static readonly System.Collections.Generic.HashSet<string> _validScheduleTypes =
        new(System.StringComparer.Ordinal) { "always", "fixed", "annual", "weekly", "daily" };

    /// <summary>Returns an error message if any schedule in the collection has an invalid type, or null if all are valid.</summary>
    private static string? ValidateSchedules(System.Collections.Generic.IEnumerable<Configuration.BannerSchedule?> schedules, string context)
    {
        foreach (var sch in schedules)
        {
            if (sch is null) continue;
            if (!string.IsNullOrEmpty(sch.Type) && !_validScheduleTypes.Contains(sch.Type))
                return $"Invalid schedule type \"{sch.Type}\" in {context}: must be one of always, fixed, annual, weekly, daily.";
        }
        return null;
    }

    /// <summary>Returns true for null/empty URLs and URLs starting with http://, https://, or /.</summary>
    private static bool IsUrlSafe(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("/", StringComparison.Ordinal);
    }
}

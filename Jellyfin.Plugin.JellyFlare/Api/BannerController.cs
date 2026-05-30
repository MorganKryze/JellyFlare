using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyFlare.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyFlare.Api;

/// <summary>
/// Exposes the plugin configuration as JSON for the banner script.
/// All endpoints require authentication — the banner is intended for registered users only.
/// </summary>
[ApiController]
[Route("JellyFlare")]
public class BannerController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly ILogger<BannerController> _logger;

    /// <summary>Initializes a new instance of <see cref="BannerController"/>.</summary>
    public BannerController(IUserManager userManager, ILogger<BannerController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

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

        // Sanitize and validate schedule types
        if (config.PermanentOverride?.Entries is not null)
        {
            var err = ValidateSchedules(config.PermanentOverride.Entries.Select(e => e.Schedule), "permanent entry");
            if (err is not null) return BadRequest(err);
        }
        if (config.RotationMessages is not null)
        {
            var err = ValidateSchedules(config.RotationMessages.Select(m => m.Schedule), "rotation message");
            if (err is not null) return BadRequest(err);
        }

        // Sanitize route patterns (trim, remove empty) then validate
        config.PermanentOverride?.Entries?.ForEach(e => SanitizeRouteList(e.Routes));
        config.RotationMessages?.ForEach(m => SanitizeRouteList(m.Routes));

        if (config.PermanentOverride?.Entries is not null)
        {
            var err = ValidateRoutes(config.PermanentOverride.Entries.Select(e => (List<string>?)e.Routes), "permanent entry");
            if (err is not null) return BadRequest(err);
        }
        if (config.RotationMessages is not null)
        {
            var err = ValidateRoutes(config.RotationMessages.Select(m => (List<string>?)m.Routes), "rotation message");
            if (err is not null) return BadRequest(err);
        }

        // Maintenance mode is managed by its own endpoint — preserve the live state unchanged.
        config.MaintenanceMode = Plugin.Instance.Configuration.MaintenanceMode ?? new MaintenanceSetting();

        config.LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Plugin.Instance.UpdateConfiguration(config);
        Plugin.Instance.SaveConfiguration();
        return NoContent();
    }

    /// <summary>Returns the current maintenance mode configuration.</summary>
    [HttpGet("maintenance")]
    // No [Authorize] — intentionally public so the login-page overlay works for unauthenticated users.
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<MaintenanceSetting> GetMaintenance()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        return Ok(config.MaintenanceMode ?? new MaintenanceSetting());
    }

    /// <summary>Saves the maintenance mode configuration. Handles user enable/disable transitions server-side.</summary>
    [HttpPost("maintenance")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveMaintenance([FromBody] MaintenanceSetting maintenance)
    {
        ArgumentNullException.ThrowIfNull(maintenance);

        if (Plugin.Instance is null)
            return NotFound();

        if (!IsUrlSafe(maintenance.StatusUrl))
            return BadRequest("Invalid statusUrl: only http://, https://, and relative URLs are permitted.");

        if (maintenance.ScheduledStart.HasValue
            && maintenance.ScheduledEnd.HasValue
            && maintenance.ScheduledEnd.Value <= maintenance.ScheduledStart.Value)
            return BadRequest("scheduledEnd must be after scheduledStart.");

        var config = Plugin.Instance.Configuration;
        var wasActive = config.MaintenanceMode.IsActive;

        // Update non-user-tracking fields first so the helpers see the latest values when they save.
        config.MaintenanceMode.Message = maintenance.Message ?? config.MaintenanceMode.Message;
        config.MaintenanceMode.StatusUrl = maintenance.StatusUrl;
        config.MaintenanceMode.ScheduleEnabled = maintenance.ScheduleEnabled;
        config.MaintenanceMode.ScheduledStart = maintenance.ScheduledStart;
        config.MaintenanceMode.ScheduledEnd = maintenance.ScheduledEnd;
        config.MaintenanceMode.ScheduledRestart = maintenance.ScheduledRestart;

        if (!wasActive && maintenance.IsActive)
        {
            // Persist updated fields before activation so the helper reads the latest message/url.
            Plugin.Instance.UpdateConfiguration(config);
            Plugin.Instance.SaveConfiguration();
            await MaintenanceHelper.ActivateAsync(_userManager, _logger).ConfigureAwait(false);
        }
        else if (wasActive && !maintenance.IsActive)
        {
            Plugin.Instance.UpdateConfiguration(config);
            Plugin.Instance.SaveConfiguration();
            await MaintenanceHelper.DeactivateAsync(_userManager, _logger).ConfigureAwait(false);
        }
        else
        {
            Plugin.Instance.UpdateConfiguration(config);
            Plugin.Instance.SaveConfiguration();
        }

        return NoContent();
    }

    private static readonly HashSet<string> _validScheduleTypes =
        new(StringComparer.Ordinal) { "always", "fixed", "annual", "monthly", "weekly", "daily" };

    /// <summary>Returns an error message if any schedule in the collection has an invalid type, or null if all are valid.</summary>
    private static string? ValidateSchedules(IEnumerable<BannerSchedule?> schedules, string context)
    {
        foreach (var sch in schedules)
        {
            if (sch is null) continue;
            if (!string.IsNullOrEmpty(sch.Type) && !_validScheduleTypes.Contains(sch.Type))
                return $"Invalid schedule type \"{sch.Type}\" in {context}: must be one of always, fixed, annual, monthly, weekly, daily.";
        }
        return null;
    }

    /// <summary>Trims whitespace from each route pattern and removes empty entries in-place.</summary>
    private static void SanitizeRouteList(List<string> routes)
    {
        if (routes is null) return;
        routes.RemoveAll(string.IsNullOrWhiteSpace);
        for (var i = 0; i < routes.Count; i++)
            routes[i] = routes[i].Trim();
    }

    /// <summary>Returns an error message if any route pattern in the collection is invalid, or null if all are valid.</summary>
    private static string? ValidateRoutes(IEnumerable<List<string>?> routeLists, string context)
    {
        foreach (var list in routeLists)
        {
            if (list is null) continue;
            foreach (var pattern in list)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                if (!Regex.IsMatch(pattern, @"^[A-Za-z0-9\-._/*?=&#+%]+$"))
                    return $"Invalid route pattern \"{pattern}\" in {context}.";
                if (pattern.Length > 512)
                    return $"Route pattern in {context} exceeds 512 characters.";
            }
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

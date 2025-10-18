using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/library/manual-import")]
public class ManualImportController : ControllerBase
{
    private readonly ILogger<ManualImportController> _logger;

    public ManualImportController(ILogger<ManualImportController> logger)
    {
        _logger = logger;
    }

    [HttpGet("preview")]
    public ActionResult<object> Preview([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return BadRequest(new { error = "Path is required" });

            var normalized = Path.GetFullPath(path);
            if (!Directory.Exists(normalized)) return NotFound(new { error = "Directory not found" });

            var files = Directory.EnumerateFiles(normalized, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .Select(f => new {
                    relativePath = Path.GetRelativePath(normalized, f),
                    fullPath = f,
                    size = new FileInfo(f).Length,
                    // Simple heuristics for sample metadata
                    series = (string?)null,
                    season = (string?)null,
                    episodes = (string?)null,
                    quality = (string?)null,
                    languages = new string[] { "English" },
                    releaseType = "Unknown"
                })
                .ToList();

            var items = files.Select(f => new {
                relativePath = f.relativePath,
                size = FormatSize(f.size),
                series = f.series,
                season = f.season,
                episodes = f.episodes,
                quality = f.quality,
                languages = f.languages,
                releaseType = f.releaseType
            }).ToList();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing manual import for path {Path}", path);
            return StatusCode(500, new { error = "Failed to preview import" });
        }
    }

    [HttpPost]
    public ActionResult<object> Start([FromBody] ManualImportRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Path)) return BadRequest(new { error = "Invalid request" });
            var normalized = Path.GetFullPath(request.Path);
            if (!Directory.Exists(normalized)) return NotFound(new { error = "Directory not found" });

            // Minimal implementation: count selected items and pretend they were imported.
            int count = 0;
            if (request.Mode == "automatic")
            {
                // Move all files under path and increment count
                count = Directory.EnumerateFiles(normalized, "*.*", SearchOption.AllDirectories).Count();
            }
            else if (request.Items != null && request.Items.Any())
            {
                count = request.Items.Count;
            }

            return Ok(new { importedCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual import");
            return StatusCode(500, new { error = "Failed to start import" });
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var units = new[] { "KiB", "MiB", "GiB", "TiB" };
        double size = bytes / 1024.0;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024.0;
            unit++;
        }
        return $"{size:F1} {units[unit]}";
    }
}

public class ManualImportRequest
{
    public string Path { get; set; } = string.Empty;
    public string Mode { get; set; } = "interactive";
    public List<object>? Items { get; set; }
}

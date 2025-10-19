using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Listenarr.Api.Services;
using Listenarr.Api.Models;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/library/manual-import")]
public class ManualImportController : ControllerBase
{
    private readonly ILogger<ManualImportController> _logger;
    private readonly IAudiobookRepository _audiobookRepository;
    private readonly IMetadataService _metadataService;
    private readonly IFileNamingService _fileNamingService;
    private readonly IConfigurationService _configService;

    public ManualImportController(
        ILogger<ManualImportController> logger,
        IAudiobookRepository audiobookRepository,
        IMetadataService metadataService,
        IFileNamingService fileNamingService,
        IConfigurationService configService)
    {
        _logger = logger;
        _audiobookRepository = audiobookRepository;
        _metadataService = metadataService;
        _fileNamingService = fileNamingService;
        _configService = configService;
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
                fullPath = f.fullPath,
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
    public async Task<ActionResult<object>> Start([FromBody] ManualImportRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Path)) 
                return BadRequest(new { error = "Invalid request" });
            
            var normalized = Path.GetFullPath(request.Path);
            if (!Directory.Exists(normalized)) 
                return NotFound(new { error = "Directory not found" });

            if (request.Mode == "automatic")
            {
                // TODO: Implement automatic import
                return BadRequest(new { error = "Automatic import not yet implemented" });
            }
            else if (request.Mode == "interactive" && request.Items != null && request.Items.Any())
            {
                var results = new List<ManualImportResult>();
                foreach (var item in request.Items)
                {
                    var result = await ImportFileAsync(item, request.InputMode ?? "copy");
                    results.Add(result);
                }
                
                var successCount = results.Count(r => r.Success);
                return Ok(new { 
                    importedCount = successCount, 
                    totalCount = results.Count,
                    results = results
                });
            }

            return BadRequest(new { error = "No items to import" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual import");
            return StatusCode(500, new { error = "Failed to start import" });
        }
    }

    private async Task<ManualImportResult> ImportFileAsync(ManualImportItem item, string inputMode)
    {
        try
        {
            // Validate FullPath
            if (string.IsNullOrWhiteSpace(item.FullPath))
            {
                return new ManualImportResult 
                { 
                    Success = false, 
                    Error = "FullPath is required",
                    FilePath = item.FullPath 
                };
            }

            // Get the associated audiobook
            var audiobook = await _audiobookRepository.GetByIdAsync(item.MatchedAudiobookId);
            if (audiobook == null)
            {
                return new ManualImportResult 
                { 
                    Success = false, 
                    Error = $"Audiobook with ID {item.MatchedAudiobookId} not found",
                    FilePath = item.FullPath 
                };
            }

            // Check if source file exists
            if (!System.IO.File.Exists(item.FullPath))
            {
                return new ManualImportResult 
                { 
                    Success = false, 
                    Error = "Source file not found",
                    FilePath = item.FullPath 
                };
            }

            // Check if audiobook has a base path
            if (string.IsNullOrWhiteSpace(audiobook.BasePath))
            {
                var appSettings = await _configService.GetApplicationSettingsAsync() ?? new ApplicationSettings();
                var fallbackPath = appSettings.OutputPath;
                
                if (string.IsNullOrWhiteSpace(fallbackPath))
                {
                    return new ManualImportResult 
                    { 
                        Success = false, 
                        Error = "No base path configured for audiobook and no default output path set",
                        FilePath = item.FullPath 
                    };
                }
                
                // Use fallback path
                audiobook.BasePath = fallbackPath;
            }

            // Extract metadata from the file
            var metadata = await _metadataService.ExtractFileMetadataAsync(item.FullPath);
            if (metadata == null)
            {
                return new ManualImportResult 
                { 
                    Success = false, 
                    Error = "Failed to extract metadata from file",
                    FilePath = item.FullPath 
                };
            }

            // Generate destination path using only disc/chapter components
            var destinationPath = await GenerateManualImportPathAsync(audiobook, metadata);
            
            // Ensure destination directory exists
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Move or copy the file
            if (inputMode == "move")
            {
                System.IO.File.Move(item.FullPath, destinationPath);
                _logger.LogInformation("Moved file {Source} to {Destination}", item.FullPath, destinationPath);
            }
            else
            {
                System.IO.File.Copy(item.FullPath, destinationPath);
                _logger.LogInformation("Copied file {Source} to {Destination}", item.FullPath, destinationPath);
            }

            return new ManualImportResult 
            { 
                Success = true, 
                FilePath = item.FullPath,
                DestinationPath = destinationPath,
                AudiobookId = audiobook.Id,
                AudiobookTitle = audiobook.Title
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing file {FilePath}", item.FullPath);
            return new ManualImportResult 
            { 
                Success = false, 
                Error = ex.Message,
                FilePath = item.FullPath 
            };
        }
    }

    private async Task<string> GenerateManualImportPathAsync(Audiobook audiobook, AudioMetadata metadata)
    {
        // For manual import, we only use disc and chapter components from the naming pattern
        // The base path comes from the audiobook's BasePath
        
        var settings = await _configService.GetApplicationSettingsAsync() ?? new ApplicationSettings();
        var pattern = settings.FileNamingPattern;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}";
        }

        // Create a simplified pattern that only includes disc and chapter components
        // We need to extract just the disc/chapter parts from the full pattern
        var discChapterPattern = ExtractDiscChapterPattern(pattern);
        
        // Build variables dictionary with only disc/chapter data
        var variables = new Dictionary<string, object>
        {
            { "DiskNumber", metadata.DiscNumber?.ToString() ?? string.Empty },
            { "ChapterNumber", metadata.TrackNumber?.ToString() ?? string.Empty }
        };

        // Apply the simplified pattern
        var relativePath = _fileNamingService.ApplyNamingPattern(discChapterPattern, variables);
        
        // Get file extension
        var extension = Path.GetExtension(metadata.Title ?? "unknown").ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".m4b"; // Default extension
        }
        
        // Ensure the relative path has the correct extension
        if (!relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            relativePath += extension;
        }

        // Combine with audiobook's base path
        var basePath = audiobook.BasePath;
        if (string.IsNullOrWhiteSpace(basePath))
        {
            // This should have been checked earlier, but fallback to empty string
            basePath = string.Empty;
        }
        
        return Path.Combine(basePath, relativePath);
    }

    private string ExtractDiscChapterPattern(string fullPattern)
    {
        // Extract only the disc and chapter related parts from the pattern
        // Look for {DiskNumber} and {ChapterNumber}/{TrackNumber} tokens and build a minimal pattern
        
        var parts = new List<string>();
        var hasDisk = fullPattern.Contains("{DiskNumber");
        var hasChapter = fullPattern.Contains("{ChapterNumber") || fullPattern.Contains("{TrackNumber");
        
        if (hasDisk)
        {
            parts.Add("{DiskNumber:00}");
        }
        
        if (hasChapter)
        {
            if (parts.Count > 0) parts.Add(" - ");
            parts.Add("{ChapterNumber:00}");
        }
        
        // If no disc/chapter components found, use a default
        if (parts.Count == 0)
        {
            return "{DiskNumber:00} - {ChapterNumber:00}";
        }
        
        return string.Join("", parts);
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
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "interactive";
    
    [JsonPropertyName("inputMode")]
    public string? InputMode { get; set; } // "move" or "copy"
    
    [JsonPropertyName("items")]
    public List<ManualImportItem>? Items { get; set; }
}

public class ManualImportItem
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;
    
    [JsonPropertyName("fullPath")]
    [Required]
    public string? FullPath { get; set; }
    
    [JsonPropertyName("matchedAudiobookId")]
    public int MatchedAudiobookId { get; set; }
    
    [JsonPropertyName("releaseGroup")]
    public string? ReleaseGroup { get; set; }
    
    [JsonPropertyName("qualityProfileId")]
    public int? QualityProfileId { get; set; }
    
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    
    [JsonPropertyName("size")]
    public string? Size { get; set; }
}

public class ManualImportResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? DestinationPath { get; set; }
    public int? AudiobookId { get; set; }
    public string? AudiobookTitle { get; set; }
    public string? Error { get; set; }
}

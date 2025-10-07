using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Models;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DownloadsController : ControllerBase
{
    private readonly ListenArrDbContext _dbContext;
    private readonly ILogger<DownloadsController> _logger;

    public DownloadsController(ListenArrDbContext dbContext, ILogger<DownloadsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all downloads, optionally filtered by status
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Download>>> GetDownloads([FromQuery] string? status = null)
    {
        try
        {
            IQueryable<Download> query = _dbContext.Downloads;

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<DownloadStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(d => d.Status == parsedStatus);
                }
            }

            var downloads = await query
                .OrderByDescending(d => d.StartedAt)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} downloads", downloads.Count);
            return Ok(downloads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving downloads");
            return StatusCode(500, new { error = "Failed to retrieve downloads", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific download by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Download>> GetDownload(string id)
    {
        try
        {
            var download = await _dbContext.Downloads.FindAsync(id);

            if (download == null)
            {
                return NotFound(new { error = "Download not found", id });
            }

            return Ok(download);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving download {DownloadId}", id);
            return StatusCode(500, new { error = "Failed to retrieve download", message = ex.Message });
        }
    }

    /// <summary>
    /// Get active downloads (Queued or Downloading status)
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Download>>> GetActiveDownloads()
    {
        try
        {
            var activeDownloads = await _dbContext.Downloads
                .Where(d => d.Status == DownloadStatus.Queued || 
                           d.Status == DownloadStatus.Downloading ||
                           d.Status == DownloadStatus.Processing)
                .OrderByDescending(d => d.StartedAt)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} active downloads", activeDownloads.Count);
            return Ok(activeDownloads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active downloads");
            return StatusCode(500, new { error = "Failed to retrieve active downloads", message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a download record (does not cancel active download)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteDownload(string id)
    {
        try
        {
            var download = await _dbContext.Downloads.FindAsync(id);

            if (download == null)
            {
                return NotFound(new { error = "Download not found", id });
            }

            _dbContext.Downloads.Remove(download);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted download record {DownloadId}", id);
            return Ok(new { message = "Download deleted successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting download {DownloadId}", id);
            return StatusCode(500, new { error = "Failed to delete download", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear completed downloads
    /// </summary>
    [HttpDelete("completed")]
    public async Task<ActionResult> ClearCompletedDownloads()
    {
        try
        {
            var completedDownloads = await _dbContext.Downloads
                .Where(d => d.Status == DownloadStatus.Completed)
                .ToListAsync();

            _dbContext.Downloads.RemoveRange(completedDownloads);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cleared {Count} completed downloads", completedDownloads.Count);
            return Ok(new { message = "Completed downloads cleared", count = completedDownloads.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing completed downloads");
            return StatusCode(500, new { error = "Failed to clear completed downloads", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear failed downloads
    /// </summary>
    [HttpDelete("failed")]
    public async Task<ActionResult> ClearFailedDownloads()
    {
        try
        {
            var failedDownloads = await _dbContext.Downloads
                .Where(d => d.Status == DownloadStatus.Failed)
                .ToListAsync();

            _dbContext.Downloads.RemoveRange(failedDownloads);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cleared {Count} failed downloads", failedDownloads.Count);
            return Ok(new { message = "Failed downloads cleared", count = failedDownloads.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing failed downloads");
            return StatusCode(500, new { error = "Failed to clear failed downloads", message = ex.Message });
        }
    }
}

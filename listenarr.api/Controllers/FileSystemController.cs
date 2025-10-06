using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileSystemController : ControllerBase
{
    private readonly ILogger<FileSystemController> _logger;

    public FileSystemController(ILogger<FileSystemController> logger)
    {
        _logger = logger;
    }

    [HttpGet("browse")]
    public ActionResult<FileSystemBrowseResponse> BrowseDirectory([FromQuery] string? path)
    {
        try
        {
            // If no path provided, return root drives/directories
            if (string.IsNullOrWhiteSpace(path))
            {
                return GetRootDirectories();
            }

            // Validate and normalize the path
            var normalizedPath = Path.GetFullPath(path);
            
            if (!Directory.Exists(normalizedPath))
            {
                return NotFound(new { error = "Directory not found" });
            }

            var directories = new List<FileSystemItem>();
            var parent = Directory.GetParent(normalizedPath);

            try
            {
                // Get directories in the current path
                var dirInfo = new DirectoryInfo(normalizedPath);
                foreach (var dir in dirInfo.GetDirectories())
                {
                    // Skip hidden and system directories
                    if ((dir.Attributes & FileAttributes.Hidden) != 0 ||
                        (dir.Attributes & FileAttributes.System) != 0)
                    {
                        continue;
                    }

                    directories.Add(new FileSystemItem
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTime
                    });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to directory: {Path}", normalizedPath);
            }

            return new FileSystemBrowseResponse
            {
                CurrentPath = normalizedPath,
                ParentPath = parent?.FullName,
                Items = directories.OrderBy(d => d.Name).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing directory: {Path}", path);
            return StatusCode(500, new { error = "Error browsing directory" });
        }
    }

    [HttpGet("validate")]
    public ActionResult<FileSystemValidateResponse> ValidatePath([FromQuery] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new FileSystemValidateResponse
                {
                    IsValid = false,
                    Message = "Path cannot be empty"
                };
            }

            var normalizedPath = Path.GetFullPath(path);
            var exists = Directory.Exists(normalizedPath);
            var isWritable = false;
            
            if (exists)
            {
                try
                {
                    // Try to create a temporary file to check write permissions
                    var testFile = Path.Combine(normalizedPath, $".listenarr_test_{Guid.NewGuid()}.tmp");
                    System.IO.File.WriteAllText(testFile, "test");
                    System.IO.File.Delete(testFile);
                    isWritable = true;
                }
                catch
                {
                    isWritable = false;
                }
            }

            return new FileSystemValidateResponse
            {
                IsValid = exists && isWritable,
                Exists = exists,
                IsWritable = isWritable,
                Message = !exists ? "Directory does not exist" : 
                         !isWritable ? "Directory is not writable" : 
                         "Directory is valid"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating path: {Path}", path);
            return new FileSystemValidateResponse
            {
                IsValid = false,
                Message = $"Error validating path: {ex.Message}"
            };
        }
    }

    private FileSystemBrowseResponse GetRootDirectories()
    {
        var items = new List<FileSystemItem>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Get all drives on Windows
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    items.Add(new FileSystemItem
                    {
                        Name = $"{drive.Name} ({drive.VolumeLabel})",
                        Path = drive.Name,
                        IsDirectory = true,
                        LastModified = DateTime.Now
                    });
                }
            }
        }
        else
        {
            // Unix-like systems start at root
            items.Add(new FileSystemItem
            {
                Name = "/",
                Path = "/",
                IsDirectory = true,
                LastModified = DateTime.Now
            });

            // Add common directories
            var commonDirs = new[] { "/home", "/mnt", "/media", "/opt" };
            foreach (var dir in commonDirs)
            {
                if (Directory.Exists(dir))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    items.Add(new FileSystemItem
                    {
                        Name = dirInfo.Name,
                        Path = dirInfo.FullName,
                        IsDirectory = true,
                        LastModified = dirInfo.LastWriteTime
                    });
                }
            }
        }

        return new FileSystemBrowseResponse
        {
            CurrentPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Computer" : "/",
            ParentPath = null,
            Items = items
        };
    }
}

public class FileSystemBrowseResponse
{
    public string CurrentPath { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
    public List<FileSystemItem> Items { get; set; } = new();
}

public class FileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
}

public class FileSystemValidateResponse
{
    public bool IsValid { get; set; }
    public bool Exists { get; set; }
    public bool IsWritable { get; set; }
    public string Message { get; set; } = string.Empty;
}

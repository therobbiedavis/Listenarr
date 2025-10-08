using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Services;

public class RemotePathMappingService : IRemotePathMappingService
{
    private readonly ListenArrDbContext _context;
    private readonly ILogger<RemotePathMappingService> _logger;

    public RemotePathMappingService(
        ListenArrDbContext context,
        ILogger<RemotePathMappingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<RemotePathMapping>> GetAllAsync()
    {
        return await _context.RemotePathMappings
            .OrderBy(m => m.DownloadClientId)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<RemotePathMapping?> GetByIdAsync(int id)
    {
        return await _context.RemotePathMappings.FindAsync(id);
    }

    public async Task<List<RemotePathMapping>> GetByClientIdAsync(string downloadClientId)
    {
        return await _context.RemotePathMappings
            .Where(m => m.DownloadClientId == downloadClientId)
            .OrderBy(m => m.RemotePath.Length) // Order by path length descending for best match
            .ToListAsync();
    }

    public async Task<RemotePathMapping> CreateAsync(RemotePathMapping mapping)
    {
        // Normalize paths before saving
        mapping.NormalizePaths();
        
        mapping.CreatedAt = DateTime.UtcNow;
        mapping.UpdatedAt = DateTime.UtcNow;

        _context.RemotePathMappings.Add(mapping);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created remote path mapping {MappingId} for client {ClientId}: {RemotePath} -> {LocalPath}",
            mapping.Id, mapping.DownloadClientId, mapping.RemotePath, mapping.LocalPath);

        return mapping;
    }

    public async Task<RemotePathMapping> UpdateAsync(RemotePathMapping mapping)
    {
        var existing = await _context.RemotePathMappings.FindAsync(mapping.Id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Remote path mapping with ID {mapping.Id} not found");
        }

        // Normalize paths before saving
        mapping.NormalizePaths();

        existing.DownloadClientId = mapping.DownloadClientId;
        existing.Name = mapping.Name;
        existing.RemotePath = mapping.RemotePath;
        existing.LocalPath = mapping.LocalPath;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated remote path mapping {MappingId} for client {ClientId}: {RemotePath} -> {LocalPath}",
            mapping.Id, mapping.DownloadClientId, mapping.RemotePath, mapping.LocalPath);

        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var mapping = await _context.RemotePathMappings.FindAsync(id);
        if (mapping == null)
        {
            return false;
        }

        _context.RemotePathMappings.Remove(mapping);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted remote path mapping {MappingId} for client {ClientId}",
            id, mapping.DownloadClientId);

        return true;
    }

    public async Task<string> TranslatePathAsync(string downloadClientId, string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return remotePath;
        }

        // Normalize the input path for consistent comparison
        var normalizedRemotePath = NormalizePath(remotePath);

        // Get all mappings for this client, ordered by path length (longest first)
        // This ensures we match the most specific path first
        var mappings = await _context.RemotePathMappings
            .Where(m => m.DownloadClientId == downloadClientId)
            .OrderByDescending(m => m.RemotePath.Length)
            .ToListAsync();

        foreach (var mapping in mappings)
        {
            var normalizedMappingPath = NormalizePath(mapping.RemotePath);
            
            // Check if the remote path starts with this mapping's remote path
            if (normalizedRemotePath.StartsWith(normalizedMappingPath, StringComparison.OrdinalIgnoreCase))
            {
                // Replace the remote path prefix with the local path
                var relativePath = normalizedRemotePath.Substring(normalizedMappingPath.Length);
                var localPath = NormalizePath(mapping.LocalPath) + relativePath;

                _logger.LogDebug(
                    "Translated path for client {ClientId}: {RemotePath} -> {LocalPath} (using mapping {MappingId})",
                    downloadClientId, remotePath, localPath, mapping.Id);

                return localPath;
            }
        }

        _logger.LogDebug(
            "No path mapping found for client {ClientId} and path {RemotePath}, returning original path",
            downloadClientId, remotePath);

        // No mapping found, return original path
        return remotePath;
    }

    public async Task<bool> RequiresTranslationAsync(string downloadClientId, string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return false;
        }

        var normalizedRemotePath = NormalizePath(remotePath);

        // Check if any mapping matches this path
        var hasMapping = await _context.RemotePathMappings
            .AnyAsync(m => m.DownloadClientId == downloadClientId &&
                          normalizedRemotePath.StartsWith(NormalizePath(m.RemotePath)));

        return hasMapping;
    }

    /// <summary>
    /// Normalize a path for consistent comparison:
    /// - Convert backslashes to forward slashes
    /// - Ensure trailing slash
    /// - Trim whitespace
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        path = path.Trim().Replace('\\', '/');
        
        // Ensure trailing slash for directory paths
        if (!path.EndsWith('/'))
        {
            path += '/';
        }

        return path;
    }
}

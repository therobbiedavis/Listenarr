using Listenarr.Api.Models;

namespace Listenarr.Api.Services;

/// <summary>
/// Service for managing remote path mappings between download clients and Listenarr.
/// Used to translate file paths when download clients are in different containers/systems
/// with different mount points than Listenarr.
/// </summary>
public interface IRemotePathMappingService
{
    /// <summary>
    /// Get all remote path mappings
    /// </summary>
    Task<List<RemotePathMapping>> GetAllAsync();

    /// <summary>
    /// Get a specific remote path mapping by ID
    /// </summary>
    Task<RemotePathMapping?> GetByIdAsync(int id);

    /// <summary>
    /// Get all remote path mappings for a specific download client
    /// </summary>
    Task<List<RemotePathMapping>> GetByClientIdAsync(string downloadClientId);

    /// <summary>
    /// Create a new remote path mapping
    /// </summary>
    Task<RemotePathMapping> CreateAsync(RemotePathMapping mapping);

    /// <summary>
    /// Update an existing remote path mapping
    /// </summary>
    Task<RemotePathMapping> UpdateAsync(RemotePathMapping mapping);

    /// <summary>
    /// Delete a remote path mapping
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Translate a remote path from a download client to a local path for Listenarr.
    /// Finds the best matching path mapping for the given client and applies it.
    /// </summary>
    /// <param name="downloadClientId">The ID of the download client reporting the path</param>
    /// <param name="remotePath">The path as reported by the download client</param>
    /// <returns>The translated local path, or the original path if no mapping matches</returns>
    Task<string> TranslatePathAsync(string downloadClientId, string remotePath);

    /// <summary>
    /// Check if a path needs translation for a given download client
    /// </summary>
    Task<bool> RequiresTranslationAsync(string downloadClientId, string remotePath);
}

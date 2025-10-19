using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public interface ISearchService
    {
        Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false);
        Task<List<SearchResult>> SearchByApiAsync(string apiId, string query, string? category = null);
        Task<bool> TestApiConnectionAsync(string apiId);
        Task<List<SearchResult>> SearchIndexersAsync(string query, string? category = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false);
    }

    public interface IDownloadService
    {
        Task<string> StartDownloadAsync(SearchResult searchResult, string downloadClientId, int? audiobookId = null);
        Task<List<Download>> GetActiveDownloadsAsync();
        Task<Download?> GetDownloadAsync(string downloadId);
        Task<bool> CancelDownloadAsync(string downloadId);
        Task UpdateDownloadStatusAsync();
        Task<SearchAndDownloadResult> SearchAndDownloadAsync(int audiobookId);
        Task<string> SendToDownloadClientAsync(SearchResult searchResult, string? downloadClientId = null, int? audiobookId = null);
        Task<List<QueueItem>> GetQueueAsync();
        Task<bool> RemoveFromQueueAsync(string downloadId, string? downloadClientId = null);
        // Exposed for processing completed downloads (move/copy + DB updates)
        Task ProcessCompletedDownloadAsync(string downloadId, string finalPath);
        
        // Reprocessing methods for existing downloads
        Task<string?> ReprocessDownloadAsync(string downloadId);
        Task<List<ReprocessResult>> ReprocessDownloadsAsync(List<string> downloadIds);
        Task<List<ReprocessResult>> ReprocessAllCompletedDownloadsAsync(bool includeProcessed = false, TimeSpan? maxAge = null);

        // Test a download client configuration (returns success flag, message and optionally the client back)
        Task<(bool Success, string Message, Listenarr.Api.Models.DownloadClientConfiguration? Client)> TestDownloadClientAsync(Listenarr.Api.Models.DownloadClientConfiguration client);
    }

    public interface IMetadataService
    {
        Task<AudioMetadata?> GetMetadataAsync(string title, string? artist = null, string? isbn = null);
        Task<AudioMetadata> ExtractFileMetadataAsync(string filePath);
        Task ApplyMetadataAsync(string filePath, AudioMetadata metadata);
        Task<byte[]?> DownloadCoverArtAsync(string coverArtUrl);
    }

    public interface IFileProcessingService
    {
        Task ProcessCompletedDownloadAsync(string downloadId);
        Task<string> OrganizeFileAsync(string sourceFilePath, AudioMetadata metadata);
        Task<bool> ValidateFileAsync(string filePath);
        Task CleanupTempFilesAsync();
    }

    public interface IConfigurationService
    {
        Task<List<ApiConfiguration>> GetApiConfigurationsAsync();
        Task<ApiConfiguration?> GetApiConfigurationAsync(string id);
        Task<string> SaveApiConfigurationAsync(ApiConfiguration config);
        Task<bool> DeleteApiConfigurationAsync(string id);
        
        Task<List<DownloadClientConfiguration>> GetDownloadClientConfigurationsAsync();
        Task<DownloadClientConfiguration?> GetDownloadClientConfigurationAsync(string id);
        Task<string> SaveDownloadClientConfigurationAsync(DownloadClientConfiguration config);
        Task<bool> DeleteDownloadClientConfigurationAsync(string id);
        
        Task<ApplicationSettings> GetApplicationSettingsAsync();
        Task SaveApplicationSettingsAsync(ApplicationSettings settings);
        
        Task<StartupConfig> GetStartupConfigAsync();
        Task SaveStartupConfigAsync(StartupConfig config);
    }

    public interface INotificationService
    {
        Task SendDownloadCompletedNotificationAsync(Download download);
        Task SendDownloadFailedNotificationAsync(Download download, string error);
        Task SendSystemNotificationAsync(string title, string message);
    }

    public interface IAmazonAsinService
    {
        Task<(bool Success, string? Asin, string? Error)> GetAsinFromIsbnAsync(string isbn, CancellationToken ct = default);
    }

    public interface IOpenLibraryService
    {
        Task<List<string>> GetIsbnsForTitleAsync(string title, string? author = null);
        Task<OpenLibrarySearchResponse> SearchBooksAsync(string title, string? author = null, int limit = 10);
    }

    public interface IAudibleSearchService
    {
        Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query);
    }

    public interface IFileNamingService
    {
        /// <summary>
        /// Apply the configured file naming pattern to generate the final file path
        /// </summary>
        /// <param name="metadata">Audiobook metadata</param>
        /// <param name="diskNumber">Optional disk/part number</param>
        /// <param name="chapterNumber">Optional chapter number</param>
        /// <param name="originalExtension">File extension (e.g., ".m4b", ".mp3")</param>
        /// <returns>Full file path using the naming pattern</returns>
        Task<string> GenerateFilePathAsync(AudioMetadata metadata, int? diskNumber = null, int? chapterNumber = null, string originalExtension = ".m4b");
        
        /// <summary>
        /// Apply the configured file naming pattern to generate the final file path with a specific output path
        /// </summary>
        /// <param name="metadata">Audiobook metadata</param>
        /// <param name="outputPath">Specific output path to use</param>
        /// <param name="diskNumber">Optional disk/part number</param>
        /// <param name="chapterNumber">Optional chapter number</param>
        /// <param name="originalExtension">File extension (e.g., ".m4b", ".mp3")</param>
        /// <returns>Full file path using the naming pattern</returns>
        Task<string> GenerateFilePathAsync(AudioMetadata metadata, string outputPath, int? diskNumber = null, int? chapterNumber = null, string originalExtension = ".m4b");
        
    /// <summary>
    /// Parse a naming pattern and replace variables with actual values
    /// </summary>
    string ApplyNamingPattern(string pattern, Dictionary<string, object> variables, bool treatAsFilename = false);
    }

    public interface IAudioFileService
    {
        /// <summary>
        /// Ensure an AudiobookFile record exists for the given audiobook and file path. Extract metadata (ffprobe/taglib) and persist file-level metadata.
        /// Returns true if a new record was created, false if it already existed.
        /// </summary>
        Task<bool> EnsureAudiobookFileAsync(int audiobookId, string filePath, string? source = "scan");
    }
}
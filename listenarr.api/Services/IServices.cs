using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public interface ISearchService
    {
        Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null);
        Task<List<SearchResult>> SearchByApiAsync(string apiId, string query, string? category = null);
        Task<bool> TestApiConnectionAsync(string apiId);
    }

    public interface IDownloadService
    {
        Task<string> StartDownloadAsync(SearchResult searchResult, string downloadClientId);
        Task<List<Download>> GetActiveDownloadsAsync();
        Task<Download?> GetDownloadAsync(string downloadId);
        Task<bool> CancelDownloadAsync(string downloadId);
        Task UpdateDownloadStatusAsync();
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
}
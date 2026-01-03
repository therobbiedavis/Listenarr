using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services.Adapters
{
    /// <summary>
    /// Encapsulates all download-client specific operations. Implement an adapter per client to keep
    /// protocol details isolated from the orchestration layer.
    /// </summary>
    public interface IDownloadClientAdapter
    {
        string ClientId { get; }
        string ClientType { get; }

        Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default);
        Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default);
        Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default);
        Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default);
        Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default);

        /// <summary>
        /// Resolves the actual import item for a completed download.
        /// Called just before import to ensure the most accurate path and metadata.
        /// EXACTLY matches Sonarr's GetImportItem pattern.
        /// </summary>
        /// <param name="client">Download client configuration</param>
        /// <param name="download">The completed download record</param>
        /// <param name="queueItem">The queue item representing this download</param>
        /// <param name="previousAttempt">Previous import attempt for retry scenarios (can be null)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Updated queue item with resolved OutputPath, or original if unable to determine</returns>
        Task<QueueItem> GetImportItemAsync(
            DownloadClientConfiguration client,
            Download download,
            QueueItem queueItem,
            QueueItem? previousAttempt = null,
            CancellationToken ct = default);
    }
}

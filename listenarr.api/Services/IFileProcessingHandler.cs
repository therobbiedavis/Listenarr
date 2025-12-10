namespace Listenarr.Api.Services
{
    /// <summary>
    /// Handler responsible for processing MoveOrCopy file jobs extracted from DownloadProcessingBackgroundService.
    /// Implementations encapsulate file naming, move/copy operations, verification and post-processing (scan enqueue).
    /// </summary>
    public interface IFileProcessingHandler
    {
        /// <summary>
        /// Process a MoveOrCopyFile job. The IServiceScope passed in can be used to resolve scoped services
        /// (DbContext, repositories) needed for the operation.
        /// </summary>
        Task HandleAsync(DownloadProcessingJob job, IServiceScope scope, CancellationToken cancellationToken);
    }
}

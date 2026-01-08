using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Strategies;

/// <summary>
/// Strategy for fetching metadata from different sources (Audimeta, Audnexus, scrapers, etc.).
/// </summary>
public interface IMetadataStrategy
{
    /// <summary>
    /// Name of the metadata source (e.g., "Audimeta", "Audnexus").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Checks if this strategy can handle the given metadata source.
    /// </summary>
    bool CanHandle(ApiConfiguration source);

    /// <summary>
    /// Attempts to fetch metadata for the given ASIN.
    /// </summary>
    /// <param name="asin">The ASIN to fetch metadata for</param>
    /// <param name="source">The metadata source configuration</param>
    /// <param name="originalSource">Original source where the ASIN was found (Amazon/Audible)</param>
    /// <returns>Metadata if successful, null otherwise</returns>
    Task<AudibleBookMetadata?> FetchMetadataAsync(string asin, ApiConfiguration source, string? originalSource);
}

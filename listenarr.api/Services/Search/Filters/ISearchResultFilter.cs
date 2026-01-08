using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Interface for filtering search results based on specific criteria.
/// </summary>
public interface ISearchResultFilter
{
    /// <summary>
    /// Determines if the result should be filtered out (excluded).
    /// </summary>
    /// <param name="result">The search result to evaluate</param>
    /// <returns>True if the result should be filtered out, false to keep it</returns>
    bool ShouldFilter(SearchResult result);

    /// <summary>
    /// Reason why the result was filtered (for logging/debugging).
    /// </summary>
    string FilterReason { get; }
}

using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Applies a pipeline of filters to search results.
/// </summary>
public class SearchResultFilterPipeline
{
    private readonly IEnumerable<ISearchResultFilter> _filters;
    private readonly ILogger<SearchResultFilterPipeline> _logger;

    public SearchResultFilterPipeline(IEnumerable<ISearchResultFilter> filters, ILogger<SearchResultFilterPipeline> logger)
    {
        _filters = filters;
        _logger = logger;
    }

    /// <summary>
    /// Filters a list of search results through all registered filters.
    /// </summary>
    /// <param name="results">Results to filter</param>
    /// <param name="logFilteredResults">Whether to log filtered results</param>
    /// <returns>Filtered list with unwanted results removed</returns>
    public List<SearchResult> ApplyFilters(List<SearchResult> results, bool logFilteredResults = true)
    {
        var filtered = new List<SearchResult>();

        foreach (var result in results)
        {
            bool shouldFilter = false;
            string? filterReason = null;

            foreach (var filter in _filters)
            {
                if (filter.ShouldFilter(result))
                {
                    shouldFilter = true;
                    filterReason = filter.FilterReason;
                    break;
                }
            }

            if (shouldFilter)
            {
                if (logFilteredResults)
                {
                    _logger.LogInformation("Filtered out result: {Title} (ASIN: {Asin}) - Reason: {Reason}", 
                        result.Title, result.Asin, filterReason);
                }
            }
            else
            {
                filtered.Add(result);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Checks if a single result would be filtered.
    /// </summary>
    public bool WouldFilter(SearchResult result, out string? filterReason)
    {
        foreach (var filter in _filters)
        {
            if (filter.ShouldFilter(result))
            {
                filterReason = filter.FilterReason;
                return true;
            }
        }

        filterReason = null;
        return false;
    }
}

using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Filters out results with missing critical information (author or title).
/// </summary>
public class MissingInformationFilter : ISearchResultFilter
{
    public string FilterReason => "missing_author_or_title";

    public bool ShouldFilter(SearchResult result)
    {
        return string.IsNullOrWhiteSpace(result.Artist) || string.IsNullOrWhiteSpace(result.Title);
    }
}

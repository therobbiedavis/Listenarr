using Listenarr.Api.Services.Search;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Filters out Kindle Edition ebooks which are not audiobooks.
/// </summary>
public class KindleEditionFilter : ISearchResultFilter
{
    public string FilterReason => "kindle_edition_filtered";

    public bool ShouldFilter(SearchResult result)
    {
        return SearchValidation.IsKindleEdition(result.Title);
    }
}

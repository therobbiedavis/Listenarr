using Listenarr.Api.Services.Search;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Filters out promotional or noise titles like "Best of", "Collection", etc.
/// </summary>
public class PromotionalTitleFilter : ISearchResultFilter
{
    public string FilterReason => "promotional_title_filtered";

    public bool ShouldFilter(SearchResult result)
    {
        return SearchValidation.IsPromotionalTitle(result.Title) || SearchValidation.IsTitleNoise(result.Title);
    }
}

using Listenarr.Api.Services.Search;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services.Search.Filters;

/// <summary>
/// Filters out results that look like physical products or have seller-like authors.
/// </summary>
public class ProductLikeTitleFilter : ISearchResultFilter
{
    public string FilterReason => "product_like_filtered";

    public bool ShouldFilter(SearchResult result)
    {
        return SearchValidation.IsProductLikeTitle(result.Title) || SearchValidation.IsSellerArtist(result.Artist);
    }
}

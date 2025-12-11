namespace Listenarr.Api.Services.Search;

/// <summary>
/// Encapsulates all search configuration parameters.
/// </summary>
public class SearchConfiguration
{
    public string Query { get; set; } = string.Empty;
    public string? Category { get; set; }
    public List<string>? ApiIds { get; set; }
    public SearchSortBy SortBy { get; set; } = SearchSortBy.Seeders;
    public SearchSortDirection SortDirection { get; set; } = SearchSortDirection.Descending;
    public bool IsAutomaticSearch { get; set; }

    public SearchConfiguration() { }

    public SearchConfiguration(
        string query,
        string? category = null,
        List<string>? apiIds = null,
        SearchSortBy sortBy = SearchSortBy.Seeders,
        SearchSortDirection sortDirection = SearchSortDirection.Descending,
        bool isAutomaticSearch = false)
    {
        Query = query;
        Category = category;
        ApiIds = apiIds;
        SortBy = sortBy;
        SortDirection = sortDirection;
        IsAutomaticSearch = isAutomaticSearch;
    }
}

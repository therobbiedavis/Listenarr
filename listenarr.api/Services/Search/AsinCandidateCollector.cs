using Listenarr.Api.Services.Search;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search;

/// <summary>
/// Collects ASIN candidates from Amazon, Audible, and OpenLibrary sources.
/// </summary>
public class AsinCandidateCollector
{
    private readonly ILogger<AsinCandidateCollector> _logger;
    private readonly IOpenLibraryService _openLibraryService;
    private readonly MetadataConverters _metadataConverters;
    private readonly SearchProgressReporter _searchProgressReporter;

    public AsinCandidateCollector(
        ILogger<AsinCandidateCollector> logger,
        IOpenLibraryService openLibraryService,
        MetadataConverters metadataConverters,
        SearchProgressReporter searchProgressReporter)
    {
        _logger = logger;
        _openLibraryService = openLibraryService;
        _metadataConverters = metadataConverters;
        _searchProgressReporter = searchProgressReporter;
    }

    /// <summary>
    /// Collects ASIN candidates from multiple sources.
    /// </summary>
    public async Task<AsinCandidateCollection> CollectCandidatesAsync(
        List<AmazonSearchResult> amazonResults,
        List<AudibleSearchResult> audibleResults,
        string query,
        bool skipOpenLibrary = false,
        int amazonProviderCap = 50,
        int audibleProviderCap = 50)
    {
        var collection = new AsinCandidateCollection();
        
        _logger.LogInformation("Collected {AmazonCount} Amazon raw results and {AudibleCount} Audible raw results", 
            amazonResults.Count, audibleResults.Count);

        // Populate ASIN candidates from Amazon results with detailed logging
        foreach (var a in amazonResults.Take(amazonProviderCap))
        {
            if (string.IsNullOrEmpty(a.Asin))
            {
                _logger.LogInformation("Amazon search result missing ASIN. Title='{Title}', Author='{Author}'", a.Title, a.Author);
                continue;
            }

            if (!SearchValidation.IsValidAsin(a.Asin!))
            {
                _logger.LogInformation("Amazon search result had invalid ASIN '{Asin}'. Title='{Title}', Author='{Author}'", 
                    a.Asin, a.Title, a.Author);
                continue;
            }

            // Filter obvious non-audiobook product results early
            if (SearchValidation.IsProductLikeTitle(a.Title) || SearchValidation.IsSellerArtist(a.Author))
            {
                _logger.LogInformation("Skipping Amazon ASIN {Asin} because title/author looks like a product or seller: Title='{Title}', Author='{Author}'", 
                    a.Asin, a.Title, a.Author);
                continue;
            }

            collection.AsinCandidates.Add(a.Asin!);
            collection.AsinToRawResult[a.Asin!] = (a.Title ?? "", a.Author ?? "", a.ImageUrl);
            collection.AsinToSource[a.Asin!] = "Amazon";
            _logger.LogInformation("Added Amazon ASIN candidate {Asin} Title='{Title}' Author='{Author}' ImageUrl='{ImageUrl}'", 
                a.Asin, a.Title, a.Author, a.ImageUrl);
        }

        // Populate from Audible results
        foreach (var a in audibleResults.Where(a => !string.IsNullOrEmpty(a.Asin) && SearchValidation.IsValidAsin(a.Asin!)).Take(audibleProviderCap))
        {
            // Filter obvious non-audiobook results even from Audible (defensive)
            if (SearchValidation.IsProductLikeTitle(a.Title) || SearchValidation.IsSellerArtist(a.Author))
            {
                _logger.LogInformation("Skipping Audible ASIN {Asin} because title/author looks like a product or seller: Title='{Title}', Author='{Author}'", 
                    a.Asin, a.Title, a.Author);
                continue;
            }

            if (collection.AsinToRawResult.TryAdd(a.Asin!, (a.Title ?? "", a.Author ?? "", a.ImageUrl)))
            {
                collection.AsinCandidates.Add(a.Asin!);
                collection.AsinToAudibleResult[a.Asin!] = a;  // Store full Audible search result
                collection.AsinToSource[a.Asin!] = "Audible";
            }
        }

        // Augment ASIN candidates with OpenLibrary suggestions
        if (!skipOpenLibrary && !string.IsNullOrEmpty(query))
        {
            await CollectOpenLibraryCandidatesAsync(query, collection);
        }

        return collection;
    }

    private async Task CollectOpenLibraryCandidatesAsync(string query, AsinCandidateCollection collection)
    {
        try
        {
            await _searchProgressReporter.BroadcastAsync($"Searching OpenLibrary for additional titles", null);
            var books = await _openLibraryService.SearchBooksAsync(query, null, 5);
            
            foreach (var book in books.Docs.Take(3))
            {
                if (!string.IsNullOrEmpty(book.Title) && !string.Equals(book.Title, query, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("OpenLibrary suggested title: {Title}", book.Title);
                    await _searchProgressReporter.BroadcastAsync($"OpenLibrary found: {book.Title}", null);

                    // Convert OpenLibrary work/edition into minimal AudibleBookMetadata and SearchResult
                    try
                    {
                        string? coverUrl = null;
                        if (book.CoverId.HasValue && book.CoverId.Value > 0)
                        {
                            coverUrl = $"https://covers.openlibrary.org/b/id/{book.CoverId}-L.jpg";
                        }

                        var metadata = new AudibleBookMetadata
                        {
                            Asin = null,
                            Source = "OpenLibrary",
                            Title = book.Title,
                            Authors = book.AuthorName?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList(),
                            Publisher = (book.Publisher?.Count > 1) ? "Multiple" : book.Publisher?.FirstOrDefault(),
                            PublishYear = book.FirstPublishYear?.ToString(),
                            Description = null,
                            ImageUrl = coverUrl
                        };

                        var searchResult = await _metadataConverters.ConvertMetadataToSearchResultAsync(metadata, string.Empty);
                        searchResult.IsEnriched = true;
                        searchResult.MetadataSource = "OpenLibrary";

                        // If OpenLibrary provides a canonical key (work or edition), expose it
                        if (!string.IsNullOrWhiteSpace(book.Key))
                        {
                            if (book.Key.StartsWith("/works", StringComparison.OrdinalIgnoreCase))
                            {
                                searchResult.ProductUrl = $"https://openlibrary.org{book.Key}";
                                searchResult.ResultUrl = $"https://openlibrary.org{book.Key}.json";
                            }
                            else if (book.Key.StartsWith("/books", StringComparison.OrdinalIgnoreCase))
                            {
                                searchResult.ProductUrl = $"https://openlibrary.org{book.Key}";
                                searchResult.ResultUrl = $"https://openlibrary.org{book.Key}.json";
                            }
                        }

                        collection.OpenLibraryDerivedResults.Add(searchResult);
                        collection.AsinToOpenLibrary[book.Key ?? Guid.NewGuid().ToString()] = book;
                    }
                    catch (Exception exConvert)
                    {
                        _logger.LogWarning(exConvert, "Failed to convert OpenLibrary book to SearchResult: {Title}", book.Title);
                    }
                }
            }
        }
        catch (Exception exOL)
        {
            _logger.LogWarning(exOL, "OpenLibrary augmentation failed: {Message}", exOL.Message);
        }
    }
}

/// <summary>
/// Contains collected ASIN candidates and associated metadata.
/// </summary>
public class AsinCandidateCollection
{
    public List<string> AsinCandidates { get; } = new List<string>();
    public Dictionary<string, (string Title, string Author, string? ImageUrl)> AsinToRawResult { get; } = new Dictionary<string, (string, string, string?)>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AudibleSearchResult> AsinToAudibleResult { get; } = new Dictionary<string, AudibleSearchResult>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AsinToSource { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, OpenLibraryBook> AsinToOpenLibrary { get; } = new Dictionary<string, OpenLibraryBook>(StringComparer.OrdinalIgnoreCase);
    public List<SearchResult> OpenLibraryDerivedResults { get; } = new List<SearchResult>();
}

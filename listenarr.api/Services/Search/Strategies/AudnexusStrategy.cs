using Listenarr.Api.Services.Search;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search.Strategies;

/// <summary>
/// Fetches metadata from Audnexus API (audnex.us).
/// </summary>
public class AudnexusStrategy : IMetadataStrategy
{
    private readonly IAudnexusService _audnexusService;
    private readonly MetadataConverters _metadataConverters;
    private readonly ILogger<AudnexusStrategy> _logger;

    public string SourceName => "Audnexus";

    public AudnexusStrategy(
        IAudnexusService audnexusService,
        MetadataConverters metadataConverters,
        ILogger<AudnexusStrategy> logger)
    {
        _audnexusService = audnexusService;
        _metadataConverters = metadataConverters;
        _logger = logger;
    }

    public bool CanHandle(ApiConfiguration source)
    {
        return source.BaseUrl.Contains("audnex.us", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AudibleBookMetadata?> FetchMetadataAsync(string asin, ApiConfiguration source, string? originalSource)
    {
        _logger.LogDebug("Calling Audnexus service for ASIN {Asin}", asin);
        var audnexusData = await _audnexusService.GetBookMetadataAsync(asin, "us", true, false);

        if (audnexusData != null)
        {
            _logger.LogInformation("✓ Audnexus returned data for ASIN {Asin}. Title: {Title}", asin, audnexusData.Title ?? "null");
            var metadata = _metadataConverters.ConvertAudnexusToMetadata(audnexusData, asin, originalSource ?? "Audible");
            _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName}", asin, source.Name);
            return metadata;
        }
        else
        {
            _logger.LogWarning("✗ Audnexus returned null for ASIN {Asin}", asin);
        }

        return null;
    }
}

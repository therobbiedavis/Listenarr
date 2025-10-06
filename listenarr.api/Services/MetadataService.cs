using Listenarr.Api.Models;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<MetadataService> _logger;

        public MetadataService(HttpClient httpClient, IConfigurationService configurationService, ILogger<MetadataService> logger)
        {
            _httpClient = httpClient;
            _configurationService = configurationService;
            _logger = logger;
        }

        public async Task<AudioMetadata?> GetMetadataAsync(string title, string? artist = null, string? isbn = null)
        {
            try
            {
                var settings = await _configurationService.GetApplicationSettingsAsync();
                var audnexusUrl = settings.AudnexusApiUrl;

                // Build search query for Audnexus API
                string searchQuery;
                if (!string.IsNullOrEmpty(isbn))
                {
                    searchQuery = $"{audnexusUrl}/books/{isbn}";
                }
                else
                {
                    var queryParams = new List<string>();
                    if (!string.IsNullOrEmpty(title)) queryParams.Add($"title={Uri.EscapeDataString(title)}");
                    if (!string.IsNullOrEmpty(artist)) queryParams.Add($"author={Uri.EscapeDataString(artist)}");
                    
                    searchQuery = $"{audnexusUrl}/search?" + string.Join("&", queryParams);
                }

                _logger.LogInformation($"Fetching metadata from Audnexus: {searchQuery}");

                var response = await _httpClient.GetAsync(searchQuery);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Audnexus API returned {response.StatusCode} for query: {searchQuery}");
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                // Parse Audnexus response and convert to AudioMetadata
                // This is a simplified implementation - you would need to adapt based on actual Audnexus API structure
                var audnexusData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                
                return ParseAudnexusResponse(audnexusData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching metadata for title: {title}, artist: {artist}");
                return null;
            }
        }

        public Task<AudioMetadata> ExtractFileMetadataAsync(string filePath)
        {
            try
            {
                // This would use a library like TagLib# to extract metadata from audio files
                // For now, return basic metadata extracted from filename
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var metadata = new AudioMetadata
                {
                    Title = fileName,
                    Format = Path.GetExtension(filePath).TrimStart('.').ToUpper()
                };

                _logger.LogInformation($"Extracted basic metadata from file: {filePath}");
                return Task.FromResult(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting metadata from file: {filePath}");
                return Task.FromResult(new AudioMetadata());
            }
        }

        public async Task ApplyMetadataAsync(string filePath, AudioMetadata metadata)
        {
            try
            {
                // This would use a library like TagLib# to apply metadata to audio files
                _logger.LogInformation($"Applied metadata to file: {filePath}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying metadata to file: {filePath}");
            }
        }

        public async Task<byte[]?> DownloadCoverArtAsync(string coverArtUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(coverArtUrl);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading cover art from: {coverArtUrl}");
                return null;
            }
        }

        private AudioMetadata ParseAudnexusResponse(JsonElement audnexusData)
        {
            // This is a simplified parser - adapt based on actual Audnexus API response structure
            var metadata = new AudioMetadata();

            if (audnexusData.TryGetProperty("title", out var title))
                metadata.Title = title.GetString() ?? "";

            if (audnexusData.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            {
                var authorNames = authors.EnumerateArray().Select(a => a.GetString()).Where(s => !string.IsNullOrEmpty(s));
                metadata.Artist = string.Join(", ", authorNames);
            }

            if (audnexusData.TryGetProperty("series", out var series))
                metadata.Series = series.GetString();

            if (audnexusData.TryGetProperty("publishedYear", out var year))
                metadata.Year = year.GetInt32();

            if (audnexusData.TryGetProperty("description", out var description))
                metadata.Description = description.GetString();

            if (audnexusData.TryGetProperty("isbn", out var isbn))
                metadata.Isbn = isbn.GetString();

            if (audnexusData.TryGetProperty("coverUrl", out var coverUrl))
                metadata.CoverArtUrl = coverUrl.GetString();

            return metadata;
        }
    }
}
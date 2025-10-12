/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    public class DownloadService : IDownloadService
    {
        private readonly IHubContext<DownloadHub> _hubContext;
        private readonly IAudiobookRepository _audiobookRepository;
        private readonly IConfigurationService _configurationService;
        private readonly ListenArrDbContext _dbContext;
        private readonly ILogger<DownloadService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly ISearchService _searchService;

        public DownloadService(
            IAudiobookRepository audiobookRepository,
            IConfigurationService configurationService,
            ListenArrDbContext dbContext,
            ILogger<DownloadService> logger,
            HttpClient httpClient,
            IServiceScopeFactory serviceScopeFactory,
            IRemotePathMappingService pathMappingService,
            ISearchService searchService,
            IHubContext<DownloadHub> hubContext)
        {
            _audiobookRepository = audiobookRepository;
            _configurationService = configurationService;
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClient;
            _serviceScopeFactory = serviceScopeFactory;
            _pathMappingService = pathMappingService;
            _searchService = searchService;
            _hubContext = hubContext;
        }

        // Placeholder implementations for existing interface methods
        public async Task<string> StartDownloadAsync(SearchResult searchResult, string downloadClientId, int? audiobookId = null)
        {
            return await SendToDownloadClientAsync(searchResult, downloadClientId, audiobookId);
        }

        public async Task<List<Download>> GetActiveDownloadsAsync()
        {
            // TODO: Implement download status tracking
            return await Task.FromResult(new List<Download>());
        }

        public async Task<Download?> GetDownloadAsync(string downloadId)
        {
            // TODO: Implement download retrieval
            return await Task.FromResult<Download?>(null);
        }

        public async Task<bool> CancelDownloadAsync(string downloadId)
        {
            // TODO: Implement download cancellation
            return await Task.FromResult(false);
        }

        public async Task UpdateDownloadStatusAsync()
        {
            // TODO: Implement download status updates
            await Task.CompletedTask;
        }

        public async Task<SearchAndDownloadResult> SearchAndDownloadAsync(int audiobookId)
        {
            // Get the audiobook
            var audiobook = await _audiobookRepository.GetByIdAsync(audiobookId);
            if (audiobook == null)
            {
                return new SearchAndDownloadResult
                {
                    Success = false,
                    Message = "Audiobook not found"
                };
            }

            if (audiobook.QualityProfile == null)
            {
                _logger.LogWarning("Audiobook '{Title}' has no quality profile assigned", audiobook.Title);
                return new SearchAndDownloadResult
                {
                    Success = false,
                    Message = "Audiobook has no quality profile assigned"
                };
            }

            // Build search query from audiobook metadata
            var searchQuery = BuildSearchQuery(audiobook);
            _logger.LogInformation("Searching for audiobook '{Title}' with query: {Query}", audiobook.Title, searchQuery);

            // Search using the working search service. This is an automatic search (triggered
            // by the background/manual 'search-and-download' endpoint), so set isAutomaticSearch
            // to true to ensure only indexers are queried (no Amazon/Audible scraping).
            var searchResults = await _searchService.SearchAsync(searchQuery, isAutomaticSearch: true);

            if (searchResults == null || !searchResults.Any())
            {
                return new SearchAndDownloadResult
                {
                    Success = false,
                    Message = "No search results found"
                };
            }

            // Score results against quality profile
            using var scope = _serviceScopeFactory.CreateScope();
            var qualityProfileService = scope.ServiceProvider.GetRequiredService<IQualityProfileService>();
            var scoredResults = await qualityProfileService.ScoreSearchResults(searchResults, audiobook.QualityProfile);

            // Log all scored results for debugging
            _logger.LogInformation("Scored {Count} search results for audiobook '{Title}':", scoredResults.Count, audiobook.Title);
            foreach (var scoredResult in scoredResults.OrderByDescending(s => s.TotalScore))
            {
                var status = scoredResult.IsRejected ? "REJECTED" : (scoredResult.TotalScore > 0 ? "ACCEPTABLE" : "LOW SCORE");
                _logger.LogInformation("  [{Status}] Score: {Score} | Title: {Title} | Source: {Source} | Size: {Size}MB | Seeders: {Seeders} | Quality: {Quality}",
                    status, scoredResult.TotalScore, scoredResult.SearchResult.Title, scoredResult.SearchResult.Source,
                    scoredResult.SearchResult.Size / 1024 / 1024, scoredResult.SearchResult.Seeders, scoredResult.SearchResult.Quality);
                if (scoredResult.IsRejected && scoredResult.RejectionReasons.Any())
                {
                    _logger.LogInformation("    Rejection reasons: {Reasons}", string.Join(", ", scoredResult.RejectionReasons));
                }
            }

            // Only consider non-rejected, score > 0 results
            var topResult = scoredResults
                .Where(s => !s.IsRejected && s.TotalScore > 0)
                .OrderByDescending(s => s.TotalScore)
                .FirstOrDefault();

            if (topResult == null)
            {
                _logger.LogWarning("No acceptable search results found for audiobook '{Title}' after quality filtering", audiobook.Title);
                return new SearchAndDownloadResult
                {
                    Success = false,
                    Message = "No acceptable search results found"
                };
            }

            // Assign score to SearchResult
            topResult.SearchResult.Score = topResult.TotalScore;

            // Handle DDL results directly
            if (!string.IsNullOrEmpty(topResult.SearchResult.DownloadType) &&
                topResult.SearchResult.DownloadType.Equals("DDL", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Top result is DDL, processing directly for: {Title}", topResult.SearchResult.Title);
                var downloadId = await DownloadDirectlyAsync(topResult.SearchResult, audiobookId);
                await LogDownloadHistory(audiobook, "Search", topResult.SearchResult);
                return new SearchAndDownloadResult
                {
                    Success = true,
                    Message = $"Successfully processed DDL download",
                    DownloadId = downloadId,
                    IndexerUsed = "Search",
                    DownloadClientUsed = "DDL",
                    SearchResult = topResult.SearchResult
                };
            }

            // Use topResult.SearchResult for torrent/nzb download
            var isTorrent = IsTorrentResult(topResult.SearchResult);
            var downloadClientId = await GetAppropriateDownloadClient(isTorrent);

            if (downloadClientId == null)
            {
                _logger.LogWarning("No suitable download client found for type: {Type}", isTorrent ? "Torrent" : "NZB");
                return new SearchAndDownloadResult
                {
                    Success = false,
                    Message = $"No suitable download client found for {(isTorrent ? "torrent" : "NZB")} results"
                };
            }

            // Send to download client with audiobookId for proper metadata linking
            var downloadId2 = await SendToDownloadClientAsync(topResult.SearchResult, downloadClientId, audiobookId);

            // Log to history
            await LogDownloadHistory(audiobook, "Search", topResult.SearchResult);

            return new SearchAndDownloadResult
            {
                Success = true,
                Message = $"Successfully sent to download client",
                DownloadId = downloadId2,
                IndexerUsed = "Search",
                DownloadClientUsed = downloadClientId,
                SearchResult = topResult.SearchResult
            };
        }

        public async Task<string> SendToDownloadClientAsync(SearchResult searchResult, string? downloadClientId = null, int? audiobookId = null)
        {
            _logger.LogInformation("SendToDownloadClientAsync called - Title: {Title}, DownloadType: '{DownloadType}', TorrentUrl: {TorrentUrl}, AudiobookId: {AudiobookId}", 
                searchResult.Title, 
                searchResult.DownloadType ?? "(null)", 
                searchResult.TorrentUrl ?? "(null)",
                audiobookId);
            
            // Check if this is a DDL (Direct Download Link) - handle it differently
            // Use case-insensitive comparison in case of serialization casing issues
            if (!string.IsNullOrEmpty(searchResult.DownloadType) && 
                searchResult.DownloadType.Equals("DDL", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Processing DDL for: {Title}, AudiobookId: {AudiobookId}", searchResult.Title, audiobookId);
                return await DownloadDirectlyAsync(searchResult, audiobookId);
            }
            
            _logger.LogInformation("Not a DDL, processing as torrent/usenet. DownloadType was: '{DownloadType}'", searchResult.DownloadType);
        

            // If no specific client provided, auto-select based on result type
            if (downloadClientId == null)
            {
                var isTorrent = IsTorrentResult(searchResult);
                downloadClientId = await GetAppropriateDownloadClient(isTorrent);
                
                if (downloadClientId == null)
                {
                    var clientType = isTorrent ? "torrent" : "NZB";
                    var neededClients = isTorrent ? "qBittorrent or Transmission" : "SABnzbd or NZBGet";
                    throw new Exception($"No suitable download client found for {clientType}. Please configure and enable a {clientType} client ({neededClients}) in Settings.");
                }
                
                _logger.LogInformation("Auto-selected download client {ClientId} for {ClientType}", downloadClientId, isTorrent ? "torrent" : "NZB");
            }

            var downloadClient = await _configurationService.GetDownloadClientConfigurationAsync(downloadClientId);
            if (downloadClient == null || !downloadClient.IsEnabled)
            {
                throw new Exception("Download client not found or disabled");
            }

            _logger.LogInformation("Sending to {ClientType} download client: {ClientName}", downloadClient.Type, downloadClient.Name);

            // Route to appropriate client handler
            return downloadClient.Type.ToLower() switch
            {
                "qbittorrent" => await SendToQBittorrent(downloadClient, searchResult),
                "transmission" => await SendToTransmission(downloadClient, searchResult),
                "sabnzbd" => await SendToSABnzbd(downloadClient, searchResult),
                "nzbget" => await SendToNZBGet(downloadClient, searchResult),
                _ => throw new Exception($"Unsupported download client type: {downloadClient.Type}")
            };
        }

        private string BuildSearchQuery(Audiobook audiobook)
        {
            // Build a search query from audiobook metadata
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(audiobook.Title))
                parts.Add(audiobook.Title);

            if (audiobook.Authors != null && audiobook.Authors.Any())
                parts.Add(audiobook.Authors.First());

            return string.Join(" ", parts);
        }

        private SearchResult GetBestResult(List<SearchResult> results, string indexerType)
        {
            // For torrents, prefer highest seeders
            // For NZBs, prefer newest/largest
            if (IsTorrentIndexer(indexerType))
            {
                return results.OrderByDescending(r => r.Seeders).ThenByDescending(r => r.Size).First();
            }
            else
            {
                return results.OrderByDescending(r => r.PublishedDate).ThenByDescending(r => r.Size).First();
            }
        }

        private bool IsTorrentIndexer(string indexerType)
        {
            return indexerType.ToLower() == "torrent";
        }

        private bool IsTorrentResult(SearchResult result)
        {
            // Check DownloadType first if it's set
            if (!string.IsNullOrEmpty(result.DownloadType))
            {
                if (result.DownloadType == "DDL")
                {
                    _logger.LogDebug("Result identified as DDL (DownloadType set): {Title}", result.Title);
                    return false; // DDL is not a torrent
                }
                else if (result.DownloadType == "Torrent")
                {
                    _logger.LogDebug("Result identified as Torrent (DownloadType set): {Title}", result.Title);
                    return true;
                }
                else if (result.DownloadType == "Usenet")
                {
                    _logger.LogDebug("Result identified as Usenet (DownloadType set): {Title}", result.Title);
                    return false;
                }
            }

            // Fallback to legacy detection logic
            // Check for NZB first - if it has an NZB URL, it's a Usenet/NZB download
            if (!string.IsNullOrEmpty(result.NzbUrl))
            {
                _logger.LogDebug("Result identified as NZB (has NzbUrl): {Title}", result.Title);
                return false;
            }

            // Check for torrent indicators - magnet link or torrent file
            if (!string.IsNullOrEmpty(result.MagnetLink) || !string.IsNullOrEmpty(result.TorrentUrl))
            {
                _logger.LogDebug("Result identified as Torrent (has MagnetLink or TorrentUrl): {Title}", result.Title);
                return true;
            }

            // If neither is set, we can't reliably determine the type
            // Log a warning and default to false (NZB) as a safer choice
            _logger.LogWarning("Unable to determine result type for '{Title}' from source '{Source}'. No MagnetLink, TorrentUrl, or NzbUrl found. Defaulting to NZB.", 
                result.Title, result.Source);
            return false;
        }

        private async Task<string?> GetAppropriateDownloadClient(bool isTorrent)
        {
            var downloadClients = await _configurationService.GetDownloadClientConfigurationsAsync();
            var enabledClients = downloadClients.Where(c => c.IsEnabled).ToList();

            _logger.LogInformation("Looking for {ClientType} client. Found {Count} enabled download clients: {Clients}", 
                isTorrent ? "torrent" : "NZB", 
                enabledClients.Count,
                string.Join(", ", enabledClients.Select(c => $"{c.Name} ({c.Type})")));

            if (isTorrent)
            {
                // Prefer qBittorrent, then Transmission
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("qbittorrent", StringComparison.OrdinalIgnoreCase)) 
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("transmission", StringComparison.OrdinalIgnoreCase));
                
                if (client != null)
                {
                    _logger.LogInformation("Selected torrent client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No torrent client (qBittorrent or Transmission) found among enabled clients");
                }
                
                return client?.Id;
            }
            else
            {
                // Prefer SABnzbd, then NZBGet
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("sabnzbd", StringComparison.OrdinalIgnoreCase))
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("nzbget", StringComparison.OrdinalIgnoreCase));
                
                if (client != null)
                {
                    _logger.LogInformation("Selected NZB client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No NZB client (SABnzbd or NZBGet) found among enabled clients");
                }
                
                return client?.Id;
            }
        }

        private async Task<string> SendToQBittorrent(DownloadClientConfiguration client, SearchResult result)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";
            
            // Login to qBittorrent
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", client.Username),
                new KeyValuePair<string, string>("password", client.Password)
            });

            var loginResponse = await _httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData);
            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new Exception("Failed to login to qBittorrent");
            }

            // Get torrent URL - prefer magnet link, fall back to torrent file URL
            var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) 
                ? result.MagnetLink 
                : result.TorrentUrl;

            if (string.IsNullOrEmpty(torrentUrl))
            {
                throw new Exception("No magnet link or torrent URL found in search result");
            }

            _logger.LogInformation("Adding torrent to qBittorrent: {Title}", result.Title);
            _logger.LogDebug("Torrent URL: {Url}", torrentUrl);

            // Add torrent
            var addData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("urls", torrentUrl),
                new KeyValuePair<string, string>("savepath", client.DownloadPath)
            });

            var addResponse = await _httpClient.PostAsync($"{baseUrl}/api/v2/torrents/add", addData);
            if (!addResponse.IsSuccessStatusCode)
            {
                var responseContent = await addResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to add torrent to qBittorrent. Status: {Status}, Response: {Response}", 
                    addResponse.StatusCode, responseContent);
                throw new Exception($"Failed to add torrent to qBittorrent: {addResponse.StatusCode}");
            }

            _logger.LogInformation("Successfully sent torrent to qBittorrent");
            return Guid.NewGuid().ToString(); // Return a download ID
        }

        private async Task<string> SendToTransmission(DownloadClientConfiguration client, SearchResult result)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/transmission/rpc";
            
            // TODO: Implement Transmission RPC API
            _logger.LogInformation("Sending to Transmission (not yet implemented)");
            await Task.Delay(100);
            
            return Guid.NewGuid().ToString();
        }

        private async Task<string> SendToSABnzbd(DownloadClientConfiguration client, SearchResult result)
        {
            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";
                
                // Get API key from settings
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                {
                    apiKey = apiKeyObj?.ToString() ?? "";
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("SABnzbd API key not configured");
                }

                // Get NZB URL
                var nzbUrl = result.NzbUrl;
                if (string.IsNullOrEmpty(nzbUrl))
                {
                    throw new Exception("No NZB URL found in search result");
                }

                _logger.LogInformation("Sending NZB to SABnzbd: {Title} from {Source}", result.Title, result.Source);
                _logger.LogDebug("NZB URL: {Url}", nzbUrl);

                // Build SABnzbd addurl API request
                var queryParams = new Dictionary<string, string>
                {
                    { "mode", "addurl" },
                    { "name", nzbUrl },
                    { "apikey", apiKey },
                    { "output", "json" },
                    { "nzbname", result.Title }
                };

                // Add priority if configured
                if (client.Settings != null && client.Settings.TryGetValue("recentPriority", out var priorityObj))
                {
                    var priority = priorityObj?.ToString();
                    if (!string.IsNullOrEmpty(priority) && priority != "default")
                    {
                        queryParams["priority"] = priority switch
                        {
                            "force" => "2",
                            "high" => "1",
                            "normal" => "0",
                            "low" => "-1",
                            _ => "0"
                        };
                    }
                }

                // Add category for audiobooks
                queryParams["cat"] = "audiobooks";

                var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                var requestUrl = $"{baseUrl}?{queryString}";

                _logger.LogDebug("SABnzbd request URL: {Url}", requestUrl.Replace(apiKey, "***"));

                var response = await _httpClient.GetAsync(requestUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SABnzbd returned error status {Status}: {Content}", response.StatusCode, responseContent);
                    throw new Exception($"SABnzbd returned status {response.StatusCode}: {responseContent}");
                }

                // Parse JSON response
                var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                // Check for errors
                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorMsg = errorElement.GetString();
                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        throw new Exception($"SABnzbd error: {errorMsg}");
                    }
                }

                // Get NZO ID (download ID)
                string downloadId = "";
                if (root.TryGetProperty("nzo_ids", out var nzoIds) && nzoIds.ValueKind == JsonValueKind.Array)
                {
                    var firstId = nzoIds.EnumerateArray().FirstOrDefault();
                    downloadId = firstId.GetString() ?? Guid.NewGuid().ToString();
                }
                else
                {
                    downloadId = Guid.NewGuid().ToString();
                }

                _logger.LogInformation("Successfully added NZB to SABnzbd with ID: {DownloadId}", downloadId);
                return downloadId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send NZB to SABnzbd");
                throw;
            }
        }

        private async Task<string> SendToNZBGet(DownloadClientConfiguration client, SearchResult result)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/jsonrpc";
            
            // TODO: Implement NZBGet JSON-RPC API
            _logger.LogInformation("Sending to NZBGet (not yet implemented)");
            await Task.Delay(100);
            
            return Guid.NewGuid().ToString();
        }

        public async Task<List<QueueItem>> GetQueueAsync()
        {
            var queueItems = new List<QueueItem>();
            var downloadClients = await _configurationService.GetDownloadClientConfigurationsAsync();
            var enabledClients = downloadClients.Where(c => c.IsEnabled).ToList();

            foreach (var client in enabledClients)
            {
                try
                {
                    List<QueueItem> clientQueue = client.Type.ToLower() switch
                    {
                        "qbittorrent" => await GetQBittorrentQueueAsync(client),
                        "transmission" => await GetTransmissionQueueAsync(client),
                        "sabnzbd" => await GetSABnzbdQueueAsync(client),
                        "nzbget" => await GetNZBGetQueueAsync(client),
                        _ => new List<QueueItem>()
                    };

                    queueItems.AddRange(clientQueue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting queue from download client {ClientName}", client.Name);
                }
            }

            return queueItems.OrderByDescending(q => q.AddedAt).ToList();
        }

        public async Task<bool> RemoveFromQueueAsync(string downloadId, string? downloadClientId = null)
        {
            try
            {
                if (downloadClientId == null)
                {
                    // Try all clients to find and remove the item
                    var downloadClients = await _configurationService.GetDownloadClientConfigurationsAsync();
                    var enabledClients = downloadClients.Where(c => c.IsEnabled).ToList();

                    foreach (var client in enabledClients)
                    {
                        bool removed = await RemoveFromClientAsync(client, downloadId);
                        if (removed) return true;
                    }
                    return false;
                }
                else
                {
                    var client = await _configurationService.GetDownloadClientConfigurationAsync(downloadClientId);
                    if (client == null) return false;
                    return await RemoveFromClientAsync(client, downloadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from queue: {DownloadId}", downloadId);
                return false;
            }
        }

        private async Task<List<QueueItem>> GetQBittorrentQueueAsync(DownloadClientConfiguration client)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";
            var items = new List<QueueItem>();

            try
            {
                // Login
                var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", client.Username),
                    new KeyValuePair<string, string>("password", client.Password)
                });

                var loginResponse = await _httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData);
                if (!loginResponse.IsSuccessStatusCode) return items;

                // Get torrents
                var torrentsResponse = await _httpClient.GetAsync($"{baseUrl}/api/v2/torrents/info");
                if (!torrentsResponse.IsSuccessStatusCode) return items;

                var torrentsJson = await torrentsResponse.Content.ReadAsStringAsync();
                var torrents = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(torrentsJson);

                if (torrents != null)
                {
                    foreach (var torrent in torrents)
                    {
                        var name = torrent.ContainsKey("name") ? torrent["name"].GetString() ?? "" : "";
                        var progress = torrent.ContainsKey("progress") ? torrent["progress"].GetDouble() * 100 : 0;
                        var size = torrent.ContainsKey("size") ? torrent["size"].GetInt64() : 0;
                        var downloaded = torrent.ContainsKey("downloaded") ? torrent["downloaded"].GetInt64() : 0;
                        var dlspeed = torrent.ContainsKey("dlspeed") ? torrent["dlspeed"].GetDouble() : 0;
                        var eta = torrent.ContainsKey("eta") ? (int?)torrent["eta"].GetInt32() : null;
                        var state = torrent.ContainsKey("state") ? torrent["state"].GetString() ?? "unknown" : "unknown";
                        var hash = torrent.ContainsKey("hash") ? torrent["hash"].GetString() ?? "" : "";
                        var addedOn = torrent.ContainsKey("added_on") ? torrent["added_on"].GetInt64() : 0;
                        var numSeeds = torrent.ContainsKey("num_seeds") ? (int?)torrent["num_seeds"].GetInt32() : null;
                        var numLeechs = torrent.ContainsKey("num_leechs") ? (int?)torrent["num_leechs"].GetInt32() : null;
                        var ratio = torrent.ContainsKey("ratio") ? (double?)torrent["ratio"].GetDouble() : null;
                        var savePath = torrent.ContainsKey("save_path") ? torrent["save_path"].GetString() ?? "" : "";

                        // Apply remote path mapping for Docker scenarios
                        var localPath = !string.IsNullOrEmpty(savePath)
                            ? await _pathMappingService.TranslatePathAsync(client.Id, savePath)
                            : savePath;

                        var status = state switch
                        {
                            "downloading" => "downloading",
                            "pausedDL" => "paused",
                            "queuedDL" => "queued",
                            "uploading" or "stalledUP" => "seeding",
                            "completedUP" or "completedDL" => "completed",
                            "error" or "missingFiles" => "failed",
                            _ => "queued"
                        };

                        items.Add(new QueueItem
                        {
                            Id = hash,
                            Title = name,
                            Quality = "Unknown",
                            Status = status,
                            Progress = progress,
                            Size = size,
                            Downloaded = downloaded,
                            DownloadSpeed = dlspeed,
                            Eta = eta >= 8640000 ? null : eta, // Filter out invalid ETAs
                            DownloadClient = client.Name,
                            DownloadClientId = client.Id,
                            DownloadClientType = "qbittorrent",
                            AddedAt = DateTimeOffset.FromUnixTimeSeconds(addedOn).DateTime,
                            Seeders = numSeeds,
                            Leechers = numLeechs,
                            Ratio = ratio,
                            CanPause = status == "downloading" || status == "queued",
                            CanRemove = true,
                            RemotePath = savePath,
                            LocalPath = localPath
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting qBittorrent queue - client may be unreachable");
            }

            return items;
        }

        private async Task<List<QueueItem>> GetTransmissionQueueAsync(DownloadClientConfiguration client)
        {
            // TODO: Implement Transmission queue retrieval
            _logger.LogInformation("Transmission queue retrieval not yet implemented");
            await Task.Delay(1);
            return new List<QueueItem>();
        }

        private async Task<List<QueueItem>> GetSABnzbdQueueAsync(DownloadClientConfiguration client)
        {
            var items = new List<QueueItem>();
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";

            try
            {
                // Get API key from settings
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                {
                    apiKey = apiKeyObj?.ToString() ?? "";
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("SABnzbd API key not configured for {ClientName}", client.Name);
                    return items;
                }

                // Build queue API request
                var requestUrl = $"{baseUrl}?mode=queue&output=json&apikey={Uri.EscapeDataString(apiKey)}";
                
                var response = await _httpClient.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SABnzbd queue request failed with status {Status}", response.StatusCode);
                    return items;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(jsonContent);
                
                // Navigate to queue.slots array
                if (!doc.RootElement.TryGetProperty("queue", out var queue))
                {
                    return items;
                }

                if (!queue.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
                {
                    return items;
                }

                foreach (var slot in slots.EnumerateArray())
                {
                    try
                    {
                        var nzoId = slot.TryGetProperty("nzo_id", out var id) ? id.GetString() ?? "" : "";
                        var filename = slot.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "Unknown" : "Unknown";
                        var status = slot.TryGetProperty("status", out var st) ? st.GetString() ?? "Unknown" : "Unknown";
                        
                        // Helper to parse numeric values that might be strings or numbers
                        double ParseNumericValue(JsonElement element)
                        {
                            if (element.ValueKind == JsonValueKind.Number)
                                return element.GetDouble();
                            if (element.ValueKind == JsonValueKind.String)
                            {
                                var str = element.GetString() ?? "0";
                                if (double.TryParse(str, out var value))
                                    return value;
                            }
                            return 0;
                        }
                        
                        var sizeMB = slot.TryGetProperty("mb", out var mb) ? ParseNumericValue(mb) : 0;
                        var mbLeft = slot.TryGetProperty("mbleft", out var left) ? ParseNumericValue(left) : 0;
                        var downloadedMB = sizeMB - mbLeft;
                        var percentage = slot.TryGetProperty("percentage", out var pct) ? ParseNumericValue(pct) : 0;
                        
                        var timeLeft = slot.TryGetProperty("timeleft", out var time) ? time.GetString() ?? "0:00:00" : "0:00:00";
                        var category = slot.TryGetProperty("cat", out var cat) ? cat.GetString() ?? "" : "";
                        var priority = slot.TryGetProperty("priority", out var pri) ? pri.GetString() ?? "" : "";

                        // Parse time left (format: "0:12:34" or "1 day 2:34:56")
                        int etaSeconds = 0;
                        if (!string.IsNullOrEmpty(timeLeft) && timeLeft != "0:00:00")
                        {
                            etaSeconds = ParseSABnzbdTimeLeft(timeLeft);
                        }

                        // Convert MB to bytes
                        var sizeBytes = (long)(sizeMB * 1024 * 1024);
                        var downloadedBytes = (long)(downloadedMB * 1024 * 1024);

                        // Get download speed (bytes per second)
                        var speed = 0.0;
                        if (queue.TryGetProperty("speed", out var speedProp))
                        {
                            var speedStr = speedProp.GetString() ?? "0";
                            // Speed is in format like "1.2 M" or "500 K"
                            speed = ParseSABnzbdSpeed(speedStr);
                        }

                        // Map SABnzbd status to our status
                        var mappedStatus = status.ToLower() switch
                        {
                            "downloading" => "downloading",
                            "queued" => "queued",
                            "paused" => "paused",
                            "checking" => "downloading",
                            "extracting" => "downloading",
                            "moving" => "downloading",
                            "completed" => "completed",
                            "failed" => "failed",
                            _ => "queued"
                        };

                        // SABnzbd queue API doesn't include path, but we can get it from client config
                        // The complete_dir setting would be the remote path to translate
                        var remotePath = client.DownloadPath ?? "";
                        var localPath = !string.IsNullOrEmpty(remotePath)
                            ? await _pathMappingService.TranslatePathAsync(client.Id, remotePath)
                            : remotePath;

                        items.Add(new QueueItem
                        {
                            Id = nzoId,
                            Title = filename,
                            Quality = category,
                            Status = mappedStatus,
                            Progress = percentage,
                            Size = sizeBytes,
                            Downloaded = downloadedBytes,
                            DownloadSpeed = speed,
                            Eta = etaSeconds > 0 ? etaSeconds : null,
                            DownloadClient = client.Name,
                            DownloadClientId = client.Id,
                            DownloadClientType = "sabnzbd",
                            AddedAt = DateTime.UtcNow, // SABnzbd doesn't provide this easily
                            CanPause = mappedStatus == "downloading" || mappedStatus == "queued",
                            CanRemove = true,
                            RemotePath = remotePath,
                            LocalPath = localPath
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing SABnzbd queue item");
                    }
                }

                _logger.LogInformation("Retrieved {Count} items from SABnzbd queue", items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SABnzbd queue");
            }

            return items;
        }

        private int ParseSABnzbdTimeLeft(string timeLeft)
        {
            try
            {
                // Format can be "0:12:34" or "1 day 2:34:56"
                var totalSeconds = 0;

                if (timeLeft.Contains("day"))
                {
                    var parts = timeLeft.Split(new[] { " day ", " days " }, StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[0], out var days))
                    {
                        totalSeconds += days * 86400;
                        timeLeft = parts[1];
                    }
                }

                var timeParts = timeLeft.Split(':');
                if (timeParts.Length == 3)
                {
                    if (int.TryParse(timeParts[0], out var hours))
                        totalSeconds += hours * 3600;
                    if (int.TryParse(timeParts[1], out var minutes))
                        totalSeconds += minutes * 60;
                    if (int.TryParse(timeParts[2], out var seconds))
                        totalSeconds += seconds;
                }

                return totalSeconds;
            }
            catch
            {
                return 0;
            }
        }

        private double ParseSABnzbdSpeed(string speedStr)
        {
            try
            {
                // Format: "1.2 M" (MB/s), "500 K" (KB/s), "5.0 B" (B/s)
                var parts = speedStr.Trim().Split(' ');
                if (parts.Length != 2)
                    return 0;

                if (!double.TryParse(parts[0], out var value))
                    return 0;

                var unit = parts[1].ToUpper();
                return unit switch
                {
                    "B" => value,
                    "K" => value * 1024,
                    "M" => value * 1024 * 1024,
                    "G" => value * 1024 * 1024 * 1024,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<QueueItem>> GetNZBGetQueueAsync(DownloadClientConfiguration client)
        {
            // TODO: Implement NZBGet queue retrieval
            _logger.LogInformation("NZBGet queue retrieval not yet implemented");
            await Task.Delay(1);
            return new List<QueueItem>();
        }

        private async Task<bool> RemoveFromClientAsync(DownloadClientConfiguration client, string downloadId)
        {
            return client.Type.ToLower() switch
            {
                "qbittorrent" => await RemoveFromQBittorrentAsync(client, downloadId),
                "transmission" => await RemoveFromTransmissionAsync(client, downloadId),
                "sabnzbd" => await RemoveFromSABnzbdAsync(client, downloadId),
                "nzbget" => await RemoveFromNZBGetAsync(client, downloadId),
                _ => false
            };
        }

        private async Task<bool> RemoveFromQBittorrentAsync(DownloadClientConfiguration client, string hash)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";

            try
            {
                // Login
                var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", client.Username),
                    new KeyValuePair<string, string>("password", client.Password)
                });

                var loginResponse = await _httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData);
                if (!loginResponse.IsSuccessStatusCode) return false;

                // Delete torrent
                var deleteData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("hashes", hash),
                    new KeyValuePair<string, string>("deleteFiles", "true")
                });

                var deleteResponse = await _httpClient.PostAsync($"{baseUrl}/api/v2/torrents/delete", deleteData);
                return deleteResponse.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing torrent from qBittorrent: {Hash}", hash);
                return false;
            }
        }

        private async Task<bool> RemoveFromTransmissionAsync(DownloadClientConfiguration client, string downloadId)
        {
            // TODO: Implement
            await Task.Delay(1);
            return false;
        }

        private async Task<bool> RemoveFromSABnzbdAsync(DownloadClientConfiguration client, string downloadId)
        {
            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";

            try
            {
                // Get API key from settings
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                {
                    apiKey = apiKeyObj?.ToString() ?? "";
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("SABnzbd API key not configured for {ClientName}", client.Name);
                    return false;
                }

                // Check if we should remove completed downloads or delete files
                var removeCompleted = false;
                if (client.Settings != null && client.Settings.TryGetValue("removeCompleted", out var removeObj))
                {
                    removeCompleted = removeObj is bool b && b;
                }

                // Build remove API request (mode=queue with name=delete and value=nzo_id)
                var requestUrl = $"{baseUrl}?mode=queue&name=delete&value={Uri.EscapeDataString(downloadId)}&apikey={Uri.EscapeDataString(apiKey)}&output=json";
                
                if (removeCompleted)
                {
                    // Also delete files
                    requestUrl += "&del_files=1";
                }

                var response = await _httpClient.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to remove from SABnzbd: Status {Status}", response.StatusCode);
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);

                // Check for success
                if (doc.RootElement.TryGetProperty("status", out var status))
                {
                    var statusBool = status.GetBoolean();
                    _logger.LogInformation("Removed {DownloadId} from SABnzbd: {Success}", downloadId, statusBool);
                    return statusBool;
                }

                return true; // Assume success if no error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from SABnzbd: {DownloadId}", downloadId);
                return false;
            }
        }

        private async Task<bool> RemoveFromNZBGetAsync(DownloadClientConfiguration client, string downloadId)
        {
            // TODO: Implement
            await Task.Delay(1);
            return false;
        }

        private async Task LogDownloadHistory(Audiobook audiobook, string indexerName, SearchResult result)
        {
            var historyEntry = new History
            {
                AudiobookId = audiobook.Id,
                AudiobookTitle = audiobook.Title ?? "Unknown",
                EventType = "Download Started",
                Message = $"Found match on {indexerName}: {result.Title}",
                Source = "AutomaticSearch",
                Data = JsonSerializer.Serialize(new
                {
                    IndexerName = indexerName,
                    ResultTitle = result.Title,
                    ResultSize = result.Size,
                    ResultSeeders = result.Seeders
                }),
                Timestamp = DateTime.UtcNow
            };

            _dbContext.History.Add(historyEntry);
            await _dbContext.SaveChangesAsync();
        }

        private async Task<string> DownloadDirectlyAsync(SearchResult searchResult, int? audiobookId = null)
        {
            var downloadId = Guid.NewGuid().ToString();
            
            try
            {
                _logger.LogInformation("Starting direct download for: {Title}, AudiobookId: {AudiobookId}", searchResult.Title, audiobookId);
                
                // Create temporary download folder within the application directory
                var tempDownloadFolder = Path.Combine(Directory.GetCurrentDirectory(), "config", "temp", "downloads");
                if (!Directory.Exists(tempDownloadFolder))
                {
                    _logger.LogInformation("Creating temporary download directory: {Path}", tempDownloadFolder);
                    Directory.CreateDirectory(tempDownloadFolder);
                }

                // Get the download URL (stored in TorrentUrl field for DDL)
                var downloadUrl = searchResult.TorrentUrl;
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new Exception("No download URL found for DDL result");
                }

                // Extract filename from URL or use title with format extension
                var uri = new Uri(downloadUrl);
                var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.LocalPath));
                if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.'))
                {
                    // Fallback: use title and format
                    var extension = searchResult.Format?.ToLower() switch
                    {
                        "librivox apple audiobook" => ".m4b",
                        "m4b" => ".m4b",
                        "128kbps mp3" => ".mp3",
                        "vbr mp3" => ".mp3",
                        "mp3" => ".mp3",
                        "flac" => ".flac",
                        "ogg vorbis" => ".ogg",
                        "ogg" => ".ogg",
                        "opus" => ".opus",
                        "64kbps mp3" => ".mp3",
                        _ => ".audio"
                    };
                    fileName = $"{SanitizeFileName(searchResult.Title)}{extension}";
                }

                // Download to temporary folder first
                var tempFilePath = Path.Combine(tempDownloadFolder, $"{downloadId}_{fileName}");
                
                // Create Download record in database
                var download = new Download
                {
                    Id = downloadId,
                    AudiobookId = audiobookId, // Link to audiobook for metadata
                    Title = searchResult.Title,
                    Artist = searchResult.Artist,
                    Album = searchResult.Album,
                    OriginalUrl = downloadUrl,
                    Status = DownloadStatus.Downloading,
                    Progress = 0,
                    TotalSize = searchResult.Size,
                    DownloadedSize = 0,
                    DownloadPath = tempFilePath, // Store temp path initially
                    FinalPath = tempFilePath, // Will be updated after processing
                    StartedAt = DateTime.UtcNow,
                    DownloadClientId = "DDL",
                    Metadata = new Dictionary<string, object>
                    {
                        { "Format", searchResult.Format ?? "Unknown" },
                        { "Source", searchResult.Source ?? "Unknown" },
                        { "DownloadType", "DDL" },
                        { "Score", searchResult.Score }
                    }
                };

                _dbContext.Downloads.Add(download);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Starting DDL download [{DownloadId}]: {Title} from {Url}", downloadId, searchResult.Title, downloadUrl);
                _logger.LogInformation("Downloading to temporary location: {TempFilePath}", tempFilePath);

                // Download the file with progress tracking (background task)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Create a new HttpClient for the background task to avoid disposal issues
                        using var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromHours(2); // Allow up to 2 hours for large files
                        
                        using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();

                            var totalBytes = response.Content.Headers.ContentLength ?? searchResult.Size;
                            var downloadedBytes = 0L;

                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                var lastProgressUpdate = DateTime.UtcNow;

                                while (true)
                                {
                                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                                    if (bytesRead == 0) break;

                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    downloadedBytes += bytesRead;

                                    // Update database progress every 5 seconds
                                    if ((DateTime.UtcNow - lastProgressUpdate).TotalSeconds >= 5)
                                    {
                                        var progress = totalBytes > 0 ? (decimal)((downloadedBytes * 100) / totalBytes) : 0;
                                        
                                        // Create a new scope for database operations
                                        using (var scope = _serviceScopeFactory.CreateScope())
                                        {
                                            var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                                            var downloadToUpdate = await dbContext.Downloads.FindAsync(downloadId);
                                            if (downloadToUpdate != null)
                                            {
                                                downloadToUpdate.DownloadedSize = downloadedBytes;
                                                downloadToUpdate.Progress = progress;
                                                dbContext.Downloads.Update(downloadToUpdate);
                                                await dbContext.SaveChangesAsync();
                                            }
                                        }

                                        _logger.LogInformation("DDL download [{DownloadId}] progress: {Progress}% ({Downloaded}/{Total} MB)", 
                                            downloadId, (int)progress, downloadedBytes / 1024 / 1024, totalBytes / 1024 / 1024);
                                        lastProgressUpdate = DateTime.UtcNow;
                                    }
                                }
                            }
                        }

                        var fileInfo = new FileInfo(tempFilePath);
                        
                        _logger.LogInformation("DDL download [{DownloadId}] completed: {FileName} ({Size} MB)", 
                            downloadId, fileName, fileInfo.Length / 1024 / 1024);

                        // Apply file naming pattern and move file to final location
                        string finalPath = tempFilePath;
                        try
                        {
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var fileNamingService = scope.ServiceProvider.GetRequiredService<IFileNamingService>();
                                var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                                
                                var settings = await configService.GetApplicationSettingsAsync();
                                
                                _logger.LogInformation("DDL download [{DownloadId}] Settings: EnableMetadataProcessing={Enabled}, OutputPath={OutputPath}, Pattern={Pattern}", 
                                    downloadId, settings.EnableMetadataProcessing, settings.OutputPath, settings.FileNamingPattern);
                                
                                // Only process if metadata processing is enabled
                                if (settings.EnableMetadataProcessing)
                                {
                                    _logger.LogInformation("DDL download [{DownloadId}] processing with file naming pattern...", downloadId);
                                    
                                    // Try to get audiobook metadata if linked
                                    Audiobook? audiobook = null;
                                    using (var lookupScope = _serviceScopeFactory.CreateScope())
                                    {
                                        var dbContext = lookupScope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                                        var completedDownload = await dbContext.Downloads.FindAsync(downloadId);
                                        if (completedDownload?.AudiobookId != null)
                                        {
                                            audiobook = await dbContext.Audiobooks.FindAsync(completedDownload.AudiobookId.Value);
                                            if (audiobook != null)
                                            {
                                                _logger.LogInformation("DDL download [{DownloadId}] Found linked audiobook: {Title} by {Authors}", 
                                                    downloadId, audiobook.Title, string.Join(", ", audiobook.Authors ?? new List<string>()));
                                            }
                                            else
                                            {
                                                _logger.LogWarning("DDL download [{DownloadId}] AudiobookId {AudiobookId} not found in database", 
                                                    downloadId, completedDownload.AudiobookId);
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogInformation("DDL download [{DownloadId}] No audiobook linked, using search result data", downloadId);
                                        }
                                    }
                                    
                                    // Build metadata from audiobook if available, otherwise use search result
                                    var metadata = new AudioMetadata();
                                    
                                    if (audiobook != null)
                                    {
                                        // Use audiobook metadata (preferred source)
                                        metadata.Title = audiobook.Title ?? searchResult.Title;
                                        metadata.Artist = audiobook.Authors?.FirstOrDefault() ?? searchResult.Artist ?? "Unknown Author";
                                        metadata.Album = audiobook.Series ?? audiobook.Title ?? searchResult.Album ?? searchResult.Title;
                                        metadata.AlbumArtist = audiobook.Authors?.FirstOrDefault() ?? searchResult.Artist ?? "Unknown Author";
                                        metadata.Series = audiobook.Series ?? audiobook.Title;
                                        metadata.SeriesPosition = !string.IsNullOrEmpty(audiobook.SeriesNumber) && decimal.TryParse(audiobook.SeriesNumber, out var pos) ? pos : null;
                                        metadata.Narrator = audiobook.Narrators?.FirstOrDefault();
                                        metadata.Publisher = audiobook.Publisher;
                                        metadata.Isbn = audiobook.Isbn;
                                        metadata.Asin = audiobook.Asin;
                                        metadata.Year = !string.IsNullOrEmpty(audiobook.PublishYear) && int.TryParse(audiobook.PublishYear, out var year) ? year : null;
                                        metadata.Description = audiobook.Description;
                                        metadata.Language = audiobook.Language;
                                        
                                        _logger.LogInformation("DDL download [{DownloadId}] Using audiobook metadata: Title='{Title}', Author='{Author}', Series='{Series}'", 
                                            downloadId, metadata.Title, metadata.Artist, metadata.Series);
                                    }
                                    else
                                    {
                                        // Fallback to search result data
                                        metadata.Title = searchResult.Title;
                                        metadata.Artist = searchResult.Artist ?? "Unknown Author";
                                        metadata.Album = searchResult.Album ?? searchResult.Title;
                                        metadata.AlbumArtist = searchResult.Artist ?? "Unknown Author";
                                        metadata.Series = searchResult.Album ?? searchResult.Title;
                                        
                                        _logger.LogInformation("DDL download [{DownloadId}] Using search result metadata: Title='{Title}', Artist='{Artist}', Series='{Series}'", 
                                            downloadId, metadata.Title, metadata.Artist, metadata.Series);
                                    }

                                    // Try to extract existing metadata from file (only supplement missing fields)
                                    try
                                    {
                                        _logger.LogInformation("DDL download [{DownloadId}] Extracting metadata from file: {FilePath}", downloadId, tempFilePath);
                                        var existingMetadata = await metadataService.ExtractFileMetadataAsync(tempFilePath);
                                        if (existingMetadata != null)
                                        {
                                            // Only use file metadata to fill in missing fields, preserve audiobook/search result data
                                            if (audiobook == null)
                                            {
                                                // If no audiobook linked, use file metadata as primary source
                                                metadata.Title = existingMetadata.Title ?? metadata.Title;
                                                metadata.Artist = existingMetadata.Artist ?? metadata.Artist;
                                                metadata.Album = existingMetadata.Album ?? metadata.Album;
                                                metadata.AlbumArtist = existingMetadata.AlbumArtist ?? metadata.AlbumArtist;
                                                metadata.Series = existingMetadata.Series ?? metadata.Series;
                                                metadata.Year = existingMetadata.Year ?? metadata.Year;
                                            }
                                            // Always use file metadata for track/disc numbers (these are per-file)
                                            metadata.TrackNumber = existingMetadata.TrackNumber;
                                            metadata.DiscNumber = existingMetadata.DiscNumber;
                                            
                                            _logger.LogInformation("DDL download [{DownloadId}] File metadata extracted - Track: {Track}, Disc: {Disc}", 
                                                downloadId, metadata.TrackNumber, metadata.DiscNumber);
                                        }
                                    }
                                    catch (Exception metaEx)
                                    {
                                        _logger.LogWarning(metaEx, "DDL download [{DownloadId}] Failed to extract metadata from {FilePath}, using audiobook/search result data", downloadId, tempFilePath);
                                    }

                                    // Generate final path using naming pattern
                                    _logger.LogInformation("DDL download [{DownloadId}] Generating final path with pattern: {Pattern}", downloadId, settings.FileNamingPattern);
                                    var extension = Path.GetExtension(tempFilePath);
                                    finalPath = await fileNamingService.GenerateFilePathAsync(
                                        metadata, 
                                        diskNumber: metadata.DiscNumber, 
                                        chapterNumber: metadata.TrackNumber, 
                                        originalExtension: extension
                                    );

                                    _logger.LogInformation("DDL download [{DownloadId}] Generated final path: {FinalPath}", downloadId, finalPath);

                                    // Create directory if it doesn't exist
                                    var finalDirectory = Path.GetDirectoryName(finalPath);
                                    if (!string.IsNullOrEmpty(finalDirectory) && !Directory.Exists(finalDirectory))
                                    {
                                        _logger.LogInformation("DDL download [{DownloadId}] Creating directory: {Directory}", downloadId, finalDirectory);
                                        Directory.CreateDirectory(finalDirectory);
                                    }

                                    // Move file to final location
                                    if (tempFilePath != finalPath && File.Exists(tempFilePath))
                                    {
                                        _logger.LogInformation("DDL download [{DownloadId}] Moving file from {SourcePath} to {FinalPath}", downloadId, tempFilePath, finalPath);
                                        File.Move(tempFilePath, finalPath, overwrite: true);
                                        _logger.LogInformation("DDL download [{DownloadId}] ✅ File moved successfully to: {FinalPath}", downloadId, finalPath);
                                        
                                        // Clean up temp file after successful move
                                        CleanupTempFile(tempFilePath);
                                    }
                                    else if (tempFilePath == finalPath)
                                    {
                                        _logger.LogInformation("DDL download [{DownloadId}] Source and destination paths are the same, no move needed: {Path}", downloadId, tempFilePath);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("DDL download [{DownloadId}] Source file does not exist: {FilePath}", downloadId, tempFilePath);
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation("DDL download [{DownloadId}] ⚠️ Metadata processing is DISABLED in settings, keeping file at: {FilePath}", downloadId, tempFilePath);
                                }
                            }
                        }
                        catch (Exception processEx)
                        {
                            _logger.LogError(processEx, "DDL download [{DownloadId}] ❌ Failed to process file with naming pattern, keeping original location: {FilePath}", downloadId, tempFilePath);
                            finalPath = tempFilePath; // Keep original path if processing fails
                        }
                        
                        // Update database with completion status and final path
                        // Defer to shared completion handler (testable)
                        await ProcessCompletedDownloadAsync(downloadId, finalPath);

                        _logger.LogInformation("DDL download [{DownloadId}] finalized at: {FinalPath}", downloadId, finalPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DDL download [{DownloadId}] failed: {Title}", downloadId, searchResult.Title);
                        
                        // Create a new scope for error update
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                            var failedDownload = await dbContext.Downloads.FindAsync(downloadId);
                            if (failedDownload != null)
                            {
                                failedDownload.Status = DownloadStatus.Failed;
                                failedDownload.ErrorMessage = ex.Message;
                                dbContext.Downloads.Update(failedDownload);
                                await dbContext.SaveChangesAsync();
                            }
                        }
                    }
                });

                // Return immediately with download ID
                return downloadId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting DDL download [{DownloadId}]: {Title}", downloadId, searchResult.Title);
                throw new Exception($"Failed to start download: {ex.Message}", ex);
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters from filename
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            
            // Limit length to 200 characters
            if (sanitized.Length > 200)
            {
                sanitized = sanitized.Substring(0, 200);
            }
            
            return sanitized;
        }

        private void CleanupTempFile(string tempFilePath)
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                    _logger.LogInformation("Cleaned up temporary file: {TempFilePath}", tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary file: {TempFilePath}", tempFilePath);
            }
        }

        /// <summary>
        /// Clean up old temporary files that are older than the specified retention period
        /// </summary>
        public void CleanupOldTempFiles(int retentionHours = 24)
        {
            try
            {
                var tempDownloadFolder = Path.Combine(Directory.GetCurrentDirectory(), "config", "temp", "downloads");
                if (!Directory.Exists(tempDownloadFolder))
                {
                    return;
                }

                var retentionTime = DateTime.UtcNow.AddHours(-retentionHours);
                var tempFiles = Directory.GetFiles(tempDownloadFolder, "*.*", SearchOption.AllDirectories);
                
                foreach (var file in tempFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < retentionTime)
                        {
                            fileInfo.Delete();
                            _logger.LogInformation("Cleaned up old temporary file: {FilePath}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup old temporary file: {FilePath}", file);
                    }
                }

                _logger.LogInformation("Completed cleanup of old temporary files (retention: {RetentionHours} hours)", retentionHours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temporary file cleanup");
            }
        }

        /// <summary>
        /// Process a completed download by updating its Download record and creating an AudiobookFile if linked.
        /// Exposed for reuse and unit testing.
        /// </summary>
        public async Task ProcessCompletedDownloadAsync(string downloadId, string finalPath)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var completedDownload = await dbContext.Downloads.FindAsync(downloadId);
                if (completedDownload != null)
                {
                    completedDownload.Status = DownloadStatus.Completed;
                    completedDownload.Progress = 100;
                    var finalFileInfo = new FileInfo(finalPath);
                    completedDownload.DownloadedSize = finalFileInfo.Length;
                    completedDownload.CompletedAt = DateTime.UtcNow;
                    completedDownload.FinalPath = finalPath;
                    dbContext.Downloads.Update(completedDownload);

                    // If this download is linked to an audiobook, create an AudiobookFile record (centralized service)
                    if (completedDownload.AudiobookId != null)
                    {
                        try
                        {
                            var audiobookId = completedDownload.AudiobookId.Value;
                            using var afScope = _serviceScopeFactory.CreateScope();
                            var audioFileService = afScope.ServiceProvider.GetRequiredService<IAudioFileService>();
                            await audioFileService.EnsureAudiobookFileAsync(audiobookId, finalPath, completedDownload.DownloadClientId ?? "download");
                        }
                        catch (Exception abEx)
                        {
                            _logger.LogWarning(abEx, "Failed to create AudiobookFile record for AudiobookId: {AudiobookId}", completedDownload.AudiobookId.Value);
                        }
                    }

                    await dbContext.SaveChangesAsync();

                    try
                    {
                        // Reload the download to ensure we have the latest values (detached entity may not track all fields)
                        var updatedDownload = await dbContext.Downloads.FindAsync(completedDownload.Id);
                        if (updatedDownload != null)
                        {
                            // Broadcast a single-item update so SignalR clients receive immediate notification
                            await _hubContext.Clients.All.SendAsync("DownloadUpdate", new List<Download> { updatedDownload });
                            _logger.LogInformation("Broadcasted DownloadUpdate for {DownloadId} via SignalR", updatedDownload.Id);
                        }
                    }
                    catch (Exception hubEx)
                    {
                        _logger.LogWarning(hubEx, "Failed to broadcast DownloadUpdate for {DownloadId}", completedDownload.Id);
                    }

                    // Additionally, if this download was linked to an audiobook, broadcast the updated audiobook (including Files)
                    try
                    {
                        if (completedDownload.AudiobookId != null)
                        {
                            var abId = completedDownload.AudiobookId.Value;
                            var updatedAudiobook = await dbContext.Audiobooks
                                .Include(a => a.QualityProfile)
                                .Include(a => a.Files)
                                .FirstOrDefaultAsync(a => a.Id == abId);

                            if (updatedAudiobook != null)
                            {
                                var audiobookDto = new
                                {
                                    id = updatedAudiobook.Id,
                                    title = updatedAudiobook.Title,
                                    authors = updatedAudiobook.Authors,
                                    description = updatedAudiobook.Description,
                                    imageUrl = updatedAudiobook.ImageUrl,
                                    filePath = updatedAudiobook.FilePath,
                                    fileSize = updatedAudiobook.FileSize,
                                    runtime = updatedAudiobook.Runtime,
                                    monitored = updatedAudiobook.Monitored,
                                    quality = updatedAudiobook.Quality,
                                    series = updatedAudiobook.Series,
                                    seriesNumber = updatedAudiobook.SeriesNumber,
                                    tags = updatedAudiobook.Tags,
                                    files = updatedAudiobook.Files?.Select(f => new {
                                        id = f.Id,
                                        path = f.Path,
                                        size = f.Size,
                                        durationSeconds = f.DurationSeconds,
                                        format = f.Format,
                                        bitrate = f.Bitrate,
                                        sampleRate = f.SampleRate,
                                        channels = f.Channels,
                                        source = f.Source,
                                        createdAt = f.CreatedAt
                                    }).ToList()
                                    ,
                                    wanted = updatedAudiobook.Monitored && (updatedAudiobook.Files == null || !updatedAudiobook.Files.Any())
                                };

                                await _hubContext.Clients.All.SendAsync("AudiobookUpdate", audiobookDto);
                                _logger.LogInformation("Broadcasted AudiobookUpdate for AudiobookId {AudiobookId} via SignalR", abId);
                            }
                        }
                    }
                    catch (Exception abHubEx)
                    {
                        _logger.LogWarning(abHubEx, "Failed to broadcast AudiobookUpdate for download {DownloadId}", completedDownload.Id);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Adapters
{
    public class TransmissionAdapter : IDownloadClientAdapter
    {
        public string ClientId => "transmission";
        public string ClientType => "transmission";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly ILogger<TransmissionAdapter> _logger;

        public TransmissionAdapter(IHttpClientFactory httpClientFactory, IRemotePathMappingService pathMappingService, ILogger<TransmissionAdapter> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _pathMappingService = pathMappingService ?? throw new ArgumentNullException(nameof(pathMappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            try
            {
                // Use old format for compatibility with Transmission < 4.1.0
                var payload = new
                {
                    method = "session-get",
                    arguments = new { },
                    tag = 1
                };
                await InvokeRpcAsync(client, payload, ct);
                return (true, "Transmission: session established");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Transmission test failed for client {ClientId}", LogRedaction.SanitizeText(client?.Id ?? client?.Name ?? client?.Type));
                return (false, ex.Message);
            }
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var arguments = new Dictionary<string, object>();

            // Prefer cached torrent file data over URL (required for private trackers with authentication)
            if (result.TorrentFileContent != null && result.TorrentFileContent.Length > 0)
            {
                // Use metainfo field for torrent file data (base64 encoded)
                arguments["metainfo"] = Convert.ToBase64String(result.TorrentFileContent);
                _logger.LogDebug("Using cached torrent file data ({Bytes} bytes) for '{Title}'", result.TorrentFileContent.Length, LogRedaction.SanitizeText(result.Title));
            }
            else
            {
                // Fall back to filename field for URLs/magnet links
                var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) ? result.MagnetLink : result.TorrentUrl;
                if (string.IsNullOrEmpty(torrentUrl))
                {
                    throw new ArgumentException("No magnet link, torrent URL, or cached torrent file provided", nameof(result));
                }
                arguments["filename"] = torrentUrl;
                _logger.LogDebug("Using torrent URL for '{Title}': {Url}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeUrl(torrentUrl));
            }

            // Only include download-dir if it's not empty (Transmission requires absolute path or omit)
            if (!string.IsNullOrWhiteSpace(client.DownloadPath))
            {
                arguments["download-dir"] = client.DownloadPath;
            }

            var labels = CollectLabels(client);
            if (labels.Count > 0)
            {
                arguments["labels"] = labels.ToArray();
            }

            // Use old format for compatibility with Transmission < 4.1.0
            var payload = new
            {
                method = "torrent-add",
                arguments,
                tag = 1
            };

            try
            {
                var response = await InvokeRpcAsync(client, payload, ct);
                
                // Log the full response for debugging
                _logger.LogDebug("Transmission add torrent response: {Response}", response.GetRawText());

                // Check result field
                if (!response.TryGetProperty("result", out var resultProp) || !string.Equals(resultProp.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMsg = resultProp.ValueKind == JsonValueKind.String ? resultProp.GetString() : "Unknown error";
                    throw new Exception($"Transmission RPC error: {errorMsg}");
                }

                if (response.TryGetProperty("arguments", out var args))
                {
                    if (args.TryGetProperty("torrent-added", out var added) && added.ValueKind == JsonValueKind.Object)
                    {
                        var torrentId = ExtractTorrentIdentifier(added);
                        _logger.LogInformation("Transmission successfully added torrent '{Title}' with id/hash: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(torrentId));
                        return torrentId;
                    }

                    if (args.TryGetProperty("torrent-duplicate", out var duplicate) && duplicate.ValueKind == JsonValueKind.Object)
                    {
                        var existingId = ExtractTorrentIdentifier(duplicate);
                        _logger.LogInformation("Transmission reported duplicate torrent for '{Title}' with id/hash {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(existingId));
                        return existingId;
                    }
                }

                _logger.LogWarning("Transmission AddAsync returning null - torrent may not have been added");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add torrent to Transmission for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
                throw;
            }
        }

        public async Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            var idsPayload = ParseTransmissionIds(id);
            var arguments = new Dictionary<string, object>
            {
                ["ids"] = idsPayload,
                ["delete-local-data"] = deleteFiles
            };

            // Use old format for compatibility with Transmission < 4.1.0
            var payload = new
            {
                method = "torrent-remove",
                arguments,
                tag = 2
            };

            try
            {
                var response = await InvokeRpcAsync(client, payload, ct);
                if (response.TryGetProperty("result", out var resultProp) && string.Equals(resultProp.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Removed torrent {Id} from Transmission (deleteFiles={DeleteFiles})", LogRedaction.SanitizeText(id), deleteFiles);
                    return true;
                }

                var errorMsg = resultProp.ValueKind == JsonValueKind.String ? resultProp.GetString() ?? "Unknown error" : "Unknown error";
                _logger.LogWarning("Transmission failed to remove torrent {Id}: {Message}", LogRedaction.SanitizeText(id), LogRedaction.SanitizeText(errorMsg));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing torrent {Id} from Transmission", LogRedaction.SanitizeText(id));
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

            // Use old format for compatibility with Transmission < 4.1.0
            var payload = new
            {
                method = "torrent-get",
                arguments = new
                {
                    fields = new[]
                    {
                        "id", "hashString", "name", "percentDone", "status", "totalSize", "rateDownload", "rateUpload",
                        "leftUntilDone", "eta", "downloadDir", "addedDate", "uploadedEver", "uploadRatio"
                    }
                },
                tag = 3
            };

            try
            {
                var response = await InvokeRpcAsync(client, payload, ct);
                if (!response.TryGetProperty("arguments", out var args) || !args.TryGetProperty("torrents", out var torrents) || torrents.ValueKind != JsonValueKind.Array)
                {
                    return items;
                }

                foreach (var torrent in torrents.EnumerateArray())
                {
                    try
                    {
                        var queueItem = await MapTorrentAsync(client, torrent, ct);
                        items.Add(queueItem);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to map Transmission torrent entry (non-fatal)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Transmission queue for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
            }

            return items;
        }

        public Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            // Transmission does not expose a dedicated history endpoint via RPC.
            return Task.FromResult(new List<(string Id, string Name)>());
        }

        /// <summary>
        /// Resolves the actual import item for a completed download.
        /// Queries Transmission API for downloadDir and builds the content path.
        /// EXACTLY matches Sonarr's Transmission.GetImportItem pattern.
        /// </summary>
        public async Task<QueueItem> GetImportItemAsync(
            DownloadClientConfiguration client,
            Download download,
            QueueItem queueItem,
            QueueItem? previousAttempt = null,
            CancellationToken ct = default)
        {
            // Clone to avoid mutating the original
            var result = queueItem.Clone();

            // If ContentPath is already set and exists, use it
            if (!string.IsNullOrEmpty(result.ContentPath))
            {
                var localPath = await _pathMappingService.TranslatePathAsync(client.Id, result.ContentPath);
                if (!string.IsNullOrEmpty(localPath) && (File.Exists(localPath) || Directory.Exists(localPath)))
                {
                    result.ContentPath = localPath;
                    return result;
                }
            }

            // Query Transmission for the torrent details
            var payload = new
            {
                method = "torrent-get",
                arguments = new
                {
                    ids = ParseTransmissionIds(queueItem.Id),
                    fields = new[] { "id", "name", "downloadDir" }
                },
                tag = 5
            };

            try
            {
                var response = await InvokeRpcAsync(client, payload, ct);
                if (!response.TryGetProperty("arguments", out var args) || 
                    !args.TryGetProperty("torrents", out var torrents) || 
                    torrents.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Failed to query Transmission for torrent {TorrentId}", queueItem.Id);
                    return result;
                }

                var torrent = torrents.EnumerateArray().FirstOrDefault();
                if (torrent.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogWarning("Torrent {TorrentId} not found in Transmission", queueItem.Id);
                    return result;
                }

                var downloadDir = torrent.TryGetProperty("downloadDir", out var dirProp) ? dirProp.GetString() : null;
                var name = torrent.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

                if (string.IsNullOrEmpty(downloadDir) || string.IsNullOrEmpty(name))
                {
                    _logger.LogWarning("Missing downloadDir or name for torrent {TorrentId}", queueItem.Id);
                    return result;
                }

                // Transmission stores files as: downloadDir/name
                var contentPath = Path.Combine(downloadDir, name);
                
                // Apply path mapping
                var localContentPath = await _pathMappingService.TranslatePathAsync(client.Id, contentPath);
                result.ContentPath = localContentPath;

                _logger.LogDebug(
                    "Resolved Transmission content path for {TorrentId}: {ContentPath}",
                    queueItem.Id,
                    localContentPath);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resolving import item for Transmission torrent {TorrentId}", queueItem.Id);
                return result;
            }
        }

        private async Task<QueueItem> MapTorrentAsync(DownloadClientConfiguration client, JsonElement torrent, CancellationToken ct)
        {
            // Try snake_case (JSON-RPC 2.0 / Transmission 4.1+) first, fall back to camelCase for backwards compatibility
            var id = torrent.TryGetProperty("hash_string", out var hashProp) || torrent.TryGetProperty("hashString", out hashProp) 
                ? hashProp.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(id) && torrent.TryGetProperty("id", out var numericId))
            {
                id = numericId.GetInt32().ToString(CultureInfo.InvariantCulture);
            }

            var name = torrent.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            var percentDone = (torrent.TryGetProperty("percent_done", out var percentProp) || torrent.TryGetProperty("percentDone", out percentProp))
                ? percentProp.GetDouble() * 100 : 0d;
            var totalSize = (torrent.TryGetProperty("total_size", out var sizeProp) || torrent.TryGetProperty("totalSize", out sizeProp))
                ? sizeProp.GetInt64() : 0L;
            var leftUntilDone = (torrent.TryGetProperty("left_until_done", out var leftProp) || torrent.TryGetProperty("leftUntilDone", out leftProp))
                ? leftProp.GetInt64() : 0L;
            var rateDownload = (torrent.TryGetProperty("rate_download", out var rateProp) || torrent.TryGetProperty("rateDownload", out rateProp))
                ? rateProp.GetDouble() : 0d;
            var eta = torrent.TryGetProperty("eta", out var etaProp) ? etaProp.GetInt32() : -1;
            var downloadDir = (torrent.TryGetProperty("download_dir", out var dirProp) || torrent.TryGetProperty("downloadDir", out dirProp))
                ? dirProp.GetString() ?? string.Empty : string.Empty;
            var statusCode = torrent.TryGetProperty("status", out var statusProp) ? statusProp.GetInt32() : 0;
            var addedDate = (torrent.TryGetProperty("added_date", out var addedProp) || torrent.TryGetProperty("addedDate", out addedProp))
                ? addedProp.GetInt64() : 0L;
            var uploadRatio = (torrent.TryGetProperty("upload_ratio", out var ratioProp) || torrent.TryGetProperty("uploadRatio", out ratioProp))
                ? ratioProp.GetDouble() : 0d;

            var downloaded = Math.Max(0, totalSize - leftUntilDone);

            var status = statusCode switch
            {
                0 => "paused",          // TR_STATUS_STOPPED
                1 => "queued",          // TR_STATUS_CHECK_WAIT
                2 => "downloading",     // TR_STATUS_CHECK
                3 => "queued",          // TR_STATUS_DOWNLOAD_WAIT
                4 => "downloading",     // TR_STATUS_DOWNLOAD
                5 => "queued",          // TR_STATUS_SEED_WAIT
                6 => "seeding",         // TR_STATUS_SEED
                7 => "failed",          // TR_STATUS_ISOLATED
                _ => "unknown"
            };

            _logger.LogDebug("Before completion check: hash={Hash}, percentDone={PercentDone}, status={Status}", 
                id, percentDone, status);
            
            if (percentDone >= 100.0 && (status == "seeding" || status == "queued" || status == "paused"))
            {
                status = "completed";
            }
            
            _logger.LogDebug("After completion check: hash={Hash}, finalStatus={Status}", id, status);

            string? localPath = downloadDir;
            if (!string.IsNullOrEmpty(downloadDir))
            {
                try
                {
                    localPath = await _pathMappingService.TranslatePathAsync(client.Id, downloadDir);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to translate Transmission path '{Path}' for client {ClientName}", LogRedaction.SanitizeFilePath(downloadDir), LogRedaction.SanitizeText(client.Name ?? client.Id));
                }
            }

            var addedAt = addedDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(addedDate).UtcDateTime : DateTime.UtcNow;

            // For Transmission, construct ContentPath from downloadDir + name
            var contentPath = !string.IsNullOrEmpty(downloadDir) && !string.IsNullOrEmpty(name)
                ? Path.Combine(downloadDir, name)
                : downloadDir;
            var localContentPath = !string.IsNullOrEmpty(contentPath)
                ? await _pathMappingService.TranslatePathAsync(client.Id, contentPath)
                : contentPath;

            var queueItem = new QueueItem
            {
                Id = id,
                Title = name,
                Quality = "Unknown",
                Status = status,
                Progress = percentDone,
                Size = totalSize,
                Downloaded = downloaded,
                DownloadSpeed = rateDownload,
                Eta = eta >= 0 ? eta : null,
                DownloadClient = client.Name ?? client.Id ?? "Transmission",
                DownloadClientId = client.Id ?? string.Empty,
                DownloadClientType = ClientType,
                AddedAt = addedAt,
                Ratio = uploadRatio,
                CanPause = status is "downloading" or "queued",
                CanRemove = true,
                RemotePath = downloadDir,
                LocalPath = localPath,
                ContentPath = localContentPath
            };

            return queueItem;
        }

        private List<string> CollectLabels(DownloadClientConfiguration client)
        {
            var labels = new List<string>();

            if (client.Settings != null && client.Settings.TryGetValue("category", out var categoryObj))
            {
                var category = categoryObj?.ToString();
                if (!string.IsNullOrWhiteSpace(category))
                {
                    labels.Add(category);
                }
            }

            if (client.Settings != null && client.Settings.TryGetValue("tags", out var tagsObj))
            {
                var tags = tagsObj?.ToString();
                if (!string.IsNullOrWhiteSpace(tags))
                {
                    labels.AddRange(tags
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t)));
                }
            }

            return labels;
        }

        private object[] ParseTransmissionIds(string id)
        {
            if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
            {
                return new object[] { numericId };
            }

            return new object[] { id };
        }

        private async Task<JsonElement> InvokeRpcAsync(DownloadClientConfiguration client, object payload, CancellationToken ct)
        {
            var httpClient = _httpClientFactory.CreateClient("transmission");
            var baseUrl = BuildBaseUrl(client);
            var serializedPayload = JsonSerializer.Serialize(payload);
            string? sessionId = null;
            
            _logger.LogDebug("Transmission RPC request to {Url}: {Payload}", LogRedaction.SanitizeUrl(baseUrl), LogRedaction.SanitizeText(serializedPayload, 500));

            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(sessionId))
                {
                    request.Headers.Add("X-Transmission-Session-Id", sessionId);
                    _logger.LogDebug("Using X-Transmission-Session-Id: {SessionId}", LogRedaction.SanitizeText(sessionId));
                }

                var authHeader = BuildAuthHeader(client);
                if (authHeader != null)
                {
                    request.Headers.Authorization = authHeader;
                }

                var response = await httpClient.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.Conflict && attempt == 0 && response.Headers.TryGetValues("X-Transmission-Session-Id", out var values))
                {
                    sessionId = values.FirstOrDefault();
                    _logger.LogDebug("Received 409 Conflict, retrying with session ID: {SessionId}", LogRedaction.SanitizeText(sessionId));
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var sensitiveValues = LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { client.Password ?? string.Empty });
                    var redacted = LogRedaction.RedactText(body, sensitiveValues);
                    _logger.LogWarning("Transmission returned {StatusCode}: {Body}", response.StatusCode, redacted);
                    throw new HttpRequestException($"Transmission returned {response.StatusCode}: {redacted}");
                }

                _logger.LogDebug("Transmission RPC response ({StatusCode}): {Body}", response.StatusCode, body);

                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("Transmission returned empty response body");
                    using var emptyDoc = JsonDocument.Parse("{}");
                    return emptyDoc.RootElement.Clone();
                }

                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.Clone();
            }

            throw new InvalidOperationException("Transmission did not supply a session identifier after retrying.");
        }

        private static string BuildBaseUrl(DownloadClientConfiguration client)
        {
            var scheme = client.UseSSL ? "https" : "http";
            return $"{scheme}://{client.Host}:{client.Port}/transmission/rpc";
        }

        private static AuthenticationHeaderValue? BuildAuthHeader(DownloadClientConfiguration client)
        {
            if (string.IsNullOrWhiteSpace(client.Username))
            {
                return null;
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{client.Username}:{client.Password}"));
            return new AuthenticationHeaderValue("Basic", credentials);
        }

        private static string? ExtractTorrentIdentifier(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Try snake_case (JSON-RPC 2.0 / Transmission 4.1+) first, fall back to camelCase
            if ((element.TryGetProperty("hash_string", out var hashProp) || element.TryGetProperty("hashString", out hashProp)))
            {
                var hash = hashProp.GetString();
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    return hash;
                }
            }

            if (element.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                return idProp.GetInt32().ToString(CultureInfo.InvariantCulture);
            }

            return null;
        }
    }
}

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
                await InvokeRpcAsync(client, new { method = "session-get", tag = 0 }, ct);
                return (true, "Transmission: session established");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Transmission test failed for client {ClientId}", client?.Id ?? client?.Name ?? client?.Type);
                return (false, ex.Message);
            }
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) ? result.MagnetLink : result.TorrentUrl;
            if (string.IsNullOrEmpty(torrentUrl))
            {
                throw new ArgumentException("No magnet link or torrent URL provided", nameof(result));
            }

            var arguments = new Dictionary<string, object>
            {
                ["filename"] = torrentUrl,
                ["download-dir"] = client.DownloadPath ?? string.Empty
            };

            var labels = CollectLabels(client);
            if (labels.Count > 0)
            {
                arguments["labels"] = labels.ToArray();
            }

            var payload = new
            {
                method = "torrent-add",
                arguments,
                tag = 1
            };

            try
            {
                var response = await InvokeRpcAsync(client, payload, ct);

                if (!response.TryGetProperty("result", out var resultProp) || !string.Equals(resultProp.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMsg = resultProp.ValueKind == JsonValueKind.String ? resultProp.GetString() : "Unknown error";
                    throw new Exception($"Transmission RPC error: {errorMsg}");
                }

                if (response.TryGetProperty("arguments", out var args))
                {
                    if (args.TryGetProperty("torrent-added", out var added) && added.ValueKind == JsonValueKind.Object)
                    {
                        return ExtractTorrentIdentifier(added);
                    }

                    if (args.TryGetProperty("torrent-duplicate", out var duplicate) && duplicate.ValueKind == JsonValueKind.Object)
                    {
                        var existingId = ExtractTorrentIdentifier(duplicate);
                        _logger.LogInformation("Transmission reported duplicate torrent for '{Title}' with id/hash {Id}", result.Title, existingId);
                        return existingId;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add torrent to Transmission for client {ClientName}", client.Name ?? client.Id);
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
                    _logger.LogInformation("Removed torrent {Id} from Transmission (deleteFiles={DeleteFiles})", id, deleteFiles);
                    return true;
                }

                var errorMsg = resultProp.ValueKind == JsonValueKind.String ? resultProp.GetString() ?? "Unknown error" : "Unknown error";
                _logger.LogWarning("Transmission failed to remove torrent {Id}: {Message}", id, errorMsg);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing torrent {Id} from Transmission", id);
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

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
                _logger.LogWarning(ex, "Failed to retrieve Transmission queue for client {ClientName}", client.Name ?? client.Id);
            }

            return items;
        }

        public Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            // Transmission does not expose a dedicated history endpoint via RPC.
            return Task.FromResult(new List<(string Id, string Name)>());
        }

        private async Task<QueueItem> MapTorrentAsync(DownloadClientConfiguration client, JsonElement torrent, CancellationToken ct)
        {
            var id = torrent.TryGetProperty("hashString", out var hashProp) ? hashProp.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(id) && torrent.TryGetProperty("id", out var numericId))
            {
                id = numericId.GetInt32().ToString(CultureInfo.InvariantCulture);
            }

            var name = torrent.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            var percentDone = torrent.TryGetProperty("percentDone", out var percentProp) ? percentProp.GetDouble() * 100 : 0d;
            var totalSize = torrent.TryGetProperty("totalSize", out var sizeProp) ? sizeProp.GetInt64() : 0L;
            var leftUntilDone = torrent.TryGetProperty("leftUntilDone", out var leftProp) ? leftProp.GetInt64() : 0L;
            var rateDownload = torrent.TryGetProperty("rateDownload", out var rateProp) ? rateProp.GetDouble() : 0d;
            var eta = torrent.TryGetProperty("eta", out var etaProp) ? etaProp.GetInt32() : -1;
            var downloadDir = torrent.TryGetProperty("downloadDir", out var dirProp) ? dirProp.GetString() ?? string.Empty : string.Empty;
            var statusCode = torrent.TryGetProperty("status", out var statusProp) ? statusProp.GetInt32() : 0;
            var addedDate = torrent.TryGetProperty("addedDate", out var addedProp) ? addedProp.GetInt64() : 0L;
            var uploadRatio = torrent.TryGetProperty("uploadRatio", out var ratioProp) ? ratioProp.GetDouble() : 0d;

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

            if (percentDone >= 100.0 && status is "seeding" or "queued" or "paused")
            {
                status = "completed";
            }

            string? localPath = downloadDir;
            if (!string.IsNullOrEmpty(downloadDir))
            {
                try
                {
                    localPath = await _pathMappingService.TranslatePathAsync(client.Id, downloadDir);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to translate Transmission path '{Path}' for client {ClientName}", downloadDir, client.Name ?? client.Id);
                }
            }

            var addedAt = addedDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(addedDate).UtcDateTime : DateTime.UtcNow;

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
                LocalPath = localPath
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

            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(sessionId))
                {
                    request.Headers.Add("X-Transmission-Session-Id", sessionId);
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
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var sensitiveValues = LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { client.Password ?? string.Empty });
                    var redacted = LogRedaction.RedactText(body, sensitiveValues);
                    throw new HttpRequestException($"Transmission returned {response.StatusCode}: {redacted}");
                }

                if (string.IsNullOrWhiteSpace(body))
                {
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

            if (element.TryGetProperty("hashString", out var hashProp))
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

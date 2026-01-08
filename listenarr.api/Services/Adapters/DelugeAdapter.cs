using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Adapters
{
    public class DelugeAdapter : IDownloadClientAdapter
    {
        public string ClientId => "deluge";
        public string ClientType => "deluge";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly ILogger<DelugeAdapter> _logger;

        public DelugeAdapter(IHttpClientFactory httpClientFactory, IRemotePathMappingService pathMappingService, ILogger<DelugeAdapter> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _pathMappingService = pathMappingService ?? throw new ArgumentNullException(nameof(pathMappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            try
            {
                // Prefer an auth.login call if a password is provided, otherwise try a lightweight RPC
                if (!string.IsNullOrWhiteSpace(client.Password))
                {
                    var authRes = await InvokeAsync(client, "auth.login", new object[] { client.Password }, ct);
                    if (authRes.ValueKind == JsonValueKind.True || (authRes.ValueKind == JsonValueKind.String && authRes.GetString() == "True"))
                        return (true, "Deluge: authenticated");
                }

                // Fallback: ask daemon for version via core.get_version
                var ver = await InvokeAsync(client, "core.get_version", Array.Empty<object>(), ct);
                if (ver.ValueKind != JsonValueKind.Undefined)
                {
                    return (true, "Deluge: API reachable");
                }

                return (false, "Deluge: no response");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Deluge test connection failed for client {ClientId}", LogRedaction.SanitizeText(client?.Id ?? client?.Name ?? client?.Type));
                return (false, ex.Message);
            }
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var options = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(client.DownloadPath))
                options["download_location"] = client.DownloadPath;

            // Map common Listenarr client settings to Deluge torrent options for better parity with other adapters.
            // Prowlarr/Sonarr typically expose fields like "category" (labels), "tags", "initialState", and move options.
            if (client.Settings != null)
            {
                if (client.Settings.TryGetValue("category", out var categoryObj))
                {
                    var category = categoryObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        // Deluge common option: download_location is the canonical path; label plugin may use "label" or "labels".
                        options["label"] = category;
                        options["category"] = category;
                    }
                }

                if (client.Settings.TryGetValue("tags", out var tagsObj))
                {
                    var tags = tagsObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(tags))
                    {
                        var tagArr = tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
                        if (tagArr.Length == 1)
                            options["label"] = tagArr[0];
                        else if (tagArr.Length > 1)
                            options["labels"] = tagArr;

                        options["tags"] = tags;
                    }
                }

                if (client.Settings.TryGetValue("initialState", out var stateObj))
                {
                    var state = stateObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(state) && state.Equals("pause", StringComparison.OrdinalIgnoreCase))
                    {
                        // Deluge option to add paused
                        options["add_paused"] = true;
                    }
                }

                if (client.Settings.TryGetValue("moveCompleted", out var moveObj) && bool.TryParse(moveObj?.ToString(), out var mv) && mv)
                {
                    options["move_completed"] = true;
                }

                if (client.Settings.TryGetValue("moveCompletedPath", out var movePathObj))
                {
                    var mp = movePathObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(mp))
                        options["move_completed_path"] = mp;
                }


            }

            try
            {
                if (result.TorrentFileContent != null && result.TorrentFileContent.Length > 0)
                {
                    // Use core.add_torrent_file(filename, filedump, options)
                    var filename = string.IsNullOrEmpty(result.TorrentFileName) ? "listenarr.torrent" : result.TorrentFileName;
                    var filedump = Convert.ToBase64String(result.TorrentFileContent);
                    var res = await InvokeAsync(client, "core.add_torrent_file", new object[] { filename, filedump, options }, ct);
                    var id = res.ValueKind == JsonValueKind.String ? res.GetString() : null;
                    _logger.LogInformation("Deluge successfully added torrent '{Title}' with id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(id));
                    return id;
                }

                var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) ? result.MagnetLink : result.TorrentUrl;
                if (string.IsNullOrEmpty(torrentUrl))
                    throw new ArgumentException("No magnet link, torrent URL, or cached torrent file provided", nameof(result));

                if (!string.IsNullOrEmpty(result.MagnetLink))
                {
                    var res = await InvokeAsync(client, "core.add_torrent_magnet", new object[] { torrentUrl, options }, ct);
                    var id = res.ValueKind == JsonValueKind.String ? res.GetString() : null;
                    _logger.LogInformation("Deluge added magnet for '{Title}' id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(id));
                    return id;
                }

                // Torrent URL - add by URL (no custom headers supported)
                var urlRes = await InvokeAsync(client, "core.add_torrent_url", new object[] { torrentUrl, options }, ct);
                var urlId = urlRes.ValueKind == JsonValueKind.String ? urlRes.GetString() : null;
                _logger.LogInformation("Deluge added torrent URL for '{Title}' id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(urlId));
                return urlId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add torrent to Deluge for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
                throw;
            }
        }

        public async Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                var res = await InvokeAsync(client, "core.remove_torrent", new object[] { id, deleteFiles }, ct);
                // Expect a truthy return (bool true) on success
                if (res.ValueKind == JsonValueKind.True || (res.ValueKind == JsonValueKind.String && res.GetString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true))
                {
                    _logger.LogInformation("Removed torrent {Id} from Deluge (deleteFiles={DeleteFiles})", LogRedaction.SanitizeText(id), deleteFiles);
                    return true;
                }

                _logger.LogWarning("Deluge remove returned unexpected value for {Id}: {Value}", LogRedaction.SanitizeText(id), res.ToString());
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing torrent {Id} from Deluge", LogRedaction.SanitizeText(id));
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

            try
            {
                // Request per-torrent status map
                var keys = new[] { "name", "progress", "total_size", "state", "save_path", "time_added", "ratio", "download_payload_rate", "upload_payload_rate" };
                var res = await InvokeAsync(client, "core.get_torrents_status", new object[] { new Dictionary<string, object>(), keys }, ct);

                if (res.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in res.EnumerateObject())
                    {
                        try
                        {
                            var torrentId = prop.Name;
                            var data = prop.Value;
                            var q = await MapTorrentAsync(client, torrentId, data, ct);
                            items.Add(q);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to map Deluge torrent entry (non-fatal)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Deluge queue for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
            }

            return items;
        }

        public Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            // Deluge does not provide a lightweight history endpoint in the web API; return empty list for now.
            return Task.FromResult(new List<(string Id, string Name)>());
        }

        public async Task<QueueItem> GetImportItemAsync(DownloadClientConfiguration client, Download download, QueueItem queueItem, QueueItem? previousAttempt = null, CancellationToken ct = default)
        {
            // Clone
            var result = queueItem.Clone();

            if (!string.IsNullOrEmpty(result.ContentPath))
            {
                var localPath = await _pathMappingService.TranslatePathAsync(client.Id, result.ContentPath);
                if (!string.IsNullOrEmpty(localPath) && (File.Exists(localPath) || Directory.Exists(localPath)))
                {
                    result.ContentPath = localPath;
                    return result;
                }
            }

            try
            {
                var keys = new[] { "name", "save_path" };
                var res = await InvokeAsync(client, "core.get_torrent_status", new object[] { queueItem.Id, keys }, ct);
                if (res.ValueKind == JsonValueKind.Object)
                {
                    var name = res.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var savePath = res.TryGetProperty("save_path", out var spProp) ? spProp.GetString() : null;

                    if (!string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(name))
                    {
                        var contentPath = Path.Combine(savePath, name);
                        var localContentPath = await _pathMappingService.TranslatePathAsync(client.Id, contentPath);
                        result.ContentPath = localContentPath;

                        _logger.LogDebug("Resolved Deluge content path for {TorrentId}: {ContentPath}", queueItem.Id, localContentPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resolving import item for Deluge torrent {TorrentId}", queueItem.Id);
            }

            return result;
        }

        private async Task<QueueItem> MapTorrentAsync(DownloadClientConfiguration client, string id, JsonElement data, CancellationToken ct)
        {
            var name = data.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            var progress = data.TryGetProperty("progress", out var progProp) ? progProp.GetDouble() : 0d;
            var total = data.TryGetProperty("total_size", out var sizeProp) ? sizeProp.GetInt64() : 0L;
            var state = data.TryGetProperty("state", out var stateProp) ? stateProp.GetString() ?? string.Empty : string.Empty;
            var savePath = data.TryGetProperty("save_path", out var spProp) ? spProp.GetString() ?? string.Empty : string.Empty;
            var added = data.TryGetProperty("time_added", out var taProp) ? (taProp.ValueKind == JsonValueKind.Number ? taProp.GetInt64() : 0L) : 0L;
            var ratio = data.TryGetProperty("ratio", out var ratioProp) ? ratioProp.GetDouble() : 0d;
            var dl = data.TryGetProperty("download_payload_rate", out var dlProp) ? dlProp.GetDouble() : 0d;

            var status = state.ToLowerInvariant() switch
            {
                var s when s.Contains("downloading") => "downloading",
                var s when s.Contains("seeding") => "seeding",
                var s when s.Contains("paused") => "paused",
                var s when s.Contains("error") => "failed",
                _ => "unknown"
            };

            if (progress >= 100.0 && (status == "seeding" || status == "queued" || status == "paused"))
            {
                status = "completed";
            }

            string? localPath = savePath;
            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    localPath = await _pathMappingService.TranslatePathAsync(client.Id, savePath);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to translate Deluge path '{Path}' for client {ClientName}", LogRedaction.SanitizeFilePath(savePath), LogRedaction.SanitizeText(client.Name ?? client.Id));
                }
            }

            var addedAt = added > 0 ? DateTimeOffset.FromUnixTimeSeconds(added).UtcDateTime : DateTime.UtcNow;

            // Build content path if possible (Deluge usually stores files under save_path/name)
            var contentPath = !string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(name) ? Path.Combine(savePath, name) : savePath;
            var localContentPath = !string.IsNullOrEmpty(contentPath) ? await _pathMappingService.TranslatePathAsync(client.Id, contentPath) : contentPath;

            var queueItem = new QueueItem
            {
                Id = id,
                Title = name,
                Quality = "Unknown",
                Status = status,
                Progress = progress * 100.0,
                Size = total,
                Downloaded = Math.Max(0, total - 0),
                DownloadSpeed = dl,
                Eta = null,
                DownloadClient = client.Name ?? client.Id ?? "Deluge",
                DownloadClientId = client.Id ?? string.Empty,
                DownloadClientType = ClientType,
                AddedAt = addedAt,
                Ratio = ratio,
                CanPause = status is "downloading" or "queued",
                CanRemove = true,
                RemotePath = savePath,
                LocalPath = localPath,
                ContentPath = localContentPath
            };

            return queueItem;
        }

        /// <summary>
        /// Attempts to invoke a Deluge JSON-RPC method. The implementation tries two common payload formats
        /// (named "jsonrpc" style and the array RPC-style used by some Deluge versions) for maximum
        /// compatibility with different Deluge/Web versions.
        /// </summary>
        private async Task<JsonElement> InvokeAsync(DownloadClientConfiguration client, string method, object[] args, CancellationToken ct)
        {
            var http = _httpClientFactory.CreateClient("deluge");
            var baseUrl = BuildBaseUrl(client);

            // Try JSON-RPC style first: { id:1, method: method, params: [args] }
            var payloadObj = new Dictionary<string, object>
            {
                ["id"] = 1,
                ["method"] = method,
                ["params"] = args
            };

            var serialized = JsonSerializer.Serialize(payloadObj);
            _logger.LogDebug("Deluge JSON-RPC request to {Url}: {Method}", LogRedaction.SanitizeUrl(baseUrl), LogRedaction.SanitizeText(method));

            using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl))
            {
                request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");

                // Add basic auth header if username is present
                if (!string.IsNullOrEmpty(client.Username))
                {
                    var cred = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{client.Username}:{client.Password}"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", cred);
                }

                var resp = await http.SendAsync(request, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogDebug("Deluge HTTP response {Status}: {BodyLength} bytes", resp.StatusCode, body?.Length ?? 0);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Deluge returned non-success status {Status} for client {ClientName}", resp.StatusCode, client.Name);
                    throw new HttpRequestException($"Deluge returned {resp.StatusCode}");
                }

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var resultProp))
                        {
                            return resultProp.Clone();
                        }

                        // Some Deluge responses are direct result objects
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("1", out var numbered))
                        {
                            return numbered.Clone();
                        }

                        // Fall through to return root
                        return root.Clone();
                    }
                    catch (JsonException)
                    {
                        // Fall back to array-style RPC if JSON-RPC style failed
                    }
                }
            }

            // Array-style payload: [[request_id, method, [args], {}]]
            var arrayPayload = new object[] { new object[] { 1, method, args, new Dictionary<string, object>() } };
            var serializedArray = JsonSerializer.Serialize(arrayPayload);

            using (var request2 = new HttpRequestMessage(HttpMethod.Post, baseUrl))
            {
                request2.Content = new StringContent(serializedArray, Encoding.UTF8, "application/json");
                var resp2 = await http.SendAsync(request2, ct);
                var body2 = await resp2.Content.ReadAsStringAsync(ct);

                if (!resp2.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Deluge returned non-success status {Status} for client {ClientName} on array-style request", resp2.StatusCode, client.Name);
                    throw new HttpRequestException($"Deluge returned {resp2.StatusCode}");
                }

                if (string.IsNullOrWhiteSpace(body2))
                {
                    using var empty = JsonDocument.Parse("{}");
                    return empty.RootElement.Clone();
                }

                using var doc2 = JsonDocument.Parse(body2);
                var root2 = doc2.RootElement;

                if (root2.ValueKind == JsonValueKind.Array && root2.GetArrayLength() > 0)
                {
                    var first = root2[0];
                    // Array-style responses are generally [message_type, request_id, [return_value]]
                    if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() >= 3)
                    {
                        var rv = first[2];
                        // If return value is array, try to return first item if single
                        if (rv.ValueKind == JsonValueKind.Array && rv.GetArrayLength() == 1)
                            return rv[0].Clone();

                        return rv.Clone();
                    }
                }

                return root2.Clone();
            }
        }

        private static string BuildBaseUrl(DownloadClientConfiguration client)
        {
            var scheme = client.UseSSL ? "https" : "http";
            return $"{scheme}://{client.Host}:{client.Port}/json";
        }
    }
}

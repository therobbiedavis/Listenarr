using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Listenarr.Api.Services.Adapters
{
    /// <summary>
    /// Adapter interface that encapsulates download client protocol specifics.
    /// Implement one adapter per client (qBittorrent, Transmission, SABnzbd, NZBGet).
    /// </summary>
    public interface IDownloadClientAdapter
    {
        string ClientId { get; }
        string ClientType { get; } // e.g. "qbittorrent", "transmission", "sabnzbd", "nzbget"

        Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default);
        Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default);
        Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default);
        Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default);
        Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default);
    }

    /// <summary>
    /// Minimal qBittorrent adapter stub. Move qBittorrent-specific logic out of DownloadService and into an implementation like this.
    /// This implementation uses IHttpClientFactory so HttpClient lifetimes and policies can be centralized in Program.cs.
    /// </summary>
    public class QbittorrentAdapter : IDownloadClientAdapter
    {
        public string ClientId => "qbittorrent";
        public string ClientType => "qbittorrent";

        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<QbittorrentAdapter> _logger;
        private readonly IRemotePathMappingService _pathMappingService;

        public QbittorrentAdapter(IHttpClientFactory httpFactory, IRemotePathMappingService pathMappingService, ILogger<QbittorrentAdapter> logger)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _pathMappingService = pathMappingService ?? throw new ArgumentNullException(nameof(pathMappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";
                var http = _httpFactory.CreateClient("qbittorrent");

                var resp = await http.GetAsync($"{baseUrl}/api/v2/app/version", ct);
                if (resp.IsSuccessStatusCode)
                    return (true, "qBittorrent API reachable");

                return (false, $"qBittorrent returned {resp.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "qBittorrent TestConnection failed");
                return (false, ex.Message);
            }
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            // Prefer magnet link, fallback to torrent URL
            var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) ? result.MagnetLink : result.TorrentUrl;
            if (string.IsNullOrEmpty(torrentUrl))
                throw new ArgumentException("No magnet link or torrent URL provided", nameof(result));

            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";

            // Try to extract hash from magnet if present
            string? extractedHash = null;
            if (!string.IsNullOrEmpty(result.MagnetLink) && result.MagnetLink.Contains("xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
            {
                var start = result.MagnetLink.IndexOf("xt=urn:btih:", StringComparison.OrdinalIgnoreCase) + "xt=urn:btih:".Length;
                var end = result.MagnetLink.IndexOf('&', start);
                if (end == -1) end = result.MagnetLink.Length;
                extractedHash = result.MagnetLink[start..end].ToLowerInvariant();
            }

            try
            {
                // qBittorrent requires cookies for session management; create a handler per operation
                var cookieJar = new CookieContainer();
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookieJar,
                    UseCookies = true,
                    AutomaticDecompression = DecompressionMethods.All
                };

                // Use a dedicated HttpClient when cookies are required
                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

                // Attempt login (may return 403 if auth is disabled)
                using var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", client.Username ?? string.Empty),
                    new KeyValuePair<string, string>("password", client.Password ?? string.Empty)
                });

                var loginResp = await httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData, ct);
                if (!loginResp.IsSuccessStatusCode)
                {
                    var body = await loginResp.Content.ReadAsStringAsync(ct);
                    var redacted = LogRedaction.RedactText(body, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { client.Password ?? string.Empty }));

                    if (loginResp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // Try API without auth to detect disabled auth
                        var testResp = await httpClient.GetAsync($"{baseUrl}/api/v2/app/version", ct);
                        if (!testResp.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("qBittorrent auth appears enabled and credentials are invalid for client {ClientId}", client.Id);
                            throw new Exception("qBittorrent authentication enabled but credentials are incorrect");
                        }
                        else
                        {
                            _logger.LogInformation("qBittorrent authentication disabled; proceeding without credentials for client {ClientId}", client.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("qBittorrent login failed: {Status} - {Body}", loginResp.StatusCode, redacted);
                    }
                }
                else
                {
                    _logger.LogDebug("Authenticated to qBittorrent for client {ClientId}", client.Id);
                }

                // Fetch existing torrents before adding so we can detect the newly added torrent
                var beforeResp = await httpClient.GetAsync($"{baseUrl}/api/v2/torrents/info", ct);
                var existingHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (beforeResp.IsSuccessStatusCode)
                {
                    var beforeJson = await beforeResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(beforeJson))
                    {
                        try
                        {
                            var beforeList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(beforeJson);
                            if (beforeList != null)
                            {
                                foreach (var t in beforeList)
                                {
                                    if (t.TryGetValue("hash", out var h)) existingHashes.Add(h.GetString() ?? string.Empty);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to parse qBittorrent 'before' torrents list (non-fatal)");
                        }
                    }
                }

                // Prepare add form
                var form = new List<KeyValuePair<string, string>>
                {
                    new("urls", torrentUrl),
                    new("savepath", client.DownloadPath ?? string.Empty)
                };

                // Category
                if (client.Settings != null && client.Settings.TryGetValue("category", out var catObj))
                {
                    var cat = catObj?.ToString();
                    if (!string.IsNullOrEmpty(cat)) form.Add(new KeyValuePair<string, string>("category", cat));
                }

                // Tags
                if (client.Settings != null && client.Settings.TryGetValue("tags", out var tagsObj))
                {
                    var tags = tagsObj?.ToString();
                    if (!string.IsNullOrEmpty(tags)) form.Add(new KeyValuePair<string, string>("tags", tags));
                }

                using var addContent = new FormUrlEncodedContent(form);
                var addResp = await httpClient.PostAsync($"{baseUrl}/api/v2/torrents/add", addContent, ct);
                if (!addResp.IsSuccessStatusCode)
                {
                    var respText = await addResp.Content.ReadAsStringAsync(ct);
                    var redacted = LogRedaction.RedactText(respText, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { client.Password ?? string.Empty }));
                    _logger.LogWarning("qBittorrent add failed: {Status} - {Body}", addResp.StatusCode, redacted);
                    return extractedHash; // fallback to extracted hash when server accepted the magnet but didn't return a hash
                }

                _logger.LogInformation("Added torrent to qBittorrent for client {ClientId}", client.Id);

                // Wait briefly for qBittorrent to process the new torrent
                await Task.Delay(1000, ct);

                // Fetch torrents after adding and look for new hashes
                var afterResp = await httpClient.GetAsync($"{baseUrl}/api/v2/torrents/info", ct);
                if (afterResp.IsSuccessStatusCode)
                {
                    var afterJson = await afterResp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(afterJson))
                    {
                        try
                        {
                            var afterList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(afterJson);
                            if (afterList != null)
                            {
                                foreach (var t in afterList)
                                {
                                    if (t.TryGetValue("hash", out var hEl))
                                    {
                                        var hash = hEl.GetString() ?? string.Empty;
                                        if (!existingHashes.Contains(hash))
                                        {
                                            _logger.LogInformation("Detected new qBittorrent torrent: hash={Hash}", hash);
                                            return hash;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to parse qBittorrent 'after' torrents list (non-fatal)");
                        }
                    }
                }

                // Fall back to extracted magnet hash when available
                if (!string.IsNullOrEmpty(extractedHash))
                {
                    _logger.LogInformation("Using extracted magnet hash as fallback: {Hash}", extractedHash);
                    return extractedHash;
                }

                _logger.LogWarning("Unable to determine torrent hash after adding to qBittorrent for client {ClientId}", client.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "qBittorrent AddAsync failed for client {ClientId}", client?.Id);
                throw;
            }
        }

        public async Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";

            try
            {
                // Cookie-based client for auth
                var cookieJar = new CookieContainer();
                var handler = new HttpClientHandler { CookieContainer = cookieJar, UseCookies = true, AutomaticDecompression = DecompressionMethods.All };

                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

                using var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", client.Username ?? string.Empty),
                    new KeyValuePair<string, string>("password", client.Password ?? string.Empty)
                });

                var loginResp = await httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData, ct);
                if (!loginResp.IsSuccessStatusCode)
                {
                    // If forbidden, check if API is accessible without auth
                    if (loginResp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        var testResp = await httpClient.GetAsync($"{baseUrl}/api/v2/app/version", ct);
                        if (!testResp.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("qBittorrent auth appears enabled and credentials are invalid for client {ClientId}", client.Id);
                            return false;
                        }
                    }
                }

                using var deleteData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("hashes", id),
                    new KeyValuePair<string, string>("deleteFiles", deleteFiles ? "true" : "false")
                });

                var deleteResp = await httpClient.PostAsync($"{baseUrl}/api/v2/torrents/delete", deleteData, ct);
                if (!deleteResp.IsSuccessStatusCode)
                {
                    var body = await deleteResp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("qBittorrent delete returned {Status}: {Body}", deleteResp.StatusCode, LogRedaction.RedactText(body, LogRedaction.GetSensitiveValuesFromEnvironment()));
                    return false;
                }

                _logger.LogInformation("Removed torrent {Id} from qBittorrent (deleteFiles={DeleteFiles})", id, deleteFiles);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing torrent from qBittorrent: {Id}", id);
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

            var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";

            try
            {
                // Cookie-based HttpClient for qBittorrent
                var cookieJar = new CookieContainer();
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookieJar,
                    UseCookies = true,
                    AutomaticDecompression = DecompressionMethods.All
                };

                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

                // Try login (403 may indicate auth disabled)
                using var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", client.Username ?? string.Empty),
                    new KeyValuePair<string, string>("password", client.Password ?? string.Empty)
                });

                var loginResp = await httpClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginData, ct);
                if (loginResp.StatusCode == HttpStatusCode.Forbidden)
                {
                    var testResp = await httpClient.GetAsync($"{baseUrl}/api/v2/app/version", ct);
                    if (!testResp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("qBittorrent authentication appears to be enabled and credentials are invalid for client {ClientId}", client.Id);
                        return items;
                    }
                }
                else if (!loginResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("qBittorrent login failed with status {Status} for client {ClientId}", loginResp.StatusCode, client.Id);
                    return items;
                }

                var torrentsResp = await httpClient.GetAsync($"{baseUrl}/api/v2/torrents/info", ct);
                if (!torrentsResp.IsSuccessStatusCode) return items;

                var json = await torrentsResp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(json)) return items;

                var torrents = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (torrents == null) return items;

                foreach (var torrent in torrents)
                {
                    var name = torrent.TryGetValue("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                    var progress = torrent.TryGetValue("progress", out var progressEl) ? progressEl.GetDouble() * 100 : 0;
                    var size = torrent.TryGetValue("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                    var downloaded = torrent.TryGetValue("downloaded", out var downloadedEl) ? downloadedEl.GetInt64() : 0;
                    var dlspeed = torrent.TryGetValue("dlspeed", out var dlspeedEl) ? dlspeedEl.GetDouble() : 0;
                    var eta = torrent.TryGetValue("eta", out var etaEl) ? (int?)etaEl.GetInt32() : null;
                    var state = torrent.TryGetValue("state", out var stateEl) ? stateEl.GetString() ?? "unknown" : "unknown";
                    var hash = torrent.TryGetValue("hash", out var hashEl) ? hashEl.GetString() ?? "" : "";
                    var addedOn = torrent.TryGetValue("added_on", out var addedOnEl) ? addedOnEl.GetInt64() : 0;
                    var numSeeds = torrent.TryGetValue("num_seeds", out var numSeedsEl) ? (int?)numSeedsEl.GetInt32() : null;
                    var numLeechs = torrent.TryGetValue("num_leechs", out var numLeechsEl) ? (int?)numLeechsEl.GetInt32() : null;
                    var ratio = torrent.TryGetValue("ratio", out var ratioEl) ? (double?)ratioEl.GetDouble() : null;
                    var savePath = torrent.TryGetValue("save_path", out var savePathEl) ? savePathEl.GetString() ?? "" : "";

                    var localPath = !string.IsNullOrEmpty(savePath)
                        ? await _pathMappingService.TranslatePathAsync(client.Id, savePath)
                        : savePath;

                    var status = state switch
                    {
                        "downloading" => "downloading",
                        "metaDL" => "downloading",
                        "forcedDL" => "downloading",
                        "forcedMetaDL" => "downloading",
                        "stalledDL" => "downloading",
                        "checkingDL" => "downloading",
                        "stoppedDL" => "paused",
                        "stoppedUP" => "paused",
                        "queuedDL" => "queued",
                        "queuedUP" => "queued",
                        "uploading" => "seeding",
                        "stalledUP" => "seeding",
                        "checkingUP" => "seeding",
                        "forcedUP" => "seeding",
                        "checkingResumeData" => "downloading",
                        "moving" => "downloading",
                        "error" => "failed",
                        "missingFiles" => "failed",
                        _ => "unknown"
                    };

                    if (progress >= 100.0 && (status == "seeding" || state == "uploading" || state == "stalledUP" || state == "checkingUP" || state == "forcedUP" || state == "stoppedUP"))
                    {
                        status = "completed";
                    }

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
                        Eta = eta >= 8640000 ? null : eta,
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting qBittorrent queue - client may be unreachable");
            }

            return items;
        }

        public Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            // qBittorrent doesn't provide an explicit history endpoint; return empty list for now.
            return Task.FromResult(new List<(string Id, string Name)>());
        }
    }

}

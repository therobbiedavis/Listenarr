using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                    try
                    {
                        _logger.LogDebug("Deluge add (file) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, res.ValueKind, res.ToString());
                    }
                    catch { }

                    var id = res.ValueKind == JsonValueKind.String ? res.GetString() : null;
                    if (string.IsNullOrEmpty(id))
                    {
                        // Log raw response to assist debugging (non-sensitive)
                        try { _logger.LogWarning("Deluge add (core.add_torrent_file) returned non-string/empty result for client {ClientName}: {Response}", client.Name ?? client.Id, res.ToString()); } catch { }

                        _logger.LogWarning("Deluge add returned no id for client {ClientName} after core.add_torrent_file. Attempting discovery retries.", client.Name ?? client.Id);
                        var discoveredId = await DiscoverAddedTorrentAsync(client, result.Title, result.Size > 0 ? result.Size : (long?)null, ct);
                        if (!string.IsNullOrEmpty(discoveredId))
                        {
                            _logger.LogInformation("Discovered torrent after add for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredId, result.Title);
                            return discoveredId;
                        }

                        _logger.LogDebug("Discovery retries did not find the torrent after add for client {ClientName}", client.Name ?? client.Id);

                        // Fallback: try adding via URL (some indexer endpoints behave better when instructing Deluge to fetch the URL)
                        if (!string.IsNullOrEmpty(result.TorrentUrl))
                        {
                            try
                            {
                                _logger.LogDebug("Attempting fallback add via core.add_torrent_url for client {ClientName}", client.Name ?? client.Id);
                                var fallbackUrlRes1 = await InvokeAsync(client, "core.add_torrent_url", new object[] { result.TorrentUrl, options }, ct);
                                try { _logger.LogDebug("Deluge fallback add (url) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, fallbackUrlRes1.ValueKind, fallbackUrlRes1.ToString()); } catch { }
                                var fallbackUrlId1 = fallbackUrlRes1.ValueKind == JsonValueKind.String ? fallbackUrlRes1.GetString() : null;
                                if (!string.IsNullOrEmpty(fallbackUrlId1))
                                {
                                    _logger.LogInformation("Deluge fallback add by URL succeeded for '{Title}' id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(fallbackUrlId1));
                                    return fallbackUrlId1;
                                }

                                // Try discovery again after fallback attempt
                                var discoveredAfterUrl = await DiscoverAddedTorrentAsync(client, result.Title, result.Size > 0 ? result.Size : (long?)null, ct);
                                if (!string.IsNullOrEmpty(discoveredAfterUrl))
                                {
                                    _logger.LogInformation("Discovered torrent after fallback URL add for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredAfterUrl, result.Title);
                                    return discoveredAfterUrl;
                                }
                            }
                            catch (Exception exFallback)
                            {
                                _logger.LogDebug(exFallback, "Fallback add via URL failed for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                            }
                        }
                    }

                    _logger.LogInformation("Deluge successfully added torrent '{Title}' with id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(id));
                    return id;
                }

                var torrentUrl = !string.IsNullOrEmpty(result.MagnetLink) ? result.MagnetLink : result.TorrentUrl;
                if (string.IsNullOrEmpty(torrentUrl))
                    throw new ArgumentException("No magnet link, torrent URL, or cached torrent file provided", nameof(result));

                if (!string.IsNullOrEmpty(result.MagnetLink))
                {
                    var res = await InvokeAsync(client, "core.add_torrent_magnet", new object[] { torrentUrl, options }, ct);
                    try { _logger.LogDebug("Deluge add (magnet) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, res.ValueKind, res.ToString()); } catch { }
                    var id = res.ValueKind == JsonValueKind.String ? res.GetString() : null;
                    if (string.IsNullOrEmpty(id))
                    {
                        _logger.LogWarning("Deluge add returned no id for client {ClientName} after core.add_torrent_magnet. Attempting discovery retries.", client.Name ?? client.Id);
                        var discoveredId = await DiscoverAddedTorrentAsync(client, result.Title, result.Size > 0 ? result.Size : (long?)null, ct);
                        if (!string.IsNullOrEmpty(discoveredId))
                        {
                            _logger.LogInformation("Discovered torrent after add (magnet) for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredId, result.Title);
                            return discoveredId;
                        }

                        _logger.LogDebug("Discovery retries did not find the torrent after magnet add for client {ClientName}", client.Name ?? client.Id);
                    }
                    _logger.LogInformation("Deluge added magnet for '{Title}' id: {Id}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(id));
                    return id;
                }

                // Torrent URL - attempt to download the torrent file server-side and add as a file if possible.
                try
                {
                    var http = _httpClientFactory.CreateClient("deluge");
                    using var getReq = new HttpRequestMessage(HttpMethod.Get, torrentUrl);

                    var getResp = await http.SendAsync(getReq, ct);
                    if (getResp.IsSuccessStatusCode)
                    {
                        var contentBytes = await getResp.Content.ReadAsByteArrayAsync(ct);
                        var mediaType = getResp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                        var looksLikeTorrent = (!string.IsNullOrEmpty(mediaType) && mediaType.Contains("torrent", StringComparison.OrdinalIgnoreCase))
                                              || (contentBytes != null && contentBytes.Length > 0 && Encoding.ASCII.GetString(contentBytes, 0, Math.Min(128, contentBytes.Length)).Contains("announce", StringComparison.OrdinalIgnoreCase));

                        if (looksLikeTorrent)
                        {
                            var uri = new Uri(torrentUrl);
                            var filename = Path.GetFileName(uri.LocalPath);
                            if (string.IsNullOrEmpty(filename))
                            {
                                try
                                {
                                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                                    if (query.TryGetValue("file", out var fileVal) && !string.IsNullOrEmpty(fileVal))
                                    {
                                        filename = fileVal.ToString();
                                    }
                                }
                                catch { /* ignore parsing issues */ }
                            }

                            if (string.IsNullOrEmpty(filename)) filename = "listenarr.torrent";
                            var filedump = Convert.ToBase64String(contentBytes!);
                            var res = await InvokeAsync(client, "core.add_torrent_file", new object[] { filename, filedump, options }, ct);
                            try { _logger.LogDebug("Deluge add (file from URL) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, res.ValueKind, res.ToString()); } catch { }
                            var fileId = res.ValueKind == JsonValueKind.String ? res.GetString() : null;
                            if (string.IsNullOrEmpty(fileId))
                            {
                                // Log raw response for diagnostics
                                try { _logger.LogWarning("Deluge add (file from URL) returned non-string/empty result for client {ClientName}: {Response}", client.Name ?? client.Id, res.ToString()); } catch { }

                                _logger.LogWarning("Deluge add (file from URL) returned no id for client {ClientName}. Attempting discovery retries.", client.Name ?? client.Id);
                                var discoveredId = await DiscoverAddedTorrentAsync(client, result.Title, result.Size, ct);
                                if (!string.IsNullOrEmpty(discoveredId))
                                {
                                    _logger.LogInformation("Discovered torrent after add (file from URL) for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredId, result.Title);
                                    return discoveredId;
                                }
                                _logger.LogDebug("Discovery retries did not find the torrent after add (file from URL) for client {ClientName}", client.Name ?? client.Id);

                                // Fallback to URL add in case the file upload path is unreliable for this indexer link
                                try
                                {
                                    _logger.LogDebug("Attempting fallback add via core.add_torrent_url for client {ClientName}", client.Name ?? client.Id);
                                    var fallbackUrlRes2 = await InvokeAsync(client, "core.add_torrent_url", new object[] { torrentUrl, options }, ct);
                                    try { _logger.LogDebug("Deluge fallback add (url) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, fallbackUrlRes2.ValueKind, fallbackUrlRes2.ToString()); } catch { }
                                    var fallbackUrlId2 = fallbackUrlRes2.ValueKind == JsonValueKind.String ? fallbackUrlRes2.GetString() : null;
                                    if (!string.IsNullOrEmpty(fallbackUrlId2))
                                    {
                                        _logger.LogInformation("Deluge fallback add by URL succeeded for '{Url}' id: {Id}", LogRedaction.SanitizeUrl(torrentUrl), LogRedaction.SanitizeText(fallbackUrlId2));
                                        return fallbackUrlId2;
                                    }

                                    var discoveredAfterUrl = await DiscoverAddedTorrentAsync(client, result.Title, result.Size, ct);
                                    if (!string.IsNullOrEmpty(discoveredAfterUrl))
                                    {
                                        _logger.LogInformation("Discovered torrent after fallback URL add for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredAfterUrl, result.Title);
                                        return discoveredAfterUrl;
                                    }
                                }
                                catch (Exception exFallback)
                                {
                                    _logger.LogDebug(exFallback, "Fallback add via URL failed for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                                }
                            }
                            _logger.LogInformation("Deluge added torrent file for URL '{Url}' id: {Id}", LogRedaction.SanitizeUrl(torrentUrl), LogRedaction.SanitizeText(fileId));
                            return fileId;
                        }
                        else
                        {
                            _logger.LogDebug("Fetched URL {Url} but content did not appear to be a torrent (content-type={ContentType}, length={Length})", LogRedaction.SanitizeUrl(torrentUrl), mediaType, contentBytes?.Length ?? 0);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Failed to fetch torrent URL {Url} (status={Status})", LogRedaction.SanitizeUrl(torrentUrl), getResp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch torrent URL for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                }

                // Fallback to add by URL if file fetch didn't succeed or didn't look like a torrent
                var urlRes = await InvokeAsync(client, "core.add_torrent_url", new object[] { torrentUrl, options }, ct);
                try { _logger.LogDebug("Deluge add (url) response for client {ClientName}: {Kind} - {Response}", client.Name ?? client.Id, urlRes.ValueKind, urlRes.ToString()); } catch { }
                var urlId = urlRes.ValueKind == JsonValueKind.String ? urlRes.GetString() : null;
                if (string.IsNullOrEmpty(urlId))
                {
                    _logger.LogWarning("Deluge add (url) returned no id for client {ClientName}. Attempting discovery retries.", client.Name ?? client.Id);
                    var discoveredId = await DiscoverAddedTorrentAsync(client, result.Title, result.Size > 0 ? result.Size : (long?)null, ct);
                    if (!string.IsNullOrEmpty(discoveredId))
                    {
                        _logger.LogInformation("Discovered torrent after add (url) for client {ClientName}: {Id} (Title: {Title})", client.Name ?? client.Id, discoveredId, result.Title);
                        return discoveredId;
                    }

                    _logger.LogDebug("Discovery retries did not find the torrent after add (url) for client {ClientName}", client.Name ?? client.Id);
                }
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
                    var count = res.EnumerateObject().Count();
                    _logger.LogDebug("Deluge queue response for client {ClientName} contains {Count} entries", client.Name ?? client.Id, count);
                    try { _logger.LogInformation("Deluge raw queue object for client {ClientName}: {Response}", client.Name ?? client.Id, res.ToString()); } catch { }

                    var fallbackHandled = false;

                    if (count == 0)
                    {
                        // Log the returned JSON for debugging (non-sensitive info)
                        try
                        {
                            _logger.LogDebug("Deluge queue returned empty object for client {ClientName}: {Response}", client.Name ?? client.Id, res.ToString());
                        }
                        catch { /* swallow logging errors */ }

                        // If the first call returned nothing, wait briefly and retry the same call once (with keys) to handle delayed population
                        try
                        {
                            await Task.Delay(250, ct);
                            var retryRes = await InvokeAsync(client, "core.get_torrents_status", new object[] { new Dictionary<string, object>(), keys }, ct);
                            if (retryRes.ValueKind == JsonValueKind.Object)
                            {
                                var count2 = retryRes.EnumerateObject().Count();
                                _logger.LogDebug("Deluge retry queue response for client {ClientName} contains {Count} entries", client.Name ?? client.Id, count2);
                                if (count2 > 0)
                                {
                                    foreach (var prop2 in retryRes.EnumerateObject())
                                    {
                                        try
                                        {
                                            var torrentId = prop2.Name;
                                            var data = prop2.Value;
                                            var q = await MapTorrentAsync(client, torrentId, data, ct);
                                            items.Add(q);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogDebug(ex, "Failed to map Deluge torrent entry from retry (non-fatal)");
                                        }
                                    }

                                    // Mark that retry returned entries so we skip primary enumeration below
                                    fallbackHandled = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Deluge queue retry failed for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                        }
                    }

                    if (!fallbackHandled)
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
                else
                {
                    // Unexpected response type - log for troubleshooting
                    try
                    {
                        _logger.LogDebug("Deluge queue response had kind {Kind} for client {ClientName}: {Response}", res.ValueKind, client.Name ?? client.Id, res.ToString());
                    }
                    catch { /* swallow logging errors */ }
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
            var progress = data.TryGetProperty("progress", out var progProp) ? (progProp.ValueKind == JsonValueKind.Number ? progProp.GetDouble() : 0d) : 0d;
            var total = data.TryGetProperty("total_size", out var sizeProp) ? sizeProp.GetInt64() : 0L;
            // Deluge returns progress either as fraction (0.0..1.0) or as percentage (0..100). Normalize to 0..100
            double progressPercent = progress;
            if (progressPercent <= 1.01) // likely a fraction
                progressPercent = progressPercent * 100.0;
            progressPercent = Math.Max(0.0, Math.Min(100.0, progressPercent));
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

            if (progressPercent >= 100.0 && (status == "seeding" || status == "queued" || status == "paused") && (progress > 1.01 || Math.Abs(progress - 100.0) < 0.0001))
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
                Progress = progressPercent,
                Size = total,
                Downloaded = total > 0 ? (long)Math.Round(total * (progressPercent / 100.0)) : 0L,
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

            try { _logger.LogInformation("Mapped Deluge torrent {TorrentId}: state={State}, progressRaw={RawProgress}, progressPercent={ProgressPercent}, total={Total}", id, state, progress, progressPercent, total); } catch { }
            return queueItem;
        }

        /// <summary>
        /// Attempts to invoke a Deluge JSON-RPC method. The implementation tries two common payload formats
        /// (named "jsonrpc" style and the array RPC-style used by some Deluge versions) for maximum
        /// compatibility with different Deluge/Web versions.
        /// </summary>
        private async Task<JsonElement> InvokeAsync(DownloadClientConfiguration client, string method, object[] args, CancellationToken ct, bool hasAttemptedAuth = false)
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

                        // If Deluge returned an error (e.g., Not authenticated), attempt a single auth.login and retry once
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var errorProp))
                        {
                            string? msg = null;
                            int? code = null;
                            try { if (errorProp.TryGetProperty("message", out var m)) msg = m.GetString(); } catch { }
                            try { if (errorProp.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number) code = c.GetInt32(); } catch { }

                            if (!hasAttemptedAuth && (msg?.IndexOf("Not authenticated", StringComparison.OrdinalIgnoreCase) >= 0 || code == 1))
                            {
                                _logger.LogWarning("Deluge returned authentication error for client {ClientName}; attempting auth.login and retrying once", client.Name ?? client.Id);
                                if (!string.IsNullOrEmpty(client.Password))
                                {
                                    try
                                    {
                                        var authRes = await InvokeAsync(client, "auth.login", new object[] { client.Password }, ct, true);
                                        if (authRes.ValueKind == JsonValueKind.True || (authRes.ValueKind == JsonValueKind.String && authRes.GetString() == "True"))
                                        {
                                            _logger.LogInformation("Deluge auth.login succeeded for client {ClientName}; retrying method {Method}", client.Name ?? client.Id, method);
                                            return await InvokeAsync(client, method, args, ct, true);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Deluge auth.login did not succeed for client {ClientName}", client.Name ?? client.Id);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Deluge auth.login attempt failed for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                                    }
                                }
                            }
                        }

                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var resultProp))
                        {
                            // If this is an 'add' RPC and the result is not a string (id), log request/response for diagnosis
                            if (method.StartsWith("core.add", StringComparison.OrdinalIgnoreCase) && resultProp.ValueKind != JsonValueKind.String)
                            {
                                try { _logger.LogWarning("Deluge add RPC returned non-string result. Request: {RequestBody} Response: {ResponseBody}", serialized, body); } catch { }
                            }
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
                        // If this is an 'add' RPC and the return value is not a string id, log details for diagnosis
                        if (method.StartsWith("core.add", StringComparison.OrdinalIgnoreCase) && rv.ValueKind != JsonValueKind.String)
                        {
                            try { _logger.LogWarning("Deluge add RPC (array-style) returned non-string result. Request: {RequestBody} Response: {ResponseBody}", serializedArray, body2); } catch { }
                        }
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

        private async Task<string?> DiscoverAddedTorrentAsync(DownloadClientConfiguration client, string? title, long? size, CancellationToken ct)
        {
            const int maxAttempts = 6;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    var delay = 500 * attempt; // 500ms, 1000ms, ...
                    try { await Task.Delay(delay, ct); } catch { /* swallow */ }
                }

                try
                {
                    var queue = await GetQueueAsync(client, ct);
                    if (queue == null || queue.Count == 0)
                    {
                        _logger.LogDebug("Discovery attempt {Attempt} found empty queue for client {ClientName}", attempt + 1, client.Name ?? client.Id);
                        continue;
                    }

                    _logger.LogDebug("Discovery attempt {Attempt} found {Count} queue items for client {ClientName}", attempt + 1, queue.Count, client.Name ?? client.Id);

                    foreach (var q in queue)
                    {
                        try
                        {
                            // Use fuzzy title matching to be tolerant of minor variations
                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(q.Title) && AreTitlesSimilar(title, q.Title))
                                return q.Id;

                            // Allow exact size match as another heuristic
                            if (size.HasValue && q.Size > 0 && q.Size == size.Value)
                                return q.Id;

                            // Allow size matches within 1% or 1MB margin
                            if (size.HasValue && q.Size > 0)
                            {
                                var diff = Math.Abs(q.Size - size.Value);
                                if (diff <= Math.Max(1024 * 1024, (long)Math.Ceiling(size.Value * 0.01)))
                                    return q.Id;
                            }
                        }
                        catch { /* non-fatal */ }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Discovery attempt {Attempt} failed for client {ClientName} (non-fatal)", attempt + 1, client.Name ?? client.Id);
                }
            }

            return null;
        }

        private bool AreTitlesSimilar(string a, string b)
        {
            try
            {
                var An = NormalizeTitle(a);
                var Bn = NormalizeTitle(b);
                return An.Contains(Bn) || Bn.Contains(An) || An == Bn;
            }
            catch { return false; }
        }

        private string NormalizeTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var lower = s.ToLowerInvariant();
            var cleaned = new string(lower.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray());
            return string.Join(' ', cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

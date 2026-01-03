using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Adapters
{
    public class SabnzbdAdapter : IDownloadClientAdapter
    {
        public string ClientId => "sabnzbd";
        public string ClientType => "sabnzbd";

        private readonly IHttpClientFactory _httpFactory;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly INzbUrlResolver _nzbUrlResolver;
        private readonly ILogger<SabnzbdAdapter> _logger;

        public SabnzbdAdapter(
            IHttpClientFactory httpFactory,
            IRemotePathMappingService pathMappingService,
            INzbUrlResolver nzbUrlResolver,
            ILogger<SabnzbdAdapter> logger)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _pathMappingService = pathMappingService ?? throw new ArgumentNullException(nameof(pathMappingService));
            _nzbUrlResolver = nzbUrlResolver ?? throw new ArgumentNullException(nameof(nzbUrlResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            try
            {
                if (client == null) throw new ArgumentNullException(nameof(client));

                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                    apiKey = apiKeyObj?.ToString() ?? "";

                if (string.IsNullOrEmpty(apiKey))
                    return (false, "SABnzbd API key not configured in client settings");

                var url = $"{baseUrl}?mode=version&output=json&apikey={Uri.EscapeDataString(apiKey)}";
                var http = _httpFactory.CreateClient("DownloadClient");
                var resp = await http.GetAsync(url, ct);
                var txt = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var redacted = LogRedaction.RedactText(txt, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { apiKey }));
                    return (false, $"SABnzbd returned {resp.StatusCode}: {redacted}");
                }

                return (true, "SABnzbd: API reachable and key validated");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SABnzbd TestConnection failed");
                return (false, ex.Message);
            }
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";

                // Get API key
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                {
                    apiKey = apiKeyObj?.ToString() ?? "";
                }

                if (string.IsNullOrEmpty(apiKey))
                    throw new Exception("SABnzbd API key not configured");

                var (nzbUrl, indexerApiKey) = await _nzbUrlResolver.ResolveAsync(result, ct);
                if (string.IsNullOrEmpty(nzbUrl))
                    throw new Exception("No NZB URL found in search result");

                _logger.LogInformation("Sending NZB to SABnzbd: {Title} from {Source}", LogRedaction.SanitizeText(result.Title), LogRedaction.SanitizeText(result.Source));

                var sensitiveValues = LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { apiKey }).ToList();
                if (!string.IsNullOrEmpty(indexerApiKey)) sensitiveValues.Add(indexerApiKey);

                var queryParams = new Dictionary<string, string>
                {
                    { "mode", "addurl" },
                    { "name", nzbUrl },
                    { "apikey", apiKey },
                    { "output", "json" },
                    { "nzbname", result.Title }
                };

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

                var category = "audiobooks";
                if (client.Settings != null && client.Settings.TryGetValue("category", out var categoryObj))
                {
                    var configuredCategory = categoryObj?.ToString();
                    if (!string.IsNullOrEmpty(configuredCategory))
                        category = configuredCategory;
                }
                queryParams["cat"] = category;

                var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                var requestUrl = $"{baseUrl}?{queryString}";

                _logger.LogDebug("SABnzbd request URL: {Url}", LogRedaction.RedactText(requestUrl, sensitiveValues));

                var http = _httpFactory.CreateClient("DownloadClient");
                var response = await http.GetAsync(requestUrl, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var redacted = LogRedaction.RedactText(responseContent, sensitiveValues);
                    _logger.LogError("SABnzbd returned error status {Status}: {Content}", response.StatusCode, redacted);
                    throw new Exception($"SABnzbd returned status {response.StatusCode}: {redacted}");
                }

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    _logger.LogWarning("SABnzbd returned empty response body when adding NZB: {Url}", LogRedaction.RedactText(requestUrl, sensitiveValues));
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorMsg = errorElement.GetString();
                    if (!string.IsNullOrEmpty(errorMsg))
                        throw new Exception($"SABnzbd error: {errorMsg}");
                }

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

                _logger.LogInformation("Successfully added NZB to SABnzbd with ID: {DownloadId}", LogRedaction.SanitizeText(downloadId));
                return downloadId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send NZB to SABnzbd");
                throw;
            }
        }

        public async Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";

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

                var removeUrl = $"{baseUrl}?mode=queue&name=delete&value={Uri.EscapeDataString(id)}&apikey={Uri.EscapeDataString(apiKey)}&output=json";
                if (deleteFiles)
                    removeUrl += "&del_files=1";

                var http = _httpFactory.CreateClient("DownloadClient");
                var response = await http.GetAsync(removeUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to remove from SABnzbd: Status {Status}", response.StatusCode);
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("status", out var status))
                {
                    var statusBool = status.GetBoolean();
                    _logger.LogInformation("Removed {DownloadId} from SABnzbd: {Success}", LogRedaction.SanitizeText(id), statusBool);
                    return statusBool;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from SABnzbd: {DownloadId}", LogRedaction.SanitizeText(id));
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";
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

                var requestUrl = $"{baseUrl}?mode=queue&output=json&apikey={Uri.EscapeDataString(apiKey)}";
                _logger.LogDebug("SABnzbd queue request (redacted): {Url}", LogRedaction.RedactText(requestUrl, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { apiKey })));

                var http = _httpFactory.CreateClient("DownloadClient");
                var response = await http.GetAsync(requestUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SABnzbd queue request failed with status {Status}", response.StatusCode);
                    return items;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("SABnzbd returned empty response for client {ClientName}", LogRedaction.SanitizeText(client.Name));
                    return items;
                }

                var doc = JsonDocument.Parse(jsonContent);
                if (!doc.RootElement.TryGetProperty("queue", out var queue)) return items;
                if (!queue.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array) return items;

                foreach (var slot in slots.EnumerateArray())
                {
                    try
                    {
                        var nzoId = slot.TryGetProperty("nzo_id", out var id) ? id.GetString() ?? "" : "";
                        var filename = slot.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "Unknown" : "Unknown";
                        var status = slot.TryGetProperty("status", out var st) ? st.GetString() ?? "Unknown" : "Unknown";

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

                        int etaSeconds = 0;
                        if (!string.IsNullOrEmpty(timeLeft) && timeLeft != "0:00:00")
                        {
                            etaSeconds = ParseSABnzbdTimeLeft(timeLeft);
                        }

                        var sizeBytes = (long)(sizeMB * 1024 * 1024);
                        var downloadedBytes = (long)(downloadedMB * 1024 * 1024);

                        var speed = 0.0;
                        if (queue.TryGetProperty("speed", out var speedProp))
                        {
                            var speedStr = speedProp.GetString() ?? "0";
                            speed = ParseSABnzbdSpeed(speedStr);
                        }

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

                        var remotePath = client.DownloadPath ?? "";
                        var localPath = !string.IsNullOrEmpty(remotePath)
                            ? await _pathMappingService.TranslatePathAsync(client.Id, remotePath)
                            : remotePath;

                        // For SABnzbd, construct ContentPath from download path + filename
                        var contentPath = !string.IsNullOrEmpty(remotePath) && !string.IsNullOrEmpty(filename)
                            ? Path.Combine(remotePath, filename)
                            : remotePath;
                        var localContentPath = !string.IsNullOrEmpty(contentPath)
                            ? await _pathMappingService.TranslatePathAsync(client.Id, contentPath)
                            : contentPath;

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
                            AddedAt = DateTime.UtcNow,
                            CanPause = mappedStatus == "downloading" || mappedStatus == "queued",
                            CanRemove = true,
                            RemotePath = remotePath,
                            LocalPath = localPath,
                            ContentPath = localContentPath
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

        public async Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            var result = new List<(string Id, string Name)>();
            if (client == null) return result;

            try
            {
                var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";
                var apiKey = "";
                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                {
                    apiKey = apiKeyObj?.ToString() ?? "";
                }
                if (string.IsNullOrEmpty(apiKey)) return result;

                var historyUrl = $"{baseUrl}?mode=history&output=json&limit={limit}&apikey={Uri.EscapeDataString(apiKey)}";
                var http = _httpFactory.CreateClient("DownloadClient");
                var historyResp = await http.GetAsync(historyUrl, ct);
                if (!historyResp.IsSuccessStatusCode) return result;

                var historyText = await historyResp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(historyText)) return result;

                var doc = JsonDocument.Parse(historyText);
                if (doc.RootElement.TryGetProperty("history", out var history) && history.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array)
                {
                    foreach (var slot in slots.EnumerateArray())
                    {
                        var nzoId = slot.TryGetProperty("nzo_id", out var nzo) ? nzo.GetString() ?? string.Empty : string.Empty;
                        var name = slot.TryGetProperty("name", out var nm) ? nm.GetString() ?? string.Empty : string.Empty;
                        result.Add((nzoId, name));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch SABnzbd history (non-fatal)");
            }

            return result;
        }

        private int ParseSABnzbdTimeLeft(string timeLeft)
        {
            try
            {
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
    }
}

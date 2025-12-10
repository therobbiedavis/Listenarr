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
using System.IO;
using System.Xml.Linq;

namespace Listenarr.Api.Services.Adapters
{
    public class NzbgetAdapter : IDownloadClientAdapter
    {
        public string ClientId => "nzbget";
        public string ClientType => "nzbget";

        private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INzbUrlResolver _nzbUrlResolver;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly ILogger<NzbgetAdapter> _logger;

        public NzbgetAdapter(
            IHttpClientFactory httpClientFactory,
            INzbUrlResolver nzbUrlResolver,
            IRemotePathMappingService pathMappingService,
            ILogger<NzbgetAdapter> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _nzbUrlResolver = nzbUrlResolver ?? throw new ArgumentNullException(nameof(nzbUrlResolver));
            _pathMappingService = pathMappingService ?? throw new ArgumentNullException(nameof(pathMappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            if (client == null)
            {
                return (false, "NZBGet: Configuration not provided");
            }

            if (!string.IsNullOrWhiteSpace(client.Username) && string.IsNullOrWhiteSpace(client.Password))
            {
                return (false, "NZBGet: Password is required when a username is specified");
            }

            try
            {
                // Test connection via XML-RPC
                var versionResult = await CallXmlRpcAsync(client, "version");
                var version = versionResult.Element("string")?.Value ?? "unknown";

                if (string.IsNullOrWhiteSpace(version))
                {
                    return (false, "NZBGet: Unable to retrieve version");
                }

                return (true, $"NZBGet: Authentication succeeded (version {version}, using XML-RPC)");
            }
            catch (HttpRequestException httpEx) when (httpEx.StatusCode == HttpStatusCode.Unauthorized || httpEx.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogDebug(httpEx, "NZBGet authentication failed for client {ClientId}", LogRedaction.SanitizeText(client.Id ?? client.Name ?? client.Type));
                return (false, "NZBGet: Authentication failed (check username/password)");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "NZBGet test failed for client {ClientId}", LogRedaction.SanitizeText(client.Id ?? client.Name ?? client.Type));
                return (false, ex.Message);
            }
        }

        private static bool IsVersion25OrNewer(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            
            // Version format: "25.4" or "25.4-testing"
            var parts = version.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out var major))
            {
                return major >= 25;
            }
            
            return false;
        }

        public async Task<string?> AddAsync(DownloadClientConfiguration client, SearchResult result, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var (nzbUrl, indexerApiKey) = await _nzbUrlResolver.ResolveAsync(result, ct);
            if (string.IsNullOrWhiteSpace(nzbUrl))
            {
                throw new ArgumentException("No NZB URL available for NZBGet", nameof(result));
            }

            // Use JSON-RPC for all versions (v25+ REST API has authentication issues)
            _logger.LogInformation("Using NZBGet JSON-RPC append method");
            return await AddViaJsonRpcAsync(client, result, nzbUrl, indexerApiKey, ct);
        }

        private async Task<string?> AddViaRestApiAsync(
            DownloadClientConfiguration client, 
            SearchResult result, 
            string nzbUrl, 
            string? indexerApiKey, 
            CancellationToken ct)
        {
            var category = ResolveCategory(client);
            var priority = ResolvePriority(client);
            var droneId = Guid.NewGuid().ToString().Replace("-", string.Empty);
            
            // Download NZB content
            var nzbBytes = await DownloadNzbAsync(nzbUrl, indexerApiKey, ct);
            var nzbFileName = BuildNzbFileName(result);

            var scheme = client.UseSSL ? "https" : "http";
            var uploadUrl = $"{scheme}://{client.Host}:{client.Port}/api/v2/nzb";
            
            using var httpClient = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            
            // Add NZB file
            content.Add(new ByteArrayContent(nzbBytes), "file", nzbFileName);
            
            // Add metadata
            if (!string.IsNullOrWhiteSpace(category))
            {
                content.Add(new StringContent(category), "Category");
            }
            
            if (priority != 0)
            {
                content.Add(new StringContent(priority.ToString()), "Priority");
            }
            
            // Add drone tracking parameter
            content.Add(new StringContent($"drone={droneId}"), "PPParameters");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
            {
                Content = content
            };
            
            // Add Basic Auth (NZBGet v25 REST API accepts Basic Auth)
            var authHeader = BuildAuthHeader(client);
            if (authHeader != null)
            {
                request.Headers.Authorization = authHeader;
            }
            
            _logger.LogDebug("NZBGet REST API POST to {Url} with file {FileName}", LogRedaction.SanitizeUrl(uploadUrl), LogRedaction.SanitizeText(nzbFileName));
            
            var response = await httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("NZBGet REST API upload failed: {StatusCode} - {Body}", response.StatusCode, responseBody);
                throw new Exception($"NZBGet REST API upload error: {response.StatusCode} - {responseBody}");
            }
            
            // Parse response JSON to get queue ID
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            if (jsonResponse.TryGetProperty("nzbId", out var nzbIdProp))
            {
                var queueId = nzbIdProp.GetInt32();
                _logger.LogInformation("NZBGet REST API added '{Title}' with queue ID {QueueId}", LogRedaction.SanitizeText(result.Title), queueId);
                return queueId.ToString();
            }
            
            _logger.LogWarning("NZBGet REST API response missing nzbId: {Body}", responseBody);
            return null;
        }

        private async Task<string?> AddViaJsonRpcAsync(
            DownloadClientConfiguration client, 
            SearchResult result, 
            string nzbUrl, 
            string? indexerApiKey, 
            CancellationToken ct)
        {
            var category = ResolveCategory(client);
            var priority = ResolvePriority(client);
            var droneId = Guid.NewGuid().ToString().Replace("-", string.Empty);
            
            // Download and base64-encode the NZB content
            var nzbBytes = await DownloadNzbAsync(nzbUrl, indexerApiKey, ct);
            var nzbContentBase64 = Convert.ToBase64String(nzbBytes);
            var nzbFileName = BuildNzbFileName(result);

            // PPParameters as array of structs (key-value pairs)
            var ppParams = new[]
            {
                new Dictionary<string, object>
                {
                    { "Name", "drone" },
                    { "Value", droneId }
                }
            };
            
            try
            {
                // Call append via XML-RPC
                _logger.LogInformation("Calling NZBGet append via XML-RPC for '{Title}'", LogRedaction.SanitizeText(result.Title));
                var appendResult = await CallXmlRpcAsync(client, "append",
                    nzbFileName,
                    nzbContentBase64,
                    category ?? string.Empty,
                    priority,
                    false,  // addToTop
                    false,  // addPaused
                    string.Empty,  // dupeKey
                    0,      // dupeScore
                    "SCORE", // dupeMode
                    ppParams
                );

                var queueId = int.Parse(appendResult.Element("i4")?.Value ?? appendResult.Element("int")?.Value ?? "0");

                if (queueId <= 0)
                {
                    _logger.LogWarning("NZBGet rejected NZB '{Title}', returned ID: {QueueId}", LogRedaction.SanitizeText(result.Title), queueId);
                    return null;
                }

                _logger.LogInformation("NZBGet XML-RPC queued '{Title}' with ID {QueueId}, droneId: {DroneId}", LogRedaction.SanitizeText(result.Title), queueId, LogRedaction.SanitizeText(droneId));
                return droneId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add NZB via XML-RPC");
                throw;
            }
        }

        public async Task<bool> RemoveAsync(DownloadClientConfiguration client, string id, bool deleteFiles = false, CancellationToken ct = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            var command = deleteFiles ? "GroupDeleteFinal" : "GroupDelete";
            var numericId = TryParseId(id);
            if (!numericId.HasValue)
            {
                _logger.LogWarning("Cannot remove NZB {Id} - invalid ID format", LogRedaction.SanitizeText(id));
                return false;
            }

            try
            {
                var editResult = await CallXmlRpcAsync(client, "editqueue", command, 0, string.Empty, new[] { numericId.Value });
                var success = editResult.Element("boolean")?.Value == "1";
                
                if (success)
                {
                    _logger.LogInformation("Removed NZB {Id} from NZBGet (deleteFiles={DeleteFiles})", LogRedaction.SanitizeText(id), deleteFiles);
                    return true;
                }

                _logger.LogWarning("NZBGet reported failure when removing {Id}", LogRedaction.SanitizeText(id));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing NZB {Id} from NZBGet", LogRedaction.SanitizeText(id));
                return false;
            }
        }

        public async Task<List<QueueItem>> GetQueueAsync(DownloadClientConfiguration client, CancellationToken ct = default)
        {
            var items = new List<QueueItem>();
            if (client == null) return items;

            try
            {
                var listResult = await CallXmlRpcAsync(client, "listgroups");
                var arrayData = listResult.Element("array")?.Element("data");
                
                if (arrayData == null)
                {
                    return items;
                }

                foreach (var valueElement in arrayData.Elements("value"))
                {
                    try
                    {
                        var structElement = valueElement.Element("struct");
                        if (structElement != null)
                        {
                            var queueItem = MapGroup(client, structElement);
                            items.Add(queueItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to map NZBGet queue item (non-fatal)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve NZBGet queue for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
            }

            return items;
        }

        public async Task<List<(string Id, string Name)>> GetRecentHistoryAsync(DownloadClientConfiguration client, int limit = 100, CancellationToken ct = default)
        {
            var history = new List<(string Id, string Name)>();
            if (client == null) return history;

            try
            {
                var historyResult = await CallXmlRpcAsync(client, "history", false);
                var arrayData = historyResult.Element("array")?.Element("data");
                
                if (arrayData == null)
                {
                    return history;
                }

                var count = 0;
                foreach (var valueElement in arrayData.Elements("value"))
                {
                    if (count >= limit) break;
                    
                    var structElement = valueElement.Element("struct");
                    if (structElement != null)
                    {
                        var members = structElement.Elements("member").ToDictionary(
                            m => m.Element("name")?.Value ?? string.Empty,
                            m => m.Element("value")?.Elements().FirstOrDefault()?.Value ?? string.Empty
                        );
                        
                        var entryId = members.GetValueOrDefault("ID", string.Empty);
                        var entryName = members.GetValueOrDefault("NZBName", string.Empty);
                        
                        if (!string.IsNullOrEmpty(entryId) && !string.IsNullOrEmpty(entryName))
                        {
                            history.Add((entryId, entryName));
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch NZBGet history for client {ClientName}", LogRedaction.SanitizeText(client.Name ?? client.Id));
            }

            return history;
        }

        private QueueItem MapGroup(DownloadClientConfiguration client, XElement structElement)
        {
            var members = structElement.Elements("member").ToDictionary(
                m => m.Element("name")?.Value ?? string.Empty,
                m => m.Element("value")?.Elements().FirstOrDefault()?.Value ?? string.Empty
            ) as IReadOnlyDictionary<string, string?>;
            
            var id = members.GetValueOrDefault("GroupID", null)
                ?? members.GetValueOrDefault("LastID", null)
                ?? Guid.NewGuid().ToString("N");

            var title = members.GetValueOrDefault("NZBName", string.Empty);
            var statusRaw = members.GetValueOrDefault("Status", string.Empty);
            var category = members.GetValueOrDefault("Category", string.Empty);
            var sizeMb = double.TryParse(members.GetValueOrDefault("FileSizeMB", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var sm) ? sm : 0d;
            var remainingMb = double.TryParse(members.GetValueOrDefault("RemainingSizeMB", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var rm) ? rm : 0d;
            var downloadedMb = sizeMb - remainingMb;
            var downloadRate = double.TryParse(members.GetValueOrDefault("DownloadRate", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var dr) ? dr : 0d;
            var destDir = members.GetValueOrDefault("DestDir", string.Empty);

            var sizeBytes = Convert.ToInt64(Math.Max(0, sizeMb) * 1024 * 1024);
            var downloadedBytes = Convert.ToInt64(Math.Max(0, downloadedMb) * 1024 * 1024);

            int? etaSeconds = null;
            if (downloadRate > 0 && remainingMb > 0)
            {
                var remainingBytes = remainingMb * 1024 * 1024;
                etaSeconds = (int)Math.Max(0, remainingBytes / downloadRate);
            }

            string status = (statusRaw ?? "QUEUED").ToUpperInvariant() switch
            {
                "QUEUED" => "queued",
                "DOWNLOADING" => "downloading",
                "PAUSED" => "paused",
                "FETCHING" => "downloading",
                "SCANNING" => "downloading",
                "PP_QUEUED" => "downloading",
                "PP_PROCESSING" => "downloading",
                "SUCCESS" => "completed",
                "FAILURE" => "failed",
                _ => "queued"
            };

            string? localPath = destDir;
            if (!string.IsNullOrEmpty(destDir))
            {
                try
                {
                    localPath = _pathMappingService.TranslatePathAsync(client.Id, destDir).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to translate NZBGet path '{Path}' for client {ClientName}", LogRedaction.SanitizeFilePath(destDir), LogRedaction.SanitizeText(client.Name ?? client.Id));
                }
            }

            var addedAt = DateTime.UtcNow;

            return new QueueItem
            {
                Id = id,
                Title = title ?? string.Empty,
                Quality = category ?? string.Empty,
                Status = status,
                Progress = sizeMb > 0 ? Math.Clamp(downloadedMb / sizeMb * 100, 0, 100) : 0,
                Size = sizeBytes,
                Downloaded = downloadedBytes,
                DownloadSpeed = downloadRate,
                Eta = etaSeconds > 0 ? etaSeconds : null,
                DownloadClient = client.Name ?? client.Id ?? "NZBGet",
                DownloadClientId = client.Id ?? string.Empty,
                DownloadClientType = ClientType,
                AddedAt = addedAt,
                CanPause = status is "downloading" or "queued",
                CanRemove = true,
                RemotePath = destDir,
                LocalPath = localPath
            };
        }

        private string ResolveCategory(DownloadClientConfiguration client)
        {
            if (client.Settings != null && client.Settings.TryGetValue("category", out var categoryObj))
            {
                var category = categoryObj?.ToString();
                if (!string.IsNullOrWhiteSpace(category))
                {
                    return category;
                }
            }

            return string.Empty;
        }

        private int ResolvePriority(DownloadClientConfiguration client)
        {
            if (client.Settings != null && client.Settings.TryGetValue("recentPriority", out var priorityObj))
            {
                var priority = priorityObj?.ToString();
                if (!string.IsNullOrWhiteSpace(priority) && !string.Equals(priority, "default", StringComparison.OrdinalIgnoreCase))
                {
                    return priority.ToLowerInvariant() switch
                    {
                        "force" => 100,
                        "high" => 50,
                        "normal" => 0,
                        "low" => -50,
                        _ => 0
                    };
                }
            }

            return 0;
        }

        private static string BuildNzbFileName(SearchResult result)
        {
            if (result == null)
            {
                return "listenarr-download.nzb";
            }

            var rawName = result.Title;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                if (!string.IsNullOrWhiteSpace(result.NzbUrl) && Uri.TryCreate(result.NzbUrl, UriKind.Absolute, out var nzbUri))
                {
                    rawName = Path.GetFileName(nzbUri.AbsolutePath);
                }

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    rawName = result.Id;
                }
            }

            if (string.IsNullOrWhiteSpace(rawName))
            {
                rawName = "listenarr-download";
            }

            // NZBGet v25.4 is very strict about filenames - remove ALL special characters except basic ones
            var sanitizedChars = rawName.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.').ToArray();
            var sanitized = new string(sanitizedChars).Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "listenarr-download";
            }

            if (!sanitized.EndsWith(".nzb", StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized + ".nzb";
            }

            return sanitized;
        }

        private async Task<XElement> CallXmlRpcAsync(DownloadClientConfiguration client, string methodName, params object[] parameters)
        {
            var baseUrl = BuildBaseUrl(client);
            var httpClient = _httpClientFactory.CreateClient();

            // Build XML-RPC request
            var methodCall = new XElement("methodCall",
                new XElement("methodName", methodName),
                new XElement("params",
                    parameters.Select(p => new XElement("param", new XElement("value", SerializeValue(p))))
                )
            );

            var xmlContent = $"<?xml version=\"1.0\"?>\n{methodCall}";
            var content = new StringContent(xmlContent, Encoding.UTF8, "text/xml");

            var response = await httpClient.PostAsync(baseUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"NZBGet XML-RPC error: {response.StatusCode} - {responseBody}");
            }

            var doc = XDocument.Parse(responseBody);
            var fault = doc.Root?.Element("fault");
            if (fault != null)
            {
                var faultStruct = fault.Descendants("member").ToDictionary(
                    m => m.Element("name")?.Value ?? string.Empty,
                    m => m.Element("value")?.Value ?? string.Empty
                );
                var faultString = faultStruct.GetValueOrDefault("faultString", "Unknown error");
                throw new Exception($"NZBGet XML-RPC fault: {faultString}");
            }

            return doc.Root?.Element("params")?.Element("param")?.Element("value")
                ?? throw new Exception("Invalid XML-RPC response");
        }

        private XElement SerializeValue(object value)
        {
            return value switch
            {
                string s => new XElement("string", s),
                int i => new XElement("i4", i),
                bool b => new XElement("boolean", b ? "1" : "0"),
                double d => new XElement("double", d.ToString(CultureInfo.InvariantCulture)),
                int[] arr => new XElement("array",
                    new XElement("data",
                        arr.Select(item => new XElement("value", new XElement("i4", item)))
                    )
                ),
                object[] arr => new XElement("array",
                    new XElement("data",
                        arr.Select(item => new XElement("value", SerializeValue(item)))
                    )
                ),
                Dictionary<string, object> dict => new XElement("struct",
                    dict.Select(kvp => new XElement("member",
                        new XElement("name", kvp.Key),
                        new XElement("value", SerializeValue(kvp.Value))
                    ))
                ),
                _ => new XElement("string", value?.ToString() ?? string.Empty)
            };
        }

        private static string BuildBaseUrl(DownloadClientConfiguration client)
        {
            var scheme = client.UseSSL ? "https" : "http";
            
            // NZBGet XML-RPC requires authentication in URL: http://username:password@host:port/xmlrpc
            if (!string.IsNullOrWhiteSpace(client.Username) && !string.IsNullOrWhiteSpace(client.Password))
            {
                var encodedUsername = Uri.EscapeDataString(client.Username);
                var encodedPassword = Uri.EscapeDataString(client.Password);
                return $"{scheme}://{encodedUsername}:{encodedPassword}@{client.Host}:{client.Port}/xmlrpc";
            }
            
            return $"{scheme}://{client.Host}:{client.Port}/xmlrpc";
        }

        private async Task<byte[]> DownloadNzbAsync(string nzbUrl, string? indexerApiKey, CancellationToken ct)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, nzbUrl);

                if (!string.IsNullOrWhiteSpace(indexerApiKey))
                {
                    request.Headers.Add("X-Api-Key", indexerApiKey);
                }

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var contentBytes = await response.Content.ReadAsByteArrayAsync(ct);
                return contentBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download NZB content from {Url}", LogRedaction.SanitizeUrl(nzbUrl));
                throw new InvalidOperationException($"Unable to retrieve NZB content from {nzbUrl}");
            }
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

        private static int? TryParseId(string id)
        {
            if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
            {
                return numericId;
            }

            return null;
        }
    }
}

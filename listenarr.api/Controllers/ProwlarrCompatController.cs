using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Listenarr.Api.Services;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]

    public class ProwlarrCompatController : ControllerBase
    {
        private readonly ILogger<ProwlarrCompatController> _logger;
        private readonly ListenArrDbContext _dbContext;
        private readonly IHubContext<SettingsHub> _settingsHub;
        private readonly IToastService _toastService;

        public ProwlarrCompatController(ILogger<ProwlarrCompatController> logger, ListenArrDbContext dbContext, IHubContext<SettingsHub> settingsHub, IToastService toastService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _settingsHub = settingsHub;
            _toastService = toastService;
        }

        private static string GetApplicationVersion()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var ver = asm?.GetName()?.Version?.ToString();
                if (!string.IsNullOrEmpty(ver))
                    return ver;

                var fi = FileVersionInfo.GetVersionInfo(asm?.Location ?? string.Empty);
                return fi?.ProductVersion ?? fi?.FileVersion ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// GET /api/v1/system/status
        /// Minimal Prowlarr-compatible system status endpoint.
        /// </summary>
        [HttpGet("system/status")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetSystemStatus()
        {
            Response.ContentType = "application/json";

            var dto = new SystemStatusDto
            {
                Status = "ok",
                Version = GetApplicationVersion(),
                Api = "Listenarr"
            };

            return Ok(dto);
        }

        /// <summary>
        /// POST /api/v1/indexer/test
        /// Responds with JSON and includes X-Application-Version header (useful for Prowlarr client checks)
        /// </summary>
        [HttpPost("indexer/test")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult PostIndexerTest()
        {
            _logger?.LogInformation("Prowlarr indexer test invoked (POST)");
            Response.ContentType = "application/json";

            var version = GetApplicationVersion();
            // Return header for clients that expect it
            Response.Headers["X-Application-Version"] = version;

            var dto = new IndexerTestResponseDto
            {
                Success = true,
                Message = "Test OK",
                Version = version
            };

            return Ok(dto);
        }

        [HttpGet("indexer/test")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexerTest()
        {
            _logger?.LogInformation("Prowlarr indexer test invoked (GET)");
            Response.ContentType = "application/json";

            var version = GetApplicationVersion();
            Response.Headers["X-Application-Version"] = version;

            var dto = new IndexerTestResponseDto
            {
                Success = true,
                Message = "Test OK (GET)",
                Version = version
            };

            return Ok(dto);
        }

        // Debug-only POST to verify POST handling bypasses antiforgery and auth middleware
        [HttpPost("debug/test")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Produces("application/json")]
        public IActionResult PostDebugTest()
        {
            Response.ContentType = "application/json";
            return Ok(new { ok = true });
        }

        /// <summary>
        /// GET /api/v1/indexer
        /// Returns the list of configured indexers (Prowlarr expects a JSON array here).
        /// Maintained for Prowlarr compatibility: returns persisted indexers from the DB.
        /// </summary>
        [HttpGet("indexer")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexers()
        {
            Response.ContentType = "application/json";
            // Fetch persisted indexers so that remote applications (Prowlarr) receive a JSON array
            var indexers = _dbContext.Indexers
                .OrderBy(i => i.Priority)
                .ThenBy(i => i.Name)
                .AsNoTracking()
                .ToList()
                .Select(i => new
                {
                    id = i.Id,
                    name = i.Name,
                    implementation = i.Implementation,
                    baseUrl = i.Url,
                    apiKey = i.ApiKey,
                    categories = string.IsNullOrEmpty(i.Categories) ? System.Array.Empty<string>() : i.Categories.Split(',').Select(s => s.Trim()).ToArray()
                })
                .ToArray();

            return Ok(indexers);
        }

        /// <summary>
        /// GET /api/v1/indexer/{id}
        /// Returns a detailed indexer object for a specific indexer id. Includes a `settings` object for compatibility with consumers expecting nested settings.
        /// </summary>
        [HttpGet("indexer/{id:int}")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexerById(int id)
        {
            Response.ContentType = "application/json";
            var i = _dbContext.Indexers.AsNoTracking().FirstOrDefault(x => x.Id == id);
            if (i == null)
            {
                // Return a 200 with a minimal compatibility object for id=0 or unknown ids
                var fallback = new
                {
                    id = id,
                    name = "Prowlarr Indexer",
                    implementation = "Newznab",
                    baseUrl = string.Empty,
                    apiKey = (string?)null,
                    categories = System.Array.Empty<string>(),
                    settings = new
                    {
                        baseUrl = string.Empty,
                        apiKey = (string?)null,
                        apiPath = string.Empty,
                        categories = (string[]?)null
                    }
                };

                return Ok(fallback);
            }

            var dto = new
            {
                id = i.Id,
                name = i.Name,
                implementation = i.Implementation,
                baseUrl = i.Url,
                apiKey = i.ApiKey,
                categories = string.IsNullOrEmpty(i.Categories) ? System.Array.Empty<string>() : i.Categories.Split(',').Select(s => s.Trim()).ToArray(),
                settings = new
                {
                    baseUrl = i.Url,
                    apiKey = i.ApiKey,
                    apiPath = string.Empty,
                    categories = string.IsNullOrEmpty(i.Categories) ? System.Array.Empty<string>() : i.Categories.Split(',').Select(s => s.Trim()).ToArray()
                }
            };

            return Ok(dto);
        }

        /// <summary>
        /// GET /api/v1/indexer/info
        /// Compatibility endpoint that returns metadata about supported implementations and schema endpoint.
        /// </summary>
        [HttpGet("indexer/info")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexersInfo()
        {
            Response.ContentType = "application/json";
            var payload = new
            {
                implementations = new[] { "Newznab", "Torznab" },
                schema = "/api/v1/indexer/schema"
            };
            return Ok(payload);
        }

        /// <summary>
        /// GET /api/v1/indexers
        /// Returns the list of configured indexers (Prowlarr expects a JSON array here).
        /// </summary>
        [HttpGet("indexers")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexersList()
        {
            Response.ContentType = "application/json";
            // Return an empty array by default. Prowlarr/Sonarr will POST indexers to populate.
            return Ok(System.Array.Empty<object>());
        }

        /// <summary>
        /// POST /api/v1/indexers
        /// Accepts an array of indexers from Prowlarr. Expects a JSON array; returns 200 OK if received.
        /// </summary>
        [HttpPost("indexers")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> PostIndexers([FromBody] System.Text.Json.JsonElement payload)
        {
                _logger?.LogInformation("Prowlarr indexers payload received: {Kind}", payload.ValueKind.ToString());
                // Log raw request body (redacted) to aid debugging; truncate/sanitize sensitive values
                try
                {
                    var raw = payload.GetRawText();
                    var redacted = LogRedaction.RedactText(raw, LogRedaction.GetSensitiveValuesFromEnvironment());
                    _logger?.LogInformation("Prowlarr indexers payload body: {Payload}", redacted);
                }
                catch { }

                if (HttpContext?.Response != null) HttpContext.Response.ContentType = "application/json";

            if (payload.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return BadRequest(new { message = "Expected JSON array of indexers" });
            }

            var created = 0;
            var skipped = 0;
            var createdIndexers = new System.Collections.Generic.List<Listenarr.Domain.Models.Indexer>();

            foreach (var item in payload.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                    continue;

                // Extract common fields with tolerant mapping
                string getString(System.Text.Json.JsonElement el, string prop1, string? prop2 = null)
                {
                    if (el.TryGetProperty(prop1, out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
                        return p.GetString() ?? string.Empty;
                    if (prop2 != null && el.TryGetProperty(prop2, out var p2) && p2.ValueKind == System.Text.Json.JsonValueKind.String)
                        return p2.GetString() ?? string.Empty;
                    return string.Empty;
                }

                var name = getString(item, "name", "title");
                var implementation = getString(item, "implementation", "type");
                var baseUrl = getString(item, "baseUrl", "url");
                var apiPath = getString(item, "apiPath", null);
                var apiKey = getString(item, "apiKey", null);

                // categories can be array or string
                string? categories = null;
                if (item.TryGetProperty("categories", out var cats))
                {
                    if (cats.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var parts = cats.EnumerateArray().Select(x => x.ValueKind == System.Text.Json.JsonValueKind.Number ? x.GetInt32().ToString() : x.GetString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s));
                        categories = string.Join(',', parts);
                    }
                    else if (cats.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        categories = cats.GetString();
                    }
                }

                if (!string.IsNullOrEmpty(apiPath) && !string.IsNullOrEmpty(baseUrl))
                {
                    baseUrl = baseUrl.TrimEnd('/') + "/" + apiPath.Trim('/');
                }

                // If baseUrl/apiKey/apiPath absent, try to look for a settings object with baseUrl/apiKey
                if (string.IsNullOrEmpty(baseUrl) && item.TryGetProperty("settings", out var settings) && settings.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    baseUrl = getString(settings, "baseUrl", "url");
                    if (string.IsNullOrEmpty(apiKey)) apiKey = getString(settings, "apiKey", "apikey");
                    if (string.IsNullOrEmpty(apiPath)) apiPath = getString(settings, "apiPath", null);
                }

                // If still missing, try to extract from a "fields" array (Prowlarr sends baseUrl/apiKey/categories within fields)
                if ((string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiPath) || string.IsNullOrEmpty(categories)) &&
                    item.TryGetProperty("fields", out var fields) && fields.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var f in fields.EnumerateArray())
                    {
                        if (f.ValueKind != System.Text.Json.JsonValueKind.Object)
                            continue;

                        var fname = getString(f, "name", null);
                        if (string.IsNullOrEmpty(fname))
                            continue;

                        // baseUrl, apiKey, apiPath are strings inside field.value
                        if (string.IsNullOrEmpty(baseUrl) && fname.Equals("baseUrl", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (f.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                                baseUrl = v.GetString() ?? string.Empty;
                        }

                        if (string.IsNullOrEmpty(apiKey) && fname.Equals("apiKey", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (f.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                                apiKey = v.GetString() ?? string.Empty;
                        }

                        if (string.IsNullOrEmpty(apiPath) && fname.Equals("apiPath", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (f.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                                apiPath = v.GetString() ?? string.Empty;
                        }

                        if (string.IsNullOrEmpty(categories) && fname.Equals("categories", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (f.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var parts = v.EnumerateArray().Select(x => x.ValueKind == System.Text.Json.JsonValueKind.Number ? x.GetInt32().ToString() : x.GetString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s));
                                categories = string.Join(',', parts);
                            }
                            else if (f.TryGetProperty("value", out var vs) && vs.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                categories = vs.GetString();
                            }
                        }

                        if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiPath) && !string.IsNullOrEmpty(categories))
                        {
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(name)) name = baseUrl ?? "Prowlarr Indexer";
                if (string.IsNullOrEmpty(implementation)) implementation = "Custom";

                // Normalize URLs
                var url = (baseUrl ?? string.Empty).Trim();

                // Deduplicate by normalized URL + ApiKey (normalizes trailing slash and trailing /api)
                var normalizedUrl = NormalizeIndexerUrl(url);
                var existingIndexers = _dbContext.Indexers.AsNoTracking().ToList();
                var exists = existingIndexers.FirstOrDefault(i => NormalizeIndexerUrl(i.Url) == normalizedUrl && (i.ApiKey ?? string.Empty) == (apiKey ?? string.Empty));
                if (exists != null)
                {
                    skipped++;
                    _logger?.LogInformation("Prowlarr: Skipping existing indexer (name={Name}, url={Url}, apiKeyPresent={HasApiKey})", name, exists.Url, !string.IsNullOrEmpty(apiKey));
                    continue;
                }

                var indexer = new Listenarr.Domain.Models.Indexer
                {
                    Name = name,
                    Implementation = implementation,
                    Url = url,
                    ApiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey,
                    Categories = categories,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                // Guess Type from implementation
                var implLower = (implementation ?? string.Empty).ToLowerInvariant();
                indexer.Type = implLower.Contains("newznab") ? "Usenet" : (implLower.Contains("torznab") ? "Torrent" : "Custom");

                _dbContext.Indexers.Add(indexer);
                created++;
                createdIndexers.Add(indexer);
                _logger?.LogInformation("Prowlarr: Created indexer (name={Name}, url={Url}, apiKeyPresent={HasApiKey})", indexer.Name, indexer.Url, !string.IsNullOrEmpty(indexer.ApiKey));
            }

            if (created > 0)
            {
                await _dbContext.SaveChangesAsync();
                try
                {
                    // Notify connected clients that indexers changed so the UI can refresh
                    var createdInfo = createdIndexers.Select(i => new { id = i.Id, name = i.Name, baseUrl = i.Url }).ToArray();

                    _logger?.LogInformation("Broadcasting IndexersUpdated to clients: created={Created}, skipped={Skipped}, indexerCount={Count}", created, skipped, createdInfo.Length);

                    await _settingsHub.Clients.All.SendAsync("IndexersUpdated", new { created, skipped, indexers = createdInfo });

                    _logger?.LogInformation("IndexersUpdated broadcast complete");

                    // Publish a toast + dropdown notification so the activity bell receives the update
                    try
                    {
                        var names = createdIndexers.Select(i => i.Name).ToArray();
                        var message = names.Length > 0 ? $"Imported {created} indexer(s): {string.Join(", ", names)}" : $"Imported {created} indexer(s) successfully";
                        await _toastService.PublishToastAsync("success", "Indexers", message, timeoutMs: 8000);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to publish indexer import notification");
                    }
                }
                catch (System.Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to broadcast IndexersUpdated via SignalR");
                }
            }

            // Log a summary for diagnostics
            _logger?.LogInformation("Prowlarr: Indexers processed - created={Created}, skipped={Skipped}", created, skipped);

            // Include created indexers in the response (id will be populated after SaveChanges)
            var createdDtos = createdIndexers.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                implementation = i.Implementation,
                baseUrl = i.Url,
                apiKey = i.ApiKey,
                categories = string.IsNullOrEmpty(i.Categories) ? System.Array.Empty<string>() : i.Categories.Split(',').Select(s => s.Trim()).ToArray()
            }).ToArray();

            return Ok(new { accepted = true, created, skipped, indexers = createdDtos });
        }

        /// <summary>
        /// DEBUG: POST /api/v1/debug/indexers/publish
        /// Manually trigger an IndexersUpdated SignalR broadcast for testing client connectivity.
        /// </summary>
        [HttpPost("debug/indexers/publish")]
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> DebugPublishIndexers([FromBody] System.Text.Json.JsonElement? payload)
        {
            // Build a small payload from optional incoming body or a default sample
            var created = 0;
            var indexers = new List<object>();

            if (payload.HasValue && payload.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                try
                {
                    if (payload.Value.TryGetProperty("indexers", out var idxs) && idxs.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var i in idxs.EnumerateArray())
                        {
                            var id = i.TryGetProperty("id", out var pid) && pid.ValueKind == System.Text.Json.JsonValueKind.Number ? pid.GetInt32() : 0;
                            var name = i.TryGetProperty("name", out var pname) && pname.ValueKind == System.Text.Json.JsonValueKind.String ? pname.GetString() ?? string.Empty : string.Empty;
                            var baseUrl = i.TryGetProperty("baseUrl", out var pbase) && pbase.ValueKind == System.Text.Json.JsonValueKind.String ? pbase.GetString() ?? string.Empty : string.Empty;

                            indexers.Add(new { id, name, baseUrl });
                        }

                        created = indexers.Count;
                    }
                }
                catch { }
            }

            if (!indexers.Any())
            {
                created = 1;
                indexers.Add(new { id = 999999, name = "Debug Indexer", baseUrl = "http://debug" });
            }

            _logger?.LogInformation("DEBUG: Broadcasting IndexersUpdated (manual test): created={Created}", created);

            await _settingsHub.Clients.All.SendAsync("IndexersUpdated", new { created, skipped = 0, indexers });

            _logger?.LogInformation("DEBUG: IndexersUpdated broadcast sent");

            // Also publish a toast/notification to show up in the activity dropdown
            try
            {
                var names = indexers.Select(i => i.GetType().GetProperty("name")?.GetValue(i)?.ToString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                var message = names.Length > 0 ? $"Imported {created} indexer(s): {string.Join(", ", names)}" : $"Imported {created} indexer(s) successfully";
                await _toastService.PublishToastAsync("success", "Indexers", message, timeoutMs: 8000);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to publish debug indexer notification");
            }

            return Ok(new { sent = true, created, indexers });
        }

        /// <summary>
        /// DEBUG: GET /api/v1/debug/settings/clients
        /// Returns the list and count of currently connected SettingsHub clients.
        /// </summary>
        [HttpGet("debug/settings/clients")]
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult GetSettingsHubClients()
        {
            try
            {
                var clients = Listenarr.Api.Hubs.SettingsHub.ConnectedClientIds.ToArray();
                return Ok(new { connected = clients.Length, clients });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to retrieve SettingsHub clients");
                return StatusCode(500, new { error = "Failed to retrieve clients" });
            }
        }

        /// <summary>
        /// POST /api/v1/indexer
        /// Accepts a single indexer object (or an array) for compatibility with some clients that POST to the singular route.
        /// Delegates to PostIndexers for the actual processing so persistence and SignalR broadcast happen in one place.
        /// </summary>
        [HttpPost("indexer")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> PostIndexer([FromBody] System.Text.Json.JsonElement payload)
        {
            _logger?.LogInformation("Prowlarr indexer payload (single) received: {Kind}", payload.ValueKind.ToString());
            try
            {
                var raw = payload.GetRawText();
                var redacted = LogRedaction.RedactText(raw, LogRedaction.GetSensitiveValuesFromEnvironment());
                _logger?.LogInformation("Prowlarr indexer payload body: {Payload}", redacted);
            }
            catch { }

            if (HttpContext?.Response != null) HttpContext.Response.ContentType = "application/json";

            if (payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // Wrap single object into an array and delegate to existing handler
                var arrJson = "[" + payload.GetRawText() + "]";
                using var doc = System.Text.Json.JsonDocument.Parse(arrJson);
                return await PostIndexers(doc.RootElement);
            }

            if (payload.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return await PostIndexers(payload);
            }

            return BadRequest(new { message = "Expected JSON object or array for indexer(s)" });
        }

        /// <summary>
        /// GET /api/v1/indexer/schema
        /// Returns a minimal list of indexer fields / schema entries.
        /// </summary>
        [HttpGet("indexer/schema")]
        [AllowAnonymous]
        [Produces("application/json")]
        public IActionResult GetIndexerSchema()
        {
            Response.ContentType = "application/json";

            var schema = new IndexerSchemaDto
            {
                Fields = new[]
                {
                    new IndexerFieldDto { Name = "name", Type = "string", Required = true, Description = "Indexer name" },
                    new IndexerFieldDto { Name = "baseUrl", Type = "string", Required = true, Description = "Base URL of indexer" },
                    new IndexerFieldDto { Name = "apiPath", Type = "string", Required = true, Description = "API path (e.g. /api or /torznab)" },
                    new IndexerFieldDto { Name = "apiKey", Type = "string", Required = false, Description = "API key or token" },
                    new IndexerFieldDto { Name = "categories", Type = "array", Required = false, Description = "Optional categories filter (array of integers or strings)" }
                },
                Implementations = new[] { "Newznab", "Torznab" }
            };

            return Ok(schema);
        }

        private static string NormalizeIndexerUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath ?? string.Empty;
                // Trim trailing slash
                path = path.TrimEnd('/');

                // Remove trailing /api if present
                if (path.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - 4);
                }

                var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
                var normalized = $"{uri.Scheme}://{uri.Host}{port}{path}";
                return normalized.TrimEnd('/');
            }
            catch
            {
                return url?.TrimEnd('/') ?? string.Empty;
            }
        }

        // DTOs
        public record SystemStatusDto
        {
            public string Status { get; init; } = string.Empty;
            public string Version { get; init; } = string.Empty;
            public string Api { get; init; } = string.Empty;
        }

        public record IndexerTestResponseDto
        {
            public bool Success { get; init; }
            public string Message { get; init; } = string.Empty;
            public string Version { get; init; } = string.Empty;
        }

        public record IndexerSchemaDto
        {
            public IndexerFieldDto[] Fields { get; init; } = System.Array.Empty<IndexerFieldDto>();
            /// <summary>
            /// The implementations supported by this indexer schema (Prowlarr expects at least one of 'Newznab' or 'Torznab').
            /// </summary>
            public string[] Implementations { get; init; } = new[] { "Newznab", "Torznab" };
        }

        public record IndexerFieldDto
        {
            public string Name { get; init; } = string.Empty;
            public string Type { get; init; } = string.Empty;
            public bool Required { get; init; }
            public string Description { get; init; } = string.Empty;
        }
    }
}

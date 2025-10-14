using Listenarr.Api.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Listenarr.Api.Middleware
{
    /// <summary>
    /// Middleware to accept an API key via header X-Api-Key and treat the request as authenticated when it matches the configured startup ApiKey.
    /// This is intentionally simple: a valid key results in a lightweight ClaimsPrincipal so downstream authorization and the AuthenticationEnforcerMiddleware
    /// will treat the request as authenticated. The key itself is stored in the startup config (config.json).
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IStartupConfigService _startupConfigService;

        public ApiKeyMiddleware(RequestDelegate next, IStartupConfigService startupConfigService)
        {
            _next = next;
            _startupConfigService = startupConfigService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var cfg = _startupConfigService.GetConfig();
                var configuredKey = cfg?.ApiKey;
                if (!string.IsNullOrWhiteSpace(configuredKey))
                {
                    // Accept header X-Api-Key or Authorization: ApiKey <key>
                    string? provided = null;
                    if (context.Request.Headers.TryGetValue("X-Api-Key", out var h))
                        provided = h.ToString();

                    if (string.IsNullOrWhiteSpace(provided) && context.Request.Headers.TryGetValue("Authorization", out var auth))
                    {
                        var s = auth.ToString();
                        if (s.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
                            provided = s.Substring("ApiKey ".Length).Trim();
                    }

                    // If headers didn't supply the key, accept query string tokens for browser-driven requests
                    // (e.g. SignalR access_token or image URLs containing ?access_token=... or ?apikey=...)
                    if (string.IsNullOrWhiteSpace(provided))
                    {
                        try
                        {
                            var qs = context.Request.Query;
                            if (qs.ContainsKey("access_token")) provided = qs["access_token"].FirstOrDefault();
                            if (string.IsNullOrWhiteSpace(provided) && qs.ContainsKey("apikey")) provided = qs["apikey"].FirstOrDefault();
                        }
                        catch
                        {
                            // ignore any query parsing errors
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(provided) && provided == configuredKey)
                    {
                        // Create a minimal authenticated principal for downstream checks
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, "ApiKey"),
                            new Claim("AuthMethod", "ApiKey")
                        };

                        var identity = new ClaimsIdentity(claims, "ApiKey");
                        context.User = new ClaimsPrincipal(identity);
                    }
                }
            }
            catch
            {
                // Do not fail the request if config cannot be read - just continue without API key auth
            }

            await _next(context);
        }
    }
}

using Listenarr.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Listenarr.Api.Middleware
{
    public class AuthenticationEnforcerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IStartupConfigService _startupConfigService;

        public AuthenticationEnforcerMiddleware(RequestDelegate next, IStartupConfigService startupConfigService)
        {
            _next = next;
            _startupConfigService = startupConfigService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var cfg = _startupConfigService.GetConfig();
            var authRequired = false;
            if (cfg != null && cfg.AuthenticationRequired != null)
            {
                if (bool.TryParse(cfg.AuthenticationRequired, out var b)) authRequired = b;
                else authRequired = cfg.AuthenticationRequired?.ToLower() == "enabled";
            }

            // If endpoint explicitly allows anonymous, skip enforcement
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null)
            {
                await _next(context);
                return;
            }

            // Allow some public paths used by SPA and startup (swagger/ui, antiforgery token, startup config and account login/register)
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger") || path.StartsWith("/api/antiforgery") || path.StartsWith("/api/startupconfig") || path.StartsWith("/api/configuration/startupconfig") || path.StartsWith("/api/account/login") || path.StartsWith("/api/account/register"))
            {
                await _next(context);
                return;
            }

            if (authRequired && !(context.User?.Identity?.IsAuthenticated ?? false))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Authentication required" });
                return;
            }

            await _next(context);
        }
    }
}

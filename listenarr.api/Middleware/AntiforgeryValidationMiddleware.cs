using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace Listenarr.Api.Middleware
{
    public class AntiforgeryValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAntiforgery _antiforgery;

        public AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
        {
            _next = next;
            _antiforgery = antiforgery;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only validate for unsafe HTTP methods
            if (HttpMethods.IsPost(context.Request.Method)
                || HttpMethods.IsPut(context.Request.Method)
                || HttpMethods.IsDelete(context.Request.Method)
                || HttpMethods.IsPatch(context.Request.Method))
            {
                var endpoint = context.GetEndpoint();
                // Skip if endpoint allows anonymous access
                if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null)
                {
                    await _next(context);
                    return;
                }

                // Allow some public endpoints without antiforgery (startup config reads, token request itself, login/register)
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/api/antiforgery") || path.StartsWith("/api/account/login") || path.StartsWith("/api/account/register") || path.StartsWith("/api/startupconfig"))
                {
                    await _next(context);
                    return;
                }

                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing CSRF token" });
                    return;
                }
            }

            await _next(context);
        }
    }
}

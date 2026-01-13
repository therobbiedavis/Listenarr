using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Listenarr.Api.Services;

namespace Listenarr.Api.Middleware
{
    /// <summary>
    /// Middleware to log incoming request bodies for debugging purposes.
    /// Only logs for HTTP methods that typically carry request bodies (POST/PUT/PATCH).
    /// Body is redacted using LogRedaction and truncated to a safe maximum length.
    /// </summary>
    public class RequestBodyLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestBodyLoggingMiddleware> _logger;
        private const int MaxLogBodySize = 64 * 1024; // 64KB

        public RequestBodyLoggingMiddleware(RequestDelegate next, ILogger<RequestBodyLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var method = context.Request.Method?.ToUpperInvariant() ?? string.Empty;
            if (method == HttpMethods.Post || method == HttpMethods.Put || method == "PATCH")
            {
                try
                {
                    context.Request.EnableBuffering();
                    context.Request.Body.Seek(0, SeekOrigin.Begin);
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(body))
                    {
                        var truncated = body.Length > MaxLogBodySize ? body.Substring(0, MaxLogBodySize) + "..." : body;
                        var redacted = LogRedaction.RedactText(truncated, LogRedaction.GetSensitiveValuesFromEnvironment());
                        _logger.LogInformation("Incoming {Method} {Path} body: {Body}", method, context.Request.Path, redacted);
                    }

                    context.Request.Body.Seek(0, SeekOrigin.Begin);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log request body for {Method} {Path}", method, context.Request.Path);
                }
            }

            await _next(context);
        }
    }
}
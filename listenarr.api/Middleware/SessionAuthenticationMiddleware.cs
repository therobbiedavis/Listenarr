/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Services;
using System.Security.Claims;

namespace Listenarr.Api.Middleware
{
    public class SessionAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionAuthenticationMiddleware> _logger;

        public SessionAuthenticationMiddleware(RequestDelegate next, ILogger<SessionAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            // Get the session service - will always be available but may not be functional if auth is disabled
            var sessionService = serviceProvider.GetRequiredService<ISessionService>();
            
            // Only process session authentication if no user is already authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                var sessionToken = ExtractSessionToken(context);
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    try
                    {
                        var principal = await sessionService.GetSessionUserAsync(sessionToken);
                        if (principal != null)
                        {
                            context.User = principal;
                            _logger.LogDebug("Session authentication successful for token: {TokenPrefix}...", sessionToken[..8]);
                        }
                        else
                        {
                            _logger.LogDebug("Session token invalid or expired: {TokenPrefix}...", sessionToken[..8]);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during session authentication");
                    }
                }
            }

            await _next(context);
        }

        private static string? ExtractSessionToken(HttpContext context)
        {
            // Try Authorization header first (Bearer token)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader[7..]; // Remove "Bearer " prefix
            }

            // Try X-Session-Token header
            var sessionHeader = context.Request.Headers["X-Session-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionHeader))
            {
                return sessionHeader;
            }

            return null;
        }
    }
}
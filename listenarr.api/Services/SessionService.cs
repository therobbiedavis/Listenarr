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

using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Listenarr.Api.Services
{
    public interface ISessionService
    {
        Task<string> CreateSessionAsync(string username, bool isAdmin, bool rememberMe = false);
        Task<ClaimsPrincipal?> GetSessionUserAsync(string sessionToken);
        Task<bool> InvalidateSessionAsync(string sessionToken);
        Task InvalidateAllSessionsForUserAsync(string username);
        Task<int> GetActiveSessionCountAsync(string username);
    }

    public class SessionService : ISessionService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SessionService> _logger;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(8);
        private readonly TimeSpan _rememberMeExpiration = TimeSpan.FromDays(30);

        public SessionService(IMemoryCache cache, ILogger<SessionService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<string> CreateSessionAsync(string username, bool isAdmin, bool rememberMe = false)
        {
            var sessionToken = GenerateSecureToken();
            var expiration = rememberMe ? _rememberMeExpiration : _defaultExpiration;
            
            var sessionData = new SessionData
            {
                Username = username,
                IsAdmin = isAdmin,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(expiration),
                LastAccessed = DateTimeOffset.UtcNow
            };

            var cacheKey = GetSessionCacheKey(sessionToken);
            var userSessionsKey = GetUserSessionsCacheKey(username);

            // Store session data
            _cache.Set(cacheKey, sessionData, expiration);

            // Track user sessions for bulk invalidation
            if (!_cache.TryGetValue(userSessionsKey, out HashSet<string>? userSessions) || userSessions == null)
            {
                userSessions = new HashSet<string>();
            }
            userSessions.Add(sessionToken);
            _cache.Set(userSessionsKey, userSessions, TimeSpan.FromDays(31)); // Slightly longer than max session

            _logger.LogInformation("Created session for user {Username} (RememberMe: {RememberMe})", username, rememberMe);
            
            return Task.FromResult(sessionToken);
        }

        public Task<ClaimsPrincipal?> GetSessionUserAsync(string sessionToken)
        {
            var cacheKey = GetSessionCacheKey(sessionToken);
            
            if (!_cache.TryGetValue(cacheKey, out SessionData? sessionData) || sessionData == null)
            {
                return Task.FromResult<ClaimsPrincipal?>(null);
            }

            // Check if session is expired
            if (sessionData.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _cache.Remove(cacheKey);
                _logger.LogInformation("Session expired for user {Username}", sessionData.Username);
                return Task.FromResult<ClaimsPrincipal?>(null);
            }

            // Update last accessed time for sliding expiration
            sessionData.LastAccessed = DateTimeOffset.UtcNow;
            var remainingTime = sessionData.ExpiresAt - DateTimeOffset.UtcNow;
            _cache.Set(cacheKey, sessionData, remainingTime);

            // Create claims principal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, sessionData.Username),
                new Claim("session_token", sessionToken)
            };

            if (sessionData.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
            }

            var identity = new ClaimsIdentity(claims, "Session");
            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult<ClaimsPrincipal?>(principal);
        }

        public Task<bool> InvalidateSessionAsync(string sessionToken)
        {
            var cacheKey = GetSessionCacheKey(sessionToken);
            
            if (_cache.TryGetValue(cacheKey, out SessionData? sessionData) && sessionData != null)
            {
                _cache.Remove(cacheKey);
                
                // Remove from user sessions list
                var userSessionsKey = GetUserSessionsCacheKey(sessionData.Username);
                if (_cache.TryGetValue(userSessionsKey, out HashSet<string>? userSessions) && userSessions != null)
                {
                    userSessions.Remove(sessionToken);
                    if (userSessions.Count == 0)
                    {
                        _cache.Remove(userSessionsKey);
                    }
                    else
                    {
                        _cache.Set(userSessionsKey, userSessions, TimeSpan.FromDays(31));
                    }
                }

                _logger.LogInformation("Invalidated session for user {Username}", sessionData.Username);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task InvalidateAllSessionsForUserAsync(string username)
        {
            var userSessionsKey = GetUserSessionsCacheKey(username);
            
            if (_cache.TryGetValue(userSessionsKey, out HashSet<string>? userSessions) && userSessions != null)
            {
                foreach (var sessionToken in userSessions)
                {
                    var cacheKey = GetSessionCacheKey(sessionToken);
                    _cache.Remove(cacheKey);
                }
                
                _cache.Remove(userSessionsKey);
                _logger.LogInformation("Invalidated all sessions for user {Username} (count: {Count})", username, userSessions.Count);
            }

            return Task.CompletedTask;
        }

        public Task<int> GetActiveSessionCountAsync(string username)
        {
            var userSessionsKey = GetUserSessionsCacheKey(username);
            
            if (_cache.TryGetValue(userSessionsKey, out HashSet<string>? userSessions) && userSessions != null)
            {
                // Clean up expired sessions
                var validSessions = new HashSet<string>();
                foreach (var sessionToken in userSessions)
                {
                    var cacheKey = GetSessionCacheKey(sessionToken);
                    if (_cache.TryGetValue(cacheKey, out SessionData? sessionData) && 
                        sessionData != null && sessionData.ExpiresAt >= DateTimeOffset.UtcNow)
                    {
                        validSessions.Add(sessionToken);
                    }
                }

                if (validSessions.Count != userSessions.Count)
                {
                    if (validSessions.Count == 0)
                    {
                        _cache.Remove(userSessionsKey);
                    }
                    else
                    {
                        _cache.Set(userSessionsKey, validSessions, TimeSpan.FromDays(31));
                    }
                }

                return Task.FromResult(validSessions.Count);
            }

            return Task.FromResult(0);
        }

        private static string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32]; // 256-bit token
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string GetSessionCacheKey(string sessionToken) => $"session:{sessionToken}";
        private static string GetUserSessionsCacheKey(string username) => $"user_sessions:{username}";
    }

    public class SessionData
    {
        public required string Username { get; init; }
        public required bool IsAdmin { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset LastAccessed { get; set; }
    }
}
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

namespace Listenarr.Api.Services
{
    /// <summary>
    /// A wrapper around SessionService that only provides session functionality
    /// when authentication is required in the configuration.
    /// </summary>
    public class ConditionalSessionService : ISessionService
    {
        private readonly IStartupConfigService _startupConfigService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SessionService> _logger;
        private SessionService? _actualService;

        public ConditionalSessionService(IStartupConfigService startupConfigService, IMemoryCache cache, ILogger<SessionService> logger)
        {
            _startupConfigService = startupConfigService;
            _cache = cache;
            _logger = logger;
        }

        private SessionService? GetActualService()
        {
            if (_actualService != null) return _actualService;

            var config = _startupConfigService.GetConfig();
            if (config?.AuthenticationRequired?.ToLowerInvariant() is "true" or "yes" or "1")
            {
                _actualService = new SessionService(_cache, _logger);
            }

            return _actualService;
        }

        public Task<string> CreateSessionAsync(string username, bool isAdmin, bool rememberMe = false)
        {
            var service = GetActualService();
            if (service == null)
            {
                throw new InvalidOperationException("Authentication is not enabled. Set AuthenticationRequired to 'true' in configuration.");
            }
            return service.CreateSessionAsync(username, isAdmin, rememberMe);
        }

        public Task<ClaimsPrincipal?> GetSessionUserAsync(string sessionToken)
        {
            var service = GetActualService();
            return service?.GetSessionUserAsync(sessionToken) ?? Task.FromResult<ClaimsPrincipal?>(null);
        }

        public Task<bool> InvalidateSessionAsync(string sessionToken)
        {
            var service = GetActualService();
            return service?.InvalidateSessionAsync(sessionToken) ?? Task.FromResult(false);
        }

        public Task InvalidateAllSessionsForUserAsync(string username)
        {
            var service = GetActualService();
            return service?.InvalidateAllSessionsForUserAsync(username) ?? Task.CompletedTask;
        }

        public Task<int> GetActiveSessionCountAsync(string username)
        {
            var service = GetActualService();
            return service?.GetActiveSessionCountAsync(username) ?? Task.FromResult(0);
        }
    }
}
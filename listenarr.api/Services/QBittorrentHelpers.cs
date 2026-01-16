using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Shared helper methods for qBittorrent client operations.
    /// </summary>
    public static class QBittorrentHelpers
    {
        private const string CategorySettingKey = "category";

        /// <summary>
        /// Extracts category from client settings and builds URL query parameter.
        /// </summary>
        /// <param name="settings">Client settings dictionary</param>
        /// <param name="queryPrefix">Query string prefix: ampersand if appending to existing params, question mark if first param</param>
        /// <returns>Tuple containing the query parameter string and the extracted category value</returns>
        public static (string categoryParam, string? category) BuildCategoryParameter(
            Dictionary<string, object> settings, 
            string queryPrefix)
        {
            string? category = null;
            if (settings.TryGetValue(CategorySettingKey, out var categoryObj))
            {
                category = categoryObj?.ToString();
            }

            var categoryParam = !string.IsNullOrEmpty(category)
                ? $"{queryPrefix}category={Uri.EscapeDataString(category)}"
                : string.Empty;

            return (categoryParam, category);
        }

        /// <summary>
        /// Logs appropriate message based on whether category filtering is configured.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="category">Category value (null if not configured)</param>
        public static void LogCategoryFiltering(ILogger logger, string? category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                logger.LogInformation("Fetching qBittorrent queue filtered by category: {Category}", category);
            }
            else
            {
                logger.LogWarning("No category configured in download client settings - fetching ALL torrents from qBittorrent");
            }
        }
    }
}

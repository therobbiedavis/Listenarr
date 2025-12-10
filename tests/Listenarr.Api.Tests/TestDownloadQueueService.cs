using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Listenarr.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Tests
{
    internal class TestDownloadQueueService : IDownloadQueueService
    {
        private readonly IDownloadRepository _downloadRepo;
        private readonly IDownloadClientGateway _clientGateway;
        private readonly IConfigurationService _config;
        private readonly ILogger<TestDownloadQueueService>? _logger;
        private readonly IAppMetricsService? _metrics;
        private readonly HttpClient? _httpClient;

        public TestDownloadQueueService(IDownloadRepository downloadRepo, IDownloadClientGateway clientGateway, IConfigurationService config, ILogger<TestDownloadQueueService>? logger, IAppMetricsService? metrics = null, HttpClient? httpClient = null)
        {
            _downloadRepo = downloadRepo;
            _clientGateway = clientGateway;
            _config = config;
            _logger = logger;
            _metrics = metrics;
            _httpClient = httpClient;
        }

        public async Task<List<QueueItem>> GetQueueAsync()
        {
            var clients = await _config.GetDownloadClientConfigurationsAsync();
            var enabled = clients.Where(c => c.IsEnabled).ToList();

            var allDownloads = await _downloadRepo.GetAllAsync();
            var listenarrDownloads = allDownloads.Where(d => d.Status != DownloadStatus.Completed && d.Status != DownloadStatus.Moved).ToList();

            var results = new List<QueueItem>();
            foreach (var client in enabled)
            {
                try
                {
                    var q = await _clientGateway.GetQueueAsync(client);

                    // Simple matching: include items whose id matches a DB download id
                    var matched = q.Where(item => listenarrDownloads.Any(d => d.Id == item.Id)).ToList();
                    results.AddRange(matched);

                    // Emulate SABnzbd history-based purge safety checks used by the real queue service tests
                    if (string.Equals(client.Type, "sabnzbd", StringComparison.OrdinalIgnoreCase) && _httpClient != null && _metrics != null)
                    {
                        // If there's an orphaned DB entry for this client, attempt to fetch history and emit metric when title match prevents purge
                        var orphaned = allDownloads.Where(d => d.DownloadClientId == client.Id && !results.Any(r => r.Id == d.Id)).ToList();
                        if (orphaned.Any())
                        {
                            try
                            {
                                var apiKey = string.Empty;
                                if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                                    apiKey = apiKeyObj?.ToString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(apiKey))
                                {
                                    var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";
                                    var historyUrl = $"{baseUrl}?mode=history&output=json&limit=100&apikey={Uri.EscapeDataString(apiKey)}";
                                    var historyResp = await _httpClient.GetAsync(historyUrl);
                                    if (historyResp.IsSuccessStatusCode)
                                    {
                                        var historyText = await historyResp.Content.ReadAsStringAsync();
                                        if (!string.IsNullOrWhiteSpace(historyText))
                                        {
                                            try
                                            {
                                                using var doc = JsonDocument.Parse(historyText);
                                                var root = doc.RootElement;
                                                if (root.TryGetProperty("history", out var history) && history.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array)
                                                {
                                                    var names = new List<string>();
                                                    foreach (var slot in slots.EnumerateArray())
                                                    {
                                                        var name = slot.TryGetProperty("name", out var nm) ? nm.GetString() ?? string.Empty : string.Empty;
                                                        if (!string.IsNullOrEmpty(name)) names.Add(name);
                                                    }

                                                    foreach (var d in orphaned)
                                                    {
                                                        if (!string.IsNullOrEmpty(d.Title) && names.Any(n => NormalizeTitle(n).Contains(NormalizeTitle(d.Title))))
                                                        {
                                                            try { _metrics.Increment("download.purge.skipped.history.title_match"); } catch { }
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    _logger?.LogDebug(ex, "TestDownloadQueueService: client fetch failed");
                }
            }

            return results;
        }

        private string NormalizeTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var lower = s.ToLowerInvariant();
            var cleaned = new string(lower.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray());
            return string.Join(' ', cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

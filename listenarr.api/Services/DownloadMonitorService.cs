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

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using Listenarr.Api.Hubs;
using Listenarr.Api.Models;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Background service that monitors download clients and pushes updates via SignalR
    /// </summary>
    public class DownloadMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubContext<DownloadHub> _hubContext;
        private readonly ILogger<DownloadMonitorService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAppMetricsService _metrics;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, Download> _lastDownloadStates = new();
    // Tracks downloads that appear complete and the time they were first observed complete
    private readonly Dictionary<string, DateTime> _completionCandidates = new();
    private readonly TimeSpan _completionStableWindow = TimeSpan.FromSeconds(10);
    // Track missing-source retry attempts and scheduled retries to avoid duplicate scheduling
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _missingSourceRetryAttempts = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _missingSourceRetryScheduled = new();

        public DownloadMonitorService(
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<DownloadHub> hubContext,
            ILogger<DownloadMonitorService> logger,
            IHttpClientFactory httpClientFactory,
            IAppMetricsService? appMetrics = null)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _hubContext = hubContext;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _metrics = appMetrics ?? new NoopAppMetricsService();
        }

        /// <summary>
        /// Normalizes a title for better matching by removing format indicators and extra spaces
        /// </summary>
        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // Remove ALL bracketed content [anything] - more robust than specific patterns
            var result = System.Text.RegularExpressions.Regex.Replace(title, @"\[.*?\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove ALL parentheses content (anything) - handles unknown quality/group indicators
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\(.*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove curly braces content {anything}
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\{.*?\}", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove common separators and replace with spaces
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[\-_\.]+", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove common quality/format indicators that might not be in brackets
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\b(mp3|m4a|m4b|flac|aac|ogg|opus|320|256|128|v0|v2|audiobook|unabridged|abridged)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Normalize multiple spaces to single spaces
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
            
            // Remove trailing/leading spaces, dashes, etc.
            result = result.Trim(' ', '-', '.', ',');
            
            return result;
        }

        /// <summary>
        /// Checks if two titles are similar enough to be considered a match
        /// </summary>
        private static bool AreTitlesSimilar(string title1, string title2)
        {
            var norm1 = NormalizeTitle(title1);
            var norm2 = NormalizeTitle(title2);
            
            // Exact match after normalization
            if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Bidirectional contains
            if (norm1.Contains(norm2, StringComparison.OrdinalIgnoreCase) || 
                norm2.Contains(norm1, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // First 50 chars (for very long titles)
            if (norm1.Length > 20 && norm2.Length > 20)
            {
                var len = Math.Min(50, Math.Min(norm1.Length, norm2.Length));
                if (norm1.Substring(0, len).Equals(norm2.Substring(0, len), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Create a filesystem-safe name from arbitrary text by removing invalid path characters
        /// and normalizing whitespace. Keeps it conservative to avoid unexpected folder creation.
        /// </summary>
        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";
            // Remove invalid path chars
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            // Replace sequences of non-alphanumeric characters with single space
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "[^A-Za-z0-9 _-]+", " ");
            // Collapse whitespace and trim
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\\s+", " ").Trim();
            // If nothing left, fallback
            if (string.IsNullOrWhiteSpace(cleaned)) return "unknown";
            return cleaned;
        }

        /// <summary>
        /// Attempt to move a directory with retries and exponential backoff. Emits diagnostics (file listing and ACLs)
        /// on failures to aid debugging file-lock/permission issues.
        /// </summary>
        private async Task<bool> TryMoveDirectoryWithRetryAsync(string sourceDir, string destDir, int maxAttempts = 4, int initialDelayMs = 1000)
        {
            var attempt = 0;
            var delay = initialDelayMs;

            for (; attempt < maxAttempts; attempt++)
            {
                try
                {
                    Directory.Move(sourceDir, destDir);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Directory.Move attempt {Attempt}/{Max} failed: {Source} -> {Dest}", attempt + 1, maxAttempts, sourceDir, destDir);

                    // Dump a small directory listing sample for diagnostics
                    try
                    {
                        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                        _logger.LogWarning("Directory listing for {Source} (count={Count}), sample: {Sample}", sourceDir, files.Length, string.Join(", ", files.Take(5).Select(f => Path.GetFileName(f))));
                    }
                    catch (Exception listEx)
                    {
                        _logger.LogDebug(listEx, "Failed to enumerate files in {Source} while diagnosing move failure", sourceDir);
                    }

                    // Dump ACL/owner information if available (Windows-friendly). Failures are non-blocking.
                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            var dirSec = new DirectoryInfo(sourceDir).GetAccessControl();
                            var owner = dirSec.GetOwner(typeof(NTAccount))?.ToString() ?? "unknown";
                            _logger.LogWarning("Directory owner for {Source}: {Owner}", sourceDir, owner);

                            var rules = dirSec.GetAccessRules(true, true, typeof(NTAccount));
                            foreach (FileSystemAccessRule rule in rules.Cast<FileSystemAccessRule>().Take(10))
                            {
                                _logger.LogWarning("ACL {Source}: {Identity} {Type} {Rights}", sourceDir, rule.IdentityReference.Value, rule.AccessControlType, rule.FileSystemRights);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Skipping ACL diagnostics for {Source} (non-Windows OS)", sourceDir);
                        }
                    }
                    catch (Exception aclEx)
                    {
                        _logger.LogDebug(aclEx, "Failed to read ACLs for {Source}", sourceDir);
                    }

                    if (attempt < maxAttempts - 1)
                    {
                        _logger.LogInformation("Retrying Directory.Move in {Delay}ms...", delay);
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                }
            }

            return false;
        }

        private async Task<bool> TryMoveFileWithRetryAsync(string sourceFile, string destFile, int maxAttempts = 4, int initialDelayMs = 1000)
        {
            var attempt = 0;
            var delay = initialDelayMs;

            for (; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Use File.Move with overwrite when available
                    File.Move(sourceFile, destFile, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "File.Move attempt {Attempt}/{Max} failed: {Source} -> {Dest}", attempt + 1, maxAttempts, sourceFile, destFile);

                    // Try opening the source file to detect locks
                    try
                    {
                        using var stream = File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        _logger.LogDebug("Able to open source file for read during diagnostic: {File}", sourceFile);
                    }
                    catch (Exception openEx)
                    {
                        _logger.LogWarning(openEx, "Failed to open source file for read (may be locked): {File}", sourceFile);
                    }

                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            var fileSec = new FileInfo(sourceFile).GetAccessControl();
                            var owner = fileSec.GetOwner(typeof(NTAccount))?.ToString() ?? "unknown";
                            _logger.LogWarning("File owner for {File}: {Owner}", sourceFile, owner);
                            var rules = fileSec.GetAccessRules(true, true, typeof(NTAccount));
                            foreach (FileSystemAccessRule rule in rules.Cast<FileSystemAccessRule>().Take(10))
                            {
                                _logger.LogWarning("ACL {File}: {Identity} {Type} {Rights}", sourceFile, rule.IdentityReference.Value, rule.AccessControlType, rule.FileSystemRights);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Skipping file ACL diagnostics for {File} (non-Windows OS)", sourceFile);
                        }
                    }
                    catch (Exception aclEx)
                    {
                        _logger.LogDebug(aclEx, "Failed to read file ACLs for {File}", sourceFile);
                    }

                    if (attempt < maxAttempts - 1)
                    {
                        _logger.LogInformation("Retrying File.Move in {Delay}ms...", delay);
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                }
            }

            return false;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Download Monitor Service starting");

            // Wait a bit before starting to ensure the app is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorDownloadsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Download Monitor Service");
                }

                // Wait before next poll
                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Download Monitor Service stopping");
        }

        private async Task MonitorDownloadsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

            // Get all active downloads from database
            var activeDownloads = await dbContext.Downloads
                .Where(d => d.Status == DownloadStatus.Queued ||
                           d.Status == DownloadStatus.Downloading ||
                           d.Status == DownloadStatus.Processing)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("DownloadMonitorService found {Count} active downloads", activeDownloads.Count);
            foreach (var dl in activeDownloads)
            {
                _logger.LogDebug("Active download: {Id} - {Title} - Status: {Status} - Client: {ClientId}", 
                    dl.Id, dl.Title, dl.Status, dl.DownloadClientId);
            }

            // Only poll download clients if there are active downloads
            if (activeDownloads.Any())
            {
                var clientDownloads = activeDownloads.Where(d => d.DownloadClientId != "DDL").ToList();

                if (clientDownloads.Any())
                {
                    await PollDownloadClientsAsync(clientDownloads, configService, dbContext, cancellationToken);
                }
            }

            // Get all downloads to send to clients (only if there are active downloads or every 30 seconds)
            List<Download> allDownloads = new();
            var shouldFetchAll = activeDownloads.Any() || (DateTime.UtcNow.Second % 30 == 0);

            if (shouldFetchAll)
            {
                allDownloads = await dbContext.Downloads
                    .OrderByDescending(d => d.StartedAt)
                    .Take(100) // Limit to recent 100 downloads
                    .ToListAsync(cancellationToken);
            }

            // Check for changes and broadcast updates (only if we have data)
            if (allDownloads.Any())
            {
                await BroadcastDownloadUpdatesAsync(allDownloads, cancellationToken);
            }
        }

        /// <summary>
        /// Broadcast a candidate update for a download so clients can show completion candidates
        /// without requiring the DB status to change.
        /// </summary>
        private async Task BroadcastCandidateUpdateAsync(Download dl, bool isCandidate, CancellationToken cancellationToken)
        {
            try
            {
                var metadata = (dl.Metadata ?? new Dictionary<string, object>()).Where(kvp => !string.Equals(kvp.Key, "ClientContentPath", StringComparison.OrdinalIgnoreCase)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                metadata["CompletionCandidate"] = isCandidate;

                var payload = new
                {
                    id = dl.Id,
                    audiobookId = dl.AudiobookId,
                    title = dl.Title,
                    artist = dl.Artist,
                    album = dl.Album,
                    originalUrl = dl.OriginalUrl,
                    // Surface as Completed so UI's Completed lists can include candidates
                    status = isCandidate ? DownloadStatus.Completed.ToString() : dl.Status.ToString(),
                    progress = dl.Progress,
                    totalSize = dl.TotalSize,
                    downloadedSize = dl.DownloadedSize,
                    finalPath = dl.FinalPath,
                    startedAt = dl.StartedAt,
                    completedAt = dl.CompletedAt,
                    errorMessage = dl.ErrorMessage,
                    downloadClientId = dl.DownloadClientId,
                    metadata = metadata
                };

                await _hubContext.Clients.All.SendAsync("DownloadUpdate", new[] { payload }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast candidate update for {DownloadId}", dl.Id);
            }
        }

        private async Task PollDownloadClientsAsync(
            List<Download> downloads, 
            IConfigurationService configService,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // Group downloads by client
            var downloadsByClient = downloads.GroupBy(d => d.DownloadClientId);

            foreach (var clientGroup in downloadsByClient)
            {
                var clientId = clientGroup.Key;
                if (string.IsNullOrEmpty(clientId)) continue;

                try
                {
                    var client = await configService.GetDownloadClientConfigurationAsync(clientId);
                    if (client == null || !client.IsEnabled) continue;

                    // Poll based on client type
                    switch (client.Type.ToLower())
                    {
                        case "qbittorrent":
                            await PollQBittorrentAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "transmission":
                            await PollTransmissionAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "sabnzbd":
                            await PollSABnzbdAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "nzbget":
                            await PollNZBGetAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling download client {ClientId}", clientId);
                }
            }
        }

        private Task PollQBittorrentAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogDebug("Polling qBittorrent client {ClientName}", client.Name);
                try
                {
                    var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}";

                    using var http = _httpClientFactory.CreateClient("DownloadClient");
                    _logger.LogInformation("Created HttpClient from factory for SABnzbd polling. BaseAddress={BaseAddress}", http.BaseAddress);

                    // Login
                    using var loginData = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("username", client.Username),
                        new KeyValuePair<string, string>("password", client.Password)
                    });
                    var loginResp = await http.PostAsync($"{baseUrl}/api/v2/auth/login", loginData, cancellationToken);
                    if (!loginResp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("qBittorrent login failed for client {ClientName}", client.Name);
                        return;
                    }

                    var torrentsResp = await http.GetAsync($"{baseUrl}/api/v2/torrents/info", cancellationToken);
                    if (!torrentsResp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch torrents from qBittorrent for {ClientName}", client.Name);
                        return;
                    }

                    var json = await torrentsResp.Content.ReadAsStringAsync(cancellationToken);
                    var torrents = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(json);
                    if (torrents == null) return;

                    // Build comprehensive lookup with all torrent info we need
                    var torrentLookup = new List<(string Hash, string Name, string SavePath, string ContentPath, double Progress, long AmountLeft, string State, long Size, string Category)>();
                    foreach (var t in torrents)
                    {
                        var hash = t.ContainsKey("hash") ? t["hash"].GetString() ?? "" : "";
                        var name = t.ContainsKey("name") ? t["name"].GetString() ?? "" : "";
                        var savePath = t.ContainsKey("save_path") ? t["save_path"].GetString() ?? "" : "";
                        var contentPath = t.ContainsKey("content_path") ? t["content_path"].GetString() ?? "" : "";
                        var progress = t.ContainsKey("progress") ? t["progress"].GetDouble() : 0.0;
                        var amountLeft = t.ContainsKey("amount_left") ? t["amount_left"].GetInt64() : 0L;
                        var state = t.ContainsKey("state") ? t["state"].GetString() ?? "" : "";
                        var size = t.ContainsKey("size") ? t["size"].GetInt64() : 0L;
                        var category = t.ContainsKey("category") ? t["category"].GetString() ?? "" : "";
                        torrentLookup.Add((hash, name, savePath, contentPath, progress, amountLeft, state, size, category));
                    }

                    _logger.LogDebug("Found {TorrentCount} torrents in qBittorrent for client {ClientName}", torrentLookup.Count, client.Name);

                    // For each DB download associated with this client, try to find matching torrent
                    foreach (var dl in downloads)
                    {
                        try
                        {
                            _logger.LogDebug("Looking for qBittorrent match for download {DownloadId}: {Title}", dl.Id, dl.Title);
                            
                            // Try hash-based matching first (most reliable for qBittorrent)
                            var matched = (Hash: "", Name: "", SavePath: "", ContentPath: "", Progress: 0.0, AmountLeft: 0L, State: "", Size: 0L, Category: "");
                            
                            // Check if we have a stored torrent hash for this download
                            if (dl.Metadata != null && dl.Metadata.TryGetValue("TorrentHash", out var hashObj))
                            {
                                var storedHash = hashObj?.ToString();
                                if (!string.IsNullOrEmpty(storedHash))
                                {
                                    matched = torrentLookup.FirstOrDefault(t => 
                                        string.Equals(t.Hash, storedHash, StringComparison.OrdinalIgnoreCase));
                                    
                                    if (!string.IsNullOrEmpty(matched.Hash))
                                    {
                                        _logger.LogDebug("Found qBittorrent torrent by hash match: {Hash} for download {DownloadId}", storedHash, dl.Id);
                                    }
                                }
                            }
                            
                            // Fallback to title/name matching if hash matching failed
                            if (string.IsNullOrEmpty(matched.Hash))
                            {
                                _logger.LogInformation("Hash matching failed for download {DownloadId}, trying title/name matching", dl.Id);
                                matched = torrentLookup.FirstOrDefault(t =>
                                {
                                    // Exact name match
                                    if (string.Equals(t.Name, dl.Title, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                    
                                    // Enhanced title matching with robust normalization
                                    if (AreTitlesSimilar(dl.Title, t.Name))
                                    {
                                        _logger.LogInformation("Title match found: '{DbTitle}' <-> '{TorrentTitle}' (normalized: '{NormDb}' <-> '{NormTorrent}')",
                                            dl.Title, t.Name, NormalizeTitle(dl.Title), NormalizeTitle(t.Name));
                                        return true;
                                    }
                                    
                                    // Path-based matching
                                    if (!string.IsNullOrEmpty(dl.DownloadPath) && !string.IsNullOrEmpty(t.SavePath) && 
                                        (t.SavePath.Contains(dl.DownloadPath, StringComparison.OrdinalIgnoreCase) || 
                                         dl.DownloadPath.Contains(t.SavePath, StringComparison.OrdinalIgnoreCase)))
                                        return true;
                                    
                                    return false;
                                });
                            }

                            if (string.IsNullOrEmpty(matched.Hash))
                            {
                                _logger.LogWarning("No matching qBittorrent torrent found for download {DownloadId}: {Title}", dl.Id, dl.Title);
                                continue;
                            }

                            _logger.LogDebug("Found matching qBittorrent torrent for {DownloadId}: {TorrentName} (Hash: {Hash}, State: {State}, Progress: {Progress:P2}, SavePath: {SavePath}, ContentPath: {ContentPath})", 
                                dl.Id, matched.Name, matched.Hash, matched.State, matched.Progress, matched.SavePath, matched.ContentPath);

                            // Persist client's save/content path to the download as a fallback so FinalizeDownloadAsync
                            // can locate files even if later client state is noisy or the torrent is removed.
                            try
                            {
                                var dbDownload = await dbContext.Downloads.FindAsync(new object[] { dl.Id }, cancellationToken);
                                if (dbDownload != null)
                                {
                                    var changed = false;
                                    if (!string.IsNullOrEmpty(matched.SavePath) && dbDownload.DownloadPath != matched.SavePath)
                                    {
                                        dbDownload.DownloadPath = matched.SavePath;
                                        changed = true;
                                    }

                                    if (dbDownload.Metadata == null) dbDownload.Metadata = new Dictionary<string, object>();
                                    // Record content path in metadata as it's often the most accurate file path
                                    if (!string.IsNullOrEmpty(matched.ContentPath))
                                    {
                                        dbDownload.Metadata["ClientContentPath"] = matched.ContentPath;
                                        changed = true;
                                    }

                                    if (changed)
                                    {
                                        dbContext.Downloads.Update(dbDownload);
                                        await dbContext.SaveChangesAsync(cancellationToken);
                                        _logger.LogDebug("Persisted client paths for download {DownloadId}: DownloadPath={DownloadPath}, ClientContentPath={ClientContentPath}", dl.Id, dbDownload.DownloadPath, matched.ContentPath);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist client paths for download {DownloadId}", dl.Id);
                            }

                            // Update database with real-time progress information
                            await UpdateDownloadProgressAsync(dl.Id, matched.Progress * 100, matched.AmountLeft, matched.State, dbContext, cancellationToken);

                            // Correct completion detection for qBittorrent
                            // A torrent is complete when:
                            // 1. Progress >= 100% (1.0) AND
                            // 2. In an uploading/seeding state OR amount left is 0
                            var isComplete = (matched.Progress >= 1.0 && 
                                            (matched.State == "uploading" || matched.State == "stalledUP" || 
                                             matched.State == "checkingUP" || matched.State == "forcedUP" || 
                                             matched.State == "stoppedUP" || matched.State == "queuedUP")) ||
                                            matched.AmountLeft == 0;

                            _logger.LogDebug("Completion check for {DownloadId}: IsComplete={IsComplete}, Progress={Progress:P2}, AmountLeft={AmountLeft}, State={State}", 
                                dl.Id, isComplete, matched.Progress, matched.AmountLeft, matched.State);

                            if (isComplete)
                            {
                                // Determine the best path to use for file discovery
                                // Priority: content_path (actual file/folder) > save_path (download directory)
                                var completionPath = !string.IsNullOrEmpty(matched.ContentPath) ? matched.ContentPath : matched.SavePath;
                                
                                _logger.LogInformation("qBittorrent torrent {TorrentName} detected as complete. Using path: {CompletionPath}", 
                                    matched.Name, completionPath);
                                
                                // Candidate for completion
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed as complete candidate (qBittorrent). Torrent: {TorrentName}, Path: {Path}. Waiting for stability window.", 
                                        dl.Id, matched.Name, completionPath);
                                    // Broadcast candidate so UI can surface it immediately
                                    _ = BroadcastCandidateUpdateAsync(dl, true, cancellationToken);
                                    continue;
                                }

                                // Use configured stability window if available
                                TimeSpan stableWindow = _completionStableWindow;
                                try
                                {
                                    using var settingsScope = _serviceScopeFactory.CreateScope();
                                    var cfg = settingsScope.ServiceProvider.GetService<IConfigurationService>();
                                    if (cfg != null)
                                    {
                                        var appSettings = await cfg.GetApplicationSettingsAsync();
                                        if (appSettings != null && appSettings.DownloadCompletionStabilitySeconds > 0)
                                        {
                                            stableWindow = TimeSpan.FromSeconds(appSettings.DownloadCompletionStabilitySeconds);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to read application settings for stability window, falling back to default");
                                }

                                var firstSeen = _completionCandidates[dl.Id];
                                if (DateTime.UtcNow - firstSeen >= stableWindow)
                                {
                                    // Finalize: attempt to move/copy files and mark complete
                                    _logger.LogInformation("Download {DownloadId} confirmed complete after stability window (qBittorrent). Torrent: {TorrentName}, Size: {Size:N0} bytes. Finalizing from path: {Path}", 
                                        dl.Id, matched.Name, matched.Size, completionPath);
                                    await FinalizeDownloadAsync(dl, completionPath, client, cancellationToken);
                                    _completionCandidates.Remove(dl.Id);
                                }
                                else
                                {
                                    var remainingTime = _completionStableWindow - (DateTime.UtcNow - firstSeen);
                                    _logger.LogDebug("Download {DownloadId} still in stability window, {RemainingSeconds:F1} seconds remaining", 
                                        dl.Id, remainingTime.TotalSeconds);
                                }
                            }
                                else
                                {
                                    // Not complete anymore - remove candidate if present
                                    if (_completionCandidates.ContainsKey(dl.Id))
                                    {
                                        _completionCandidates.Remove(dl.Id);
                                        _logger.LogDebug("Download {DownloadId} no longer appears complete in qBittorrent, removed from candidates", dl.Id);
                                        _ = BroadcastCandidateUpdateAsync(dl, false, cancellationToken);
                                    }
                                }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing download {DownloadId} while polling qBittorrent", dl.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling qBittorrent client {ClientName}", client.Name);
                }
            }, cancellationToken);
        }

        private Task PollTransmissionAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogDebug("Polling Transmission client {ClientName}", client.Name);
                try
                {
                    var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/transmission/rpc";
                    using var http = _httpClientFactory.CreateClient("DownloadClient");

                    // Get session id
                    string sessionId = string.Empty;
                    try
                    {
                        // Request to get session id (session-get)
                        var dummy = new { method = "session-get", tag = 0 };
                        var dummyContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(dummy), System.Text.Encoding.UTF8, "application/json");
                        var dummyResp = await http.PostAsync(baseUrl, dummyContent, cancellationToken);
                        if (dummyResp.Headers.TryGetValues("X-Transmission-Session-Id", out var sids))
                        {
                            sessionId = sids.First();
                        }
                    }
                    catch { }

                    // Request torrent-get for fields we need
                    var rpc = new
                    {
                        method = "torrent-get",
                        arguments = new
                        {
                            fields = new[] { "id", "name", "percentDone", "leftUntilDone", "isFinished", "status", "downloadDir" }
                        },
                        tag = 4
                    };

                    var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(rpc), System.Text.Encoding.UTF8, "application/json");
                    if (!string.IsNullOrEmpty(sessionId)) content.Headers.Add("X-Transmission-Session-Id", sessionId);

                    var resp = await http.PostAsync(baseUrl, content, cancellationToken);
                    if (!resp.IsSuccessStatusCode) return;
                    var respText = await resp.Content.ReadAsStringAsync(cancellationToken);
                    var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(respText);
                    if (!doc.TryGetProperty("arguments", out var args) || !args.TryGetProperty("torrents", out var torrents) || torrents.ValueKind != System.Text.Json.JsonValueKind.Array) return;

                    foreach (var dl in downloads)
                    {
                        try
                        {
                            // Attempt to match by name or download dir
                            var matching = torrents.EnumerateArray().FirstOrDefault(t =>
                            {
                                var name = t.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                                var dir = t.TryGetProperty("downloadDir", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                                return string.Equals(name, dl.Title, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(dl.DownloadPath) && dir.Contains(dl.DownloadPath));
                            });

                            if (matching.ValueKind == System.Text.Json.JsonValueKind.Undefined) continue;

                            var percent = matching.TryGetProperty("percentDone", out var p) ? p.GetDouble() : 0.0;
                            var left = matching.TryGetProperty("leftUntilDone", out var l) ? l.GetInt64() : 0L;
                            var isFinished = matching.TryGetProperty("isFinished", out var f) ? f.GetBoolean() : false;

                            // Update database with real-time progress information
                            await UpdateDownloadProgressAsync(dl.Id, percent * 100, left, "downloading", dbContext, cancellationToken);

                            var isComplete = percent >= 1.0 || left == 0 || isFinished;

                            if (isComplete)
                            {
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed complete candidate (Transmission). Waiting for stability window.", dl.Id);
                                    _ = BroadcastCandidateUpdateAsync(dl, true, cancellationToken);
                                    continue;
                                }

                                var firstSeen = _completionCandidates[dl.Id];
                                if (DateTime.UtcNow - firstSeen >= _completionStableWindow)
                                {
                                    // Determine downloadDir
                                    var downloadDir = matching.TryGetProperty("downloadDir", out var dprop) ? dprop.GetString() ?? string.Empty : string.Empty;
                                    _logger.LogInformation("Download {DownloadId} confirmed complete after stability window (Transmission). Finalizing.", dl.Id);
                                    await FinalizeDownloadAsync(dl, downloadDir, client, cancellationToken);
                                    _completionCandidates.Remove(dl.Id);
                                }
                            }
                                else
                                {
                                if (_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates.Remove(dl.Id);
                                    _ = BroadcastCandidateUpdateAsync(dl, false, cancellationToken);
                                }
                                }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing download {DownloadId} while polling Transmission", dl.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling Transmission client {ClientName}", client.Name);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Attempt to finalize a download after it is observed complete on the client.
        /// This will try to locate the downloaded file(s) under clientPath and move or copy
        /// the best candidate to the final destination determined by the file naming service
        /// or settings.OutputPath.
        /// </summary>
        private async Task FinalizeDownloadAsync(Download download, string clientPath, DownloadClientConfiguration client, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting download finalization for {DownloadId}: {Title} from client {ClientName}", 
                    download.Id, download.Title, client.Name);
                _logger.LogDebug("Initial client path: {ClientPath}", clientPath);

                using var scope = _serviceScopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var fileNaming = scope.ServiceProvider.GetService<IFileNamingService>();
                var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();
                var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

                // Check if this download is already being processed by the background service
                var queueService = scope.ServiceProvider.GetService<IDownloadProcessingQueueService>();
                if (queueService != null)
                {
                    var existingJobs = await queueService.GetJobsForDownloadAsync(download.Id);
                    var activeJobs = existingJobs?.Where(j => j.Status == ProcessingJobStatus.Pending || 
                                                             j.Status == ProcessingJobStatus.Processing || 
                                                             j.Status == ProcessingJobStatus.Retry).ToList();
                    
                    if (activeJobs != null && activeJobs.Any())
                    {
                        _logger.LogInformation("Download {DownloadId} is already being processed by background service (job {JobId}), skipping duplicate finalization", 
                            download.Id, activeJobs.First().Id);
                        return;
                    }
                    
                    // Also check if download has already been moved/processed
                    if (download.Status == DownloadStatus.Moved)
                    {
                        _logger.LogInformation("Download {DownloadId} has already been processed (status: Moved), skipping duplicate finalization", download.Id);
                        return;
                    }
                }

                var settings = await configService.GetApplicationSettingsAsync();
                _logger.LogDebug("Application settings: OutputPath='{OutputPath}', EnableMetadataProcessing={EnableMetadata}, CompletedFileAction={Action}", 
                    settings.OutputPath, settings.EnableMetadataProcessing, settings.CompletedFileAction);

                // Determine localPath (apply remote path mapping if needed)
                string localPath = clientPath;
                _logger.LogDebug("Original client path: {ClientPath}", clientPath);
                
                if (!string.IsNullOrEmpty(clientPath))
                {
                    try
                    {
                        var pathMapper = scope.ServiceProvider.GetService<IRemotePathMappingService>();
                        if (pathMapper != null)
                        {
                            var mappedPath = await pathMapper.TranslatePathAsync(client.Id, clientPath);
                            if (!string.Equals(mappedPath, clientPath, StringComparison.OrdinalIgnoreCase))
                            {
                                localPath = mappedPath;
                                _logger.LogInformation("Applied path mapping for client {ClientId}: {RemotePath} -> {LocalPath}", 
                                    client.Id, clientPath, localPath);
                            }
                            else
                            {
                                _logger.LogDebug("No path mapping applied for client {ClientId} and path {ClientPath}", client.Id, clientPath);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No path mapping service available");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error during path mapping for client {ClientId} and path {ClientPath}", client.Id, clientPath);
                    }
                }

                _logger.LogInformation("Searching for files in local path: {LocalPath}", localPath);

                string sourceFile = string.Empty;
                List<string> foundFiles = new();

                // Enhanced file discovery logic
                if (!string.IsNullOrEmpty(localPath))
                {
                    // Check if the path itself is a file
                    if (File.Exists(localPath) && settings.AllowedFileExtensions.Any(ext => localPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        sourceFile = localPath;
                        foundFiles.Add(localPath);
                        _logger.LogInformation("Client path is directly a valid file: {FilePath}", localPath);
                    }
                    // Check if it's a directory
                    else if (Directory.Exists(localPath))
                    {
                        _logger.LogDebug("Scanning directory for audio files: {Directory}", localPath);
                        
                        try
                        {
                            // Find all audio files in the directory and subdirectories
                            foundFiles = Directory.GetFiles(localPath, "*.*", SearchOption.AllDirectories)
                                .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                .ToList();

                            _logger.LogInformation("Found {FileCount} audio files in directory {Directory}", foundFiles.Count, localPath);
                            try { _metrics.Increment("finalize.files.found_in_dir", foundFiles.Count); } catch { }
                            foreach (var file in foundFiles.Take(5)) // Log first 5 files for debugging
                            {
                                var fileInfo = new FileInfo(file);
                                _logger.LogDebug("Found file: {FileName} ({Size:N0} bytes)", Path.GetFileName(file), fileInfo.Length);
                            }

                            if (foundFiles.Any())
                            {
                                // Smart file selection logic
                                var titleWords = download.Title?.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                
                                // Try to find file with most title words in the filename
                                var bestMatch = foundFiles
                                    .Select(f => new { 
                                        Path = f, 
                                        FileName = Path.GetFileNameWithoutExtension(f),
                                        Size = new FileInfo(f).Length,
                                        MatchScore = titleWords.Count(word => 
                                            Path.GetFileNameWithoutExtension(f).Contains(word, StringComparison.OrdinalIgnoreCase))
                                    })
                                    .OrderByDescending(x => x.MatchScore)
                                    .ThenByDescending(x => x.Size) // Prefer larger files as tie-breaker
                                    .FirstOrDefault();

                                if (bestMatch != null)
                                {
                                    // If there is more than one audio file in the directory we treat this
                                    // as a multi-file release (audiobook with multiple tracks). In that
                                    // case enqueue the directory itself so downstream processing will
                                    // delegate to ImportService.ImportFilesFromDirectoryAsync which
                                    // imports all files in the folder. If just a single audio file
                                    // exists, keep the current behavior and select the file.
                                    if (foundFiles.Count > 1)
                                    {
                                        sourceFile = localPath; // queue the directory for multi-file import
                                        _logger.LogInformation("Detected multi-file download (contains {Count} audio files) - enqueuing directory for import: {Directory}", foundFiles.Count, localPath);
                                    }
                                    else
                                    {
                                        sourceFile = bestMatch.Path;
                                        _logger.LogInformation("Selected best matching file: {FileName} (match score: {Score}, size: {Size:N0} bytes)", 
                                            Path.GetFileName(sourceFile), bestMatch.MatchScore, bestMatch.Size);
                                        try { _metrics.Increment("finalize.file.selected_from_dir"); } catch { }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error scanning directory for files: {Directory}", localPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Local path does not exist or is not accessible: {LocalPath}", localPath);

                        // Heuristic attempts to handle common SABnzbd/staging variations
                        // Some clients (and SABnzbd history entries) append numeric suffixes
                        // like '.1' to folder names or file names. Try stripping trailing
                        // numeric suffix and/or searching the parent directory for a
                        // similarly named folder that contains valid audio files.
                        try
                        {
                            // Normalize: remove any trailing slash for manipulation
                            var trimmed = localPath.TrimEnd('/', '\\');

                            // 1) Strip trailing numeric suffixes like '.1', '.2' etc
                            var noNumericSuffix = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\.\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (!string.Equals(noNumericSuffix, trimmed, StringComparison.OrdinalIgnoreCase))
                            {
                                // If this candidate exists, treat it as the local path
                                if (File.Exists(noNumericSuffix) && settings.AllowedFileExtensions.Any(ext => noNumericSuffix.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                {
                                    sourceFile = noNumericSuffix;
                                    foundFiles.Add(sourceFile);
                                    _logger.LogInformation("Found file by stripping numeric suffix: {File}", sourceFile);
                                    try { _metrics.Increment("finalize.heuristic.strip_suffix"); } catch { }
                                }
                                else if (Directory.Exists(noNumericSuffix))
                                {
                                    var tmpFiles = Directory.GetFiles(noNumericSuffix, "*.*", SearchOption.AllDirectories)
                                        .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                        .ToList();

                                    if (tmpFiles.Any())
                                    {
                                        foundFiles = tmpFiles;
                                        sourceFile = tmpFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                                        _logger.LogInformation("Found files by stripping numeric suffix in directory: {Directory}. Selected: {File}", noNumericSuffix, sourceFile);
                                        try { _metrics.Increment("finalize.heuristic.strip_suffix"); } catch { }
                                    }
                                }
                            }

                            // 2) If still not found, search parent directory for near matches
                            if (string.IsNullOrEmpty(sourceFile))
                            {
                                var parent = Path.GetDirectoryName(trimmed);
                                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                                {
                                    var baseName = Path.GetFileName(trimmed);
                                    // Remove a trailing numeric suffix for matching purposes
                                    var baseNoSuffix = System.Text.RegularExpressions.Regex.Replace(baseName, @"\.\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                                    var candidateDirs = Directory.GetDirectories(parent)
                                        .Where(d => Path.GetFileName(d).IndexOf(baseNoSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                                        .ToList();

                                    foreach (var cand in candidateDirs)
                                    {
                                        try
                                        {
                                            var candFiles = Directory.GetFiles(cand, "*.*", SearchOption.AllDirectories)
                                                .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                                .ToList();

                                            if (candFiles.Any())
                                            {
                                                sourceFile = candFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                                                foundFiles = candFiles;
                                                _logger.LogInformation("Found file by searching parent directory: {Candidate} -> {File}", cand, sourceFile);
                                                try { _metrics.Increment("finalize.heuristic.parent_search_found"); } catch { }
                                                break;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Error scanning candidate directory {Directory}", cand);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Heuristic search for alternate local path variants failed: {LocalPath}", localPath);
                        }
                    }
                }

                // Fallback 1: Try download record paths
                if (string.IsNullOrEmpty(sourceFile))
                {
                    _logger.LogDebug("No file found in client path, trying fallback paths from download record");
                    
                    var fallbackPaths = new[] { download.FinalPath, download.DownloadPath }.Where(p => !string.IsNullOrEmpty(p));
                    
                    foreach (var fallbackPath in fallbackPaths)
                    {
                        _logger.LogDebug("Checking fallback path: {Path}", fallbackPath);
                        
                        if (File.Exists(fallbackPath!))
                        {
                            sourceFile = fallbackPath!;
                            _logger.LogInformation("Found file using fallback path: {FilePath}", sourceFile);
                            break;
                        }
                        else if (Directory.Exists(fallbackPath!))
                        {
                            _logger.LogDebug("Fallback path is a directory, scanning: {Directory}", fallbackPath);
                            
                            try
                            {
                                var dirFiles = Directory.GetFiles(fallbackPath!, "*.*", SearchOption.AllDirectories)
                                    .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();

                                if (dirFiles.Any())
                                {
                                    sourceFile = dirFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                                    _logger.LogInformation("Found file in fallback directory: {FilePath}", sourceFile);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error scanning fallback directory: {Directory}", fallbackPath);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Fallback path does not exist: {Path}", fallbackPath);
                        }
                    }
                }
                
                // Fallback 2: Try common Docker volume paths if still not found
                if (string.IsNullOrEmpty(sourceFile) && !string.IsNullOrEmpty(clientPath))
                {
                    _logger.LogDebug("Trying Docker volume path variations for: {ClientPath}", clientPath);
                    
                    var dockerPaths = new List<string>();
                    
                    // Common Docker path variations
                    var variations = new List<string> { clientPath };

                    // Common Docker path variations
                    try
                    {
                        // Replace container /data path with /host/data if present
                        variations.Add(clientPath.Replace("/data", "/host/data"));

                        // Only attempt to replace client.DownloadPath if it is non-empty.
                        // String.Replace throws when oldValue is empty, and some clients
                        // may not set DownloadPath, so guard against that case.
                        if (!string.IsNullOrEmpty(client.DownloadPath))
                        {
                            variations.Add(clientPath.Replace(client.DownloadPath, "/host" + client.DownloadPath));
                        }

                        variations.Add(Path.Combine("/host", clientPath.TrimStart('/')));
                        variations.Add(Path.Combine("/data", clientPath.TrimStart('/')));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error building docker path variations for {ClientPath}", clientPath);
                    }

                    var pathVariations = variations.Distinct().ToArray();
                    
                    foreach (var variation in pathVariations.Distinct())
                    {
                        _logger.LogDebug("Trying Docker path variation: {Path}", variation);
                        
                        if (Directory.Exists(variation))
                        {
                            try
                            {
                                var dockerFiles = Directory.GetFiles(variation, "*.*", SearchOption.AllDirectories)
                                    .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();

                                if (dockerFiles.Any())
                                {
                                    var titleWords = download.Title?.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                                    
                                    var bestDockerMatch = dockerFiles
                                        .Select(f => new {
                                            Path = f,
                                            Size = new FileInfo(f).Length,
                                            MatchScore = titleWords.Count(word => 
                                                Path.GetFileNameWithoutExtension(f).Contains(word, StringComparison.OrdinalIgnoreCase))
                                        })
                                        .OrderByDescending(x => x.MatchScore)
                                        .ThenByDescending(x => x.Size)
                                        .First();

                                    sourceFile = bestDockerMatch.Path;
                                    _logger.LogInformation("Found file using Docker path variation: {FilePath} (match score: {Score})", 
                                        sourceFile, bestDockerMatch.MatchScore);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error scanning Docker path variation: {Path}", variation);
                            }
                        }
                    }
                }
                
                // Fallback 3: Try to find files in client's base download directory
                if (string.IsNullOrEmpty(sourceFile) && !string.IsNullOrEmpty(client.DownloadPath))
                {
                    _logger.LogDebug("Trying client base download directory: {DownloadPath}", client.DownloadPath);
                    
                    var searchPaths = new[] { client.DownloadPath };
                    
                    // Apply path mapping to client download path
                    try
                    {
                        var pathMapper = scope.ServiceProvider.GetService<IRemotePathMappingService>();
                        if (pathMapper != null)
                        {
                            var mappedClientPath = await pathMapper.TranslatePathAsync(client.Id, client.DownloadPath);
                            if (!string.Equals(mappedClientPath, client.DownloadPath, StringComparison.OrdinalIgnoreCase))
                            {
                                searchPaths = new[] { client.DownloadPath, mappedClientPath };
                                _logger.LogDebug("Added mapped client path to search: {MappedPath}", mappedClientPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error applying path mapping to client download path");
                    }
                    
                    foreach (var searchPath in searchPaths)
                    {
                        if (Directory.Exists(searchPath))
                        {
                            _logger.LogDebug("Searching in client directory: {SearchPath}", searchPath);
                            
                            try
                            {
                                var clientFiles = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories)
                                    .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                                    .Where(f => !string.IsNullOrEmpty(download.Title) && 
                                               Path.GetFileName(f).Contains(download.Title.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                                    .ToList();

                                if (clientFiles.Any())
                                {
                                    sourceFile = clientFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                                    _logger.LogInformation("Found file in client directory search: {FilePath}", sourceFile);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error searching client directory: {SearchPath}", searchPath);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Client search path does not exist: {SearchPath}", searchPath);
                        }
                    }
                }

                // If the source is empty OR neither a file nor a directory exists at the path,
                // treat it as a missing source. We need to consider directories valid here because
                // a multi-file download is represented by the directory path.
                if (string.IsNullOrEmpty(sourceFile) || (!File.Exists(sourceFile) && !Directory.Exists(sourceFile)))
                {
                    // If the background processing queue already has an active job for this
                    // download, it's likely a race: the file is being moved/processed by the
                    // background worker. Avoid logging a noisy error and let the background
                    // worker finish. Only surface an error if there is no active processing job.
                    try
                    {
                        var processingQueue = scope.ServiceProvider.GetService<IDownloadProcessingQueueService>();
                        if (processingQueue != null)
                        {
                            var jobs = await processingQueue.GetJobsForDownloadAsync(download.Id);
                            if (jobs != null && jobs.Any(j => j.Status == ProcessingJobStatus.Pending || j.Status == ProcessingJobStatus.Processing || j.Status == ProcessingJobStatus.Retry))
                            {
                                _logger.LogDebug("Download {DownloadId} appears to be currently processed by the background queue - skipping missing-source check", download.Id);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Failing this diagnostic lookup shouldn't hide the underlying problem - fall through and log the error
                        _logger.LogDebug(ex, "Error while checking processing queue for download {DownloadId}", download.Id);
                    }

                    // If we get here and no processing job is active, it's likely the files are not yet
                    // present (extraction/unpack not finished). Rather than immediately erroring out
                    // we schedule a bounded retry/backoff so transient delays are handled gracefully.
                    int attempts = 0;
                    int maxRetries = 3;
                    int initialDelay = 30;

                    try
                    {
                        var appSettings = await configService.GetApplicationSettingsAsync();
                        if (appSettings != null)
                        {
                            maxRetries = Math.Max(0, appSettings.MissingSourceMaxRetries);
                            initialDelay = Math.Max(1, appSettings.MissingSourceRetryInitialDelaySeconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to read application settings for missing-source retry, falling back to defaults");
                    }

                    // Read or initialize attempt count
                    attempts = _missingSourceRetryAttempts.GetOrAdd(download.Id, 0);

                    if (attempts >= maxRetries)
                    {
                        _logger.LogError("Unable to locate source file for download {DownloadId} after {Attempts} attempts. Searched paths: ClientPath={ClientPath}, LocalPath={LocalPath}, FinalPath={FinalPath}, DownloadPath={DownloadPath}",
                            download.Id, attempts, clientPath, localPath, download.FinalPath, download.DownloadPath);
                        try { _metrics.Increment("finalize.failed.file_not_found"); } catch { }
                        try { _metrics.Increment("finalize.retry.exhausted"); } catch { }
                        // Reset retry tracking if we have exhausted attempts
                        _missingSourceRetryAttempts.TryRemove(download.Id, out _);
                        _missingSourceRetryScheduled.TryRemove(download.Id, out _);
                        return;
                    }

                    // Ensure we only schedule one retry task per download at a time
                    var scheduled = _missingSourceRetryScheduled.GetOrAdd(download.Id, false);
                    if (scheduled)
                    {
                        _logger.LogDebug("Retry already scheduled for download {DownloadId}, skipping duplicate schedule", download.Id);
                        return;
                    }

                    // Mark as scheduled and increment attempt counter
                    _missingSourceRetryScheduled[download.Id] = true;
                    _missingSourceRetryAttempts.AddOrUpdate(download.Id, 1, (k, v) => v + 1);

                    // Compute exponential backoff delay
                    var currentAttempt = _missingSourceRetryAttempts[download.Id];
                    var delaySeconds = initialDelay * (int)Math.Pow(2, Math.Max(0, currentAttempt - 1));
                    _logger.LogInformation("Source not found for download {DownloadId}. Scheduling retry #{Attempt} in {Delay}s (paths: {LocalPath})", download.Id, currentAttempt, delaySeconds, localPath);

                    try { _metrics.Increment("finalize.retry.scheduled"); } catch { }

                    // Fire-and-forget retry task. Use a safe small delay and then attempt finalize again.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            // Attempt finalization again; do not pass the original cancellation token to avoid accidental cancellation
                            await FinalizeDownloadAsync(download, clientPath, client, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Scheduled retry for download {DownloadId} failed", download.Id);
                            try { _metrics.Increment("finalize.retry.scheduled.failed"); } catch { }
                        }
                        finally
                        {
                            _missingSourceRetryScheduled.TryRemove(download.Id, out _);
                        }
                    });

                    return;
                }

                // If we had scheduled attempts previously, count this as a retry-success
                try
                {
                    if (_missingSourceRetryAttempts.TryGetValue(download.Id, out var prevAttempts) && prevAttempts > 0)
                    {
                        try { _metrics.Increment("finalize.retry.success"); } catch { }
                    }
                }
                catch { }

                // Clear any retry tracking since we've located the file successfully
                _missingSourceRetryAttempts.TryRemove(download.Id, out _);
                _missingSourceRetryScheduled.TryRemove(download.Id, out _);

                // If the source is a directory (multi-file release) we don't try to read
                // file-specific properties like Length. Log a directory-specific message.
                if (Directory.Exists(sourceFile))
                {
                    _logger.LogInformation("Source directory located (multi-file release): {SourceDir}", sourceFile);
                }
                else
                {
                    var sourceFileInfo = new FileInfo(sourceFile);
                    _logger.LogInformation("Source file located: {SourceFile} ({Size:N0} bytes)", sourceFile, sourceFileInfo.Length);
                }

                // Determine destination path
                string destinationPath = string.Empty;
                try
                {
                    if (!string.IsNullOrEmpty(sourceFile) && Directory.Exists(sourceFile))
                    {
                        // When the source is a directory we try to determine the final
                        // audiobook folder under the configured OutputPath (the library).
                        // Prefer using the FileNamingService so naming patterns and
                        // subdirectory rules are respected; fall back to simple dirName.
                        var dirName = Path.GetFileName(sourceFile.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "import";
                        var outRoot = settings.OutputPath;
                        if (string.IsNullOrWhiteSpace(outRoot))
                        {
                            outRoot = "./completed";
                            _logger.LogDebug("No output path configured, using default: {OutputRoot}", outRoot);
                        }

                        // For multi-file directories use a predictable folder under OutputPath
                        // instead of relying on FileNamingService which may create author-based
                        // subfolders (e.g. 'Unknown Author') in unexpected roots.
                        try
                        {
                            // Build destination using OutputPath/Author[/Series]/Title semantics
                            destinationPath = FinalizePathHelper.BuildMultiFileDestination(settings, download, dirName);
                            _logger.LogDebug("Computed directory destination for multi-file release: {DestinationPath}", destinationPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to compute destination folder for multi-file download, falling back to simple OutputPath destination");
                            destinationPath = Path.Combine(outRoot, dirName);
                        }
                    }
                    else if (fileNaming != null)
                    {
                        _logger.LogDebug("Using file naming service to generate destination path");
                        
                        // Always use file naming service for consistent naming
                        AudioMetadata metadata = new AudioMetadata { Title = download.Title ?? "Unknown Title" };
                        
                        if (settings.EnableMetadataProcessing)
                        {
                            // TEMPORARY: Skip ffprobe/ffmpeg metadata extraction during finalization/import.
                            // Calling ffprobe here has been causing noisy Win32Exception logs in test environments
                            // and can be deferred to the background import/metadata processing stage. Use the
                            // download info (title) for naming now and let background processors enrich metadata.
                            _logger.LogInformation("Temporarily skipping ffprobe metadata extraction during finalization for download {DownloadId}", download.Id);
                        }
                        else
                        {
                            _logger.LogDebug("Metadata processing disabled, using download info for naming");
                        }
                        
                        var ext = Path.GetExtension(sourceFile);
                        var generatedPath = await fileNaming.GenerateFilePathAsync(metadata, null, null, ext);
                        
                        // Ensure the file goes directly to OutputPath (root folder) without subdirectories
                        var outRoot = settings.OutputPath;
                        if (string.IsNullOrWhiteSpace(outRoot))
                        {
                            outRoot = "./completed";
                            _logger.LogDebug("No output path configured, using default: {OutputRoot}", outRoot);
                        }
                        
                        // Extract just the filename from the generated path (ignore any directories)
                        var generatedFileName = Path.GetFileName(generatedPath);
                        destinationPath = Path.Combine(outRoot, generatedFileName);
                        
                        _logger.LogInformation("Generated destination path: {DestinationPath}", destinationPath);
                    }
                    else
                    {
                        _logger.LogWarning("File naming service not available, using simple naming");
                        
                        var outRoot = settings.OutputPath;
                        if (string.IsNullOrWhiteSpace(outRoot))
                        {
                            outRoot = "./completed";
                            _logger.LogDebug("No output path configured, using default: {OutputRoot}", outRoot);
                        }
                        
                        var fileName = Path.GetFileName(sourceFile);
                        destinationPath = Path.Combine(outRoot, fileName);
                        _logger.LogInformation("Generated simple destination path: {DestinationPath}", destinationPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate destination path for download {DownloadId}", download.Id);
                    
                    // Fallback to simple path in output directory
                    var outRoot = settings.OutputPath;
                    if (string.IsNullOrWhiteSpace(outRoot))
                    {
                        outRoot = "./completed";
                    }
                    
                    var fallbackFileName = Path.GetFileName(sourceFile);
                    destinationPath = Path.Combine(outRoot, fallbackFileName);
                    _logger.LogWarning("Using fallback destination path: {DestinationPath}", destinationPath);
                }

                // Ensure destination directory exists
                try
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                        _logger.LogDebug("Created destination directory: {Directory}", destDir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create destination directory for {DestinationPath}", destinationPath);
                    return;
                }

                // Before enqueueing, mark the download as observed complete and persist client path info
                try
                {
                    var dbDownload = await db.Downloads.FindAsync(download.Id, cancellationToken);
                    if (dbDownload != null)
                    {
                        // Ensure DownloadPath contains the client save path (already mapped to localPath earlier)
                        if (!string.IsNullOrEmpty(localPath) && dbDownload.DownloadPath != localPath)
                        {
                            dbDownload.DownloadPath = localPath;
                        }

                        // Mark the download as Processing (observed complete by client,
                        // but file-level processing/move/copy is still pending). We avoid
                        // setting Status=Completed here to prevent premature UI notifications
                        // that indicate the download is fully finished before post-processing
                        // has completed. CompletedAt will be set later by the shared
                        // ProcessCompletedDownloadAsync handler when the file is finalized.
                        dbDownload.Status = DownloadStatus.Processing;

                        db.Downloads.Update(dbDownload);
                        await db.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation("Marked download {DownloadId} as Completed (observed) and persisted DownloadPath: {DownloadPath}", download.Id, dbDownload.DownloadPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist observed completion for download {DownloadId}", download.Id);
                }

                // Enqueue download processing job and let the processing pipeline handle moving/renaming
                try
                {
                    var processingQueueService = scope.ServiceProvider.GetService<IDownloadProcessingQueueService>();
                    if (processingQueueService != null)
                    {
                        await processingQueueService.QueueDownloadProcessingAsync(download.Id, sourceFile, client.Id);
                        _logger.LogInformation("Enqueued download {DownloadId} for processing: {Source}", download.Id, sourceFile);
                    }
                    else
                    {
                        _logger.LogWarning("Download processing queue service not available; skipping enqueue for download {DownloadId}", download.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enqueue download {DownloadId} for processing: {Source}", download.Id, sourceFile);
                    return;
                }

                // Finalization step: processing work will update DB and broadcast when the processing job runs
                _logger.LogDebug("Download {DownloadId} enqueued for processing; final DB update will occur during processing", download.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinalizeDownloadAsync failed for download {DownloadId}: {Title}", download.Id, download.Title);
            }
        }

        private Task PollSABnzbdAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogDebug("Polling SABnzbd client {ClientName}", client.Name);
                try
                {
                    var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/api";

                    using var http = _httpClientFactory.CreateClient("DownloadClient");

                    // Get API key from settings
                    var apiKey = "";
                    if (client.Settings != null && client.Settings.TryGetValue("apiKey", out var apiKeyObj))
                    {
                        apiKey = apiKeyObj?.ToString() ?? "";
                    }

                    if (string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogWarning("SABnzbd API key not configured for client {ClientName}", client.Name);
                        return;
                    }

                    // Poll SABnzbd queue for active downloads progress updates
                    var queueUrl = $"{baseUrl}?mode=queue&output=json&apikey={Uri.EscapeDataString(apiKey)}";
                    // Redacted queue URL for safe diagnostics
                    _logger.LogDebug("SABnzbd poll queue URL (redacted): {Url}", LogRedaction.RedactText(queueUrl, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { apiKey })));
                    var queueResponse = await http.GetAsync(queueUrl, cancellationToken);

                    if (queueResponse.IsSuccessStatusCode)
                    {
                        var queueJson = await queueResponse.Content.ReadAsStringAsync(cancellationToken);
                        var queueDoc = System.Text.Json.JsonDocument.Parse(queueJson);

                        if (queueDoc.RootElement.TryGetProperty("queue", out var queue) &&
                            queue.TryGetProperty("slots", out var queueSlots) &&
                            queueSlots.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var slot in queueSlots.EnumerateArray())
                            {
                                try
                                {
                                    var nzoId = slot.TryGetProperty("nzo_id", out var nzoIdProp) ? nzoIdProp.GetString() ?? "" : "";
                                    var filename = slot.TryGetProperty("filename", out var filenameProp) ? filenameProp.GetString() ?? "" : "";
                                    // SABnzbd sometimes returns numeric values as numbers or strings.
                                    // Be defensive and accept either JSON number or JSON string.
                                    double GetDoubleValue(System.Text.Json.JsonElement el)
                                    {
                                        try
                                        {
                                            if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                return el.GetDouble();

                                            if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                                            {
                                                var s = el.GetString();
                                                if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                                                    return v;
                                            }
                                        }
                                        catch { }

                                        return 0.0;
                                    }

                                    var percentage = slot.TryGetProperty("percentage", out var percentageProp) ? GetDoubleValue(percentageProp) : 0.0;
                                    var mb = slot.TryGetProperty("mb", out var mbProp) ? GetDoubleValue(mbProp) : 0.0;
                                    var mbleft = slot.TryGetProperty("mbleft", out var mbleftProp) ? GetDoubleValue(mbleftProp) : 0.0;
                                    var status = slot.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : "";
                                    var category = slot.TryGetProperty("cat", out var catProp) ? catProp.GetString() ?? "" : "";

                                    // Find matching download by NZO ID
                                    var matchingDownload = downloads.FirstOrDefault(dl =>
                                        !string.IsNullOrEmpty(dl.DownloadClientId) &&
                                        dl.DownloadClientId == nzoId);

                                    if (matchingDownload != null)
                                    {
                                        // Calculate progress and update
                                        // percentage is provided by SABnzbd as a percent (e.g. 50.0). Our UpdateDownloadProgressAsync
                                        // expects a percentage in the 0..100 range. Use the percentage directly.
                                        var progressPercent = percentage; // 0..100

                                        // Convert sizes from MB -> bytes
                                        var totalSize = (long)(mb * 1024 * 1024);
                                        var amountLeft = (long)(mbleft * 1024 * 1024);

                                        // Update progress using percent and amountLeft (UpdateDownloadProgressAsync uses percent->downloaded size calculation when TotalSize is set)
                                        await UpdateDownloadProgressAsync(matchingDownload.Id, progressPercent, amountLeft, status, dbContext, cancellationToken);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error updating SABnzbd queue progress for slot");
                                }
                            }
                        }
                    }

                    // Get completed downloads (history) - limit to recent items
                    var historyUrl = $"{baseUrl}?mode=history&limit=100&output=json&apikey={Uri.EscapeDataString(apiKey)}";
                    // Redacted history URL for safe diagnostics
                    _logger.LogDebug("SABnzbd history URL (redacted): {Url}", LogRedaction.RedactText(historyUrl, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { apiKey })));
                    var historyResponse = await http.GetAsync(historyUrl, cancellationToken);

                    if (!historyResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch SABnzbd history for {ClientName}: {StatusCode}", client.Name, historyResponse.StatusCode);
                        return;
                    }

                    var historyJson = await historyResponse.Content.ReadAsStringAsync(cancellationToken);
                    var historyDoc = System.Text.Json.JsonDocument.Parse(historyJson);

                    if (!historyDoc.RootElement.TryGetProperty("history", out var history) ||
                        !history.TryGetProperty("slots", out var slots) ||
                        slots.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        _logger.LogDebug("No history data found for SABnzbd client {ClientName}", client.Name);
                        return;
                    }

                    // Build a lookup of completed items for faster matching
                    // Include nzo_id when available so we can match downloads by ID as well
                    var completedItems = new List<(string Name, string Status, string Path, DateTime CompletedTime, string NzoId)>();
                    
                    foreach (var slot in slots.EnumerateArray())
                    {
                        var name = slot.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var status = slot.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : "";
                        var path = slot.TryGetProperty("storage", out var pathProp) ? pathProp.GetString() ?? "" : "";
                        var nzoId = slot.TryGetProperty("nzo_id", out var nzoIdProp) ? nzoIdProp.GetString() ?? "" : "";
                        
                        // Parse completion time
                        var completedTime = DateTime.MinValue;
                        if (slot.TryGetProperty("completed", out var completedProp))
                        {
                            var completedTimestamp = completedProp.GetInt64();
                            completedTime = DateTimeOffset.FromUnixTimeSeconds(completedTimestamp).DateTime;
                        }
                        
                        if (!string.IsNullOrEmpty(name) && 
                            (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("Complete", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogInformation("SABnzbd history slot parsed: nzo_id={NzoId}, name={Name}, status={Status}, path={Path}, completed={Completed}", nzoId, name, status, path, completedTime);
                            
                            completedItems.Add((name, status, path, completedTime, nzoId));
                        }
                    }

                    _logger.LogDebug("Found {CompletedCount} completed items in SABnzbd history for client {ClientName}", 
                        completedItems.Count, client.Name);

                    // Check each download against completed items
                    foreach (var dl in downloads)
                    {
                        try
                        {
                            // Find matching active download by NZO ID
                            var matchingItem = completedItems.FirstOrDefault(item =>
                                // Match by NZO ID (strongest) or fall back to name/title matching
                                (!string.IsNullOrEmpty(item.NzoId) && !string.IsNullOrEmpty(dl.DownloadClientId) &&
                                    string.Equals(item.NzoId, dl.DownloadClientId, StringComparison.OrdinalIgnoreCase)) ||
                                string.Equals(item.Name, dl.Title, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(dl.Title) && item.Name.Contains(dl.Title, StringComparison.OrdinalIgnoreCase))
                            );

                            if (!string.IsNullOrEmpty(matchingItem.Name))
                            {
                                // Record match type metrics
                                try
                                {
                                    if (!string.IsNullOrEmpty(matchingItem.NzoId) && !string.IsNullOrEmpty(dl.DownloadClientId) && string.Equals(matchingItem.NzoId, dl.DownloadClientId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _metrics.Increment("sabnzbd.history.match.nzo");
                                    }
                                    else if (!string.IsNullOrEmpty(matchingItem.Name) && string.Equals(matchingItem.Name, dl.Title, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _metrics.Increment("sabnzbd.history.match.title_exact");
                                    }
                                    else
                                    {
                                        _metrics.Increment("sabnzbd.history.match.title_contains");
                                    }
                                }
                                catch { }
                                _logger.LogInformation("Found completed SABnzbd download: {DownloadTitle} -> {CompletedName} at {Path}", 
                                    dl.Title, matchingItem.Name, matchingItem.Path);

                                // Check stability window
                                // Use configured stability window if available
                                TimeSpan stableWindow = _completionStableWindow;
                                try
                                {
                                    using var settingsScope = _serviceScopeFactory.CreateScope();
                                    var cfg = settingsScope.ServiceProvider.GetService<IConfigurationService>();
                                    if (cfg != null)
                                    {
                                        var appSettings = await cfg.GetApplicationSettingsAsync();
                                        if (appSettings != null && appSettings.DownloadCompletionStabilitySeconds > 0)
                                        {
                                            stableWindow = TimeSpan.FromSeconds(appSettings.DownloadCompletionStabilitySeconds);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to read application settings for stability window, falling back to default");
                                }
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed as complete candidate (SABnzbd). Waiting for stability window.", dl.Id);
                                    // Broadcast candidate so UI can surface it immediately
                                    _ = BroadcastCandidateUpdateAsync(dl, true, cancellationToken);
                                    continue;
                                }

                                var firstSeen = _completionCandidates[dl.Id];
                                if (DateTime.UtcNow - firstSeen >= stableWindow)
                                {
                                    _logger.LogInformation("Download {DownloadId} confirmed complete after stability window (SABnzbd). Finalizing from path: {Path}", 
                                        dl.Id, matchingItem.Path);
                                    await FinalizeDownloadAsync(dl, matchingItem.Path, client, cancellationToken);
                                    _completionCandidates.Remove(dl.Id);
                                }
                            }
                            else
                            {
                                // Not found in completed items - check if it's still in queue for progress updates
                                // SABnzbd doesn't provide queue data in history API, so we can't update progress here
                                // Progress updates for SABnzbd would need to be done via the queue API
                                if (_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates.Remove(dl.Id);
                                    _logger.LogDebug("Download {DownloadId} no longer appears complete in SABnzbd, removed from candidates", dl.Id);
                                    _ = BroadcastCandidateUpdateAsync(dl, false, cancellationToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing download {DownloadId} while polling SABnzbd", dl.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling SABnzbd client {ClientName}", client.Name);
                }
            }, cancellationToken);
        }

        private Task PollNZBGetAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                _logger.LogDebug("Polling NZBGet client {ClientName}", client.Name);
                try
                {
                    var baseUrl = $"{(client.UseSSL ? "https" : "http")}://{client.Host}:{client.Port}/jsonrpc";

                    using var http = new HttpClient();

                    // Add basic auth if credentials provided
                    if (!string.IsNullOrEmpty(client.Username))
                    {
                        var authBytes = System.Text.Encoding.UTF8.GetBytes($"{client.Username}:{client.Password}");
                        var authHeader = Convert.ToBase64String(authBytes);
                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
                    }

                    // Get active downloads from status for progress updates
                    var statusRequest = new
                    {
                        method = "status",
                        id = 2
                    };

                    var statusJsonContent = System.Text.Json.JsonSerializer.Serialize(statusRequest);
                    var statusHttpContent = new StringContent(statusJsonContent, System.Text.Encoding.UTF8, "application/json");

                    var statusResponse = await http.PostAsync(baseUrl, statusHttpContent, cancellationToken);

                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
                        var statusDoc = System.Text.Json.JsonDocument.Parse(statusJson);

                        if (statusDoc.RootElement.TryGetProperty("result", out var statusResult))
                        {
                            // Get download progress from status
                            var downloadRate = statusResult.TryGetProperty("DownloadRate", out var rateProp) ? rateProp.GetInt64() : 0;
                            var remainingSize = statusResult.TryGetProperty("RemainingSizeMB", out var remainingProp) ? remainingProp.GetInt64() : 0;
                            var downloadedSizeMB = statusResult.TryGetProperty("DownloadedSizeMB", out var downloadedProp) ? downloadedProp.GetInt64() : 0;

                            // Get queue for active downloads
                            var queueRequest = new
                            {
                                method = "listgroups",
                                id = 3
                            };

                            var queueJsonContent = System.Text.Json.JsonSerializer.Serialize(queueRequest);
                            var queueHttpContent = new StringContent(queueJsonContent, System.Text.Encoding.UTF8, "application/json");

                            var queueResponse = await http.PostAsync(baseUrl, queueHttpContent, cancellationToken);

                            if (queueResponse.IsSuccessStatusCode)
                            {
                                var queueJson = await queueResponse.Content.ReadAsStringAsync(cancellationToken);
                                var queueDoc = System.Text.Json.JsonDocument.Parse(queueJson);

                                if (queueDoc.RootElement.TryGetProperty("result", out var queueResult) && queueResult.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var group in queueResult.EnumerateArray())
                                    {
                                        try
                                        {
                                            var nzbId = group.TryGetProperty("NZBID", out var nzbIdProp) ? nzbIdProp.GetInt32() : 0;
                                            var nzbName = group.TryGetProperty("NZBName", out var nameProp) ? nameProp.GetString() ?? "" : "";
                                            var status = group.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? "" : "";
                                            var fileSizeMB = group.TryGetProperty("FileSizeMB", out var sizeProp) ? sizeProp.GetString() ?? "" : "";
                                            var remainingSizeMB = group.TryGetProperty("RemainingSizeMB", out var remainingSizeProp) ? remainingSizeProp.GetString() ?? "" : "";
                                            var downloadedSizeMB_Group = group.TryGetProperty("DownloadedSizeMB", out var downloadedSizeProp) ? downloadedSizeProp.GetString() ?? "" : "";

                                            // Find matching download by NZB ID
                                            var matchingDownload = downloads.FirstOrDefault(dl =>
                                                !string.IsNullOrEmpty(dl.DownloadClientId) &&
                                                dl.DownloadClientId == nzbId.ToString());

                                            if (matchingDownload != null)
                                            {
                                                // Parse sizes
                                                if (double.TryParse(fileSizeMB, out var totalMB) && double.TryParse(remainingSizeMB, out var remainingMB))
                                                {
                                                    var progress = totalMB > 0 ? (totalMB - remainingMB) / totalMB : 0.0;
                                                    var downloadedSize = (long)((totalMB - remainingMB) * 1024 * 1024); // Convert MB to bytes
                                                    var amountLeft = (long)(remainingMB * 1024 * 1024); // Convert MB to bytes

                                                    await UpdateDownloadProgressAsync(matchingDownload.Id, progress, amountLeft, status, dbContext, cancellationToken);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Error updating NZBGet queue progress for group");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Get completed downloads from history
                    var historyRequest = new
                    {
                        method = "history",
                        @params = new object[] { false }, // hidden = false
                        id = 1
                    };

                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(historyRequest);
                    var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                    var historyResponse = await http.PostAsync(baseUrl, httpContent, cancellationToken);

                    if (!historyResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch NZBGet history for {ClientName}: {StatusCode}", client.Name, historyResponse.StatusCode);
                        return;
                    }

                    var historyJson = await historyResponse.Content.ReadAsStringAsync(cancellationToken);
                    var historyDoc = System.Text.Json.JsonDocument.Parse(historyJson);

                    // Check for RPC error
                    if (historyDoc.RootElement.TryGetProperty("error", out var error) && error.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        var errorMsg = "Unknown error";
                        if (error.TryGetProperty("message", out var errorMessage))
                        {
                            errorMsg = errorMessage.GetString() ?? "Unknown error";
                        }
                        _logger.LogWarning("NZBGet RPC error for {ClientName}: {Error}", client.Name, errorMsg);
                        return;
                    }

                    if (!historyDoc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        _logger.LogDebug("No history data found for NZBGet client {ClientName}", client.Name);
                        return;
                    }

                    // Build a lookup of completed items
                    var completedItems = new List<(string Name, string Status, string DestDir, DateTime CompletedTime)>();

                    foreach (var item in result.EnumerateArray())
                    {
                        var name = item.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var status = item.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? "" : "";
                        var destDir = item.TryGetProperty("DestDir", out var destProp) ? destProp.GetString() ?? "" : "";
                        
                        // Parse completion time
                        var completedTime = DateTime.MinValue;
                        if (item.TryGetProperty("HistoryTime", out var timeProp))
                        {
                            var timestamp = timeProp.GetInt64();
                            completedTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                        }

                        // NZBGet status values for successful completion
                        if (!string.IsNullOrEmpty(name) && 
                            (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("SUCCESS/UNPACK", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("SUCCESS/SCRIPT", StringComparison.OrdinalIgnoreCase)))
                        {
                            completedItems.Add((name, status, destDir, completedTime));
                        }
                    }

                    _logger.LogDebug("Found {CompletedCount} completed items in NZBGet history for client {ClientName}", 
                        completedItems.Count, client.Name);

                    // Check each download against completed items
                    foreach (var dl in downloads)
                    {
                        try
                        {
                            // Find matching completed download by name
                            var matchingItem = completedItems.FirstOrDefault(item =>
                                string.Equals(item.Name, dl.Title, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(dl.Title) && item.Name.Contains(dl.Title, StringComparison.OrdinalIgnoreCase))
                            );

                            if (!string.IsNullOrEmpty(matchingItem.Name))
                            {
                                _logger.LogInformation("Found completed NZBGet download: {DownloadTitle} -> {CompletedName} at {Path}", 
                                    dl.Title, matchingItem.Name, matchingItem.DestDir);

                                // Check stability window
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed as complete candidate (NZBGet). Waiting for stability window.", dl.Id);
                                    // Broadcast candidate so UI can surface it immediately
                                    _ = BroadcastCandidateUpdateAsync(dl, true, cancellationToken);
                                    continue;
                                }

                                var firstSeen = _completionCandidates[dl.Id];
                                if (DateTime.UtcNow - firstSeen >= _completionStableWindow)
                                {
                                    _logger.LogInformation("Download {DownloadId} confirmed complete after stability window (NZBGet). Finalizing from path: {Path}", 
                                        dl.Id, matchingItem.DestDir);
                                    await FinalizeDownloadAsync(dl, matchingItem.DestDir, client, cancellationToken);
                                    _completionCandidates.Remove(dl.Id);
                                }
                            }
                            else
                            {
                                // Not found in completed items - remove from candidates if present
                                if (_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates.Remove(dl.Id);
                                    _logger.LogDebug("Download {DownloadId} no longer appears complete in NZBGet, removed from candidates", dl.Id);
                                    _ = BroadcastCandidateUpdateAsync(dl, false, cancellationToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing download {DownloadId} while polling NZBGet", dl.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling NZBGet client {ClientName}", client.Name);
                }
            }, cancellationToken);
        }

        private async Task UpdateDownloadProgressAsync(string downloadId, double progress, long amountLeft, string clientState, ListenArrDbContext dbContext, CancellationToken cancellationToken)
        {
            try
            {
                var download = await dbContext.Downloads.FindAsync(new object[] { downloadId }, cancellationToken);
                if (download == null) return;

                // Map client state to our DownloadStatus
                var mappedStatus = clientState switch
                {
                    "downloading" => DownloadStatus.Downloading,
                    "metaDL" => DownloadStatus.Downloading,
                    "forcedDL" => DownloadStatus.Downloading,
                    "stalledDL" => DownloadStatus.Downloading,
                    "checkingDL" => DownloadStatus.Downloading,
                    "checkingResumeData" => DownloadStatus.Downloading,
                    "moving" => DownloadStatus.Downloading,
                    "uploading" => DownloadStatus.Downloading,
                    "stalledUP" => DownloadStatus.Downloading,
                    "checkingUP" => DownloadStatus.Downloading,
                    "forcedUP" => DownloadStatus.Downloading,
                    "stoppedDL" => DownloadStatus.Paused,
                    "stoppedUP" => DownloadStatus.Paused,
                    "queuedDL" => DownloadStatus.Queued,
                    "queuedUP" => DownloadStatus.Queued,
                    "error" => DownloadStatus.Failed,
                    "missingFiles" => DownloadStatus.Failed,
                    _ => DownloadStatus.Queued
                };

                // Calculate downloaded size from progress and total size
                long downloadedSize = download.TotalSize > 0 ? (long)(download.TotalSize * progress / 100) : 0;

                // Update download record
                download.Progress = (decimal)progress;
                download.DownloadedSize = downloadedSize;

                // Conservative guard: if the DB record is currently Failed, do not overwrite
                // the status to a non-failed value unless we have strong evidence (progress increased)
                // or the client reports Completed. This prevents transient client "error" states
                // from flipping the UI incorrectly.
                if (download.Status == DownloadStatus.Failed && mappedStatus != DownloadStatus.Failed)
                {
                    var incomingProgress = (decimal)progress;

                    // Allow transition to Completed always (finalization or client reports complete)
                    if (mappedStatus == DownloadStatus.Completed)
                    {
                        _logger.LogInformation("Allowing Failed->Completed for {DownloadId} because client reports completion", downloadId);
                        download.Status = mappedStatus;
                    }
                    else
                    {
                        // Only allow non-failed status if progress increased
                        if (incomingProgress <= download.Progress)
                        {
                            _logger.LogDebug("Skipping status overwrite for failed download {DownloadId}: incoming progress {Incoming} <= current {Current}", downloadId, incomingProgress, download.Progress);
                            // still update metadata for visibility
                            if (download.Metadata == null) download.Metadata = new Dictionary<string, object>();
                            download.Metadata["ClientState"] = clientState;
                            download.Metadata["AmountLeft"] = amountLeft;
                            dbContext.Downloads.Update(download);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            return;
                        }

                        _logger.LogInformation("Updating Failed -> {MappedStatus} for {DownloadId} because progress increased ({Old} -> {New})", mappedStatus, downloadId, download.Progress, incomingProgress);
                        download.Status = mappedStatus;
                    }
                }
                else
                {
                    download.Status = mappedStatus;
                }

                // Add metadata for real-time updates
                if (download.Metadata == null)
                {
                    download.Metadata = new Dictionary<string, object>();
                }
                download.Metadata["ClientState"] = clientState;
                download.Metadata["AmountLeft"] = amountLeft;

                dbContext.Downloads.Update(download);
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Updated download {DownloadId} progress: {Progress:F1}%, Status: {Status}, Downloaded: {Downloaded:N0} bytes", 
                    downloadId, progress, mappedStatus, downloadedSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating download progress for {DownloadId}", downloadId);
            }
        }

        private async Task BroadcastDownloadUpdatesAsync(
            List<Download> currentDownloads, 
            CancellationToken cancellationToken)
        {
            var changedDownloads = new List<Download>();

            // Try to get DownloadPushService from DI so we can avoid re-broadcasting
            // downloads that were recently pushed by clients.
            Listenarr.Api.Services.DownloadPushService? pushService = null;
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                pushService = scope.ServiceProvider.GetService<Listenarr.Api.Services.DownloadPushService>();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to resolve DownloadPushService (non-fatal)");
            }

            foreach (var download in currentDownloads)
            {
                // Check if this download has changed
                if (_lastDownloadStates.TryGetValue(download.Id, out var lastState))
                {
                    if (HasDownloadChanged(lastState, download))
                    {
                        // If this download was recently pushed by a client, skip re-broadcasting
                        if (pushService != null && pushService.WasRecentlyPushed(download.Id))
                        {
                            _logger.LogDebug("Skipping broadcast for download {DownloadId} because it was recently pushed", download.Id);
                        }
                        else
                        {
                            changedDownloads.Add(download);
                        }

                        _lastDownloadStates[download.Id] = CloneDownload(download);
                    }
                }
                else
                {
                    // New download
                    changedDownloads.Add(download);
                    _lastDownloadStates[download.Id] = CloneDownload(download);
                }
            }

            // Clean up old download states that are no longer in the list
            var currentIds = currentDownloads.Select(d => d.Id).ToHashSet();
            var keysToRemove = _lastDownloadStates.Keys.Where(k => !currentIds.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _lastDownloadStates.Remove(key);
            }

            // Broadcast updates if there are changes
            if (changedDownloads.Any())
            {
                _logger.LogDebug("Broadcasting {Count} download updates", changedDownloads.Count);

                // Sanitize each Download before broadcasting to clients (remove DownloadPath and client-local metadata)
                var sanitized = changedDownloads.Select(d => new {
                    id = d.Id,
                    audiobookId = d.AudiobookId,
                    title = d.Title,
                    artist = d.Artist,
                    album = d.Album,
                    originalUrl = d.OriginalUrl,
                    status = d.Status.ToString(),
                    progress = d.Progress,
                    totalSize = d.TotalSize,
                    downloadedSize = d.DownloadedSize,
                    finalPath = d.FinalPath,
                    startedAt = d.StartedAt,
                    completedAt = d.CompletedAt,
                    errorMessage = d.ErrorMessage,
                    downloadClientId = d.DownloadClientId,
                    metadata = (d.Metadata ?? new Dictionary<string, object>()).Where(kvp => !string.Equals(kvp.Key, "ClientContentPath", StringComparison.OrdinalIgnoreCase)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ToList();

                await _hubContext.Clients.All.SendAsync(
                    "DownloadUpdate",
                    sanitized,
                    cancellationToken);
            }

            // Also send full list periodically (every 10 polls)
            if (DateTime.UtcNow.Second % 30 == 0)
            {
                // Broadcast a sanitized full list (remove DownloadPath and client-local metadata)
                var sanitizedList = currentDownloads.Select(d => new {
                    id = d.Id,
                    audiobookId = d.AudiobookId,
                    title = d.Title,
                    artist = d.Artist,
                    album = d.Album,
                    originalUrl = d.OriginalUrl,
                    status = d.Status.ToString(),
                    progress = d.Progress,
                    totalSize = d.TotalSize,
                    downloadedSize = d.DownloadedSize,
                    finalPath = d.FinalPath,
                    startedAt = d.StartedAt,
                    completedAt = d.CompletedAt,
                    errorMessage = d.ErrorMessage,
                    downloadClientId = d.DownloadClientId,
                    metadata = (d.Metadata ?? new Dictionary<string, object>()).Where(kvp => !string.Equals(kvp.Key, "ClientContentPath", StringComparison.OrdinalIgnoreCase)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ToList();

                await _hubContext.Clients.All.SendAsync(
                    "DownloadsList",
                    sanitizedList,
                    cancellationToken);
            }
        }

        private bool HasDownloadChanged(Download oldDownload, Download newDownload)
        {
            return oldDownload.Status != newDownload.Status ||
                   oldDownload.Progress != newDownload.Progress ||
                   oldDownload.DownloadedSize != newDownload.DownloadedSize ||
                   oldDownload.ErrorMessage != newDownload.ErrorMessage ||
                   oldDownload.CompletedAt != newDownload.CompletedAt;
        }

        private Download CloneDownload(Download download)
        {
            return new Download
            {
                Id = download.Id,
                Title = download.Title,
                Artist = download.Artist,
                Album = download.Album,
                OriginalUrl = download.OriginalUrl,
                Status = download.Status,
                Progress = download.Progress,
                TotalSize = download.TotalSize,
                DownloadedSize = download.DownloadedSize,
                DownloadPath = download.DownloadPath,
                FinalPath = download.FinalPath,
                StartedAt = download.StartedAt,
                CompletedAt = download.CompletedAt,
                ErrorMessage = download.ErrorMessage,
                DownloadClientId = download.DownloadClientId
            };
        }
    }
}

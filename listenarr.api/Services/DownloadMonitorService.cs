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
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, Download> _lastDownloadStates = new();
    // Tracks downloads that appear complete and the time they were first observed complete
    private readonly Dictionary<string, DateTime> _completionCandidates = new();
    private readonly TimeSpan _completionStableWindow = TimeSpan.FromSeconds(10);

        public DownloadMonitorService(
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<DownloadHub> hubContext,
            ILogger<DownloadMonitorService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _hubContext = hubContext;
            _logger = logger;
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

                    using var http = new HttpClient();

                    // Login
                    var loginData = new FormUrlEncodedContent(new[]
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

                    // Build quick lookup by name and save_path
                    var torrentLookup = new List<(string Name, string SavePath, double Progress, long AmountLeft, string State)>();
                    foreach (var t in torrents)
                    {
                        var name = t.ContainsKey("name") ? t["name"].GetString() ?? "" : "";
                        var savePath = t.ContainsKey("save_path") ? t["save_path"].GetString() ?? "" : "";
                        var progress = t.ContainsKey("progress") ? t["progress"].GetDouble() : 0.0;
                        var amountLeft = t.ContainsKey("amount_left") ? t["amount_left"].GetInt64() : 0L;
                        var state = t.ContainsKey("state") ? t["state"].GetString() ?? "" : "";
                        torrentLookup.Add((name, savePath, progress, amountLeft, state));
                    }

                    // For each DB download associated with this client, try to find matching torrent
                    foreach (var dl in downloads)
                    {
                        try
                        {
                            // Match by title or remote path
                            var matched = torrentLookup.FirstOrDefault(t =>
                                string.Equals(t.Name, dl.Title, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(dl.DownloadPath) && !string.IsNullOrEmpty(t.SavePath) && t.SavePath.Contains(dl.DownloadPath))
                            );

                            if (matched.Name == null) continue;

                            var isComplete = matched.Progress >= 1.0 || matched.AmountLeft == 0 || matched.State?.ToLower().Contains("upload") == true || matched.State == "completedUP" || matched.State == "completedDL";

                            if (isComplete)
                            {
                                // Candidate for completion
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed complete candidate (qBittorrent). Waiting for stability window.", dl.Id);
                                    continue;
                                }

                                var firstSeen = _completionCandidates[dl.Id];
                                if (DateTime.UtcNow - firstSeen >= _completionStableWindow)
                                {
                                    // Finalize: attempt to move/copy files and mark complete
                                    _logger.LogInformation("Download {DownloadId} confirmed complete after stability window. Finalizing.", dl.Id);
                                    await FinalizeDownloadAsync(dl, matched.SavePath, client, cancellationToken);
                                    _completionCandidates.Remove(dl.Id);
                                }
                            }
                            else
                            {
                                // Not complete anymore - remove candidate if present
                                if (_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates.Remove(dl.Id);
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
                    using var http = new HttpClient();

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

                            var isComplete = percent >= 1.0 || left == 0 || isFinished;

                            if (isComplete)
                            {
                                if (!_completionCandidates.ContainsKey(dl.Id))
                                {
                                    _completionCandidates[dl.Id] = DateTime.UtcNow;
                                    _logger.LogInformation("Download {DownloadId} observed complete candidate (Transmission). Waiting for stability window.", dl.Id);
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
                                if (_completionCandidates.ContainsKey(dl.Id)) _completionCandidates.Remove(dl.Id);
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
                using var scope = _serviceScopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var fileNaming = scope.ServiceProvider.GetService<IFileNamingService>();
                var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();
                var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

                var settings = await configService.GetApplicationSettingsAsync();

                // Determine localPath (apply remote path mapping if needed)
                string localPath = clientPath;
                if (!string.IsNullOrEmpty(localPath))
                {
                    try
                    {
                        var pathMapper = scope.ServiceProvider.GetService<IRemotePathMappingService>();
                        if (pathMapper != null)
                        {
                            localPath = await pathMapper.TranslatePathAsync(client.Id, clientPath);
                        }
                    }
                    catch { }
                }

                string sourceFile = string.Empty;

                if (!string.IsNullOrEmpty(localPath) && Directory.Exists(localPath))
                {
                    // Try to find a file that matches the download title and allowed extensions
                    var files = Directory.GetFiles(localPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => settings.AllowedFileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (files.Any())
                    {
                        // Prefer a file containing the title
                        var match = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(download.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                                    ?? files.OrderByDescending(f => new FileInfo(f).Length).First();
                        sourceFile = match;
                    }
                }

                // Fallback to download.DownloadPath or FinalPath
                if (string.IsNullOrEmpty(sourceFile))
                {
                    if (!string.IsNullOrEmpty(download.FinalPath) && File.Exists(download.FinalPath)) sourceFile = download.FinalPath;
                    else if (!string.IsNullOrEmpty(download.DownloadPath) && File.Exists(download.DownloadPath)) sourceFile = download.DownloadPath;
                }

                if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
                {
                    _logger.LogWarning("Unable to locate source file for download {DownloadId} during finalization", download.Id);
                    return;
                }

                // Determine destination path
                string destinationPath = string.Empty;
                try
                {
                    if (settings.EnableMetadataProcessing && fileNaming != null)
                    {
                        // Try to extract metadata minimally
                        var metadataService = scope.ServiceProvider.GetService<IMetadataService>();
                        AudioMetadata metadata = new AudioMetadata { Title = download.Title };
                        if (metadataService != null)
                        {
                            try { metadata = await metadataService.ExtractFileMetadataAsync(sourceFile); } catch { }
                        }
                        var ext = Path.GetExtension(sourceFile);
                        destinationPath = await fileNaming.GenerateFilePathAsync(metadata, null, null, ext);
                    }
                    else
                    {
                        var outRoot = settings.OutputPath;
                        if (string.IsNullOrEmpty(outRoot)) outRoot = Path.GetDirectoryName(sourceFile) ?? ".";
                        var fileName = Path.GetFileName(sourceFile);
                        destinationPath = Path.Combine(outRoot, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate destination path for download {DownloadId}", download.Id);
                    destinationPath = Path.Combine(Path.GetDirectoryName(sourceFile) ?? ".", Path.GetFileName(sourceFile));
                }

                // Ensure destination directory exists
                try
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                }
                catch { }

                // Move or copy according to settings
                try
                {
                    if (string.Equals(settings.CompletedFileAction, "Copy", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourceFile, destinationPath, true);
                        _logger.LogInformation("Copied completed download {DownloadId} from {Source} to {Dest}", download.Id, sourceFile, destinationPath);
                    }
                    else
                    {
                        // Default to Move
                        File.Move(sourceFile, destinationPath, true);
                        _logger.LogInformation("Moved completed download {DownloadId} from {Source} to {Dest}", download.Id, sourceFile, destinationPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to move/copy file for download {DownloadId}", download.Id);
                    return;
                }

                // Finally, call shared completion handler to update DB and broadcast
                try
                {
                    await downloadService.ProcessCompletedDownloadAsync(download.Id, destinationPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing completed download {DownloadId}", download.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinalizeDownloadAsync failed for {DownloadId}", download.Id);
            }
        }

        private Task PollSABnzbdAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement SABnzbd API polling
            _logger.LogDebug("Polling SABnzbd client {ClientName}", client.Name);
            return Task.CompletedTask;
        }

        private Task PollNZBGetAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement NZBGet API polling
            _logger.LogDebug("Polling NZBGet client {ClientName}", client.Name);
            return Task.CompletedTask;
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

                await _hubContext.Clients.All.SendAsync(
                    "DownloadUpdate",
                    changedDownloads,
                    cancellationToken);
            }

            // Also send full list periodically (every 10 polls)
            if (DateTime.UtcNow.Second % 30 == 0)
            {
                await _hubContext.Clients.All.SendAsync(
                    "DownloadsList", 
                    currentDownloads, 
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

using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;

namespace Listenarr.Api.Services
{
    public class MoveBackgroundService : BackgroundService
    {
        private readonly IMoveQueueService _moveQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MoveBackgroundService> _logger;

        public MoveBackgroundService(IMoveQueueService moveQueue, IServiceScopeFactory scopeFactory, ILogger<MoveBackgroundService> logger)
        {
            _moveQueue = moveQueue ?? throw new ArgumentNullException(nameof(moveQueue));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _moveQueue.Reader.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Processing move job {JobId} for audiobook {AudiobookId} to {Path}", job.Id, job.AudiobookId, job.RequestedPath);
                    _moveQueue.UpdateJobStatus(job.Id, "Processing");

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

                    var audiobook = await db.Audiobooks.FindAsync(new object[] { job.AudiobookId }, stoppingToken);
                    if (audiobook == null)
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Failed", "Audiobook not found");
                        continue;
                    }

                    // Prefer an enqueued source path snapshot if provided, otherwise use the audiobook's BasePath
                    var source = job.SourcePath;
                    if (!string.IsNullOrWhiteSpace(source) && !Directory.Exists(source))
                    {
                        _logger.LogWarning("Provided source path {Source} for job {JobId} does not exist; falling back to audiobook.BasePath", source, job.Id);
                        source = null;
                    }

                    if (string.IsNullOrWhiteSpace(source))
                    {
                        source = audiobook.BasePath;
                    }

                    if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Failed", "Source path invalid or does not exist");
                        continue;
                    }

                    var requested = job.RequestedPath ?? string.Empty;

                    string target = requested;

                    if (string.IsNullOrWhiteSpace(target))
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Failed", "Target path not provided");
                        continue;
                    }

                    // Normalize full paths
                    target = Path.GetFullPath(target);
                    source = Path.GetFullPath(source);

                    // If source == target, nothing to do
                    if (string.Equals(source.TrimEnd(Path.DirectorySeparatorChar), target.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Completed");
                        continue;
                    }

                    // Ensure target parent exists
                    var targetParent = Path.GetDirectoryName(target);
                    if (string.IsNullOrEmpty(targetParent))
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Failed", "Invalid target path");
                        continue;
                    }

                    if (!Directory.Exists(targetParent)) Directory.CreateDirectory(targetParent);

                    // Do not overwrite an existing target
                    if (Directory.Exists(target))
                    {
                        _moveQueue.UpdateJobStatus(job.Id, "Failed", "Target directory already exists");
                        continue;
                    }

                    // Create a temporary directory under the target parent
                    var tempName = Path.Combine(targetParent, Path.GetFileName(target) + ".tmp-" + job.Id.ToString("N"));

                    // Copy recursively with retries per file
                    try
                    {
                        Directory.CreateDirectory(tempName);
                        var entries = Directory.EnumerateFileSystemEntries(source, "*", SearchOption.AllDirectories);
                        foreach (var entry in entries)
                        {
                            var rel = Path.GetRelativePath(source, entry);
                            var destPath = Path.Combine(tempName, rel);

                            if (Directory.Exists(entry))
                            {
                                if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                                continue;
                            }

                            // Ensure dest directory exists
                            var ddir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(ddir) && !Directory.Exists(ddir)) Directory.CreateDirectory(ddir);

                            // Copy file with retry/backoff and preserve timestamps/attributes on success
                            var succeeded = false;
                            const int maxAttempts = 5;
                            for (int attempt = 1; attempt <= maxAttempts; attempt++)
                            {
                                try
                                {
                                    File.Copy(entry, destPath, false);

                                    // Preserve file attributes and timestamps
                                    try
                                    {
                                        var attrs = File.GetAttributes(entry);
                                        File.SetAttributes(destPath, attrs);

                                        var lastWrite = File.GetLastWriteTimeUtc(entry);
                                        var creation = File.GetCreationTimeUtc(entry);
                                        File.SetLastWriteTimeUtc(destPath, lastWrite);
                                        File.SetCreationTimeUtc(destPath, creation);
                                    }
                                    catch (Exception attrEx)
                                    {
                                        _logger.LogDebug(attrEx, "Non-fatal: failed to preserve attributes for {File}", entry);
                                    }

                                    succeeded = true;
                                    break;
                                }
                                catch (IOException ioex)
                                {
                                    _logger.LogWarning(ioex, "IO error copying file {File} attempt {Attempt}", entry, attempt);

                                    // exponential backoff
                                    var delay = TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1)));
                                    await Task.Delay(delay, stoppingToken);
                                }
                            }

                            if (!succeeded)
                            {
                                // Increment attempt count for the DB job to surface retries
                                try
                                {
                                    var dbJob = db.MoveJobs.FirstOrDefault(j => j.Id == job.Id);
                                    if (dbJob != null)
                                    {
                                        dbJob.AttemptCount += 1;
                                        db.MoveJobs.Update(dbJob);
                                        await db.SaveChangesAsync(stoppingToken);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to increment AttemptCount for job {JobId}", job.Id);
                                }

                                throw new Exception($"Failed to copy file after {maxAttempts} attempts: {entry}");
                            }
                        }

                        // After successful copy, move temp to final target (atomic on same volume)
                        Directory.Move(tempName, target);

                        // Delete source directory
                        Directory.Delete(source, true);

                        // Update DB audiobook BasePath to new target
                        audiobook.BasePath = target;
                        db.Audiobooks.Update(audiobook);
                        await db.SaveChangesAsync(stoppingToken);

                        _moveQueue.UpdateJobStatus(job.Id, "Completed");
                        _logger.LogInformation("Move job {JobId} completed: {Source} -> {Target}", job.Id, source, target);
                    }
                    catch (Exception ex)
                    {
                        // Cleanup any temp dir
                        try { if (Directory.Exists(tempName)) Directory.Delete(tempName, true); } catch { }

                        // Increment attempt count for the job on failure
                        try
                        {
                            var dbJob = db.MoveJobs.FirstOrDefault(j => j.Id == job.Id);
                            if (dbJob != null)
                            {
                                dbJob.AttemptCount += 1;
                                db.MoveJobs.Update(dbJob);
                                await db.SaveChangesAsync(stoppingToken);
                            }
                        }
                        catch (Exception attEx)
                        {
                            _logger.LogWarning(attEx, "Failed to increment AttemptCount for job {JobId} after failure", job.Id);
                        }

                        _moveQueue.UpdateJobStatus(job.Id, "Failed", ex.Message);
                        _logger.LogError(ex, "Move job {JobId} failed", job.Id);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing move job {JobId}", job.Id);
                    try { _moveQueue.UpdateJobStatus(job.Id, "Failed", ex.Message); } catch { }
                }
            }
        }
    }
}


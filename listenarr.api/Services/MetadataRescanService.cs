using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    // Background hosted service to rescan files missing metadata and populate DB fields
    public class MetadataRescanService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MetadataRescanService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private readonly SemaphoreSlim _sem = new SemaphoreSlim(2); // bound concurrent extractions

        public MetadataRescanService(IServiceScopeFactory scopeFactory, ILogger<MetadataRescanService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MetadataRescanService starting");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                    var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();

                    var candidates = await db.AudiobookFiles
                        .Where(f => f.DurationSeconds == null || f.Format == null || f.SampleRate == null)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    if (candidates.Any())
                    {
                        _logger.LogInformation("Found {Count} files missing metadata to rescan", candidates.Count);
                    }

                    var tasks = new List<Task>();
                    foreach (var f in candidates)
                    {
                        await _sem.WaitAsync(stoppingToken);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                _logger.LogInformation("Re-extracting metadata for file id={Id} path={Path}", f.Id, f.Path);
                                var meta = await metadataService.ExtractFileMetadataAsync(f.Path ?? string.Empty);
                                if (meta != null)
                                {
                                    var fi = new System.IO.FileInfo(f.Path ?? string.Empty);
                                    f.Size = fi.Exists ? fi.Length : f.Size;
                                    f.DurationSeconds = meta.Duration.TotalSeconds != 0 ? meta.Duration.TotalSeconds : f.DurationSeconds;
                                    f.Format = !string.IsNullOrEmpty(meta.Format) ? meta.Format : f.Format;
                                    f.Bitrate = meta.Bitrate != 0 ? meta.Bitrate : f.Bitrate;
                                    f.SampleRate = meta.SampleRate != 0 ? meta.SampleRate : f.SampleRate;
                                    f.Channels = meta.Channels != 0 ? meta.Channels : f.Channels;
                                    await db.SaveChangesAsync(stoppingToken);
                                    _logger.LogInformation("Updated metadata for file id={Id}", f.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to rescan metadata for file id={Id} path={Path}", f.Id, f.Path);
                            }
                            finally
                            {
                                _sem.Release();
                            }
                        }, stoppingToken));
                    }

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running metadata rescan");
                }

                await Task.Delay(_interval, stoppingToken);
            }
            _logger.LogInformation("MetadataRescanService stopping");
        }
    }
}

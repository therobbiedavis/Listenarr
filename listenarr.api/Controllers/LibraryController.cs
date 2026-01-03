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

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.Json;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/library")]
    public class LibraryController : ControllerBase
    {
        private readonly IAudiobookRepository _repo;
        private readonly IImageCacheService _imageCacheService;
        private readonly ILogger<LibraryController> _logger;
        private readonly ListenArrDbContext _dbContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IScanQueueService? _scanQueueService;
        private readonly IMoveQueueService? _moveQueueService;
        private readonly IFileNamingService _fileNamingService;
        private readonly NotificationService? _notificationService;

        /// <summary>
        /// Initializes a new <see cref="LibraryController"/> with required services.
        /// </summary>
        /// <param name="repo">Repository for audiobook persistence and queries.</param>
        /// <param name="imageCacheService">Service for caching and moving cover images.</param>
        /// <param name="logger">Logger instance for diagnostic messages.</param>
        /// <param name="dbContext">EF Core database context instance.</param>
        /// <param name="scopeFactory">Service scope factory used to create scoped services when required.</param>
        /// <param name="fileNamingService">Service responsible for applying file naming patterns.</param>
        /// <param name="scanQueueService">Optional background scan queue service for asynchronous scans.</param>
        /// <param name="moveQueueService">Optional background move queue service for processing move requests.</param>
        /// <param name="notificationService">Service for sending webhook notifications.</param>
        public LibraryController(
            IAudiobookRepository repo,
            IImageCacheService imageCacheService,
            ILogger<LibraryController> logger,
            ListenArrDbContext dbContext,
            IServiceScopeFactory scopeFactory,
            IFileNamingService fileNamingService,
            IScanQueueService? scanQueueService = null,
            IMoveQueueService? moveQueueService = null,
            NotificationService? notificationService = null)
        {
            _repo = repo;
            _imageCacheService = imageCacheService;
            _logger = logger;
            _dbContext = dbContext;
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _fileNamingService = fileNamingService;
            _scanQueueService = scanQueueService;
            _moveQueueService = moveQueueService;
            _notificationService = notificationService;
        }

        public class ScanRequest
        {
            public string? Path { get; set; }
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToLibrary([FromBody] AddToLibraryRequest request)
        {
            var metadata = request.Metadata;

            _logger.LogInformation("AddToLibrary received metadata: Title={Title}, Asin={Asin}, PublishYear={PublishYear}, Authors={Authors}, Series={Series}",
                metadata.Title, metadata.Asin, metadata.PublishYear,
                metadata.Authors != null ? string.Join(", ", metadata.Authors) : "null",
                metadata.Series);

            // If metadata doesn't have PublishYear but we have search result with publishedDate, try to extract year
            if (string.IsNullOrWhiteSpace(metadata.PublishYear) && request.SearchResult != null)
            {
                try
                {
                    if (DateTime.TryParse(request.SearchResult.PublishedDate, out var publishDate))
                    {
                        metadata.PublishYear = publishDate.Year.ToString();
                        _logger.LogInformation("Extracted publish year from search result publishedDate: {Year}", metadata.PublishYear);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse PublishedDate as DateTime: {PublishedDate}", request.SearchResult.PublishedDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract publish year from search result publishedDate");
                }
            }

            // Check if audiobook already exists in library
            if (!string.IsNullOrEmpty(metadata.Asin))
            {
                var existingByAsin = await _repo.GetByAsinAsync(metadata.Asin);
                if (existingByAsin != null)
                {
                    return Conflict(new { message = "Audiobook already exists in library", audiobook = existingByAsin });
                }
            }

            if (!string.IsNullOrEmpty(metadata.Isbn))
            {
                var existingByIsbn = await _repo.GetByIsbnAsync(metadata.Isbn);
                if (existingByIsbn != null)
                {
                    return Conflict(new { message = "Audiobook already exists in library", audiobook = existingByIsbn });
                }
            }

            // Move image from temp cache to permanent library storage
            string? imageUrl = metadata.ImageUrl;
            if (!string.IsNullOrEmpty(metadata.Asin))
            {
                try
                {
                    var libraryImagePath = await _imageCacheService.MoveToLibraryStorageAsync(metadata.Asin);
                    if (libraryImagePath != null)
                    {
                        imageUrl = $"/{libraryImagePath}";
                        _logger.LogInformation("Moved image for ASIN {Asin} to permanent library storage", metadata.Asin);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to move image for ASIN {Asin}, image may not be in temp cache", metadata.Asin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error moving image for ASIN {Asin} to library storage", metadata.Asin);
                    // Continue with original image URL if move fails
                }
            }
            else if (!string.IsNullOrEmpty(metadata.ImageUrl))
            {
                // No ASIN available; attempt to move/download the image using a derived key
                try
                {
                    var rawKey = request.SearchResult?.Id ?? request.SearchResult?.ResultUrl ?? request.SearchResult?.ProductUrl ?? metadata.ImageUrl;
                    var derivedKey = "img-" + ComputeShortHash(rawKey);
                    var libraryImagePath = await _imageCacheService.MoveToLibraryStorageAsync(derivedKey, metadata.ImageUrl);
                    if (libraryImagePath != null)
                    {
                        imageUrl = $"/{libraryImagePath}";
                        _logger.LogInformation("Moved image for derived key {Key} to permanent library storage", derivedKey);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to move image for derived key {Key}, image may not be reachable", derivedKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error moving image for derived key when ASIN is missing");
                }
            }

            // Convert metadata to Audiobook entity and save to database
            var audiobook = new Audiobook
            {
                Title = metadata.Title,
                Subtitle = metadata.Subtitle,
                Authors = (metadata.Authors != null && metadata.Authors.Any()) ? metadata.Authors :
                          (!string.IsNullOrWhiteSpace(metadata.Author) ? new List<string> { metadata.Author! } : new List<string>()),
                ImageUrl = imageUrl,
                // Persist OpenLibrary ID when present (enables OL-only matching in the UI)
                OpenLibraryId = metadata.OpenLibraryId ?? request.SearchResult?.Id,
                PublishYear = metadata.PublishYear,
                Series = metadata.Series,
                SeriesNumber = metadata.SeriesNumber,
                Description = metadata.Description,
                Genres = metadata.Genres,
                Tags = metadata.Tags,
                Narrators = (metadata.Narrators != null && metadata.Narrators.Any()) ? metadata.Narrators :
                            (!string.IsNullOrWhiteSpace(metadata.Narrator) ? new List<string> { metadata.Narrator! } : new List<string>()),
                Isbn = metadata.Isbn,
                Asin = metadata.Asin,
                Publisher = metadata.Publisher,
                Language = metadata.Language,
                Runtime = metadata.Runtime,
                Version = metadata.Version,
                Explicit = metadata.Explicit,
                Abridged = metadata.Abridged,
                Monitored = request.Monitored  // Use custom monitored setting
            };

            _logger.LogInformation("Created Audiobook entity: Title={Title}, Asin={Asin}, PublishYear={PublishYear}",
                audiobook.Title, audiobook.Asin, audiobook.PublishYear);

            // Assign quality profile - use custom if provided, otherwise default
            if (request.QualityProfileId.HasValue)
            {
                audiobook.QualityProfileId = request.QualityProfileId.Value;
                _logger.LogInformation("Assigned custom quality profile ID {ProfileId} to new audiobook '{Title}'",
                    request.QualityProfileId.Value, audiobook.Title);
            }
            else
            {
                // Assign default quality profile to new audiobooks
                using (var scope = _scopeFactory.CreateScope())
                {
                    var qualityProfileService = scope.ServiceProvider.GetRequiredService<IQualityProfileService>();
                    var defaultProfile = await qualityProfileService.GetDefaultAsync();
                    if (defaultProfile != null)
                    {
                        audiobook.QualityProfileId = defaultProfile.Id;
                        _logger.LogInformation("Assigned default quality profile '{ProfileName}' (ID: {ProfileId}) to new audiobook '{Title}'",
                            defaultProfile.Name, defaultProfile.Id, audiobook.Title);
                    }
                    else
                    {
                        _logger.LogWarning("No default quality profile found. New audiobook '{Title}' will not have a quality profile assigned.", audiobook.Title);
                    }
                }
            }

            await _repo.AddAsync(audiobook);

            // Resolve author ASINs and cache author images via Audimeta when possible
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var audimeta = scope.ServiceProvider.GetRequiredService<AudimetaService>();

                if (audiobook.Authors != null && audiobook.Authors.Any())
                {
                    audiobook.AuthorAsins = audiobook.AuthorAsins ?? new List<string>();
                    foreach (var authorName in audiobook.Authors)
                    {
                        try
                        {
                            var info = await audimeta.LookupAuthorAsync(authorName);
                            if (info != null && !string.IsNullOrWhiteSpace(info.Asin))
                            {
                                // Avoid duplicates
                                if (!audiobook.AuthorAsins.Contains(info.Asin))
                                {
                                    audiobook.AuthorAsins.Add(info.Asin);
                                }

                                // Ensure author image is cached in authors folder (will download if necessary)
                                try
                                {
                                    var moved = await _imageCacheService.MoveToAuthorLibraryStorageAsync(info.Asin, info.Image);
                                    if (moved != null)
                                    {
                                        _logger.LogInformation("Cached author image for {Author} (ASIN: {Asin})", authorName, info.Asin);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to cache author image for {Author}", authorName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Author lookup failed for {Author}", authorName);
                        }
                    }

                    // Persist any updated author ASINs
                    try
                    {
                        _dbContext.Audiobooks.Update(audiobook);
                        await _dbContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to persist author ASINs for audiobook '{Title}'", audiobook.Title);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resolving author ASINs for audiobook '{Title}'", audiobook.Title);
            }

            // Send notification if configured
            if (_notificationService != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var settings = await configService.GetApplicationSettingsAsync();
                var data = new
                {
                    id = audiobook.Id,
                    title = audiobook.Title ?? "Unknown Title",
                    authors = audiobook.Authors,
                    narrators = audiobook.Narrators,
                    description = audiobook.Description,
                    asin = audiobook.Asin,
                    publisher = audiobook.Publisher,
                    year = audiobook.PublishYear,
                    imageUrl = audiobook.ImageUrl
                };
                await _notificationService.SendNotificationAsync("book-added", data, settings.WebhookUrl, settings.EnabledNotificationTriggers);
            }


            // Create the expected directory structure for the audiobook (but don't set FilePath)
            // FilePath should only be set when actual files are found during scanning
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                    var settings = await configService.GetApplicationSettingsAsync();

                    // Determine root for base directory: prefer explicit DestinationPath if provided, otherwise use configured OutputPath
                    var rootForBasePath = !string.IsNullOrEmpty(request.DestinationPath) ? request.DestinationPath : settings.OutputPath;

                    if (!string.IsNullOrEmpty(rootForBasePath))
                    {
                        // If caller supplied an explicit DestinationPath that looks like a full path, respect it as the final BasePath.
                        // The frontend will send the fully-composed destination when the user edits the relative path, so honor that exact value.
                        if (!string.IsNullOrEmpty(request.DestinationPath) && Path.IsPathRooted(request.DestinationPath))
                        {
                            try
                            {
                                if (!Directory.Exists(request.DestinationPath))
                                {
                                    Directory.CreateDirectory(request.DestinationPath);
                                    _logger.LogInformation("Created directory for new audiobook '{Title}' (explicit DestinationPath): {Path}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(request.DestinationPath));
                                }
                                else
                                {
                                    _logger.LogInformation("Directory already exists for new audiobook '{Title}' (explicit DestinationPath): {Path}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(request.DestinationPath));
                                }

                                audiobook.BasePath = request.DestinationPath;
                                _dbContext.Audiobooks.Update(audiobook);
                                await _dbContext.SaveChangesAsync();
                                _logger.LogInformation("Set BasePath for new audiobook '{Title}' to explicit DestinationPath: {BasePath}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(request.DestinationPath));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist explicit DestinationPath for new audiobook '{Title}'", LogRedaction.SanitizeText(audiobook.Title));
                            }
                        }
                        else
                        {
                            // Compute expected base directory from root + file naming pattern using the request-supplied metadata
                            // (use a temporary Audiobook built from the incoming metadata to ensure enriched fields are applied)
                            var tempForNaming = new Audiobook
                            {
                                Title = request.Metadata?.Title,
                                Authors = (request.Metadata?.Authors != null && request.Metadata.Authors.Any())
                                            ? request.Metadata.Authors
                                            : (!string.IsNullOrWhiteSpace(request.Metadata?.Author)
                                                ? new List<string> { request.Metadata.Author! }
                                                : null),
                                Series = request.Metadata?.Series,
                                SeriesNumber = request.Metadata?.SeriesNumber,
                                PublishYear = request.Metadata?.PublishYear
                            };

                            var directoryPath = ComputeAudiobookBaseDirectoryFromPattern(tempForNaming, rootForBasePath, settings.FileNamingPattern);

                            // Create the directory if it doesn't exist
                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                                _logger.LogInformation("Created directory for new audiobook '{Title}': {Path}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(directoryPath));
                            }
                            else
                            {
                                _logger.LogInformation("Directory already exists for new audiobook '{Title}': {Path}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(directoryPath));
                            }

                            // Persist a sensible BasePath for this audiobook so the UI can display
                            // the intended library root right away (even before any files exist).
                            try
                            {
                                audiobook.BasePath = directoryPath;
                                _dbContext.Audiobooks.Update(audiobook);
                                await _dbContext.SaveChangesAsync();
                                _logger.LogInformation("Set BasePath for new audiobook '{Title}' using naming pattern: {BasePath}", LogRedaction.SanitizeText(audiobook.Title), LogRedaction.SanitizeFilePath(directoryPath));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist BasePath for new audiobook '{Title}'", LogRedaction.SanitizeText(audiobook.Title));
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No output path configured, skipping directory creation for new audiobook '{Title}'", audiobook.Title);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create directory for new audiobook '{Title}'", audiobook.Title);
                // Continue with the rest of the process even if directory creation fails
            }

            // Log history entry for the added audiobook
            var historyEntry = new History
            {
                AudiobookId = audiobook.Id,
                AudiobookTitle = audiobook.Title ?? "Unknown Title",
                EventType = "Added",
                Message = $"Audiobook '{audiobook.Title}' added to library from Add New page",
                Source = "AddNew",
                Timestamp = DateTime.UtcNow
            };

            _dbContext.History.Add(historyEntry);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added audiobook '{Title}' (ASIN: {Asin}) to library with Monitored={Monitored}, QualityProfileId={QualityProfileId}, AutoSearch={AutoSearch}",
                audiobook.Title, audiobook.Asin, request.Monitored, audiobook.QualityProfileId, request.AutoSearch);

            return Ok(new { message = "Audiobook added to library successfully", audiobook });
        }

        [HttpPost("preview-path")]
        public async Task<IActionResult> PreviewPath([FromBody] PreviewPathRequest request)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var settings = await configService.GetApplicationSettingsAsync();

                var root = !string.IsNullOrEmpty(request.DestinationRoot) ? request.DestinationRoot : settings.OutputPath;

                // Build a temporary Audiobook to feed naming pattern logic
                var temp = new Audiobook
                {
                    Title = request.Metadata.Title,
                    Authors = request.Metadata.Authors,
                    Series = request.Metadata.Series,
                    SeriesNumber = request.Metadata.SeriesNumber,
                    PublishYear = request.Metadata.PublishYear
                };

                var full = ComputeAudiobookBaseDirectoryFromPattern(temp, root ?? string.Empty, settings.FileNamingPattern);

                var relative = full;
                if (!string.IsNullOrEmpty(root) && full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    relative = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                return Ok(new { fullPath = full, relativePath = relative, root = root });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute preview path");
                return StatusCode(500, new { message = "Failed to compute preview path" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Return audiobooks including files and an explicit 'wanted' flag
            List<Audiobook> audiobooks;
            try
            {
                audiobooks = await _dbContext.Audiobooks
                    .Include(a => a.QualityProfile)
                    .Include(a => a.Files)
                    .ToListAsync();
            }
            catch (JsonException jex)
            {
                // Defensive fallback: if a JSON-backed column contains legacy/non-JSON values
                // EF's JSON reader can throw during materialization. Retry without the
                // potentially-problematic navigation (QualityProfile) so the library view
                // can still render basic audiobook and file information.
                _logger.LogWarning(jex, "JSON parse error retrieving audiobooks; retrying without QualityProfile include to avoid malformed JSON in DB columns.");

                audiobooks = await _dbContext.Audiobooks
                    .Include(a => a.Files)
                    .ToListAsync();
            }

            var dto = audiobooks.Select(a => new
            {
                id = a.Id,
                title = a.Title,
                subtitle = a.Subtitle,
                authors = a.Authors,
                publishYear = a.PublishYear,
                series = a.Series,
                seriesNumber = a.SeriesNumber,
                description = a.Description,
                genres = a.Genres,
                tags = a.Tags,
                narrators = a.Narrators,
                isbn = a.Isbn,
                asin = a.Asin,
                openLibraryId = a.OpenLibraryId,
                publisher = a.Publisher,
                language = a.Language,
                runtime = a.Runtime,
                version = a.Version,
                @explicit = a.Explicit,
                abridged = a.Abridged,
                imageUrl = a.ImageUrl,
                filePath = a.FilePath,
                fileSize = a.FileSize,
                basePath = a.BasePath,
                monitored = a.Monitored,
                quality = a.Quality,
                qualityProfileId = a.QualityProfileId,
                files = a.Files?.Select(f => new
                {
                    id = f.Id,
                    path = f.Path,
                    size = f.Size,
                    durationSeconds = f.DurationSeconds,
                    format = f.Format,
                    bitrate = f.Bitrate,
                    sampleRate = f.SampleRate,
                    channels = f.Channels,
                    source = f.Source,
                    createdAt = f.CreatedAt
                }).ToList(),
                wanted = a.Monitored && (a.Files == null || !a.Files.Any())
            });

            return Ok(dto);
        }

        [HttpGet("by-asin/{asin}")]
        public async Task<IActionResult> GetByAsin(string asin)
        {
            var book = await _repo.GetByAsinAsync(asin);
            if (book == null) return NotFound();
            return Ok(book);
        }

        [HttpGet("by-isbn/{isbn}")]
        public async Task<IActionResult> GetByIsbn(string isbn)
        {
            var book = await _repo.GetByIsbnAsync(isbn);
            if (book == null) return NotFound();
            return Ok(book);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Audiobook>> GetAudiobook(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }
            // Include QualityProfile and Files in the query
            var updated = await _dbContext.Audiobooks
                .Include(a => a.QualityProfile)
                .Include(a => a.Files)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (updated == null)
                return NotFound(new { message = "Audiobook not found" });

            var audiobookDto = new
            {
                id = updated.Id,
                title = updated.Title,
                authors = updated.Authors,
                description = updated.Description,
                openLibraryId = updated.OpenLibraryId,
                imageUrl = updated.ImageUrl,
                filePath = updated.FilePath,
                fileSize = updated.FileSize,
                basePath = updated.BasePath,
                runtime = updated.Runtime,
                monitored = updated.Monitored,
                quality = updated.Quality,
                series = updated.Series,
                seriesNumber = updated.SeriesNumber,
                tags = updated.Tags,
                files = updated.Files?.Select(f => new
                {
                    id = f.Id,
                    path = f.Path,
                    size = f.Size,
                    durationSeconds = f.DurationSeconds,
                    format = f.Format,
                    bitrate = f.Bitrate,
                    sampleRate = f.SampleRate,
                    channels = f.Channels,
                    source = f.Source,
                    createdAt = f.CreatedAt
                }).ToList(),
                wanted = updated.Monitored && (updated.Files == null || !updated.Files.Any())
            };

            return Ok(audiobookDto);
        }

        // NOTE: Do not perform ad-hoc schema changes at runtime. Use EF Core migrations to modify the database schema.

        // DEBUG: Return raw AudiobookFile rows for an audiobook. Not intended for production use.
        [HttpGet("{id}/files-debug")]
        public async Task<IActionResult> GetAudiobookFilesDebug(int id)
        {
            var files = await _dbContext.AudiobookFiles.Where(f => f.AudiobookId == id).ToListAsync();
            return Ok(files);
        }

        // DEBUG: Scan JSON-backed TEXT columns for stored values that are clearly not JSON
        // Returns a list of offending rows per configured entity so we can diagnose deserialization errors.
        [HttpGet("debug/json-invalid")]
        public async Task<IActionResult> GetInvalidJsonColumns()
        {
            // Helper to test first non-whitespace char
            static bool LooksLikeJson(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return true; // empty is handled elsewhere
                var trimmed = s.TrimStart();
                if (trimmed.Length == 0) return true;
                var first = trimmed[0];
                if (first == '{' || first == '[' || first == '"' || first == 't' || first == 'f' || first == 'n' || first == '-' || char.IsDigit(first))
                    return true;
                return false;
            }

            var problems = new Dictionary<string, object?>();

            // QualityProfiles: Qualities, PreferredFormats, PreferredLanguages, MustContain, MustNotContain
            var qps = await _dbContext.QualityProfiles
                .Select(q => new
                {
                    q.Id,
                    Qualities = EF.Property<string>(q, "Qualities"),
                    PreferredFormats = EF.Property<string>(q, "PreferredFormats"),
                    PreferredLanguages = EF.Property<string>(q, "PreferredLanguages"),
                    MustContain = EF.Property<string>(q, "MustContain"),
                    MustNotContain = EF.Property<string>(q, "MustNotContain")
                })
                .ToListAsync();

            var qpProblems = qps.SelectMany(q => new[] {
                new { Table = "QualityProfiles.Qualities", Id = q.Id, Raw = q.Qualities },
                new { Table = "QualityProfiles.PreferredFormats", Id = q.Id, Raw = q.PreferredFormats },
                new { Table = "QualityProfiles.PreferredLanguages", Id = q.Id, Raw = q.PreferredLanguages },
                new { Table = "QualityProfiles.MustContain", Id = q.Id, Raw = q.MustContain },
                new { Table = "QualityProfiles.MustNotContain", Id = q.Id, Raw = q.MustNotContain }
            })
            .Where(x => !LooksLikeJson(x.Raw))
            .Select(x => new { x.Table, x.Id, Sample = (x.Raw ?? string.Empty).Substring(0, Math.Min(200, (x.Raw ?? string.Empty).Length)) })
            .ToList();

            problems["QualityProfiles"] = qpProblems;

            // Downloads.Metadata
            var downloads = await _dbContext.Downloads
                .Select(d => new { d.Id, Metadata = EF.Property<string>(d, "Metadata") })
                .ToListAsync();
            var dlProblems = downloads.Where(d => !LooksLikeJson(d.Metadata))
                .Select(d => new { Table = "Downloads.Metadata", d.Id, Sample = (d.Metadata ?? string.Empty).Substring(0, Math.Min(200, (d.Metadata ?? string.Empty).Length)) })
                .ToList();
            problems["Downloads"] = dlProblems;

            // DownloadProcessingJobs.JobData
            var jobs = await _dbContext.DownloadProcessingJobs
                .Select(j => new { j.Id, JobData = EF.Property<string>(j, "JobData") })
                .ToListAsync();
            var jobProblems = jobs.Where(j => !LooksLikeJson(j.JobData))
                .Select(j => new { Table = "DownloadProcessingJobs.JobData", j.Id, Sample = (j.JobData ?? string.Empty).Substring(0, Math.Min(200, (j.JobData ?? string.Empty).Length)) })
                .ToList();
            problems["DownloadProcessingJobs"] = jobProblems;

            // ApiConfigurations: HeadersJson, ParametersJson
            var apis = await _dbContext.ApiConfigurations
                .Select(a => new { a.Id, Headers = EF.Property<string>(a, "HeadersJson"), Parameters = EF.Property<string>(a, "ParametersJson") })
                .ToListAsync();
            var apiProblems = apis.SelectMany(a => new[] {
                new { Table = "ApiConfigurations.HeadersJson", Id = a.Id, Raw = a.Headers },
                new { Table = "ApiConfigurations.ParametersJson", Id = a.Id, Raw = a.Parameters }
            })
            .Where(x => !LooksLikeJson(x.Raw))
            .Select(x => new { x.Table, x.Id, Sample = (x.Raw ?? string.Empty).Substring(0, Math.Min(200, (x.Raw ?? string.Empty).Length)) })
            .ToList();
            problems["ApiConfigurations"] = apiProblems;

            return Ok(problems);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAudiobook(int id, [FromBody] Audiobook updatedAudiobook)
        {
            var existingAudiobook = await _repo.GetByIdAsync(id);
            if (existingAudiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }

            // Only update non-null properties to support partial updates
            if (updatedAudiobook.Title != null) existingAudiobook.Title = updatedAudiobook.Title;
            if (updatedAudiobook.Subtitle != null) existingAudiobook.Subtitle = updatedAudiobook.Subtitle;
            if (updatedAudiobook.Authors != null) existingAudiobook.Authors = updatedAudiobook.Authors;
            if (updatedAudiobook.ImageUrl != null) existingAudiobook.ImageUrl = updatedAudiobook.ImageUrl;
            if (updatedAudiobook.PublishYear != null) existingAudiobook.PublishYear = updatedAudiobook.PublishYear;
            if (updatedAudiobook.Series != null) existingAudiobook.Series = updatedAudiobook.Series;
            if (updatedAudiobook.SeriesNumber != null) existingAudiobook.SeriesNumber = updatedAudiobook.SeriesNumber;
            if (updatedAudiobook.Description != null) existingAudiobook.Description = updatedAudiobook.Description;
            if (updatedAudiobook.Genres != null) existingAudiobook.Genres = updatedAudiobook.Genres;
            if (updatedAudiobook.Tags != null) existingAudiobook.Tags = updatedAudiobook.Tags;
            if (updatedAudiobook.Narrators != null) existingAudiobook.Narrators = updatedAudiobook.Narrators;
            if (updatedAudiobook.Isbn != null) existingAudiobook.Isbn = updatedAudiobook.Isbn;
            if (updatedAudiobook.Asin != null) existingAudiobook.Asin = updatedAudiobook.Asin;
            if (updatedAudiobook.OpenLibraryId != null) existingAudiobook.OpenLibraryId = updatedAudiobook.OpenLibraryId;
            if (updatedAudiobook.Publisher != null) existingAudiobook.Publisher = updatedAudiobook.Publisher;
            if (updatedAudiobook.Language != null) existingAudiobook.Language = updatedAudiobook.Language;
            if (updatedAudiobook.Runtime != null) existingAudiobook.Runtime = updatedAudiobook.Runtime;
            if (updatedAudiobook.Version != null) existingAudiobook.Version = updatedAudiobook.Version;

            // Always update these fields as they have default values
            existingAudiobook.Explicit = updatedAudiobook.Explicit;
            existingAudiobook.Abridged = updatedAudiobook.Abridged;
            existingAudiobook.Monitored = updatedAudiobook.Monitored;

            if (updatedAudiobook.FilePath != null) existingAudiobook.FilePath = updatedAudiobook.FilePath;
            if (updatedAudiobook.FileSize.HasValue) existingAudiobook.FileSize = updatedAudiobook.FileSize;
            if (updatedAudiobook.Quality != null) existingAudiobook.Quality = updatedAudiobook.Quality;

            // Handle QualityProfileId - if -1 is sent, use default profile
            if (updatedAudiobook.QualityProfileId.HasValue)
            {
                if (updatedAudiobook.QualityProfileId.Value == -1)
                {
                    // -1 means "use default profile"
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var qualityProfileService = scope.ServiceProvider.GetRequiredService<IQualityProfileService>();
                        var defaultProfile = await qualityProfileService.GetDefaultAsync();
                        if (defaultProfile != null)
                        {
                            existingAudiobook.QualityProfileId = defaultProfile.Id;
                            _logger.LogInformation("Assigned default quality profile '{ProfileName}' (ID: {ProfileId}) to audiobook '{Title}'",
                                defaultProfile.Name, defaultProfile.Id, existingAudiobook.Title);
                        }
                        else
                        {
                            _logger.LogWarning("No default quality profile found. Audiobook '{Title}' quality profile set to null.", existingAudiobook.Title);
                            existingAudiobook.QualityProfileId = null;
                        }
                    }
                }
                else
                {
                    existingAudiobook.QualityProfileId = updatedAudiobook.QualityProfileId.Value;
                    _logger.LogInformation("Updated quality profile for audiobook '{Title}' to ID {ProfileId}",
                        existingAudiobook.Title, updatedAudiobook.QualityProfileId.Value);
                }
            }

            // Allow updating BasePath (destination) from the frontend when provided
            if (updatedAudiobook.BasePath != null)
            {
                existingAudiobook.BasePath = updatedAudiobook.BasePath;
                _logger.LogInformation("Updated BasePath for audiobook '{Title}' to: {BasePath}", LogRedaction.SanitizeText(existingAudiobook.Title), LogRedaction.SanitizeFilePath(updatedAudiobook.BasePath));
            }

            await _repo.UpdateAsync(existingAudiobook);

            _logger.LogInformation("Updated audiobook '{Title}' (ID: {Id})", existingAudiobook.Title, id);

            return Ok(new { message = "Audiobook updated successfully", audiobook = existingAudiobook });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAudiobook(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }

            // Delete associated image from cache if it exists
            try
            {
                // Prefer ASIN-based cleanup when available
                if (!string.IsNullOrEmpty(audiobook.Asin))
                {
                    var imagePath = await _imageCacheService.GetCachedImagePathAsync(audiobook.Asin);
                    if (imagePath != null)
                    {
                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            _logger.LogInformation("Deleted cached image for ASIN {Asin}", audiobook.Asin);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(audiobook.ImageUrl))
                {
                    // If ImageUrl points to our cached library folder, extract the filename and delete it
                    try
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(audiobook.ImageUrl, "/config/cache/images/library/(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var filename = match.Groups[1].Value;
                            var identifier = System.IO.Path.GetFileNameWithoutExtension(filename);
                            var imagePath = await _imageCacheService.GetCachedImagePathAsync(identifier);
                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                                if (System.IO.File.Exists(fullPath))
                                {
                                    System.IO.File.Delete(fullPath);
                                    _logger.LogInformation("Deleted cached image for identifier (from ImageUrl): {Identifier}", identifier);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cached image based on stored ImageUrl for audiobook id {Id}", audiobook.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cached image for audiobook id {Id}", audiobook.Id);
                // Continue with deletion even if image cleanup fails
            }

            var deleted = await _repo.DeleteByIdAsync(id);
            if (deleted)
            {
                return Ok(new { message = "Audiobook deleted successfully", id });
            }

            return StatusCode(500, new { message = "Failed to delete audiobook" });
        }

        [HttpPost("delete-bulk")]
        public async Task<IActionResult> BulkDeleteAudiobooks([FromBody] BulkDeleteRequest request)
        {
            if (request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "No audiobook IDs provided for bulk deletion" });
            }

            var deletedCount = 0;
            var deletedImagesCount = 0;
            var errors = new List<string>();
            var deletedIds = new List<int>();

            // Use a transaction to ensure all deletions succeed or all fail
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var id in request.Ids.Distinct())
                    {
                        try
                        {
                            var audiobook = await _repo.GetByIdAsync(id);
                            if (audiobook == null)
                            {
                                errors.Add($"Audiobook with ID {id} not found");
                                continue;
                            }

                            // Delete associated image from cache if it exists
                            try
                            {
                                if (!string.IsNullOrEmpty(audiobook.Asin))
                                {
                                    var imagePath = await _imageCacheService.GetCachedImagePathAsync(audiobook.Asin);
                                    if (imagePath != null)
                                    {
                                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                                        if (System.IO.File.Exists(fullPath))
                                        {
                                            System.IO.File.Delete(fullPath);
                                            deletedImagesCount++;
                                            _logger.LogInformation("Deleted cached image for ASIN {Asin}", audiobook.Asin);
                                        }
                                    }
                                }
                                else if (!string.IsNullOrEmpty(audiobook.ImageUrl))
                                {
                                    try
                                    {
                                        var match = System.Text.RegularExpressions.Regex.Match(audiobook.ImageUrl, "/config/cache/images/library/(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                        if (match.Success && match.Groups.Count > 1)
                                        {
                                            var filename = match.Groups[1].Value;
                                            var identifier = System.IO.Path.GetFileNameWithoutExtension(filename);
                                            var imagePath = await _imageCacheService.GetCachedImagePathAsync(identifier);
                                            if (!string.IsNullOrEmpty(imagePath))
                                            {
                                                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                                                if (System.IO.File.Exists(fullPath))
                                                {
                                                    System.IO.File.Delete(fullPath);
                                                    deletedImagesCount++;
                                                    _logger.LogInformation("Deleted cached image for identifier (from ImageUrl): {Identifier}", identifier);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to delete cached image based on stored ImageUrl for audiobook id {Id}", audiobook.Id);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete cached image for audiobook id {Id}", audiobook.Id);
                                // Continue with deletion even if image cleanup fails
                            }

                            // Log history entry for the deleted audiobook
                            var historyEntry = new History
                            {
                                AudiobookId = audiobook.Id,
                                AudiobookTitle = audiobook.Title ?? "Unknown Title",
                                EventType = "Deleted",
                                Message = $"Audiobook '{audiobook.Title}' deleted via bulk operation",
                                Source = "BulkDelete",
                                Timestamp = DateTime.UtcNow
                            };

                            _dbContext.History.Add(historyEntry);

                            var deleted = await _repo.DeleteByIdAsync(id);
                            if (deleted)
                            {
                                deletedCount++;
                                deletedIds.Add(id);
                                _logger.LogInformation("Deleted audiobook '{Title}' (ID: {Id}) via bulk operation", audiobook.Title, id);
                            }
                            else
                            {
                                errors.Add($"Failed to delete audiobook with ID {id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting audiobook with ID {Id}", id);
                            errors.Add($"Error deleting audiobook with ID {id}: {ex.Message}");
                        }
                    }

                    // Commit the transaction if we successfully deleted at least one audiobook
                    if (deletedCount > 0)
                    {
                        await transaction.CommitAsync();
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "No audiobooks were successfully deleted", errors });
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during bulk delete operation");
                    return StatusCode(500, new { message = "Bulk delete operation failed", error = ex.Message });
                }
            }

            object result;
            if (errors.Any())
            {
                result = new
                {
                    message = $"Partially successful: deleted {deletedCount} audiobook{(deletedCount != 1 ? "s" : "")}, {errors.Count} error{(errors.Count != 1 ? "s" : "")} occurred",
                    deletedCount,
                    deletedImagesCount,
                    ids = deletedIds,
                    errors
                };
            }
            else
            {
                result = new
                {
                    message = $"Successfully deleted {deletedCount} audiobook{(deletedCount != 1 ? "s" : "")}",
                    deletedCount,
                    deletedImagesCount,
                    ids = deletedIds
                };
            }

            return Ok(result);
        }

        [HttpPost("bulk-update")]
        public async Task<IActionResult> BulkUpdateAudiobooks([FromBody] BulkUpdateRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "No audiobook IDs provided for bulk update" });
            }

            var results = new List<object>();

            // Fetch application settings once for naming pattern when processing rootFolder changes
            ApplicationSettings? settings = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                settings = await configService.GetApplicationSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load application settings while performing bulk update");
            }

            foreach (var id in request.Ids.Distinct())
            {
                var entryErrors = new List<string>();
                var success = false;

                try
                {
                    var audiobook = await _repo.GetByIdAsync(id);
                    if (audiobook == null)
                    {
                        entryErrors.Add($"Audiobook with ID {id} not found");
                        results.Add(new { id, success, errors = entryErrors });
                        continue;
                    }

                    // Track whether any change was applied
                    var changed = false;

                    // Monitored
                    if (request.Updates != null && request.Updates.TryGetValue("monitored", out var monitoredObj))
                    {
                        try
                        {
                            bool monVal;
                            if (monitoredObj is JsonElement je)
                            {
                                monVal = je.ValueKind == JsonValueKind.True;
                            }
                            else
                            {
                                monVal = Convert.ToBoolean(monitoredObj);
                            }

                            audiobook.Monitored = monVal;
                            changed = true;
                            _logger.LogInformation("Set Monitored={Monitored} for audiobook id={Id}", monVal, id);

                            // History entry
                            _dbContext.History.Add(new History
                            {
                                AudiobookId = audiobook.Id,
                                AudiobookTitle = audiobook.Title ?? "Unknown",
                                EventType = "Updated",
                                Message = $"Monitored set to {monVal}",
                                Source = "BulkUpdate",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                        catch (Exception ex)
                        {
                            entryErrors.Add($"Invalid monitored value: {ex.Message}");
                        }
                    }

                    // QualityProfileId
                    if (request.Updates != null && request.Updates.TryGetValue("qualityProfileId", out var qpObj))
                    {
                        try
                        {
                            int qpVal;
                            if (qpObj is JsonElement jq)
                            {
                                qpVal = jq.GetInt32();
                            }
                            else
                            {
                                qpVal = Convert.ToInt32(qpObj);
                            }

                            audiobook.QualityProfileId = qpVal;
                            changed = true;
                            _logger.LogInformation("Set QualityProfileId={Profile} for audiobook id={Id}", qpVal, id);

                            _dbContext.History.Add(new History
                            {
                                AudiobookId = audiobook.Id,
                                AudiobookTitle = audiobook.Title ?? "Unknown",
                                EventType = "Updated",
                                Message = $"Quality profile set to {qpVal}",
                                Source = "BulkUpdate",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                        catch (Exception ex)
                        {
                            entryErrors.Add($"Invalid qualityProfileId value: {ex.Message}");
                        }
                    }

                    // Root folder change (rootFolder => path string)
                    if (request.Updates != null && request.Updates.TryGetValue("rootFolder", out var rootObj))
                    {
                        try
                        {
                            string? rootPath = null;
                            if (rootObj is JsonElement jr)
                            {
                                if (jr.ValueKind == JsonValueKind.String)
                                    rootPath = jr.GetString();
                            }
                            else if (rootObj != null)
                            {
                                rootPath = rootObj.ToString();
                            }

                            if (!string.IsNullOrWhiteSpace(rootPath))
                            {
                                // Use configured naming pattern to compute full base directory for this audiobook
                                var fileNamingPattern = settings?.FileNamingPattern ?? string.Empty;
                                var newBase = ComputeAudiobookBaseDirectoryFromPattern(audiobook, rootPath, fileNamingPattern);

                                try
                                {
                                    if (!Directory.Exists(newBase))
                                    {
                                        Directory.CreateDirectory(newBase);
                                        _logger.LogInformation("Created directory for audiobook id={Id} at {Path}", id, newBase);
                                    }

                                    audiobook.BasePath = newBase;
                                    changed = true;

                                    _dbContext.History.Add(new History
                                    {
                                        AudiobookId = audiobook.Id,
                                        AudiobookTitle = audiobook.Title ?? "Unknown",
                                        EventType = "Updated",
                                        Message = $"BasePath set to {newBase} via bulk update",
                                        Source = "BulkUpdate",
                                        Timestamp = DateTime.UtcNow
                                    });
                                }
                                catch (Exception ex)
                                {
                                    entryErrors.Add($"Failed to apply root folder for audiobook {id}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            entryErrors.Add($"Invalid rootFolder value: {ex.Message}");
                        }
                    }

                    // Persist updates for this audiobook
                    if (changed)
                    {
                        try
                        {
                            _dbContext.Audiobooks.Update(audiobook);
                            await _dbContext.SaveChangesAsync();
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            entryErrors.Add($"Failed to save changes for audiobook {id}: {ex.Message}");
                        }
                    }
                    else
                    {
                        entryErrors.Add("No valid updates provided for this audiobook");
                    }
                }
                catch (Exception ex)
                {
                    entryErrors.Add($"Unhandled error: {ex.Message}");
                }

                results.Add(new { id, success, errors = entryErrors });
            }

            return Ok(new { message = "Bulk update completed", results });
        }

        /// <summary>
        /// Scan the filesystem for files belonging to this audiobook, extract metadata (ffprobe) and persist AudiobookFile records.
        /// Optional body: { path: "C:\\some\\folder" } to scan a specific folder instead of the configured output path.
        /// </summary>
        [HttpPost("{id}/scan")]
        public async Task<IActionResult> ScanAudiobookFiles(int id, [FromBody] ScanRequest? request)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null) return NotFound(new { message = "Audiobook not found" });

            // If a background scan queue is available, enqueue the job and return Accepted
            if (_scanQueueService != null)
            {
                try
                {
                    var jobId = await _scanQueueService.EnqueueScanAsync(id, request?.Path);
                    _logger.LogInformation("Enqueued scan job {JobId} for audiobook {AudiobookId}", jobId, id);

                    // Broadcast initial job status via SignalR so clients can show queued state
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var hub = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
                        var job = new { jobId = jobId.ToString(), audiobookId = id, status = "Queued", enqueuedAt = DateTime.UtcNow };
                        await hub.Clients.All.SendAsync("ScanJobUpdate", job);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to broadcast ScanJobUpdate for job {JobId}", jobId);
                    }

                    return Accepted(new { message = "Scan enqueued", jobId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enqueue scan job for audiobook {AudiobookId}", id);
                    return StatusCode(500, new { message = "Failed to enqueue scan job", error = ex.Message });
                }
            }

            // Determine scan root: request.Path, audiobook.BasePath, or application settings output path
            string? scanRoot = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var settings = await configService.GetApplicationSettingsAsync();
                // If audiobook has a BasePath configured, always scan that path for safety
                // Do not fall back to the global output path when a BasePath is present.
                if (!string.IsNullOrEmpty(audiobook.BasePath))
                {
                    scanRoot = audiobook.BasePath;
                    _logger.LogDebug("Audiobook has BasePath; using it as scan root: {ScanRoot}", scanRoot);
                }
                else
                {
                    // No BasePath yet - allow explicit request path, otherwise fall back to configured output path
                    scanRoot = request?.Path ?? settings.OutputPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read application settings for scan; falling back to request path or basePath");
                // If BasePath exists prefer it, otherwise use request path (settings not available here)
                scanRoot = !string.IsNullOrEmpty(audiobook.BasePath) ? audiobook.BasePath : request?.Path;
            }

            if (string.IsNullOrEmpty(scanRoot) || !Directory.Exists(scanRoot))
            {
                return BadRequest(new { message = "Scan path not provided or does not exist", path = scanRoot });
            }

            _logger.LogInformation("Scanning for audiobook files for '{Title}' under: {Path}", audiobook.Title, scanRoot);

            // Build a simple matching predicate based on title and first author
            var titleToken = (audiobook.Title ?? string.Empty).Replace("\"", string.Empty).Trim();
            var authorToken = audiobook.Authors?.FirstOrDefault() ?? string.Empty;

            var foundFiles = new List<string>();
            try
            {
                // Search recursively but limit to common audio file extensions
                var exts = new[] { ".m4b", ".mp3", ".flac", ".ogg", ".opus", ".m4a", ".aac", ".wav" };

                // Iterative safe directory traversal to avoid unhandled IO/Access exceptions and handle special characters
                var dirs = new Stack<string>();
                dirs.Push(scanRoot);

                while (dirs.Count > 0)
                {
                    var dir = dirs.Pop();
                    try
                    {
                        var normalizedDir = Path.GetFullPath(dir);

                        foreach (var file in Directory.EnumerateFiles(normalizedDir))
                        {
                            try
                            {
                                var ext = Path.GetExtension(file);
                                if (!exts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                                var fname = Path.GetFileNameWithoutExtension(file);
                                if (!string.IsNullOrEmpty(titleToken) && fname.IndexOf(titleToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    foundFiles.Add(file);
                                    continue;
                                }
                                if (!string.IsNullOrEmpty(authorToken) && file.IndexOf(authorToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    foundFiles.Add(file);
                                    continue;
                                }
                            }
                            catch (Exception innerFileEx)
                            {
                                _logger.LogDebug(innerFileEx, "Skipped file while scanning {Dir}", normalizedDir);
                                continue;
                            }
                        }

                        foreach (var sub in Directory.EnumerateDirectories(normalizedDir))
                        {
                            dirs.Push(sub);
                        }
                    }
                    catch (System.IO.IOException ioEx)
                    {
                        _logger.LogWarning(ioEx, "IO error while enumerating directory during scan: {Dir}", dir);
                        continue;
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        _logger.LogWarning(uaEx, "Access denied while enumerating directory during scan: {Dir}", dir);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unexpected error while enumerating directory during scan: {Dir}", dir);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while scanning filesystem for audiobook files");
                return StatusCode(500, new { message = "Error scanning filesystem", error = ex.Message });
            }

            if (!foundFiles.Any())
            {
                return Ok(new { message = "No files found during scan", scannedPath = scanRoot, found = 0 });
            }

            // Calculate base path for the audiobook files
            var basePath = CalculateBasePath(foundFiles);
            _logger.LogInformation("Calculated base path for audiobook '{Title}': {BasePath}", audiobook.Title, basePath);

            var created = new List<AudiobookFile>();

            // Extract metadata and persist
            using (var scope = _scopeFactory.CreateScope())
            {
                var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

                foreach (var filePath in foundFiles)
                {
                    try
                    {
                        // Calculate relative path from base path
                        var relativePath = Path.GetRelativePath(basePath, filePath);

                        var existing = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == audiobook.Id && f.Path == relativePath);
                        if (existing != null)
                        {
                            _logger.LogInformation("Skipping existing AudiobookFile for audiobook {AudiobookId}: {Path}", audiobook.Id, relativePath);
                            continue;
                        }

                        AudioMetadata? meta = null;
                        try
                        {
                            meta = await metadataService.ExtractFileMetadataAsync(filePath);
                        }
                        catch (Exception mex)
                        {
                            _logger.LogWarning(mex, "Failed to extract metadata for file {File}", filePath);
                        }

                        var fi = new FileInfo(filePath);
                        var fileRecord = new AudiobookFile
                        {
                            AudiobookId = audiobook.Id,
                            Path = relativePath, // Store relative path
                            Size = fi.Length,
                            Source = "scan",
                            CreatedAt = DateTime.UtcNow,
                            DurationSeconds = meta?.Duration.TotalSeconds,
                            Format = meta?.Format,
                            Bitrate = meta?.Bitrate,
                            SampleRate = meta?.SampleRate,
                            Channels = meta?.Channels
                        };

                        db.AudiobookFiles.Add(fileRecord);
                        created.Add(fileRecord);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create AudiobookFile for {File}", filePath);
                    }
                }

                // Update audiobook base path only when we have a non-empty value.
                if (!string.IsNullOrEmpty(basePath))
                {
                    audiobook.BasePath = basePath;
                    await db.SaveChangesAsync();
                }

                // Add history entries for newly scanned files
                foreach (var fileRecord in created)
                {
                    var historyEntry = new History
                    {
                        AudiobookId = audiobook.Id,
                        AudiobookTitle = audiobook.Title ?? "Unknown",
                        EventType = "File Added",
                        Message = $"File scanned and added: {Path.GetFileName(fileRecord.Path)}",
                        Source = "Scan",
                        Data = JsonSerializer.Serialize(new
                        {
                            FilePath = fileRecord.Path,
                            FileSize = fileRecord.Size,
                            Format = fileRecord.Format,
                            Source = fileRecord.Source
                        }),
                        Timestamp = DateTime.UtcNow
                    };
                    db.History.Add(historyEntry);
                }
                await db.SaveChangesAsync();

                // Remove AudiobookFile DB rows for files that no longer exist on disk
                try
                {
                    var existingFiles = await db.AudiobookFiles
                        .Where(f => f.AudiobookId == audiobook.Id)
                        .ToListAsync();

                    var foundSet = new HashSet<string>(foundFiles.Select(f => Path.GetRelativePath(basePath, f)), StringComparer.OrdinalIgnoreCase);
                    var toRemove = existingFiles
                        .Where(f => f.Path != null && !foundSet.Contains(f.Path))
                        .ToList();

                    List<object> removedFilesDto = new();
                    if (toRemove.Count > 0)
                    {
                        foreach (var rem in toRemove)
                        {
                            try
                            {
                                removedFilesDto.Add(new { id = rem.Id, path = rem.Path });
                                db.AudiobookFiles.Remove(rem);
                                _logger.LogInformation("Removing missing AudiobookFile DB row Id={Id} Path={Path}", rem.Id, rem.Path);

                                // Add history entry for removed file
                                var historyEntry = new History
                                {
                                    AudiobookId = audiobook.Id,
                                    AudiobookTitle = audiobook.Title ?? "Unknown",
                                    EventType = "File Removed",
                                    Message = $"File removed (no longer exists): {Path.GetFileName(rem.Path)}",
                                    Source = "Scan",
                                    Data = JsonSerializer.Serialize(new
                                    {
                                        FilePath = rem.Path,
                                        FileSize = rem.Size,
                                        Format = rem.Format,
                                        Source = rem.Source
                                    }),
                                    Timestamp = DateTime.UtcNow
                                };
                                db.History.Add(historyEntry);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to remove AudiobookFile Id={Id} Path={Path}", rem.Id, rem.Path);
                            }
                        }

                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reconcile audiobook files after scan for audiobook {AudiobookId}", audiobook.Id);
                }

                // Handle legacy filePath field migration
                try
                {
                    var needsUpdate = false;
                    if (!string.IsNullOrEmpty(audiobook.FilePath))
                    {
                        // Check if the legacy filePath exists
                        if (System.IO.File.Exists(audiobook.FilePath))
                        {
                            // File exists - check if we already have an AudiobookFile record for it
                            var existingFileRecord = await db.AudiobookFiles
                                .FirstOrDefaultAsync(f => f.AudiobookId == audiobook.Id && f.Path == audiobook.FilePath);

                            if (existingFileRecord == null)
                            {
                                // Create AudiobookFile record for the legacy filePath
                                try
                                {
                                    using var afScope = _scopeFactory.CreateScope();
                                    var audioFileService = afScope.ServiceProvider.GetRequiredService<IAudioFileService>();
                                    var migrated = await audioFileService.EnsureAudiobookFileAsync(audiobook.Id, audiobook.FilePath, "scan-legacy");
                                    if (migrated)
                                    {
                                        _logger.LogInformation("Migrated legacy filePath to AudiobookFile record for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);
                                        created.Add(new AudiobookFile { Path = audiobook.FilePath }); // Add to created list for response
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to migrate legacy filePath for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);
                                }
                            }
                        }
                        else
                        {
                            // File doesn't exist - clear the legacy filePath and related fields
                            audiobook.FilePath = null;
                            audiobook.FileSize = null;
                            needsUpdate = true;
                            _logger.LogInformation("Cleared missing legacy filePath for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);

                            // Add history entry for cleared filePath
                            var historyEntry = new History
                            {
                                AudiobookId = audiobook.Id,
                                AudiobookTitle = audiobook.Title ?? "Unknown",
                                EventType = "File Removed",
                                Message = $"Legacy file path cleared (file no longer exists)",
                                Source = "Scan",
                                Data = JsonSerializer.Serialize(new
                                {
                                    FilePath = audiobook.FilePath,
                                    Source = "legacy-migration"
                                }),
                                Timestamp = DateTime.UtcNow
                            };
                            db.History.Add(historyEntry);
                        }
                    }

                    if (needsUpdate)
                    {
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to handle legacy filePath migration for audiobook {AudiobookId}", audiobook.Id);
                }

                // Reload audiobook with files to return
                var updated = await db.Audiobooks.Include(a => a.Files).FirstOrDefaultAsync(a => a.Id == audiobook.Id);

                // Send "book-available" notification if the audiobook is monitored and files were imported
                if (_notificationService != null && audiobook.Monitored && created.Count > 0)
                {
                    try
                    {
                        using var notificationScope = _scopeFactory.CreateScope();
                        var configService = notificationScope.ServiceProvider.GetRequiredService<IConfigurationService>();
                        var settings = await configService.GetApplicationSettingsAsync();
                        var availableData = new
                        {
                            id = audiobook.Id,
                            title = audiobook.Title ?? "Unknown Title",
                            authors = audiobook.Authors,
                            asin = audiobook.Asin,
                            imageUrl = audiobook.ImageUrl,
                            description = audiobook.Description,
                            monitored = audiobook.Monitored,
                            qualityProfileId = audiobook.QualityProfileId,
                            filesImported = created.Count,
                            totalFiles = updated?.Files?.Count ?? 0
                        };
                        await _notificationService.SendNotificationAsync("book-available", availableData, settings.WebhookUrl, settings.EnabledNotificationTriggers);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send book-available notification for audiobook {AudiobookId}", audiobook.Id);
                    }
                }

                return Ok(new { message = "Scan complete", scannedPath = scanRoot, found = foundFiles.Count, created = created.Count, audiobook = updated });
            }
        }

        /// <summary>
        /// Get in-memory scan job status by jobId (debugging/admin helper).
        /// </summary>
        [HttpGet("scan/{jobId}")]
        public IActionResult GetScanJobStatus(string jobId)
        {
            if (_scanQueueService == null) return NotFound(new { message = "Scan queue not available" });
            if (!Guid.TryParse(jobId, out var gid)) return BadRequest(new { message = "Invalid jobId" });
            if (_scanQueueService.TryGetJob(gid, out var job))
            {
                _logger.LogInformation("Queried scan job {JobId} status: {Status}", gid, job!.Status);
                return Ok(job);
            }
            return NotFound(new { message = "Job not found" });
        }

        [HttpPost("{id}/move")]
        public async Task<IActionResult> EnqueueMove(int id, [FromBody] MoveRequest request)
        {
            if (_moveQueueService == null) return NotFound(new { message = "Move queue not available" });
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null) return NotFound(new { message = "Audiobook not found" });

            if (string.IsNullOrWhiteSpace(request.DestinationPath))
            {
                return BadRequest(new { message = "DestinationPath is required" });
            }

            try
            {
                // If the path is not rooted, combine with configured output path
                using var scope = _scopeFactory.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var settings = await configService.GetApplicationSettingsAsync();

                var final = request.DestinationPath!;
                if (!Path.IsPathRooted(final))
                {
                    var root = settings.OutputPath ?? string.Empty;
                    final = Path.Combine(root, final.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }

                // If caller explicitly asked to change the DB without moving files, update the BasePath and return early.
                if (request.MoveFiles.HasValue && request.MoveFiles.Value == false)
                {
                    try
                    {
                        audiobook.BasePath = final;
                        _dbContext.Audiobooks.Update(audiobook);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Updated BasePath for audiobook {AudiobookId} without moving files: {BasePath}", id, final);
                        return Ok(new { message = "Destination updated" });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update BasePath for audiobook {AudiobookId}", id);
                        return StatusCode(500, new { message = "Failed to update BasePath", error = ex.Message });
                    }
                }

                // Determine source path snapshot to use for the move. Prefer an explicit source from the request
                // (the frontend should send the original source if it updated the audiobook BasePath before requesting a move),
                // otherwise fall back to the current audiobook.BasePath as a best-effort.
                var sourcePath = request is not null && !string.IsNullOrWhiteSpace(request.SourcePath)
                    ? request.SourcePath
                    : audiobook.BasePath;

                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    return BadRequest(new { message = "Source path not provided. Supply current source path in the Move request or ensure audiobook has a valid BasePath." });
                }

                // Validate source exists now to provide earlier feedback to clients (avoids enqueueing doomed jobs)
                if (!Directory.Exists(sourcePath))
                {
                    return BadRequest(new { message = "Source path does not exist. Ensure the audiobook's current BasePath exists or provide a valid SourcePath in the request." });
                }

                // Validate target parent is valid and writable (try to create if necessary)
                var targetParent = Path.GetDirectoryName(final);
                if (string.IsNullOrEmpty(targetParent))
                {
                    return BadRequest(new { message = "Invalid target path" });
                }
                try
                {
                    if (!Directory.Exists(targetParent)) Directory.CreateDirectory(targetParent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to access or create target parent {TargetParent}", targetParent);
                    return BadRequest(new { message = "Target parent path is not writable or unavailable" });
                }

                // If source and target are identical, nothing to do
                try
                {
                    var srcFull = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var tgtFull = Path.GetFullPath(final).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(srcFull, tgtFull, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { message = "Source and target paths are identical; nothing to move." });
                    }
                }
                catch
                {
                    // Ignore errors normalizing paths; background worker will fail if invalid
                }

                var jobId = await _moveQueueService.EnqueueMoveAsync(id, final, sourcePath);

                // Broadcast initial job status
                try
                {
                    using var hubScope = _scopeFactory.CreateScope();
                    var hub = hubScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
                    var job = new { jobId = jobId.ToString(), audiobookId = id, status = "Queued", enqueuedAt = DateTime.UtcNow };
                    await hub.Clients.All.SendAsync("MoveJobUpdate", job);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast MoveJobUpdate for job {JobId}", jobId);
                }

                return Accepted(new { message = "Move enqueued", jobId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue move job for audiobook {AudiobookId}", id);
                return StatusCode(500, new { message = "Failed to enqueue move job", error = ex.Message });
            }
        }

        [HttpGet("move/{jobId}")]
        public IActionResult GetMoveJobStatus(string jobId)
        {
            if (_moveQueueService == null) return NotFound(new { message = "Move queue not available" });
            if (!Guid.TryParse(jobId, out var gid)) return BadRequest(new { message = "Invalid jobId" });
            if (_moveQueueService.TryGetJob(gid, out var job))
            {
                _logger.LogInformation("Queried move job {JobId} status: {Status}", gid, job!.Status);
                return Ok(job);
            }
            return NotFound(new { message = "Job not found" });
        }

        [HttpPost("move/requeue/{jobId}")]
        public async Task<IActionResult> RequeueMoveJob(string jobId)
        {
            if (_moveQueueService == null) return NotFound(new { message = "Move queue not available" });
            if (!Guid.TryParse(jobId, out var gid)) return BadRequest(new { message = "Invalid jobId" });

            var newJobId = await _moveQueueService.RequeueMoveAsync(gid);
            if (newJobId == null)
            {
                return BadRequest(new { message = "Unable to requeue job (not found or invalid status)" });
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var hub = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
                var job = new { jobId = newJobId.ToString(), status = "Queued", enqueuedAt = DateTime.UtcNow };
                await hub.Clients.All.SendAsync("MoveJobUpdate", job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast MoveJobUpdate for requeued job {JobId}", newJobId);
            }

            return Accepted(new { message = "Requeued move job", jobId = newJobId });
        }

        [HttpPost("scan/requeue/{jobId}")]
        public async Task<IActionResult> RequeueScanJob(string jobId)
        {
            if (_scanQueueService == null) return NotFound(new { message = "Scan queue not available" });
            if (!Guid.TryParse(jobId, out var gid)) return BadRequest(new { message = "Invalid jobId" });

            var newJobId = await _scanQueueService.RequeueScanAsync(gid);
            if (newJobId == null)
            {
                return BadRequest(new { message = "Unable to requeue job (not found or invalid status)" });
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var hub = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
                var job = new { jobId = newJobId.ToString(), status = "Queued", enqueuedAt = DateTime.UtcNow };
                await hub.Clients.All.SendAsync("ScanJobUpdate", job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast ScanJobUpdate for requeued job {JobId}", newJobId);
            }

            return Accepted(new { message = "Requeued scan job", jobId = newJobId });
        }

        private async Task<int> ProcessAudiobookForSearchAsync(
            Audiobook audiobook,
            ISearchService searchService,
            IQualityProfileService qualityProfileService,
            IDownloadService downloadService,
            ListenArrDbContext dbContext)
        {
            // Check if quality cutoff is already met
            if (await IsQualityCutoffMetAsync(audiobook, qualityProfileService, dbContext))
            {
                _logger.LogInformation("Quality cutoff already met for audiobook '{Title}', skipping search", audiobook.Title);
                return 0;
            }

            // Build search query
            var searchQuery = BuildSearchQuery(audiobook);
            _logger.LogInformation("Searching for audiobook '{Title}' with query: {Query}", audiobook.Title, searchQuery);

            // Search for results
            var searchResults = await searchService.SearchAsync(searchQuery);
            _logger.LogInformation("Found {Count} raw search results for audiobook '{Title}'", searchResults.Count, audiobook.Title);

            // Broadcast raw search result summary for manual-triggered searches (helpful for debugging)
            try
            {
                var rawSummaries = searchResults.Take(10).Select(r => new
                {
                    title = r.Title,
                    asin = r.Asin,
                    source = r.Source,
                    sizeMB = r.Size > 0 ? (r.Size / 1024 / 1024) : -1,
                    seeders = r.Seeders,
                    format = r.Format,
                    downloadType = r.DownloadType
                }).ToList();

                using var scope = _scopeFactory.CreateScope();
                var hub = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
                // Include a structured payload so clients can distinguish manual vs automatic searches
                await hub.Clients.All.SendCoreAsync("SearchProgress", new object[] { new { message = $"Manual search query: {searchQuery}", details = new { rawCount = searchResults.Count, rawSamples = rawSummaries }, type = "interactive", audiobookId = audiobook.Id } });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast raw search results summary for manual search audiobook {Id}", audiobook.Id);
            }

            if (!searchResults.Any())
            {
                _logger.LogInformation("No search results found for audiobook '{Title}'", audiobook.Title);
                return 0;
            }

            // Score results against quality profile
            var scoredResults = await qualityProfileService.ScoreSearchResults(searchResults, audiobook.QualityProfile!);

            // Log all scored results for debugging
            _logger.LogInformation("Scored {Count} search results for audiobook '{Title}':", scoredResults.Count, audiobook.Title);
            foreach (var scoredResult in scoredResults.OrderByDescending(s => s.TotalScore))
            {
                var status = scoredResult.IsRejected ? "REJECTED" : (scoredResult.TotalScore > 0 ? "ACCEPTABLE" : "LOW SCORE");
                _logger.LogInformation("  [{Status}] Score: {Score} | Title: {Title} | Source: {Source} | Size: {Size}MB | Seeders: {Seeders} | Quality: {Quality}",
                    status, scoredResult.TotalScore, scoredResult.SearchResult.Title, scoredResult.SearchResult.Source,
                    scoredResult.SearchResult.Size / 1024 / 1024, scoredResult.SearchResult.Seeders, scoredResult.SearchResult.Quality);

                if (scoredResult.IsRejected && scoredResult.RejectionReasons.Any())
                {
                    _logger.LogInformation("    Rejection reasons: {Reasons}", string.Join(", ", scoredResult.RejectionReasons));
                }
            }

            var topResult = scoredResults
                .Where(s => !s.IsRejected && s.TotalScore > 0) // Only results that pass quality filters and are not rejected
                .OrderByDescending(s => s.TotalScore)
                .FirstOrDefault(); // Pick only the top scoring result

            if (topResult == null)
            {
                _logger.LogInformation("No acceptable search results found for audiobook '{Title}' after quality filtering", audiobook.Title);
                return 0;
            }

            _logger.LogInformation("Found top result for audiobook '{Title}': {ResultTitle} (Score: {Score})",
                audiobook.Title, topResult.SearchResult.Title, topResult.TotalScore);

            // Add score to the search result for tracking
            topResult.SearchResult.Score = topResult.TotalScore;

            // Queue download for the top result
            var downloadsQueued = 0;
            try
            {
                // Determine appropriate download client for this result
                var isTorrent = IsTorrentResult(topResult.SearchResult);
                var downloadClientId = await GetAppropriateDownloadClientAsync(topResult.SearchResult, isTorrent);

                if (string.IsNullOrEmpty(downloadClientId))
                {
                    _logger.LogWarning("No suitable download client found for result type: {Type}", isTorrent ? "torrent" : "NZB");
                    return 0;
                }

                await downloadService.StartDownloadAsync(topResult.SearchResult, downloadClientId, audiobook.Id);
                downloadsQueued++;

                _logger.LogInformation("Queued download for audiobook '{Title}': {ResultTitle} (Score: {Score})",
                    audiobook.Title, topResult.SearchResult.Title, topResult.TotalScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue download for audiobook '{Title}': {ResultTitle}",
                    audiobook.Title, topResult.SearchResult.Title);
            }

            return downloadsQueued;
        }

        private async Task<bool> IsQualityCutoffMetAsync(
            Audiobook audiobook,
            IQualityProfileService qualityProfileService,
            ListenArrDbContext dbContext)
        {
            if (audiobook.QualityProfile == null)
                return false;

            // Get existing downloads for this audiobook
            var existingDownloads = await dbContext.Downloads
                .Where(d => d.AudiobookId == audiobook.Id &&
                           (d.Status == DownloadStatus.Completed || d.Status == DownloadStatus.Downloading))
                .ToListAsync();

            // Get existing files for this audiobook
            var existingFiles = await dbContext.AudiobookFiles
                .Where(f => f.AudiobookId == audiobook.Id)
                .ToListAsync();

            if (!existingDownloads.Any() && !existingFiles.Any())
                return false;

            // Check if any existing download meets or exceeds the cutoff quality
            var cutoffQuality = audiobook.QualityProfile.Qualities
                .FirstOrDefault(q => q.Quality == audiobook.QualityProfile.CutoffQuality);

            if (cutoffQuality == null)
                return false;

            // Check downloads first
            foreach (var download in existingDownloads)
            {
                // For completed downloads, check if the file quality meets cutoff
                if (download.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(download.Metadata?.GetValueOrDefault("Quality")?.ToString()))
                {
                    var downloadQuality = download.Metadata["Quality"].ToString();
                    var downloadQualityDefinition = audiobook.QualityProfile.Qualities
                        .FirstOrDefault(q => q.Quality == downloadQuality);

                    if (downloadQualityDefinition != null && downloadQualityDefinition.Priority >= cutoffQuality.Priority)
                    {
                        _logger.LogDebug("Quality cutoff met for audiobook '{Title}' by completed download (Quality: {Quality})",
                            audiobook.Title, downloadQuality);
                        return true;
                    }
                }
                // For active downloads, assume they will meet quality requirements
                else if (download.Status == DownloadStatus.Downloading)
                {
                    _logger.LogDebug("Quality cutoff assumed met for audiobook '{Title}' due to active download", audiobook.Title);
                    return true;
                }
            }

            // Check existing files
            foreach (var file in existingFiles)
            {
                var fileQuality = DetermineFileQuality(file);
                if (!string.IsNullOrEmpty(fileQuality))
                {
                    var fileQualityDefinition = audiobook.QualityProfile.Qualities
                        .FirstOrDefault(q => q.Quality == fileQuality);

                    if (fileQualityDefinition != null && fileQualityDefinition.Priority >= cutoffQuality.Priority)
                    {
                        _logger.LogDebug("Quality cutoff met for audiobook '{Title}' by existing file (Quality: {Quality}, File: {FileName})",
                            audiobook.Title, fileQuality, Path.GetFileName(file.Path));
                        return true;
                    }
                }
            }

            return false;
        }

        private string? DetermineFileQuality(AudiobookFile file)
        {
            // Determine quality based on file properties
            // This mirrors the logic in QualityProfileService.GetQualityScore but works with file metadata

            // Check format/container first
            if (!string.IsNullOrEmpty(file.Container))
            {
                var container = file.Container.ToLower();
                if (container.Contains("flac")) return "FLAC";
                if (container.Contains("m4b") || container.Contains("m4a")) return "M4B";
            }

            if (!string.IsNullOrEmpty(file.Format))
            {
                var format = file.Format.ToLower();
                if (format.Contains("flac")) return "FLAC";
                if (format.Contains("m4b") || format.Contains("m4a")) return "M4B";
                if (format.Contains("aac")) return "M4B"; // AAC in M4B container
            }

            // Check bitrate for MP3 quality determination
            if (file.Bitrate.HasValue)
            {
                var bitrate = file.Bitrate.Value;

                // Convert bits per second to kilobits per second for easier comparison
                var kbps = bitrate / 1000;

                if (kbps >= 320) return "MP3 320kbps";
                if (kbps >= 256) return "MP3 256kbps";
                if (kbps >= 192) return "MP3 192kbps";
                if (kbps >= 128) return "MP3 128kbps";
                if (kbps >= 64) return "MP3 64kbps";

                // For very low bitrates, still classify as MP3
                return "MP3 64kbps";
            }

            // Check codec
            if (!string.IsNullOrEmpty(file.Codec))
            {
                var codec = file.Codec.ToLower();
                if (codec.Contains("flac")) return "FLAC";
                if (codec.Contains("aac")) return "M4B";
                if (codec.Contains("mp3")) return "MP3 128kbps"; // Default MP3 quality if no bitrate info
                if (codec.Contains("opus")) return "M4B"; // Opus is often in M4B containers
            }

            // If we can't determine quality from metadata, try to infer from file extension
            if (!string.IsNullOrEmpty(file.Path))
            {
                var extension = Path.GetExtension(file.Path).ToLower();
                switch (extension)
                {
                    case ".flac":
                        return "FLAC";
                    case ".m4b":
                    case ".m4a":
                        return "M4B";
                    case ".mp3":
                        return "MP3 128kbps"; // Conservative default for MP3
                    case ".aac":
                        return "M4B";
                    case ".opus":
                        return "M4B";
                }
            }

            return null; // Unable to determine quality
        }

        private string BuildSearchQuery(Audiobook audiobook)
        {
            var parts = new List<string>();

            // Add title
            if (!string.IsNullOrEmpty(audiobook.Title))
                parts.Add(audiobook.Title);

            // Add primary author
            if (audiobook.Authors != null && audiobook.Authors.Any())
                parts.Add(audiobook.Authors.First());

            // Add series if available
            if (!string.IsNullOrEmpty(audiobook.Series))
                parts.Add(audiobook.Series);

            return string.Join(" ", parts);
        }

        private bool IsTorrentResult(SearchResult result)
        {
            // Check DownloadType first if it's set
            if (!string.IsNullOrEmpty(result.DownloadType))
            {
                if (result.DownloadType == "DDL")
                {
                    return false; // DDL is not a torrent
                }
                else if (result.DownloadType == "Torrent")
                {
                    return true;
                }
                else if (result.DownloadType == "Usenet")
                {
                    return false;
                }
            }

            // Fallback to legacy detection logic
            // Check for NZB first - if it has an NZB URL, it's a Usenet/NZB download
            if (!string.IsNullOrEmpty(result.NzbUrl))
            {
                return false;
            }

            // Check for torrent indicators - magnet link or torrent file
            if (!string.IsNullOrEmpty(result.MagnetLink) || !string.IsNullOrEmpty(result.TorrentUrl))
            {
                return true;
            }

            // If neither is set, we can't reliably determine the type
            // Log a warning and default to false (NZB) as a safer choice
            _logger.LogWarning("Unable to determine result type for '{Title}' from source '{Source}'. No MagnetLink, TorrentUrl, or NzbUrl found. Defaulting to NZB.",
                result.Title, result.Source);
            return false;
        }

        private async Task<string> GetAppropriateDownloadClientAsync(SearchResult searchResult, bool isTorrent)
        {
            using var scope = _scopeFactory.CreateScope();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

            // Special handling for DDL downloads - they don't use external clients
            if (searchResult.DownloadType?.Equals("DDL", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation("DDL download detected, using internal DDL client");
                return "DDL";
            }

            // Get all configured download clients
            var clients = await configurationService.GetDownloadClientConfigurationsAsync();
            var enabledClients = clients.Where(c => c.IsEnabled).ToList();

            _logger.LogInformation("Looking for {ClientType} client. Found {Count} enabled download clients: {Clients}",
                isTorrent ? "torrent" : "NZB",
                enabledClients.Count,
                string.Join(", ", enabledClients.Select(c => $"{c.Name} ({c.Type})")));

            if (isTorrent)
            {
                // Prefer qBittorrent, then Transmission
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("qbittorrent", StringComparison.OrdinalIgnoreCase))
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("transmission", StringComparison.OrdinalIgnoreCase));

                if (client != null)
                {
                    _logger.LogInformation("Selected torrent client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No torrent client (qBittorrent or Transmission) found among enabled clients");
                }

                return client?.Id ?? string.Empty;
            }
            else
            {
                // Prefer SABnzbd, then NZBGet
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("sabnzbd", StringComparison.OrdinalIgnoreCase))
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("nzbget", StringComparison.OrdinalIgnoreCase));

                if (client != null)
                {
                    _logger.LogInformation("Selected NZB client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No NZB client (SABnzbd or NZBGet) found among enabled clients");
                }

                return client?.Id ?? string.Empty;
            }
        }

        // Helper to convert incoming update values (possibly JsonElement or boxed types) to the target property type
        private static object? ConvertUpdateValue(object? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType == typeof(string)) return string.Empty;
                if (targetType.IsValueType) return Activator.CreateInstance(targetType);
                return null;
            }

            // Unwrap JsonElement if present (from System.Text.Json)
            if (value is JsonElement je)
            {
                try
                {
                    if (je.ValueKind == JsonValueKind.Number && (targetType == typeof(int) || targetType == typeof(int?)))
                        return je.GetInt32();
                    if (je.ValueKind == JsonValueKind.Number && targetType == typeof(double))
                        return je.GetDouble();
                    if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                        return je.GetBoolean();
                    if (je.ValueKind == JsonValueKind.String)
                        return je.GetString();
                    // Fall back to raw string
                    return je.GetRawText();
                }
                catch
                {
                    // continue to other conversion attempts
                }
            }

            // Handle nullable types
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Enums
            if (underlying.IsEnum)
            {
                if (value is string s)
                    return Enum.Parse(underlying, s, true);
                return Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying)));
            }

            // If value already matches
            if (underlying.IsInstanceOfType(value))
                return value;

            // Try Convert.ChangeType on primitives
            try
            {
                return Convert.ChangeType(value, underlying);
            }
            catch
            {
                // Final fallback: attempt parse from string
                var str = value.ToString();
                if (underlying == typeof(int) && int.TryParse(str, out var i)) return i;
                if (underlying == typeof(double) && double.TryParse(str, out var d)) return d;
                if (underlying == typeof(bool) && bool.TryParse(str, out var b)) return b;
                if (underlying == typeof(string)) return str;
            }

            // As a last resort, return the original value
            return value;
        }

        private string ComputeAudiobookBaseDirectoryFromPattern(Audiobook audiobook, string rootPath, string fileNamingPattern)
        {
            // Derive directory pattern from the user's file naming pattern
            // Remove file-specific tokens like DiskNumber and ChapterNumber to create a directory structure
            string directoryPattern;
            if (!string.IsNullOrWhiteSpace(fileNamingPattern))
            {
                // Remove file-specific patterns and create a directory pattern
                directoryPattern = fileNamingPattern;

                // Remove file-specific tokens that don't make sense for directories
                directoryPattern = Regex.Replace(directoryPattern, @"\{DiskNumber[^}]*\}", "", RegexOptions.IgnoreCase);
                directoryPattern = Regex.Replace(directoryPattern, @"\{ChapterNumber[^}]*\}", "", RegexOptions.IgnoreCase);

                // Clean up any resulting double separators or empty parts
                directoryPattern = Regex.Replace(directoryPattern, @"[\\/]\s*[\\/]", "/");
                directoryPattern = Regex.Replace(directoryPattern, @"^\s*[\\/]", "");
                directoryPattern = Regex.Replace(directoryPattern, @"[\\/]\s*$", "");

                // If the pattern is now empty or doesn't contain directory separators, use a fallback
                if (string.IsNullOrWhiteSpace(directoryPattern) || !directoryPattern.Contains("/"))
                {
                    directoryPattern = "{Author}/{Title}";
                }
            }
            else
            {
                // Fallback to default directory pattern
                directoryPattern = "{Author}/{Title}";
            }

            // For series books, ensure we include the series in the directory structure
            if (!string.IsNullOrWhiteSpace(audiobook.Series) && !directoryPattern.Contains("{Series}"))
            {
                // Insert series between author and title if not already present
                if (directoryPattern.Contains("{Author}/{Title}"))
                {
                    directoryPattern = directoryPattern.Replace("{Author}/{Title}", "{Author}/{Series}/{Title}");
                }
                else if (directoryPattern.Contains("{Author}/"))
                {
                    directoryPattern = directoryPattern.Replace("{Author}/", "{Author}/{Series}/");
                }
            }

            // If the audiobook has no Series, remove any {Series} tokens from the directory pattern
            // Tests expect the controller to strip the Series token when series metadata is missing.
            if (string.IsNullOrWhiteSpace(audiobook.Series))
            {
                directoryPattern = Regex.Replace(directoryPattern, @"\{Series[^}]*\}", string.Empty, RegexOptions.IgnoreCase);
                // Clean up any resulting duplicate separators or empty parts again
                directoryPattern = Regex.Replace(directoryPattern, @"[\\/]\s*[\\/]", "/");
                directoryPattern = Regex.Replace(directoryPattern, @"^\s*[\\/]", "");
                directoryPattern = Regex.Replace(directoryPattern, @"[\\/]\s*$", "");
            }

            // Build variables for naming pattern using audiobook-level metadata
            var variables = new Dictionary<string, object>
            {
                { "Author", SanitizeDirectoryName(audiobook.Authors?.FirstOrDefault() ?? "Unknown Author") },
                { "Series", SanitizeDirectoryName(!string.IsNullOrWhiteSpace(audiobook.Series) ? audiobook.Series! : string.Empty) },
                { "Title", SanitizeDirectoryName(audiobook.Title ?? "Unknown Title") },
                { "SeriesNumber", audiobook.SeriesNumber ?? string.Empty },
                { "Year", audiobook.PublishYear ?? string.Empty },
                { "Quality", string.Empty },
                { "DiskNumber", string.Empty },
                { "ChapterNumber", string.Empty }
            };

            // Apply the directory pattern to get the relative directory path
            var relative = _fileNamingService.ApplyNamingPattern(directoryPattern, variables, false);

            // Combine with root path
            var combined = string.IsNullOrWhiteSpace(rootPath) ? relative : Path.Combine(rootPath, relative);

            return combined;
        }

        private string CalculateBasePath(List<string> filePaths)
        {
            if (!filePaths.Any())
                return string.Empty;

            // Convert all paths to directory paths (get parent directory for each file)
            var directories = filePaths.Select(p => Path.GetDirectoryName(p) ?? p).Distinct().ToList();

            if (directories.Count == 1)
            {
                // All files are in the same directory
                return directories[0];
            }

            // Find the common ancestor directory where there are no longer <=1 things stored
            var commonPath = GetCommonPath(directories);

            // Walk up the directory tree until we find a directory that has more than 1 subdirectory or file
            var currentPath = commonPath;
            while (!string.IsNullOrEmpty(currentPath))
            {
                try
                {
                    var parent = Directory.GetParent(currentPath)?.FullName;
                    if (string.IsNullOrEmpty(parent))
                        break;

                    // Count subdirectories and files in parent
                    var subDirs = Directory.GetDirectories(parent).Length;
                    var files = Directory.GetFiles(parent).Length;

                    // If parent has more than 1 thing (subdirs + files), we've found our base path
                    if (subDirs + files > 1)
                    {
                        return currentPath;
                    }

                    currentPath = parent;
                }
                catch
                {
                    // If we can't access the directory, stop here
                    break;
                }
            }

            return commonPath;
        }

        private string GetCommonPath(List<string> paths)
        {
            if (!paths.Any())
                return string.Empty;

            var firstPath = paths[0];
            var commonPath = firstPath;

            foreach (var path in paths.Skip(1))
            {
                var minLength = Math.Min(commonPath.Length, path.Length);
                var commonLength = 0;

                for (int i = 0; i < minLength; i++)
                {
                    if (commonPath[i] == path[i])
                        commonLength++;
                    else
                        break;
                }

                // Ensure we don't break in the middle of a directory name
                if (commonLength < commonPath.Length)
                {
                    var lastSep = commonPath.LastIndexOf(Path.DirectorySeparatorChar, commonLength - 1);
                    if (lastSep >= 0)
                        commonLength = lastSep + 1;
                    else
                        commonLength = 0;
                }

                commonPath = commonPath.Substring(0, commonLength);

                if (string.IsNullOrEmpty(commonPath))
                    break;
            }

            // Ensure it's a valid directory path
            if (!string.IsNullOrEmpty(commonPath) && !Directory.Exists(commonPath))
            {
                var parent = Directory.GetParent(commonPath)?.FullName;
                return parent ?? commonPath;
            }

            return commonPath;
        }

        private string SanitizeDirectoryName(string name)
        {
            // Remove or replace characters that are invalid in directory names
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            // Also replace some additional characters that might cause issues
            name = name.Replace(":", "_").Replace("*", "_").Replace("?", "_").Replace("\"", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_");

            // Trim whitespace and return
            return name.Trim();
        }

        private static string ComputeShortHash(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return Guid.NewGuid().ToString("N").Substring(0, 12);

            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha1.ComputeHash(bytes);
            // Return first 16 hex characters for a compact identifier
            return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        public class BulkDeleteRequest
        {
            public List<int> Ids { get; set; } = new List<int>();
        }

        public class BulkUpdateRequest
        {
            public List<int> Ids { get; set; } = new List<int>();
            public Dictionary<string, object> Updates { get; set; } = new Dictionary<string, object>();
        }

        public class AddToLibraryRequest
        {
            public AudibleBookMetadata Metadata { get; set; } = new();
            public bool Monitored { get; set; } = true;
            public int? QualityProfileId { get; set; }
            public bool AutoSearch { get; set; } = false;
            // Optional destination override for placing the audiobook base directory
            public string? DestinationPath { get; set; }
            public SearchResult? SearchResult { get; set; }
        }

        public class PreviewPathRequest
        {
            public AudibleBookMetadata Metadata { get; set; } = new();
            public string? DestinationRoot { get; set; }
        }

        public class MoveRequest
        {
            public string? DestinationPath { get; set; }
            public string? SourcePath { get; set; }
            // If provided and false, update DB only and do not enqueue a move job
            public bool? MoveFiles { get; set; }
            // When moving files, whether to delete the original folder if empty after the move
            public bool? DeleteEmptySource { get; set; }
        }

    }
}


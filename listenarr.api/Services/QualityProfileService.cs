/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero Gen            // Check age limit
            if (profile.MaximumAge > 0 && result.PublishedDate != default(DateTime))
            {
                var ageInDays = (DateTime.Now - result.PublishedDate).TotalDays;
                if (ageInDays > profile.MaximumAge)
                {
                    score.RejectionReasons.Add($"Too old ({ageInDays:F0} days > {profile.MaximumAge} days)");
                    return score;
                }
            }c License as published
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

using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Listenarr.Application.Repositories;

namespace Listenarr.Api.Services
{
    public interface IQualityProfileService
    {
        Task<List<QualityProfile>> GetAllAsync();
        Task<QualityProfile?> GetByIdAsync(int id);
        Task<QualityProfile?> GetDefaultAsync();
        Task<QualityProfile> CreateAsync(QualityProfile profile);
        Task<QualityProfile> UpdateAsync(QualityProfile profile);
        Task<bool> DeleteAsync(int id);
        Task<QualityScore> ScoreSearchResult(SearchResult searchResult, QualityProfile profile);
        Task<List<QualityScore>> ScoreSearchResults(List<SearchResult> searchResults, QualityProfile profile);
    }

    public class QualityProfileService : IQualityProfileService
    {
        private readonly ListenArrDbContext _dbContext;
        private readonly ILogger<QualityProfileService> _logger;
        private readonly IQualityProfileRepository _repository;

        public QualityProfileService(ListenArrDbContext dbContext, IQualityProfileRepository repository, ILogger<QualityProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        // Backwards-compatible constructor used by unit tests that previously
        // constructed the service with (ListenArrDbContext, ILogger). Tests create
        // the service with a null DbContext for scoring-only scenarios; provide a
        // lightweight repository implementation that proxies to the DbContext when available.
        public QualityProfileService(ListenArrDbContext dbContext, ILogger<QualityProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _repository = new ApiLocalQualityProfileRepository(dbContext);
        }

        public async Task<List<QualityProfile>> GetAllAsync()
        {
            // Delegate persistence concerns to repository which encapsulates defensive JSON handling
            return await _repository.GetAllAsync();
        }

        public async Task<QualityProfile?> GetByIdAsync(int id)
        {
            var profile = await _dbContext.QualityProfiles.FindAsync(id);
            if (profile != null && profile.IsDefault)
            {
                // Ensure default profiles have all required qualities
                await EnsureProfileHasRequiredQualitiesAsync(profile);
            }
            return profile;
        }

        public async Task<QualityProfile?> GetDefaultAsync()
        {
            var profile = await _dbContext.QualityProfiles
                .FirstOrDefaultAsync(p => p.IsDefault);

            if (profile == null)
            {
                _logger.LogInformation("No default quality profile found, creating one");
                profile = await CreateDefaultProfileAsync();
            }
            else
            {
                // Ensure existing profiles have all required qualities
                await EnsureProfileHasRequiredQualitiesAsync(profile);
            }

            return profile;
        }

        private async Task<QualityProfile> CreateDefaultProfileAsync()
        {
            var defaultProfile = new QualityProfile
            {
                Name = "Default",
                Description = "Default quality profile for audiobooks",
                Qualities = new List<QualityDefinition>
                {
                    new QualityDefinition { Quality = "FLAC", Allowed = true, Priority = 0 },
                    new QualityDefinition { Quality = "M4B", Allowed = true, Priority = 1 },
                    new QualityDefinition { Quality = "MP3 320kbps", Allowed = true, Priority = 2 },
                    new QualityDefinition { Quality = "MP3 256kbps", Allowed = true, Priority = 3 },
                    new QualityDefinition { Quality = "MP3 VBR", Allowed = true, Priority = 4 },
                    new QualityDefinition { Quality = "MP3 192kbps", Allowed = true, Priority = 5 },
                    new QualityDefinition { Quality = "MP3 128kbps", Allowed = true, Priority = 6 },
                    new QualityDefinition { Quality = "MP3 64kbps", Allowed = false, Priority = 7 }
                },
                CutoffQuality = "MP3 128kbps",
                MinimumSize = 50, // 50 MB minimum
                MaximumSize = 2000, // 2 GB maximum
                PreferredFormats = new List<string> { "m4b", "mp3", "m4a", "flac", "opus" },
                PreferredWords = new List<string> { "unabridged", "complete" },
                MustNotContain = new List<string> { "abridged", "sample", "excerpt" },
                MustContain = new List<string>(),
                PreferredLanguages = new List<string> { "English" },
                MinimumSeeders = 1,
                IsDefault = true,
                PreferNewerReleases = true,
                MaximumAge = 365 * 2 // 2 years
            };

            return await CreateAsync(defaultProfile);
        }

        private async Task EnsureProfileHasRequiredQualitiesAsync(QualityProfile profile)
        {
            var requiredQualities = new Dictionary<string, int>
            {
                { "FLAC", 0 },
                { "M4B", 1 },
                { "MP3 320kbps", 2 },
                { "MP3 256kbps", 3 },
                { "MP3 VBR", 4 },
                { "MP3 192kbps", 5 },
                { "MP3 128kbps", 6 },
                { "MP3 64kbps", 7 }
            };

            var updated = false;
            foreach (var (qualityName, priority) in requiredQualities)
            {
                if (!profile.Qualities.Any(q => q.Quality == qualityName))
                {
                    // Add missing quality
                    var isAllowed = qualityName != "MP3 64kbps"; // Only MP3 64kbps is disabled by default
                    profile.Qualities.Add(new QualityDefinition
                    {
                        Quality = qualityName,
                        Allowed = isAllowed,
                        Priority = priority
                    });
                    updated = true;
                    _logger.LogInformation("Added missing quality '{Quality}' to profile '{ProfileName}'", qualityName, profile.Name);
                }
            }

            if (updated)
            {
                profile.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(profile);
                _logger.LogInformation("Updated quality profile '{ProfileName}' with missing qualities", profile.Name);
            }
        }

        public async Task<QualityProfile> CreateAsync(QualityProfile profile)
        {
            // If this is set as default, unset other defaults
            if (profile.IsDefault)
            {
                await UnsetAllDefaultsAsync();
            }

            profile.CreatedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            var created = await _repository.AddAsync(profile);
            _logger.LogInformation("Created quality profile: {Name} (ID: {Id})", created.Name, created.Id);
            return created;
        }

        public async Task<QualityProfile> UpdateAsync(QualityProfile profile)
        {
            var existing = await _repository.FindByIdAsync(profile.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Quality profile with ID {profile.Id} not found");
            }

            // If this is set as default, unset other defaults
            if (profile.IsDefault && !existing.IsDefault)
            {
                await UnsetAllDefaultsAsync();
            }

            profile.UpdatedAt = DateTime.UtcNow;
            var updated = await _repository.UpdateAsync(profile);

            _logger.LogInformation("Updated quality profile: {Name} (ID: {Id})", updated.Name, updated.Id);
            return updated;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var profile = await _repository.FindByIdAsync(id);
            if (profile == null)
            {
                return false;
            }

            // Check if this is the default profile
            if (profile.IsDefault)
            {
                throw new InvalidOperationException(
                    "Cannot delete the default quality profile. Please set another profile as default first.");
            }

            // Check if any audiobooks are using this profile
            var audiobooksCount = await _repository.CountAudiobooksUsingProfileAsync(id);

            if (audiobooksCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot delete quality profile '{profile.Name}' because it is used by {audiobooksCount} audiobook(s). " +
                    "Please reassign those audiobooks to a different profile first.");
            }

            var deleted = await _repository.DeleteAsync(id);
            if (deleted)
            {
                _logger.LogInformation("Deleted quality profile: {Name} (ID: {Id})", profile.Name, id);
            }
            return deleted;
        }

        private async Task UnsetAllDefaultsAsync()
        {
            var defaults = (await _repository.GetAllAsync()).Where(p => p.IsDefault).ToList();
            foreach (var profile in defaults)
            {
                profile.IsDefault = false;
                await _repository.UpdateAsync(profile);
            }
        }

        public Task<QualityScore> ScoreSearchResult(SearchResult searchResult, QualityProfile profile)
        {
            var score = new QualityScore
            {
                SearchResult = searchResult,
                TotalScore = 100, // start at 100
                ScoreBreakdown = new Dictionary<string, int>(),
                RejectionReasons = new List<string>()
            };

            // Instant rejection: must-not-contain (forbidden words)
            foreach (var forbidden in profile.MustNotContain)
            {
                if (!string.IsNullOrEmpty(forbidden) &&
                    searchResult.Title.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    score.RejectionReasons.Add($"Contains forbidden word: '{forbidden}'");
                    score.TotalScore = -1; // negative value to indicate rejection (lowest)
                    return Task.FromResult(score);
                }
            }

            // Instant rejection: required words not present
            foreach (var required in profile.MustContain)
            {
                if (!string.IsNullOrEmpty(required) &&
                    !searchResult.Title.Contains(required, StringComparison.OrdinalIgnoreCase))
                {
                    score.RejectionReasons.Add($"Missing required word: '{required}'");
                    score.TotalScore = -1;
                    return Task.FromResult(score);
                }
            }

            // Detect NZB/Usenet results - for NZB indexers we should not factor quality or age into scoring
            var isNzb = !string.IsNullOrEmpty(searchResult.NzbUrl) ||
                         string.Equals(searchResult.DownloadType, "nzb", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(searchResult.DownloadType, "usenet", StringComparison.OrdinalIgnoreCase);

            // Size checks: reject if outside hard limits (skip for NZB/Usenet indexer results)
            if (!isNzb && searchResult.Size > 0)
            {
                if (profile.MinimumSize > 0 && searchResult.Size < profile.MinimumSize * 1024 * 1024)
                {
                    score.RejectionReasons.Add($"File too small (< {profile.MinimumSize} MB)");
                    score.TotalScore = -1;
                    return Task.FromResult(score);
                }

                if (profile.MaximumSize > 0 && searchResult.Size > profile.MaximumSize * 1024 * 1024)
                {
                    score.RejectionReasons.Add($"File too large (> {profile.MaximumSize} MB)");
                    score.TotalScore = -1;
                    return Task.FromResult(score);
                }
            }

            // Seeders hard requirement for torrents
            if (searchResult.DownloadType == "torrent" && searchResult.Seeders < profile.MinimumSeeders)
            {
                score.RejectionReasons.Add($"Not enough seeders ({searchResult.Seeders} < {profile.MinimumSeeders})");
                score.TotalScore = -1;
                return Task.FromResult(score);
            }


            // Age hard limit
            double ageDays = 0;
            if (!string.IsNullOrEmpty(searchResult.PublishedDate) && DateTime.TryParse(searchResult.PublishedDate, out var publishDate))
            {
                ageDays = (DateTime.UtcNow - publishDate).TotalDays;
                // Apply maximum-age rejection only for non-NZB results (NZB retention/age should be handled in indexer config)
                if (!isNzb && profile.MaximumAge > 0 && ageDays > profile.MaximumAge)
                {
                    score.RejectionReasons.Add($"Too old ({(int)ageDays} days > {profile.MaximumAge} days)");
                    score.TotalScore = -1;
                    return Task.FromResult(score);
                }
            }

            // Unknown size penalty (when profile has size requirements) - skip for NZB
            if (!isNzb && searchResult.Size <= 0 && (profile.MinimumSize > 0 || profile.MaximumSize > 0))
            {
                var sizePenalty = -20;
                score.TotalScore += sizePenalty;
                score.ScoreBreakdown["Size"] = sizePenalty;
            }

            // Prepare titleLower for potential detection
            var titleLower = (searchResult.Title ?? string.Empty).ToLower();

            // For NZB results, attempt to detect language from the title *before* applying language penalties
            if (isNzb && string.IsNullOrEmpty(searchResult.Language) && HasPreferredLanguages(profile))
            {
                var detectedLang = DetectLanguageFromTitle(titleLower, profile.PreferredLanguages);
                if (!string.IsNullOrEmpty(detectedLang))
                {
                    searchResult.Language = detectedLang;
                    _logger.LogDebug("Detected language from NZB title: {Language} for title '{Title}'", detectedLang, searchResult.Title);
                }
            }

            // Language: missing or mismatched
            if (HasPreferredLanguages(profile))
            {
                if (string.IsNullOrEmpty(searchResult.Language))
                {
                    var langPenalty = -10;
                    score.TotalScore += langPenalty;
                    score.ScoreBreakdown["Language"] = langPenalty;
                }
                else
                {
                    var matches = profile.PreferredLanguages.Any(l => searchResult.Language.Equals(l, StringComparison.OrdinalIgnoreCase));
                    if (!matches)
                    {
                        var langMismatchPenalty = -15;
                        score.TotalScore += langMismatchPenalty;
                        score.ScoreBreakdown["LanguageMismatch"] = langMismatchPenalty;
                    }
                }
            }

            // Format: missing or mismatched
            // For NZB indexers we may be able to detect format and language from the title when not provided
            // If NZB and format is missing, attempt to detect a format token in the title
            if (isNzb && string.IsNullOrEmpty(searchResult.Format) && HasPreferredFormats(profile))
            {
                var detectedFormat = DetectFormatFromTitle(titleLower, profile.PreferredFormats);
                if (!string.IsNullOrEmpty(detectedFormat))
                {
                    searchResult.Format = detectedFormat; // annotate result so subsequent checks can see it
                    // award a small bonus and record detection source
                    var titleFormatBonus = 1;
                    score.TotalScore += titleFormatBonus;
                    score.ScoreBreakdown["FormatMatchedInTitle"] = titleFormatBonus;
                    _logger.LogDebug("Detected format from NZB title: {Format} for title '{Title}'", detectedFormat, searchResult.Title);
                }
            }
            if (HasPreferredFormats(profile))
            {
                if (string.IsNullOrEmpty(searchResult.Format))
                {
                    var formatPenalty = -8;
                    score.TotalScore += formatPenalty;
                    score.ScoreBreakdown["Format"] = formatPenalty;
                }
                else
                {
                    // Make format matching more robust:
                    // - Check if any preferred token appears in the reported format string
                    // - Or appears in the detected quality token (e.g. Quality="M4B")
                    // - Or appears as a file extension or token in the torrent/DDL URL or source
                    var formatMatches = false;
                    var formatLower = (searchResult.Format ?? string.Empty).ToLower();
                    var qualityLower = (searchResult.Quality ?? string.Empty).ToLower();
                    var urlLower = (searchResult.TorrentUrl ?? searchResult.Source ?? string.Empty).ToLower();

                    foreach (var f in profile.PreferredFormats)
                    {
                        if (string.IsNullOrWhiteSpace(f)) continue;
                        var token = f.ToLower().Trim();

                        if (formatLower.Contains(token) || qualityLower.Contains(token))
                        {
                            formatMatches = true;
                            if (formatLower.Contains(token))
                            {
                                score.ScoreBreakdown["FormatMatchedInFormat"] = 1;
                                score.TotalScore += 1; // credit for matching format
                            }
                            if (qualityLower.Contains(token))
                            {
                                score.ScoreBreakdown["FormatMatchedInQuality"] = 1;
                                score.TotalScore += 1; // credit for matching quality token
                            }
                            break;
                        }

                        // check url for ".{token}" extension or token inside url (e.g., ".m4b" or "m4b")
                        if (!string.IsNullOrEmpty(urlLower) && (urlLower.Contains("." + token) || urlLower.Contains(token)))
                        {
                            formatMatches = true;
                            score.ScoreBreakdown["FormatMatchedInUrl"] = 1;
                            score.TotalScore += 1; // credit for matching token in URL/source
                            break;
                        }
                        // also check title tokens (helpful for NZB results which often encode format in title)
                        if (!formatMatches && !string.IsNullOrEmpty(titleLower) && titleLower.Contains(token))
                        {
                            formatMatches = true;
                            score.ScoreBreakdown["FormatMatchedInTitle"] = 1;
                            score.TotalScore += 1; // credit for matching token in Title
                            break;
                        }
                    }

                    if (!formatMatches)
                    {
                        var formatMismatchPenalty = -12;
                        score.TotalScore += formatMismatchPenalty;
                        score.ScoreBreakdown["FormatMismatch"] = formatMismatchPenalty;
                    }
                }
            }

            // Quality: if missing, apply flat penalty; else deduct based on distance from perfect
            // For NZB/Usenet indexers we intentionally ignore quality as it is often not provided by indexers
            // For NZB/Usenet indexers we intentionally ignore quality as it is often not provided by indexers
            if (!isNzb)
                if (string.IsNullOrEmpty(searchResult.Quality))
                {
                    var missingQualityPenalty = -25;
                    score.TotalScore += missingQualityPenalty;
                    score.ScoreBreakdown["QualityMissing"] = missingQualityPenalty;
                }
                else
                {
                    int qualityScore = GetQualityScore(searchResult.Quality);
                    var qualityDeduction = 100 - qualityScore; // how far from perfect
                    score.TotalScore -= qualityDeduction;
                    // Record the raw quality score (positive) so callers can reason about "what quality was detected"
                    score.ScoreBreakdown["Quality"] = qualityScore;
                    _logger.LogDebug("Quality scored using GetQualityScore: SearchResult.Quality='{SearchQuality}' => {Score}", searchResult.Quality, qualityScore);

                    // If quality not allowed by profile, add a deduction note (but don't auto-reject here)
                    if (profile.Qualities != null && profile.Qualities.Count > 0)
                    {
                        // Build allowed qualities from explicit Quality definitions
                        var allowedQualities = profile.Qualities.Where(q => q.Allowed).Select(q => (q.Quality ?? string.Empty).ToLower()).ToList();

                        // Also include any PreferredFormats (e.g., "m4b") as allowed tokens so formats configured
                        // in PreferredFormats are not incorrectly rejected when they aren't present in Qualities.
                        if (profile.PreferredFormats != null && profile.PreferredFormats.Count > 0)
                        {
                            foreach (var fmt in profile.PreferredFormats)
                            {
                                var f = (fmt ?? string.Empty).Trim().ToLower();
                                if (!string.IsNullOrEmpty(f) && !allowedQualities.Contains(f))
                                    allowedQualities.Add(f);
                            }
                        }

                        // Match allowed qualities robustly: either the search quality contains a known allowed token
                        // or an allowed quality string contains the detected quality token (handles numeric-only tokens like "320").
                        var detectedQualityLower = (searchResult.Quality ?? string.Empty).ToLower();
                        if (!allowedQualities.Any(q => detectedQualityLower.Contains(q) || q.Contains(detectedQualityLower)))
                        {
                            var notAllowedPenalty = -20;
                            score.TotalScore += notAllowedPenalty; // deduct further
                            score.ScoreBreakdown["QualityNotAllowed"] = notAllowedPenalty;
                            score.RejectionReasons.Add($"Quality '{searchResult.Quality}' not allowed by profile");
                        }
                    }
                }

            // Preferred words: small bonus per preferred word found
            if (profile.PreferredWords != null && profile.PreferredWords.Count > 0)
            {
                var bonus = 0;
                foreach (var word in profile.PreferredWords)
                {
                    if (!string.IsNullOrWhiteSpace(word) && (searchResult.Title ?? string.Empty).Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        bonus += 5;
                    }
                }
                if (bonus != 0)
                {
                    score.TotalScore += bonus;
                    score.ScoreBreakdown["PreferredWords"] = bonus;
                }
            }

            // Seeders small bonus
            if ((searchResult.Seeders ?? 0) > 0)
            {
                var seedersBonus = Math.Min(10, searchResult.Seeders ?? 0);
                if (seedersBonus > 0)
                {
                    score.TotalScore += seedersBonus;
                    score.ScoreBreakdown["Seeders"] = seedersBonus;
                }
            }

            // Age penalty: 2 points per month (30 days), capped at 60
            // Skip age penalties for NZB results - retention and age filtering should be handled by indexer settings
            if (!isNzb && ageDays > 0)
            {
                var agePenalty = (int)Math.Floor(ageDays / 30.0) * 2;
                agePenalty = Math.Min(agePenalty, 60);
                if (agePenalty > 0)
                {
                    score.TotalScore -= agePenalty;
                    score.ScoreBreakdown["Age"] = -agePenalty;
                }
            }

            // If the computed total is less than or equal to zero, treat this as a rejection
            // but keep the ScoreBreakdown so the UI can still display the tooltip details.
            if (score.TotalScore <= 0)
            {
                score.RejectionReasons.Add("Computed score <= 0 (rejected)");
                // Keep the numeric TotalScore (may be <= 0) so the UI can display the computed value.
                // Sorting will ensure rejected items appear last.
                return Task.FromResult(score);
            }

            // Clamp final score between 0 and 100 for non-rejected results
            // Clamp final score between 0 and 100 for non-rejected results
            if (!score.IsRejected)
            {
                score.TotalScore = Math.Clamp(score.TotalScore, 0, 100);
            }
            return Task.FromResult(score);
        }

        // Manual search scoring logic for quality
        private int GetQualityScore(string? quality)
        {
            if (string.IsNullOrEmpty(quality))
                return 0;

            var lowerQuality = quality.ToLower();

            // Highest quality
            if (lowerQuality.Contains("flac"))
                return 100;

            // Audible format (AAX) - high quality
            if (lowerQuality.Contains("aax"))
                return 95;

            // Container formats
            if (lowerQuality.Contains("m4b"))
                return 90;

            // Modern efficient codecs
            if (lowerQuality.Contains("opus"))
                return 85;

            // VBR quality presets (LAME VBR presets like V0/V1/V2)
            if (ContainsVbrPreset(lowerQuality, "v0"))
                return 82;
            if (ContainsVbrPreset(lowerQuality, "v1"))
                return 76;
            if (ContainsVbrPreset(lowerQuality, "v2"))
                return 70;


            // AAC / M4A (check before numeric bitrates to prefer codec score for e.g. "AAC 256")
            if (lowerQuality.Contains("aac") || lowerQuality.Contains("m4a"))
                return 78;

            // Explicit numeric bitrates
            if (lowerQuality.Contains("320"))
                return 80;
            if (lowerQuality.Contains("256"))
                return 74;
            if (lowerQuality.Contains("192"))
                return 60;

            // VBR / CBR generic tokens (treat as mid-range if no numeric bitrate provided)
            if (lowerQuality.Contains("vbr") || lowerQuality.Contains("cbr"))
            {
                // If there's an explicit numeric bitrate elsewhere, that will have matched above.
                return 65;
            }

            // Generic MP3 mention without explicit bitrate -> mid-range
            if (lowerQuality.Contains("mp3") && !ContainsAnyBitrate(lowerQuality, "64", "128", "192", "256", "320"))
                return 65;

            if (lowerQuality.Contains("128"))
                return 50;
            if (lowerQuality.Contains("64"))
                return 40;

            return 0;
        }



        public async Task<List<QualityScore>> ScoreSearchResults(List<SearchResult> searchResults, QualityProfile profile)
        {
            var scores = new List<QualityScore>();

            foreach (var result in searchResults)
            {
                var score = await ScoreSearchResult(result, profile);
                scores.Add(score);
            }

            // Ensure rejected results are ordered last regardless of numeric TotalScore
            return scores
                .OrderBy(s => s.IsRejected) // false (not rejected) first
                .ThenByDescending(s => s.TotalScore)
                .ToList();
        }

        /// <summary>
        /// Checks if a quality string contains VBR preset indicators (v0, v1, v2).
        /// </summary>
        private static bool ContainsVbrPreset(string qualityLower, string preset)
        {
            return qualityLower.Contains(preset) ||
                   qualityLower.Contains($"-{preset}") ||
                   qualityLower.Contains($" {preset}");
        }

        /// <summary>
        /// Checks if a quality string contains any of the specified bitrate indicators.
        /// </summary>
        private static bool ContainsAnyBitrate(string qualityLower, params string[] bitrates)
        {
            return bitrates.Any(b => qualityLower.Contains(b));
        }

        /// <summary>
        /// Determines if the profile has preferred languages configured.
        /// </summary>
        private static bool HasPreferredLanguages(QualityProfile profile)
        {
            return profile.PreferredLanguages != null && profile.PreferredLanguages.Count > 0;
        }

        /// <summary>
        /// Determines if the profile has preferred formats configured.
        /// </summary>
        private static bool HasPreferredFormats(QualityProfile profile)
        {
            return profile.PreferredFormats != null && profile.PreferredFormats.Count > 0;
        }

        /// <summary>
        /// Attempts to detect a preferred format token in a result title (case-insensitive).
        /// Returns the detected token (normalized) or null if nothing matched.
        /// </summary>
        private static string? DetectFormatFromTitle(string titleLower, List<string>? preferredFormats)
        {
            if (preferredFormats == null || preferredFormats.Count == 0 || string.IsNullOrEmpty(titleLower))
                return null;

            foreach (var f in preferredFormats)
            {
                if (string.IsNullOrWhiteSpace(f)) continue;
                var token = f.ToLower().Trim();

                // allow matching patterns like ".m4b", "[m4b]", "m4b" in the title
                if (titleLower.Contains(token) || titleLower.Contains("[" + token + "]") || titleLower.Contains("(" + token + ")") || titleLower.Contains("." + token))
                {
                    return token;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to detect language from title using the profile's preferred languages as hints.
        /// Returns the detected language string (as provided in the profile) or null if none matched.
        /// </summary>
        private static string? DetectLanguageFromTitle(string titleLower, List<string>? preferredLanguages)
        {
            if (preferredLanguages == null || preferredLanguages.Count == 0 || string.IsNullOrEmpty(titleLower))
                return null;

            foreach (var lang in preferredLanguages)
            {
                if (string.IsNullOrWhiteSpace(lang)) continue;
                var token = lang.ToLower().Trim();

                // Basic match for language name or common short forms
                if (titleLower.Contains(token) || titleLower.Contains("[" + token + "]") || titleLower.Contains("(" + token + ")") || titleLower.Contains(" " + token + " "))
                {
                    return lang; // return original (possibly capitalised) profile language value
                }
            }

            // Also check a small set of common language tokens to detect language even when not present in profile
            var common = new Dictionary<string, string>
            {
                { "eng", "English" },
                { "english", "English" },
                { "es", "Spanish" },
                { "spanish", "Spanish" },
                { "de", "German" },
                { "german", "German" },
                { "fr", "French" },
                { "french", "French" }
            };

            foreach (var (token, name) in common)
            {
                if (titleLower.Contains(token)) return name;
            }

            return null;
        }
    }

    // NOTE: defensive JSON deserialization helpers live in Listenarr.Infrastructure.Persistence.Converters.JsonConverterHelpers
}

// Internal lightweight implementation of IQualityProfileRepository used
// only for backwards compatibility in tests or hosts that construct the
// service directly without DI. This avoids adding a hard dependency on
// the infrastructure project from the API assembly.
internal class ApiLocalQualityProfileRepository : Listenarr.Application.Repositories.IQualityProfileRepository
{
    private readonly Listenarr.Infrastructure.Models.ListenArrDbContext? _db;

    public ApiLocalQualityProfileRepository(Listenarr.Infrastructure.Models.ListenArrDbContext? db)
    {
        _db = db;
    }

    public async Task<List<QualityProfile>> GetAllAsync()
    {
        if (_db == null) return new List<QualityProfile>();
        return await _db.QualityProfiles.ToListAsync();
    }

    public async Task<QualityProfile?> FindByIdAsync(int id)
    {
        if (_db == null) return null;
        return await _db.QualityProfiles.FindAsync(id);
    }

    public async Task<QualityProfile?> GetDefaultAsync()
    {
        if (_db == null) return null;
        return await _db.QualityProfiles.FirstOrDefaultAsync(p => p.IsDefault);
    }

    public async Task<QualityProfile> AddAsync(QualityProfile profile)
    {
        if (_db == null) throw new InvalidOperationException("No DbContext available");
        _db.QualityProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task<QualityProfile> UpdateAsync(QualityProfile profile)
    {
        if (_db == null) throw new InvalidOperationException("No DbContext available");
        _db.QualityProfiles.Update(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (_db == null) return false;
        var existing = await _db.QualityProfiles.FindAsync(id);
        if (existing == null) return false;
        _db.QualityProfiles.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> CountAudiobooksUsingProfileAsync(int profileId)
    {
        if (_db == null) return 0;
        return await _db.Audiobooks.CountAsync(a => a.QualityProfileId == profileId);
    }
}


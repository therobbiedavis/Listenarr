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

using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;

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

        public QualityProfileService(ListenArrDbContext dbContext, ILogger<QualityProfileService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<List<QualityProfile>> GetAllAsync()
        {
            return await _dbContext.QualityProfiles.ToListAsync();
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
                await _dbContext.SaveChangesAsync();
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

            _dbContext.QualityProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created quality profile: {Name} (ID: {Id})", profile.Name, profile.Id);
            return profile;
        }

        public async Task<QualityProfile> UpdateAsync(QualityProfile profile)
        {
            var existing = await _dbContext.QualityProfiles.FindAsync(profile.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Quality profile with ID {profile.Id} not found");
            }

            // If this is set as default, unset other defaults
            if (profile.IsDefault && !existing.IsDefault)
            {
                await UnsetAllDefaultsAsync();
            }

            // Update properties
            existing.Name = profile.Name;
            existing.Description = profile.Description;
            existing.Qualities = profile.Qualities;
            existing.CutoffQuality = profile.CutoffQuality;
            existing.MinimumSize = profile.MinimumSize;
            existing.MaximumSize = profile.MaximumSize;
            existing.PreferredFormats = profile.PreferredFormats;
            existing.PreferredWords = profile.PreferredWords;
            existing.MustNotContain = profile.MustNotContain;
            existing.MustContain = profile.MustContain;
            existing.PreferredLanguages = profile.PreferredLanguages;
            existing.MinimumSeeders = profile.MinimumSeeders;
            existing.IsDefault = profile.IsDefault;
            existing.PreferNewerReleases = profile.PreferNewerReleases;
            existing.MaximumAge = profile.MaximumAge;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated quality profile: {Name} (ID: {Id})", profile.Name, profile.Id);
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var profile = await _dbContext.QualityProfiles.FindAsync(id);
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
            var audiobooksCount = await _dbContext.Audiobooks
                .CountAsync(a => a.QualityProfileId == id);

            if (audiobooksCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot delete quality profile '{profile.Name}' because it is used by {audiobooksCount} audiobook(s). " +
                    "Please reassign those audiobooks to a different profile first.");
            }

            _dbContext.QualityProfiles.Remove(profile);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted quality profile: {Name} (ID: {Id})", profile.Name, id);
            return true;
        }

        private async Task UnsetAllDefaultsAsync()
        {
            var defaults = await _dbContext.QualityProfiles
                .Where(p => p.IsDefault)
                .ToListAsync();

            foreach (var profile in defaults)
            {
                profile.IsDefault = false;
            }

            await _dbContext.SaveChangesAsync();
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

            // Size checks: reject if outside hard limits
            if (searchResult.Size > 0)
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
            if (searchResult.PublishedDate != default(DateTime))
            {
                ageDays = (DateTime.UtcNow - searchResult.PublishedDate).TotalDays;
                if (profile.MaximumAge > 0 && ageDays > profile.MaximumAge)
                {
                    score.RejectionReasons.Add($"Too old ({(int)ageDays} days > {profile.MaximumAge} days)");
                    score.TotalScore = -1;
                    return Task.FromResult(score);
                }
            }

            // Unknown size penalty (when profile has size requirements)
            if (searchResult.Size <= 0 && (profile.MinimumSize > 0 || profile.MaximumSize > 0))
            {
                var sizePenalty = -20;
                score.TotalScore += sizePenalty;
                score.ScoreBreakdown["Size"] = sizePenalty;
            }

            // Language: missing or mismatched
            if (string.IsNullOrEmpty(searchResult.Language) && profile.PreferredLanguages != null && profile.PreferredLanguages.Count > 0)
            {
                var langPenalty = -10;
                score.TotalScore += langPenalty;
                score.ScoreBreakdown["Language"] = langPenalty;
            }
            else if (!string.IsNullOrEmpty(searchResult.Language) && profile.PreferredLanguages != null && profile.PreferredLanguages.Count > 0)
            {
                var matches = profile.PreferredLanguages.Any(l => searchResult.Language.Equals(l, StringComparison.OrdinalIgnoreCase));
                if (!matches)
                {
                    var langMismatchPenalty = -15;
                    score.TotalScore += langMismatchPenalty;
                    score.ScoreBreakdown["LanguageMismatch"] = langMismatchPenalty;
                }
            }

            // Format: missing or mismatched
            if (string.IsNullOrEmpty(searchResult.Format) && profile.PreferredFormats != null && profile.PreferredFormats.Count > 0)
            {
                var formatPenalty = -8;
                score.TotalScore += formatPenalty;
                score.ScoreBreakdown["Format"] = formatPenalty;
            }
            else if (!string.IsNullOrEmpty(searchResult.Format) && profile.PreferredFormats != null && profile.PreferredFormats.Count > 0)
            {
                // Make format matching more robust:
                // - Check if any preferred token appears in the reported format string
                // - Or appears in the detected quality token (e.g. Quality="M4B")
                // - Or appears as a file extension or token in the torrent/DDL URL or source
                var formatMatches = false;
                var formatLower = searchResult.Format.ToLower();
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
                }

                if (!formatMatches)
                {
                    var formatMismatchPenalty = -12;
                    score.TotalScore += formatMismatchPenalty;
                    score.ScoreBreakdown["FormatMismatch"] = formatMismatchPenalty;
                }
            }

            // Quality: if missing, apply flat penalty; else deduct based on distance from perfect
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
                    var allowedQualities = profile.Qualities.Where(q => q.Allowed).Select(q => q.Quality.ToLower()).ToList();
                    // Match allowed qualities robustly: either the search quality contains a known allowed token
                    // or an allowed quality string contains the detected quality token (handles numeric-only tokens like "320").
                    var detectedQualityLower = searchResult.Quality.ToLower();
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
                    if (!string.IsNullOrWhiteSpace(word) && searchResult.Title.Contains(word, StringComparison.OrdinalIgnoreCase))
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
            if (searchResult.Seeders > 0)
            {
                var seedersBonus = Math.Min(10, searchResult.Seeders);
                if (seedersBonus > 0)
                {
                    score.TotalScore += seedersBonus;
                    score.ScoreBreakdown["Seeders"] = seedersBonus;
                }
            }

            // Age penalty: 2 points per month (30 days), capped at 60
            if (ageDays > 0)
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
            if (lowerQuality.Contains("v0") || lowerQuality.Contains("-v0") || lowerQuality.Contains(" v0") )
                return 82;
            if (lowerQuality.Contains("v1") || lowerQuality.Contains("-v1") || lowerQuality.Contains(" v1"))
                return 76;
            if (lowerQuality.Contains("v2") || lowerQuality.Contains("-v2") || lowerQuality.Contains(" v2"))
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
            if (lowerQuality.Contains("mp3") && !lowerQuality.Contains("64") && !lowerQuality.Contains("128") && !lowerQuality.Contains("192") && !lowerQuality.Contains("256") && !lowerQuality.Contains("320"))
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
    }
}

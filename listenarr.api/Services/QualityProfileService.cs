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
                TotalScore = 0,
                ScoreBreakdown = new Dictionary<string, int>(),
                RejectionReasons = new List<string>()
            };

            // Check must-not-contain words (instant rejection)
            foreach (var forbidden in profile.MustNotContain)
            {
                if (!string.IsNullOrEmpty(forbidden) && 
                    searchResult.Title.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    score.RejectionReasons.Add($"Contains forbidden word: '{forbidden}'");
                    score.TotalScore = -99999; // Ensure rejected results always sort last
                    return Task.FromResult(score);
                }
            }

            // Check must-contain words (instant rejection if not found)
            foreach (var required in profile.MustContain)
            {
                if (!string.IsNullOrEmpty(required) && 
                    !searchResult.Title.Contains(required, StringComparison.OrdinalIgnoreCase))
                {
                    score.RejectionReasons.Add($"Missing required word: '{required}'");
                    score.TotalScore = -99999; // Ensure rejected results always sort last
                    return Task.FromResult(score);
                }
            }

            // Check size limits (only if size is known)
            if (searchResult.Size > 0)
            {
                if (profile.MinimumSize > 0 && searchResult.Size < profile.MinimumSize * 1024 * 1024)
                {
                    score.RejectionReasons.Add($"File too small (< {profile.MinimumSize} MB)");
                    score.TotalScore = -99999; // Ensure rejected results always sort last
                    return Task.FromResult(score);
                }

                if (profile.MaximumSize > 0 && searchResult.Size > profile.MaximumSize * 1024 * 1024)
                {
                    score.RejectionReasons.Add($"File too large (> {profile.MaximumSize} MB)");
                    score.TotalScore = -99999; // Ensure rejected results always sort last
                    return Task.FromResult(score);
                }
            }

            // Check seeders for torrents
            if (searchResult.DownloadType == "torrent" && searchResult.Seeders < profile.MinimumSeeders)
            {
                score.RejectionReasons.Add($"Not enough seeders ({searchResult.Seeders} < {profile.MinimumSeeders})");
                score.TotalScore = -99999; // Ensure rejected results always sort last
                return Task.FromResult(score);
            }

            // Check age limit
            if (profile.MaximumAge > 0 && searchResult.PublishedDate != default(DateTime))
            {
                var age = (DateTime.UtcNow - searchResult.PublishedDate).TotalDays;
                if (age > profile.MaximumAge)
                {
                    score.RejectionReasons.Add($"Too old ({(int)age} days > {profile.MaximumAge} days)");
                    score.TotalScore = -99999; // Ensure rejected results always sort last
                    return Task.FromResult(score);
                }
            }

            // Penalize unknown size when size requirements exist
            if (searchResult.Size <= 0 && (profile.MinimumSize > 0 || profile.MaximumSize > 0))
            {
                var sizePenalty = -20; // Significant penalty for unknown size
                score.TotalScore += sizePenalty;
                score.ScoreBreakdown["Size"] = sizePenalty;
            }

            // Score quality using the same logic as manual search
            int qualityScore = GetQualityScore(searchResult.Quality);
            score.TotalScore += qualityScore;
            score.ScoreBreakdown["Quality"] = qualityScore;
            _logger.LogDebug("Quality scored using GetQualityScore: SearchResult.Quality='{SearchQuality}' => {Score}", searchResult.Quality, qualityScore);

            // Optionally, you can still reject if the quality is not allowed by the profile
            if (!string.IsNullOrEmpty(searchResult.Quality))
            {
                var allowedQualities = profile.Qualities.Where(q => q.Allowed).Select(q => q.Quality.ToLower()).ToList();
                if (!allowedQualities.Any(q => searchResult.Quality.ToLower().Contains(q)))
                {
                    score.RejectionReasons.Add($"Quality '{searchResult.Quality}' not allowed by profile");
                }
            }
            // Ensure non-rejected results never have negative scores
            score.TotalScore = Math.Max(0, score.TotalScore);
            return Task.FromResult(score);
        }

    // Manual search scoring logic for quality
    private int GetQualityScore(string? quality)
    {
        if (string.IsNullOrEmpty(quality))
            return 0;

        var lowerQuality = quality.ToLower();

        if (lowerQuality.Contains("flac"))
            return 100;
        else if (lowerQuality.Contains("m4b"))
            return 90;
        else if (lowerQuality.Contains("320"))
            return 80;
        else if (lowerQuality.Contains("256"))
            return 70;
        else if (lowerQuality.Contains("192"))
            return 60;
        else if (lowerQuality.Contains("128"))
            return 50;
        else if (lowerQuality.Contains("64"))
            return 40;
        else
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

            return scores.OrderByDescending(s => s.TotalScore).ToList();
        }
    }
}

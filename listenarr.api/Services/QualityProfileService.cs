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
            return await _dbContext.QualityProfiles.FindAsync(id);
        }

        public async Task<QualityProfile?> GetDefaultAsync()
        {
            return await _dbContext.QualityProfiles
                .FirstOrDefaultAsync(p => p.IsDefault);
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
                    return Task.FromResult(score);
                }
            }

            // Check size limits
            if (profile.MinimumSize > 0 && searchResult.Size < profile.MinimumSize * 1024 * 1024)
            {
                score.RejectionReasons.Add($"File too small (< {profile.MinimumSize} MB)");
                return Task.FromResult(score);
            }

            if (profile.MaximumSize > 0 && searchResult.Size > profile.MaximumSize * 1024 * 1024)
            {
                score.RejectionReasons.Add($"File too large (> {profile.MaximumSize} MB)");
                return Task.FromResult(score);
            }

            // Check seeders for torrents
            if (searchResult.DownloadType == "torrent" && searchResult.Seeders < profile.MinimumSeeders)
            {
                score.RejectionReasons.Add($"Not enough seeders ({searchResult.Seeders} < {profile.MinimumSeeders})");
                return Task.FromResult(score);
            }

            // Check age limit
            if (profile.MaximumAge > 0 && searchResult.PublishedDate != default(DateTime))
            {
                var age = (DateTime.UtcNow - searchResult.PublishedDate).TotalDays;
                if (age > profile.MaximumAge)
                {
                    score.RejectionReasons.Add($"Too old ({(int)age} days > {profile.MaximumAge} days)");
                    return Task.FromResult(score);
                }
            }

            // Score quality level
            var qualityMatch = profile.Qualities.FirstOrDefault(q => 
                q.Allowed && 
                !string.IsNullOrEmpty(searchResult.Quality) &&
                searchResult.Quality.Contains(q.Quality, StringComparison.OrdinalIgnoreCase));

            if (qualityMatch != null)
            {
                var qualityScore = 100 - (qualityMatch.Priority * 10);
                score.TotalScore += qualityScore;
                score.ScoreBreakdown["Quality"] = qualityScore;
            }
            else
            {
                score.ScoreBreakdown["Quality"] = 0;
            }

            // Score format preference
            var extension = Path.GetExtension(searchResult.Title)?.TrimStart('.').ToLower();
            if (!string.IsNullOrEmpty(extension))
            {
                var formatIndex = profile.PreferredFormats.FindIndex(f => 
                    f.Equals(extension, StringComparison.OrdinalIgnoreCase));
                
                if (formatIndex >= 0)
                {
                    var formatScore = 50 - (formatIndex * 10);
                    score.TotalScore += formatScore;
                    score.ScoreBreakdown["Format"] = formatScore;
                }
            }

            // Score preferred words
            var preferredWordScore = 0;
            foreach (var preferred in profile.PreferredWords)
            {
                if (!string.IsNullOrEmpty(preferred) && 
                    searchResult.Title.Contains(preferred, StringComparison.OrdinalIgnoreCase))
                {
                    preferredWordScore += 10;
                }
            }
            if (preferredWordScore > 0)
            {
                score.TotalScore += preferredWordScore;
                score.ScoreBreakdown["PreferredWords"] = preferredWordScore;
            }

            // Score language
            if (!string.IsNullOrEmpty(searchResult.Language))
            {
                var langIndex = profile.PreferredLanguages.FindIndex(l => 
                    l.Equals(searchResult.Language, StringComparison.OrdinalIgnoreCase));
                
                if (langIndex >= 0)
                {
                    var langScore = 20 - (langIndex * 5);
                    score.TotalScore += langScore;
                    score.ScoreBreakdown["Language"] = langScore;
                }
            }

            // Score seeders for torrents
            if (searchResult.DownloadType == "torrent" && searchResult.Seeders > 0)
            {
                var seederScore = Math.Min(searchResult.Seeders, 20);
                score.TotalScore += seederScore;
                score.ScoreBreakdown["Seeders"] = seederScore;
            }

            // Score age (newer is better if preference is set)
            if (profile.PreferNewerReleases && searchResult.PublishedDate != default(DateTime))
            {
                var age = (DateTime.UtcNow - searchResult.PublishedDate).TotalDays;
                var ageScore = Math.Max(0, 30 - (int)(age / 7)); // Lose 1 point per week
                if (ageScore > 0)
                {
                    score.TotalScore += ageScore;
                    score.ScoreBreakdown["Age"] = ageScore;
                }
            }

            return Task.FromResult(score);
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

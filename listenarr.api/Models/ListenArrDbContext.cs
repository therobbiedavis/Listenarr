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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Listenarr.Api.Models
{
    public class ListenArrDbContext : DbContext
    {
        public DbSet<Audiobook> Audiobooks { get; set; }
    public DbSet<AudiobookFile> AudiobookFiles { get; set; }
        public DbSet<ApplicationSettings> ApplicationSettings { get; set; }
        public DbSet<History> History { get; set; }
        public DbSet<Indexer> Indexers { get; set; }
        public DbSet<ApiConfiguration> ApiConfigurations { get; set; }
        public DbSet<DownloadClientConfiguration> DownloadClientConfigurations { get; set; }
    public DbSet<User> Users { get; set; }
        public DbSet<Download> Downloads { get; set; }
        public DbSet<DownloadProcessingJob> DownloadProcessingJobs { get; set; }
        public DbSet<QualityProfile> QualityProfiles { get; set; }
        public DbSet<RemotePathMapping> RemotePathMappings { get; set; }

        public ListenArrDbContext(DbContextOptions<ListenArrDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure SQLite if no provider was configured externally (e.g. tests using InMemory)
            if (!optionsBuilder.IsConfigured)
            {
                // Apply PRAGMA settings when connection is configured
                optionsBuilder.UseSqlite(options =>
                {
                    options.CommandTimeout(60);
                });
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Authors)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Authors)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Genres)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Genres)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Tags)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Tags)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Narrators)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Narrators)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            // Audiobook -> AudiobookFiles one-to-many
            modelBuilder.Entity<Audiobook>()
                .HasMany(a => a.Files)
                .WithOne(f => f.Audiobook)
                .HasForeignKey(f => f.AudiobookId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationSettings configuration
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.AllowedFileExtensions)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.AllowedFileExtensions)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            // ApplicationSettings - EnabledNotificationTriggers as pipe-delimited
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.EnabledNotificationTriggers)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.EnabledNotificationTriggers)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            // ApplicationSettings - Webhooks as JSON
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.Webhooks)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<List<WebhookConfiguration>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                );
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.Webhooks)
                .Metadata.SetValueComparer(new ValueComparer<List<WebhookConfiguration>?>(
                    (c1, c2) => System.Text.Json.JsonSerializer.Serialize(c1, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(c2, (System.Text.Json.JsonSerializerOptions?)null),
                    c => c == null ? 0 : System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
                    c => c == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<WebhookConfiguration>>(System.Text.Json.JsonSerializer.Serialize(c, (System.Text.Json.JsonSerializerOptions?)null), (System.Text.Json.JsonSerializerOptions?)null)
                ));

            // ApiConfiguration - ignore computed properties
            modelBuilder.Entity<ApiConfiguration>()
                .Ignore(e => e.Headers)
                .Ignore(e => e.Parameters);

            // DownloadClientConfiguration - ignore computed properties
            modelBuilder.Entity<DownloadClientConfiguration>()
                .Ignore(e => e.Settings);

            // Download - store Metadata as JSON
            modelBuilder.Entity<Download>()
                .Property(e => e.Metadata)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new Dictionary<string, object>()
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );
            modelBuilder.Entity<Download>()
                .Property(e => e.Metadata)
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, object>>(
                    (c1, c2) => (c1 ?? new Dictionary<string, object>()).SequenceEqual(c2 ?? new Dictionary<string, object>()),
                    c => (c ?? new Dictionary<string, object>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value != null ? v.Value.GetHashCode() : 0)),
                    c => c == null ? new Dictionary<string, object>() : new Dictionary<string, object>(c)
                ));

            // QualityProfile configuration - store complex properties as JSON
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.Qualities)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<QualityDefinition>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<QualityDefinition>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<QualityDefinition>()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.Qualities)
                .Metadata.SetValueComparer(new ValueComparer<List<QualityDefinition>>(
                    (c1, c2) => (c1 ?? new List<QualityDefinition>()).SequenceEqual(c2 ?? new List<QualityDefinition>()),
                    c => (c ?? new List<QualityDefinition>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? new List<QualityDefinition>() : c.ToList()
                ));

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredFormats)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredFormats)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredWords)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredWords)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustNotContain)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustNotContain)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustContain)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustContain)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredLanguages)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredLanguages)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));

            // DownloadProcessingJob - store complex properties as JSON
            modelBuilder.Entity<DownloadProcessingJob>()
                .Property(e => e.JobData)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new Dictionary<string, object>()
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );
            modelBuilder.Entity<DownloadProcessingJob>()
                .Property(e => e.JobData)
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, object>>(
                    (c1, c2) => (c1 ?? new Dictionary<string, object>()).SequenceEqual(c2 ?? new Dictionary<string, object>()),
                    c => (c ?? new Dictionary<string, object>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value != null ? v.Value.GetHashCode() : 0)),
                    c => c == null ? new Dictionary<string, object>() : new Dictionary<string, object>(c)
                ));

            modelBuilder.Entity<DownloadProcessingJob>()
                .Property(e => e.ProcessingLog)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                );
            modelBuilder.Entity<DownloadProcessingJob>()
                .Property(e => e.ProcessingLog)
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                    c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? new List<string>() : c.ToList()
                ));

            // Ensure AudiobookFile uniqueness per audiobook path
            modelBuilder.Entity<AudiobookFile>()
                .HasIndex(f => new { f.AudiobookId, f.Path })
                .IsUnique();

            // Performance indexes for frequently queried columns
            // Download table - optimized for status queries and download client filtering
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.Status);
            
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.DownloadClientId);
            
            modelBuilder.Entity<Download>()
                .HasIndex(d => d.CompletedAt);

            // DownloadProcessingJob table - optimized for job status queries
            modelBuilder.Entity<DownloadProcessingJob>()
                .HasIndex(j => new { j.DownloadId, j.Status });
            
            modelBuilder.Entity<DownloadProcessingJob>()
                .HasIndex(j => j.Status);

            // Audiobook table - optimized for monitoring and search
            modelBuilder.Entity<Audiobook>()
                .HasIndex(a => a.Monitored);
            
            modelBuilder.Entity<Audiobook>()
                .HasIndex(a => a.LastSearchTime);

            // History table - optimized for recent activity queries
            modelBuilder.Entity<History>()
                .HasIndex(h => h.Timestamp);
        }
    }
}
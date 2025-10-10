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

namespace Listenarr.Api.Models
{
    public class ListenArrDbContext : DbContext
    {
        public DbSet<Audiobook> Audiobooks { get; set; }
        public DbSet<ApplicationSettings> ApplicationSettings { get; set; }
        public DbSet<History> History { get; set; }
        public DbSet<Indexer> Indexers { get; set; }
        public DbSet<ApiConfiguration> ApiConfigurations { get; set; }
        public DbSet<DownloadClientConfiguration> DownloadClientConfigurations { get; set; }
    public DbSet<User> Users { get; set; }
        public DbSet<Download> Downloads { get; set; }
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
                .Property(e => e.Genres)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Tags)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            modelBuilder.Entity<Audiobook>()
                .Property(e => e.Narrators)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            // ApplicationSettings configuration
            modelBuilder.Entity<ApplicationSettings>()
                .Property(e => e.AllowedFileExtensions)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            // ApiConfiguration - ignore computed properties
            modelBuilder.Entity<ApiConfiguration>()
                .Ignore(e => e.Headers)
                .Ignore(e => e.Parameters);

            // DownloadClientConfiguration - ignore computed properties
            modelBuilder.Entity<DownloadClientConfiguration>()
                .Ignore(e => e.Settings);

            // Download - ignore Metadata dictionary (not stored in DB for now)
            modelBuilder.Entity<Download>()
                .Ignore(e => e.Metadata);

            // QualityProfile configuration - store complex properties as JSON
            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.Qualities)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<QualityDefinition>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<QualityDefinition>()
                );

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredFormats)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredWords)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustNotContain)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.MustContain)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            modelBuilder.Entity<QualityProfile>()
                .Property(e => e.PreferredLanguages)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
        }
    }
}
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

        public ListenArrDbContext(DbContextOptions<ListenArrDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
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
        }
    }
}

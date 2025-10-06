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

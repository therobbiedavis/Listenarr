using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;

namespace Listenarr.Infrastructure.Models.Configurations
{
    /// <summary>
    /// EF mapping for Audiobook entity extracted from ListenArrDbContext.
    /// Keeps conversions / comparers and relationships colocated for easier testing & reuse.
    /// </summary>
    public class AudiobookConfiguration : IEntityTypeConfiguration<Audiobook>
    {
        private static ValueComparer<List<string>> StringListComparer() =>
            new ValueComparer<List<string>>(
                (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<string>() : c.ToList()
            );

        public void Configure(EntityTypeBuilder<Audiobook> builder)
        {
            // Simple list-of-string -> pipe-delimited conversion for Authors, Genres, Tags, Narrators
            builder.Property(e => e.Authors)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.Authors)
                .Metadata.SetValueComparer(StringListComparer());

            builder.Property(e => e.Genres)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.Genres)
                .Metadata.SetValueComparer(StringListComparer());

            builder.Property(e => e.Tags)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.Tags)
                .Metadata.SetValueComparer(StringListComparer());

            builder.Property(e => e.Narrators)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.Narrators)
                .Metadata.SetValueComparer(StringListComparer());

            // One-to-many: Audiobook -> AudiobookFiles
            builder.HasMany(a => a.Files)
                .WithOne(f => f.Audiobook)
                .HasForeignKey(f => f.AudiobookId)
                .OnDelete(DeleteBehavior.Cascade);

            // Performance indexes commonly used by queries
            builder.HasIndex(a => a.Monitored);
            builder.HasIndex(a => a.LastSearchTime);
        }
    }
}

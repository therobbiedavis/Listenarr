using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;
using System.Text.Json;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class IndexerConfiguration : IEntityTypeConfiguration<Indexer>
    {
        public void Configure(EntityTypeBuilder<Indexer> builder)
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Name).IsRequired();
            builder.Property(i => i.Type).IsRequired().HasMaxLength(32);
            builder.Property(i => i.Implementation).HasMaxLength(64);
            builder.Property(i => i.Url).HasMaxLength(1024);
            builder.Property(i => i.ApiKey).HasMaxLength(512);

            // Tags and categories are stored as comma-separated strings; keep length reasonable
            builder.Property(i => i.Categories).HasMaxLength(512);
            builder.Property(i => i.Tags).HasMaxLength(512);

            builder.Property(i => i.AddedByProwlarr).HasDefaultValue(false);

            builder.HasIndex(i => i.AddedByProwlarr);
            builder.HasIndex(i => i.ProwlarrIndexerId);
        }
    }
}

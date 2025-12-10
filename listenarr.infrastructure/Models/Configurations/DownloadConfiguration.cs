using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Persistence.Converters;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class DownloadConfiguration : IEntityTypeConfiguration<Download>
    {
        public void Configure(EntityTypeBuilder<Download> builder)
        {
            builder.HasKey(d => d.Id);

            // Map Metadata dictionary to a JSON TEXT column with centralized converter + comparer.
            var converter = new JsonValueConverter<Dictionary<string, object>>();
            var comparer = JsonValueComparer.Create<Dictionary<string, object>>();

            var metadataProp = builder.Property(d => d.Metadata)
                .HasConversion(converter)
                .HasColumnName("Metadata")
                .HasColumnType("TEXT")
                .IsRequired();

            metadataProp.Metadata.SetValueComparer(comparer);
        }
    }
}

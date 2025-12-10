using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Persistence.Converters;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class DownloadProcessingJobConfiguration : IEntityTypeConfiguration<DownloadProcessingJob>
    {
        public void Configure(EntityTypeBuilder<DownloadProcessingJob> builder)
        {
            builder.HasKey(j => j.Id);

            // Map JobData dictionary to a JSON TEXT column with centralized converter + comparer.
            var converter = new JsonValueConverter<Dictionary<string, object>>();
            var comparer = JsonValueComparer.Create<Dictionary<string, object>>();

            var jobDataProp = builder.Property(j => j.JobData)
                .HasConversion(converter)
                .HasColumnName("JobData")
                .HasColumnType("TEXT")
                .IsRequired();

            jobDataProp.Metadata.SetValueComparer(comparer);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Persistence.Converters;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class ApiConfigurationConfiguration : IEntityTypeConfiguration<ApiConfiguration>
    {
        public void Configure(EntityTypeBuilder<ApiConfiguration> builder)
        {
            builder.HasKey(a => a.Id);

            // Ensure JSON backing column type when using conversion (only configure the converted
            // property below so EF doesn't try to map both the backing string and the converted
            // property as separate columns which can cause duplicate '...Json1' columns to appear).

            // Centralized JSON converter/comparer — expression-tree safe.
            var converter = new JsonValueConverter<Dictionary<string, string>>();
            var comparer = JsonValueComparer.Create<Dictionary<string, string>>();

            // Ensure EF doesn't separately map the raw backing JSON string property -
            // only the converted property will be mapped to the column name.
            builder.Ignore(a => a.HeadersJson);
            builder.Ignore(a => a.ParametersJson);

            var headersProp = builder.Property(a => a.Headers)
                .HasConversion(converter)
                .HasColumnName(nameof(ApiConfiguration.HeadersJson))
                .HasColumnType("TEXT");

            headersProp.Metadata.SetValueComparer(comparer);

            var parametersProp = builder.Property(a => a.Parameters)
                .HasConversion(converter)
                .HasColumnName(nameof(ApiConfiguration.ParametersJson))
                .HasColumnType("TEXT");

            parametersProp.Metadata.SetValueComparer(comparer);
        }
    }
}

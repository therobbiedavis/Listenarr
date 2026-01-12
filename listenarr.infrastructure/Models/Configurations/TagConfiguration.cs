using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class TagConfiguration : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Label).IsRequired().HasMaxLength(128);
            builder.HasIndex(t => t.Label).IsUnique();
        }
    }
}

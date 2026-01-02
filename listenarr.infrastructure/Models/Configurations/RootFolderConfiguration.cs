using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;

namespace Listenarr.Infrastructure.Models.Configurations
{
    public class RootFolderConfiguration : IEntityTypeConfiguration<RootFolder>
    {
        public void Configure(EntityTypeBuilder<RootFolder> builder)
        {
            builder.ToTable("RootFolders");

            builder.HasIndex(r => r.Path).IsUnique();
            builder.HasIndex(r => r.Name);

            builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
            builder.Property(r => r.Path).HasMaxLength(1000).IsRequired();
            builder.Property(r => r.IsDefault).HasDefaultValue(false);

            builder.Property(r => r.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
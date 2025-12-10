using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Listenarr.Domain.Models;
using System.Text.Json;

namespace Listenarr.Infrastructure.Models.Configurations
{
    /// <summary>
    /// EF mapping for ApplicationSettings extracted from ListenArrDbContext.
    /// Handles pipe-delimited list conversions and JSON-serialized complex properties.
    /// </summary>
    public class ApplicationSettingsConfiguration : IEntityTypeConfiguration<ApplicationSettings>
    {
        private static ValueComparer<List<string>> StringListComparer() =>
            new ValueComparer<List<string>>(
                (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
                c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<string>() : c.ToList()
            );

        private static ValueComparer<List<WebhookConfiguration>?> WebhookListComparer() =>
            new ValueComparer<List<WebhookConfiguration>?>(
                (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                c => c == null ? 0 : JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                c => c == null ? null : JsonSerializer.Deserialize<List<WebhookConfiguration>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)
            );

        public void Configure(EntityTypeBuilder<ApplicationSettings> builder)
        {
            // AllowedFileExtensions stored as pipe-delimited list
            builder.Property(e => e.AllowedFileExtensions)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.AllowedFileExtensions)
                .Metadata.SetValueComparer(StringListComparer());

            // EnabledNotificationTriggers stored as pipe-delimited list
            builder.Property(e => e.EnabledNotificationTriggers)
                .HasConversion(
                    v => string.Join("|", v ?? new List<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split('|', System.StringSplitOptions.RemoveEmptyEntries).ToList()
                );
            builder.Property(e => e.EnabledNotificationTriggers)
                .Metadata.SetValueComparer(StringListComparer());

            // Webhooks stored as JSON
            builder.Property(e => e.Webhooks)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? null
                        : JsonSerializer.Deserialize<List<WebhookConfiguration>>(v, (JsonSerializerOptions?)null)
                );
            builder.Property(e => e.Webhooks)
                .Metadata.SetValueComparer(WebhookListComparer());
        }
    }
}

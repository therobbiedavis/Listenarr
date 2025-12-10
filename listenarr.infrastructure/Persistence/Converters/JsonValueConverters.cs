using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Listenarr.Infrastructure.Persistence.Converters
{
    internal static class JsonConverterHelpers
    {
        // Safe serializer: returns empty string for null values so expression lambdas remain simple.
        public static string SerializeObject<T>(T? value) =>
            value == null
                ? string.Empty
                : JsonSerializer.Serialize(value);

        // Deserialize into a non-null instance when possible. Internal implementation can be statement-bodied
        // because lambdas will call this single helper method (keeps the expression tree lambdas simple).
        public static T DeserializeObjectOrNew<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                try { return Activator.CreateInstance<T>()!; }
                catch { return default!; }
            }

            // Quick heuristic: check first non-whitespace character to avoid attempting
            // JSON deserialization on clearly non-JSON values (e.g., legacy single-letter
            // flags or database placeholders). Valid JSON values start with '{', '[' or
            // '"' (string), digits, '-', or the literals 't','f','n'. If the value does
            // not look like JSON, return a new instance without attempting to deserialize.
            var trimmed = json.TrimStart();
            if (trimmed.Length == 0)
            {
                try { return Activator.CreateInstance<T>()!; }
                catch { return default!; }
            }

            var first = trimmed[0];
            if (first != '{' && first != '[' && first != '"' && first != 't' && first != 'f' && first != 'n' && first != '-' && !char.IsDigit(first))
            {
                try { return Activator.CreateInstance<T>()!; }
                catch { return default!; }
            }

            try
            {
                var des = JsonSerializer.Deserialize<T>(json);
                if (des != null) return des;
            }
            catch
            {
                // Ignore deserialization errors and fall back to creating new instance
            }

            try { return Activator.CreateInstance<T>()!; }
            catch { return default!; }
        }
    }

    public class JsonValueConverter<T> : ValueConverter<T, string>
    {
        public JsonValueConverter()
            : base(
                v => JsonConverterHelpers.SerializeObject(v),
                s => JsonConverterHelpers.DeserializeObjectOrNew<T>(s))
        {
        }
    }

    public static class JsonValueComparer
    {
        public static ValueComparer<T> Create<T>() =>
            new ValueComparer<T>(
                (a, b) => JsonConverterHelpers.SerializeObject(a) == JsonConverterHelpers.SerializeObject(b),
                v => JsonConverterHelpers.SerializeObject(v).GetHashCode(),
                v => JsonConverterHelpers.DeserializeObjectOrNew<T>(JsonConverterHelpers.SerializeObject(v)));
    }
}

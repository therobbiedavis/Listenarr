using System;
using System.Collections.Generic;
using System.Linq;

namespace Listenarr.Api.Services
{
    internal static class LogRedaction
    {
        // Default secret environment keys we consider sensitive
        private static readonly string[] DefaultKeys = new[]
        {
            "LISTENARR_API_KEY",
            "DISCORD_TOKEN",
            "PASSWORD",
            "SECRET",
            "API_KEY",
            "TOKEN"
        };

        // Redact occurrences of known secret values in a freeform text block.
        public static string RedactText(string? text, IEnumerable<string?>? secretValues = null)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var redacted = text!;

            // If no explicit secret values provided, just return text unchanged
            if (secretValues != null)
            {
                foreach (var s in secretValues.Where(v => !string.IsNullOrEmpty(v)).Distinct())
                {
                    try
                    {
                        redacted = redacted.Replace(s!, "<redacted>", StringComparison.Ordinal);
                    }
                    catch { }
                }
            }

            return redacted;
        }

        // Mask environment dictionary values for logging. Caller can map StringDictionary to IEnumerable of keys/values.
        public static IDictionary<string, string> RedactEnvironment(IEnumerable<KeyValuePair<string, string>> env, IEnumerable<string>? sensitiveKeys = null)
        {
            sensitiveKeys ??= DefaultKeys;
            var set = new HashSet<string>(sensitiveKeys, StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in env)
            {
                if (kv.Key == null) continue;
                if (set.Contains(kv.Key))
                {
                    result[kv.Key] = "<redacted>";
                }
                else
                {
                    result[kv.Key] = kv.Value ?? string.Empty;
                }
            }

            return result;
        }

        // Collect sensitive values from environment variables declared in DefaultKeys.
        public static IEnumerable<string> GetSensitiveValuesFromEnvironment()
        {
            var vals = new List<string>();
            foreach (var k in DefaultKeys)
            {
                try
                {
                    var v = Environment.GetEnvironmentVariable(k);
                    if (!string.IsNullOrEmpty(v)) vals.Add(v!);
                }
                catch { }
            }

            return vals;
        }
    }
}

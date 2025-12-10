using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Listenarr.Api.Services.Adapters
{
    /// <summary>
    /// Validates DownloadClientsOptions at startup to surface configuration problems early.
    /// Ensures each configured client has a Type and Host/Port at minimum.
    /// </summary>
    public class DownloadClientsOptionsValidator : IValidateOptions<DownloadClientsOptions>
    {
        public ValidateOptionsResult Validate(string? name, DownloadClientsOptions options)
        {
            if (options == null) return ValidateOptionsResult.Fail("DownloadClients configuration section is missing.");

            var errors = new List<string>();

            foreach (var kv in options.Clients)
            {
                var key = kv.Key ?? "<unknown>";
                var c = kv.Value;
                if (c == null)
                {
                    errors.Add($"Download client '{key}' is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(c.Type))
                    errors.Add($"Download client '{key}' type is not configured (Type).");

                if (string.IsNullOrWhiteSpace(c.Host))
                    errors.Add($"Download client '{key}' host is not configured (Host).");

                if (c.Port <= 0 || c.Port > 65535)
                    errors.Add($"Download client '{key}' has invalid Port value: {c?.Port}.");

                // Additional checks: but keep light to avoid blocking dynamic setups.
            }

            if (errors.Count > 0) return ValidateOptionsResult.Fail(errors);
            return ValidateOptionsResult.Success;
        }
    }
}

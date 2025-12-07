using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminDataNormalizationController : ControllerBase
    {
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly ILogger<AdminDataNormalizationController> _logger;

        public AdminDataNormalizationController(IDbContextFactory<ListenArrDbContext> dbFactory, ILogger<AdminDataNormalizationController> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        /// <summary>
        /// One-time safe normalization for legacy non-JSON values stored in TEXT columns that should contain JSON.
        /// This endpoint is idempotent and will only update rows that cannot be parsed as JSON for the expected shape.
        /// It currently targets QualityProfiles (Qualities, PreferredFormats, PreferredLanguages, MustContain, MustNotContain)
        /// and DownloadClientConfigurations (SettingsJson).
        /// </summary>
        [HttpPost("normalize-json")]
        public async Task<IActionResult> NormalizeJsonColumns()
        {
            var summary = new Dictionary<string, object>();
            await using var ctx = await _dbFactory.CreateDbContextAsync();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            try
            {
                // Normalize QualityProfiles
                var qpCmd = conn.CreateCommand();
                qpCmd.CommandText = "SELECT Id, Qualities, PreferredFormats, PreferredLanguages, MustContain, MustNotContain FROM QualityProfiles";
                var qpFixed = 0; var qpChecked = 0;
                await using (var reader = await qpCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        qpChecked++;
                        var id = reader.GetInt32(0);
                        var cols = new Dictionary<string, string?>
                        {
                            ["Qualities"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                            ["PreferredFormats"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ["PreferredLanguages"] = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ["MustContain"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                            ["MustNotContain"] = reader.IsDBNull(5) ? null : reader.GetString(5)
                        };

                        var updates = new Dictionary<string, string>();

                        foreach (var kv in cols)
                        {
                            var colName = kv.Key;
                            var raw = kv.Value;
                            if (string.IsNullOrWhiteSpace(raw)) continue;
                            if (LooksLikeJson(raw))
                            {
                                // Quick test: try to parse as JSON array for these list-like columns
                                try
                                {
                                    JsonSerializer.Deserialize<object>(raw);
                                    continue; // valid JSON
                                }
                                catch { /* fallthrough to normalization */ }
                            }

                            // Normalize depending on column expectation
                            string normalized;
                            if (colName == "Qualities")
                            {
                                // Try pipe-delimited -> create list of QualityDefinition-like objects
                                var parts = raw.Split(new[] {'|',','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length>0).ToList();
                                var qualityObjs = parts.Select(p => new { Quality = p, Allowed = true, Priority = 0 }).ToList();
                                normalized = JsonSerializer.Serialize(qualityObjs);
                            }
                            else
                            {
                                // Treat as list of strings: either pipe/comma-separated or single token -> wrap into JSON array
                                var parts = raw.Split(new[] {'|',','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length>0).ToList();
                                normalized = JsonSerializer.Serialize(parts);
                            }

                            updates[colName] = normalized;
                        }

                        if (updates.Any())
                        {
                            // Build update statement
                            var setClauses = string.Join(", ", updates.Keys.Select(k => $"{k} = @{k}"));
                            var updCmd = conn.CreateCommand();
                            updCmd.CommandText = $"UPDATE QualityProfiles SET {setClauses} WHERE Id = @id";
                            var idParam = updCmd.CreateParameter(); idParam.ParameterName = "@id"; idParam.Value = id; updCmd.Parameters.Add(idParam);
                            foreach (var kv in updates)
                            {
                                var p = updCmd.CreateParameter(); p.ParameterName = "@" + kv.Key; p.Value = kv.Value; updCmd.Parameters.Add(p);
                            }
                            var affected = await updCmd.ExecuteNonQueryAsync();
                            if (affected > 0) qpFixed++;
                            _logger.LogInformation("Normalized QualityProfile {Id}: fixed columns {Cols}", id, string.Join(',', updates.Keys));
                        }
                    }
                }

                summary["QualityProfilesChecked"] = qpChecked;
                summary["QualityProfilesFixed"] = qpFixed;

                // Normalize DownloadClientConfigurations.SettingsJson (stored as TEXT)
                var dcCmd = conn.CreateCommand();
                dcCmd.CommandText = "SELECT Id, SettingsJson FROM DownloadClientConfigurations";
                var dcChecked = 0; var dcFixed = 0;
                await using (var reader = await dcCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dcChecked++;
                        var id = reader.GetInt32(0);
                        var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        if (LooksLikeJson(raw))
                        {
                            try { JsonSerializer.Deserialize<object>(raw); continue; } catch { }
                        }

                        // If not valid JSON, replace with empty object to avoid parser exceptions.
                        var normalized = JsonSerializer.Serialize(new Dictionary<string, object>());
                        var updCmd = conn.CreateCommand();
                        updCmd.CommandText = "UPDATE DownloadClientConfigurations SET SettingsJson = @settings WHERE Id = @id";
                        var p1 = updCmd.CreateParameter(); p1.ParameterName = "@settings"; p1.Value = normalized; updCmd.Parameters.Add(p1);
                        var p2 = updCmd.CreateParameter(); p2.ParameterName = "@id"; p2.Value = id; updCmd.Parameters.Add(p2);
                        var affected = await updCmd.ExecuteNonQueryAsync();
                        if (affected > 0) dcFixed++;
                        _logger.LogInformation("Normalized DownloadClientConfiguration {Id} SettingsJson (replaced legacy token)", id);
                    }
                }

                summary["DownloadClientConfigurationsChecked"] = dcChecked;
                summary["DownloadClientConfigurationsFixed"] = dcFixed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Normalization encountered an unexpected error");
                return StatusCode(500, new { message = "Normalization failed", error = ex.Message });
            }
            finally
            {
                await conn.CloseAsync();
            }

            return Ok(new { message = "Normalization completed", summary });
        }

        private static bool LooksLikeJson(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var trimmed = s.TrimStart();
            if (trimmed.Length == 0) return false;
            var c = trimmed[0];
            return c == '{' || c == '[' || c == '"' || c == 't' || c == 'f' || c == 'n' || c == '-' || char.IsDigit(c);
        }
    }
}

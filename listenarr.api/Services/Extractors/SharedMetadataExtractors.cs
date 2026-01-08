/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HtmlAgilityPack;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Extractors
{
    /// <summary>
    /// Shared metadata extraction utilities that work across both Amazon and Audible sites.
    /// These methods use generic patterns or standard formats (like JSON-LD).
    /// </summary>
    public static class SharedMetadataExtractors
    {
        public record JsonLdData(string? Title, string? Description, string? ImageUrl, string? PublishYear, string? Language, string? Publisher, List<string>? Authors, List<string>? Narrators);

        public static JsonLdData? ExtractFromJsonLd(HtmlDocument doc, ILogger logger)
        {
            try
            {
                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes == null) return null;
                foreach (var sn in scriptNodes)
                {
                    var text = sn.InnerText?.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    try
                    {
                        using var docJson = JsonDocument.Parse(text);
                        var root = docJson.RootElement;
                        // Handle either an object or an array
                        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                        {
                            root = root[0];
                        }

                        // Basic validation: prefer JSON-LD objects that represent Books/Audiobooks/CreativeWork
                        string? type = null;
                        if (root.TryGetProperty("@type", out var t)) type = t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                        else if (root.TryGetProperty("type", out var t2)) type = t2.ValueKind == JsonValueKind.String ? t2.GetString() : null;

                        var allowedTypes = new[] { "Book", "Audiobook", "CreativeWork", "Product" };
                        var hasAuthorOrIsbn = root.TryGetProperty("author", out _) || root.TryGetProperty("isbn", out _);

                        if (!string.IsNullOrWhiteSpace(type))
                        {
                            if (!allowedTypes.Any(a => string.Equals(a, type, StringComparison.OrdinalIgnoreCase)) && !hasAuthorOrIsbn)
                            {
                                continue;
                            }
                        }
                        else if (!hasAuthorOrIsbn)
                        {
                            continue;
                        }

                        string? title = null, desc = null, img = null, year = null, lang = null, pub = null;
                        List<string>? authors = null;
                        if (root.TryGetProperty("name", out var p)) title = p.GetString();
                        if (root.TryGetProperty("headline", out p)) desc = p.GetString();
                        if (root.TryGetProperty("description", out p) && string.IsNullOrEmpty(desc)) desc = p.GetString();
                        if (root.TryGetProperty("image", out p)) img = p.ValueKind == JsonValueKind.String ? p.GetString() : p[0].GetString();
                        if (root.TryGetProperty("datePublished", out p)) year = p.GetString();
                        if (root.TryGetProperty("inLanguage", out p)) lang = p.GetString();
                        if (root.TryGetProperty("publisher", out p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var np)) pub = np.GetString();
                        if (root.TryGetProperty("author", out p))
                        {
                            authors = new List<string>();
                            if (p.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var a in p.EnumerateArray())
                                {
                                    if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
                                    else if (a.ValueKind == JsonValueKind.String) authors.Add(a.GetString() ?? "");
                                }
                            }
                            else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
                            else if (p.ValueKind == JsonValueKind.String) authors.Add(p.GetString() ?? "");
                        }

                        List<string>? narrators = null;
                        if (root.TryGetProperty("readBy", out p) || root.TryGetProperty("narrator", out p))
                        {
                            narrators = new List<string>();
                            if (p.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var n in p.EnumerateArray())
                                {
                                    if (n.ValueKind == JsonValueKind.Object && n.TryGetProperty("name", out var nn)) narrators.Add(nn.GetString() ?? "");
                                    else if (n.ValueKind == JsonValueKind.String) narrators.Add(n.GetString() ?? "");
                                }
                            }
                            else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var nn)) narrators.Add(nn.GetString() ?? "");
                            else if (p.ValueKind == JsonValueKind.String) narrators.Add(p.GetString() ?? "");
                        }

                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            var tnorm = title.Trim();
                            if (string.Equals(tnorm, "About the Author", StringComparison.OrdinalIgnoreCase) || tnorm.Length < 5)
                            {
                                continue;
                            }
                        }

                        return new JsonLdData(title, desc, img, year, lang, pub, authors, narrators);
                    }
                    catch (JsonException) { continue; }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error parsing JSON-LD");
            }
            return null;
        }

        public static string? ExtractYear(string? dateText)
        {
            if (string.IsNullOrEmpty(dateText)) return null;
            var yearMatch = System.Text.RegularExpressions.Regex.Match(dateText, @"\b(19|20)\d{2}\b");
            return yearMatch.Success ? yearMatch.Value : null;
        }

        public static string? NormalizeNameString(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = System.Text.RegularExpressions.Regex.Replace(raw, "Narrated by:|Narrated by|Narrator:|By:", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            raw = raw.Replace("\n", ", ").Replace("\\r", ", ").Replace("\t", " ");
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @",?\s*\((?:Author|Narrator|Reader|Performer|Contributor)\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var parts = System.Text.RegularExpressions.Regex.Split(raw, @",|\band\b|/|&|\\u0026");
            var names = parts.Select(p => p?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (!names.Any()) return null;
            return string.Join(", ", names);
        }

        public static string? CleanImageUrl(string? imgUrl, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(imgUrl)) return null;

            if (imgUrl.Contains("/navigation/") ||
                imgUrl.Contains("logo") ||
                imgUrl.Contains("/batch/") ||
                imgUrl.Contains("fls-na.amazon") ||
                imgUrl.StartsWith("data:"))
            {
                logger.LogDebug("Filtered out non-product image: {Url}", imgUrl);
                return null;
            }

            var cleanUrl = imgUrl;
            if (imgUrl.Contains("PJAdblSocialShare") || imgUrl.Contains("_CLa") || imgUrl.Contains("_SL10_"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(imgUrl, @"/images/I/([A-Za-z0-9_-]+)\.");
                if (match.Success)
                {
                    var imageId = match.Groups[1].Value;
                    cleanUrl = $"https://m.media-amazon.com/images/I/{imageId}._SL500_.jpg";
                    logger.LogDebug("Cleaned social share URL to: {CleanUrl}", cleanUrl);
                }
            }

            return cleanUrl;
        }

        public static bool IsUnavailablePage(HtmlDocument doc)
        {
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty;
            if (!string.IsNullOrEmpty(title) && title.ToLower().Contains("not available")) return true;
            var bodyText = doc.DocumentNode.InnerText ?? string.Empty;
            if (bodyText.IndexOf("this audiobook is not available", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (bodyText.IndexOf("audiobook is not available", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static string CleanLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return language;
            var cleaned = System.Text.RegularExpressions.Regex.Replace(language, @"\s*-\s*(USD|EUR|GBP|CAD|AUD|INR)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleaned = cleaned.Replace("Language:", "").Trim();
            return cleaned;
        }

        public static bool IsAuthorNoise(string? author)
        {
            if (string.IsNullOrWhiteSpace(author)) return true;
            var a = author.Trim();
            if (a.Length < 2) return true;

            var noisePhrases = new[] {
                "Shop By", "Shop by", "Authors", "By:", "Sort by",
                "Written by", "See all", "Browse", "More by",
                "Visit Amazon", "Visit", "Learn more"
            };

            foreach (var noise in noisePhrases)
            {
                if (a.Contains(noise, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public static void ParseSeriesFromTitle(AudibleBookMetadata metadata, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(metadata.Title)) return;

            var title = metadata.Title;

            var match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?):\s*([^,]+),?\s+Book\s+(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.Series = match.Groups[2].Value.Trim();
                metadata.SeriesNumber = match.Groups[3].Value.Trim();
                logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }

            match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?)\s*\(([^,\)]+),?\s+Book\s+(\d+)\)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.Series = match.Groups[2].Value.Trim();
                metadata.SeriesNumber = match.Groups[3].Value.Trim();
                logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }

            match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?)[,:]\s+Book\s+(\d+)\s+of\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.SeriesNumber = match.Groups[2].Value.Trim();
                metadata.Series = match.Groups[3].Value.Trim();
                logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }
        }
    }
}

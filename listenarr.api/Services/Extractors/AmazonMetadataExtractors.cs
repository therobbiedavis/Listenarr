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
using System.Linq;
using HtmlAgilityPack;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Extractors
{
    /// <summary>
    /// Amazon-specific metadata extraction utilities.
    /// These methods target Amazon.com HTML structures like detailBullets_feature_div and audibleproductdetails_feature_div.
    /// </summary>
    public static class AmazonMetadataExtractors
    {
        public static string? ExtractTitle(HtmlDocument doc)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode(
                "//span[@id='productTitle'] | " +
                "//h1[@id='title'] | " +
                "//h1[contains(@class,'productTitle')] | " +
                "//meta[@property='og:title']");

            if (titleNode != null)
            {
                if (titleNode.Name == "meta")
                {
                    return titleNode.GetAttributeValue("content", null)?.Trim();
                }
                return titleNode.InnerText?.Trim();
            }
            return null;
        }

        public static string? ExtractImageUrl(HtmlDocument doc, ILogger logger)
        {
            var imageNode = doc.DocumentNode.SelectSingleNode(
                "//img[contains(@id,'ebooksImgBlkFront')] | " +
                "//img[contains(@id,'imgBlkFront')] | " +
                "//img[@id='landingImage'] | " +
                "//img[@id='original-main-image'] | " +
                "//img[@data-old-hires] | " +
                "//div[@id='img-canvas']//img | " +
                "//div[@id='imageBlock']//img | " +
                "//meta[@property='og:image']");

            if (imageNode != null)
            {
                if (imageNode.Name == "meta")
                {
                    return imageNode.GetAttributeValue("content", null);
                }

                // Try data-old-hires first (highest quality)
                var imgUrl = imageNode.GetAttributeValue("data-old-hires", null);
                if (string.IsNullOrWhiteSpace(imgUrl))
                {
                    // Try data-a-dynamic-image (JSON with multiple sizes)
                    var dynamicImageJson = imageNode.GetAttributeValue("data-a-dynamic-image", null);
                    if (!string.IsNullOrWhiteSpace(dynamicImageJson))
                    {
                        // Parse JSON and get the first URL (usually highest quality)
                        var match = System.Text.RegularExpressions.Regex.Match(dynamicImageJson, @"""(https?://[^""]+)""");
                        if (match.Success)
                        {
                            imgUrl = match.Groups[1].Value;
                        }
                        else
                        {
                            // If regex fails, don't use the JSON string
                            logger.LogWarning("Failed to parse dynamic image JSON: {Json}", dynamicImageJson.Substring(0, Math.Min(100, dynamicImageJson.Length)));
                            imgUrl = null;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(imgUrl))
                {
                    // Fallback to src attribute
                    imgUrl = imageNode.GetAttributeValue("src", null);
                }

                return SharedMetadataExtractors.CleanImageUrl(imgUrl, logger);
            }
            return null;
        }

        public static string? ExtractLanguage(HtmlDocument doc)
        {
            var languageNode = doc.DocumentNode.SelectSingleNode(
                "//div[@id='detailBullets_feature_div']//span[text()='Language']//following-sibling::span | " +
                "//div[@id='detailBullets_feature_div']//li[contains(.,'Language')]//span[position()>1] | " +
                "//li[contains(.,'Language')]//span[last()] | " +
                "//span[text()='Language:']/following-sibling::span | " +
                "//th[contains(text(),'Language')]/following-sibling::td | " +
                "//td[contains(text(),'Language:')]/following-sibling::td | " +
                "//tr[@id='detailsLanguage']//td//span");
            var text = languageNode?.InnerText.Trim();
            if (text == "Language" || string.IsNullOrEmpty(text))
            {
                var languageListItem = doc.DocumentNode.SelectSingleNode("//li[contains(.,'Language')]");
                if (languageListItem != null)
                {
                    var listText = languageListItem.InnerText.Trim();
                    var match = System.Text.RegularExpressions.Regex.Match(listText, @"Language[:\s]+([^;\(\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        text = match.Groups[1].Value.Trim();
                    }
                }
            }
            if (!string.IsNullOrEmpty(text) && text != "Language")
            {
                text = text.Replace("Language:", "").Trim();
                return text;
            }
            return null;
        }

        public static string? ExtractDescription(HtmlDocument doc)
        {
            var descriptionNode = doc.DocumentNode.SelectSingleNode(
                "//div[@data-expanded and contains(@class,'a-expander-content')] | " +
                "//div[@id='bookDescription_feature_div']//div[contains(@class,'a-expander-content')] | " +
                "//div[@id='bookDescription_feature_div']//noscript | " +
                "//div[@id='bookDescription_feature_div']//span | " +
                "//div[@id='productDescription']//p | " +
                "//div[@id='productDescription'] | " +
                "//div[contains(@class,'bookDescription')]//span | " +
                "//div[contains(@class,'productDescription')] | " +
                "//span[@id='productDescription'] | " +
                "//div[@data-feature-name='bookDescription']//span");
            if (descriptionNode != null)
            {
                var htmlContent = descriptionNode.InnerHtml?.Trim();
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, @"\s+", " ");
                    htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, @">\s+<", "><");
                    return htmlContent.Trim();
                }
            }
            return null;
        }

        public static string? ExtractPublishYear(HtmlDocument doc)
        {
            var releaseDateNode = doc.DocumentNode.SelectSingleNode(
                "//div[@id='detailBullets_feature_div']//span[contains(text(),'Audible.com release date') or contains(text(),'Release date')]/following-sibling::span | " +
                "//li[contains(.,'Audible.com release date') or contains(.,'Release date')]//span[last()] | " +
                "//span[contains(text(),'release date')]/following-sibling::span | " +
                "//th[contains(text(),'Release')]/following-sibling::td | " +
                "//td[contains(text(),'Release')]/following-sibling::td");
            var dateText = releaseDateNode?.InnerText.Trim();
            if (string.IsNullOrEmpty(dateText) || dateText.ToLower().Contains("release"))
            {
                var releaseDateListItem = doc.DocumentNode.SelectSingleNode("//li[contains(.,'release date')]");
                if (releaseDateListItem != null)
                {
                    var listText = releaseDateListItem.InnerText.Trim();
                    var match = System.Text.RegularExpressions.Regex.Match(listText, @"release date[:\s]+([^;\(\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        dateText = match.Groups[1].Value.Trim();
                    }
                }
            }
            return SharedMetadataExtractors.ExtractYear(dateText);
        }

        public static void ExtractFromAmazonAudibleDetails(HtmlDocument doc, AudibleBookMetadata metadata, ILogger logger)
        {
            var audibleDetails = doc.DocumentNode.SelectSingleNode("//div[@id='audibleproductdetails_feature_div']");
            if (audibleDetails == null)
            {
                logger.LogDebug("audibleproductdetails_feature_div not found for ASIN {Asin}", metadata.Asin);
                return;
            }

            logger.LogInformation("Found audibleproductdetails_feature_div for ASIN {Asin}", metadata.Asin);

            var rows = audibleDetails.SelectNodes(".//tr");
            if (rows == null || !rows.Any())
            {
                logger.LogDebug("No rows found in audibleproductdetails_feature_div");
                return;
            }

            foreach (var row in rows)
            {
                var labelCell = row.SelectSingleNode(".//td[@class='a-span3']|.//th");
                var valueCell = row.SelectSingleNode(".//td[@class='a-span9']|.//td[not(@class='a-span3')]");

                if (labelCell == null || valueCell == null) continue;

                var label = labelCell.InnerText?.Trim().Replace(":", "").Trim();
                var value = valueCell.InnerText?.Trim();

                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value)) continue;

                logger.LogDebug("Amazon detail - {Label}: {Value}", label, value);

                switch (label.ToLower())
                {
                    case "author":
                    case "authors":
                        if (metadata.Authors == null || !metadata.Authors.Any())
                        {
                            var authors = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => SharedMetadataExtractors.NormalizeNameString(a))
                                .Where(a => !string.IsNullOrWhiteSpace(a))
                                .ToList();
                            if (authors.Any())
                            {
                                metadata.Authors = authors!;
                                logger.LogInformation("Extracted authors from Amazon details: {Authors}", string.Join(", ", authors));
                            }
                        }
                        break;

                    case "narrator":
                    case "narrators":
                    case "narrated by":
                        if (metadata.Narrators == null || !metadata.Narrators.Any())
                        {
                            var narrators = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(n => SharedMetadataExtractors.NormalizeNameString(n))
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .ToList();
                            if (narrators.Any())
                            {
                                metadata.Narrators = narrators!;
                                logger.LogInformation("Extracted narrators from Amazon details: {Narrators}", string.Join(", ", narrators));
                            }
                        }
                        break;

                    case "listening length":
                    case "length":
                        if (metadata.Runtime == null)
                        {
                            var hrsMatch = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)\s*hour");
                            var minsMatch = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)\s*minute");
                            int total = 0;
                            if (hrsMatch.Success) total += int.Parse(hrsMatch.Groups[1].Value) * 60;
                            if (minsMatch.Success) total += int.Parse(minsMatch.Groups[1].Value);
                            if (total > 0)
                            {
                                metadata.Runtime = total;
                                logger.LogInformation("Extracted runtime from Amazon details: {Runtime} minutes", total);
                            }
                        }
                        break;

                    case "publisher":
                        if (string.IsNullOrWhiteSpace(metadata.Publisher))
                        {
                            metadata.Publisher = value;
                            logger.LogInformation("Extracted publisher from Amazon details: {Publisher}", value);
                        }
                        break;

                    case "language":
                        if (string.IsNullOrWhiteSpace(metadata.Language))
                        {
                            metadata.Language = SharedMetadataExtractors.CleanLanguage(value);
                            logger.LogInformation("Extracted language from Amazon details: {Language}", metadata.Language);
                        }
                        break;

                    case "audible.com release date":
                    case "release date":
                        if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                        {
                            var yearMatch = System.Text.RegularExpressions.Regex.Match(value, @"\d{4}");
                            if (yearMatch.Success)
                            {
                                metadata.PublishYear = yearMatch.Value;
                                logger.LogInformation("Extracted publish year from Amazon details: {Year}", metadata.PublishYear);
                            }
                        }
                        break;

                    case "version":
                        if (string.IsNullOrWhiteSpace(metadata.Version))
                        {
                            metadata.Version = value;
                            logger.LogInformation("Extracted version from Amazon details: {Version}", value);
                        }
                        break;

                    case "series":
                        if (string.IsNullOrWhiteSpace(metadata.Series))
                        {
                            var seriesMatch = System.Text.RegularExpressions.Regex.Match(value, @"Book\s+(\d+)\s+of\s+\d+\s+(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (seriesMatch.Success)
                            {
                                metadata.SeriesNumber = seriesMatch.Groups[1].Value;
                                metadata.Series = seriesMatch.Groups[2].Value.Trim();
                                logger.LogInformation("Extracted series from Amazon details: {Series} #{SeriesNumber}", metadata.Series, metadata.SeriesNumber);
                            }
                            else
                            {
                                metadata.Series = value;
                                logger.LogInformation("Extracted series from Amazon details: {Series}", value);
                            }
                        }
                        break;
                }
            }
        }
    }
}

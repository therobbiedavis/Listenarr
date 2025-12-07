using System;
using System.Text.RegularExpressions;

namespace Listenarr.Api.Services.Adapters
{
    /// <summary>
    /// Title matching helpers extracted from DownloadService for easier testing and reuse.
    /// </summary>
    public interface ITitleMatchingService
    {
        string NormalizeTitle(string title);
        bool AreTitlesSimilar(string title1, string title2);
        bool IsMatchingTitle(string title1, string title2);
    }

    public class TitleMatchingService : ITitleMatchingService
    {
        public string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // Remove bracketed content [..], (..), {..}
            var result = Regex.Replace(title, @"\[.*?\]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\(.*?\)", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\{.*?\}", "", RegexOptions.IgnoreCase);

            // Replace common separators with space
            result = Regex.Replace(result, @"[\-_\.]+", " ", RegexOptions.IgnoreCase);

            // Remove common quality/format indicators and numeric bitrates
            result = Regex.Replace(result, @"\b(mp3|m4a|m4b|flac|aac|ogg|opus|320|256|128|v0|v2|audiobook|unabridged|abridged)\b", "", RegexOptions.IgnoreCase);

            // Collapse whitespace
            result = Regex.Replace(result, @"\s+", " ");

            // Trim punctuation/whitespace
            result = result.Trim(' ', '-', '.', ',');

            return result;
        }

        public bool AreTitlesSimilar(string title1, string title2)
        {
            var norm1 = NormalizeTitle(title1);
            var norm2 = NormalizeTitle(title2);

            if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(norm1) && !string.IsNullOrEmpty(norm2))
            {
                if (norm1.Contains(norm2, StringComparison.OrdinalIgnoreCase) ||
                    norm2.Contains(norm1, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (norm1.Length > 20 && norm2.Length > 20)
                {
                    var len = Math.Min(50, Math.Min(norm1.Length, norm2.Length));
                    if (len > 0 && norm1.Substring(0, len).Equals(norm2.Substring(0, len), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public bool IsMatchingTitle(string title1, string title2)
        {
            if (string.IsNullOrEmpty(title1) || string.IsNullOrEmpty(title2))
                return false;

            return AreTitlesSimilar(title1, title2);
        }
    }
}

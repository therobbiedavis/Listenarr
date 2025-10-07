using System.Text;
using System.Text.RegularExpressions;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public class FileNamingService : IFileNamingService
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<FileNamingService> _logger;

        public FileNamingService(IConfigurationService configService, ILogger<FileNamingService> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Apply the configured file naming pattern to generate the final file path
        /// </summary>
        public async Task<string> GenerateFilePathAsync(
            AudioMetadata metadata, 
            int? diskNumber = null, 
            int? chapterNumber = null, 
            string originalExtension = ".m4b")
        {
            var settings = await _configService.GetApplicationSettingsAsync();
            var pattern = settings.FileNamingPattern;
            var outputPath = settings.OutputPath;

            if (string.IsNullOrWhiteSpace(pattern))
            {
                pattern = "{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}";
            }

            // Build variable dictionary
            var variables = new Dictionary<string, object>
            {
                { "Author", SanitizePathComponent(metadata.Artist ?? metadata.AlbumArtist ?? "Unknown Author") },
                { "Series", SanitizePathComponent(metadata.Series ?? metadata.Album ?? metadata.Title ?? "Unknown Series") },
                { "Title", SanitizePathComponent(metadata.Title ?? "Unknown Title") },
                { "SeriesNumber", metadata.SeriesPosition?.ToString() ?? metadata.TrackNumber?.ToString() ?? "" },
                { "Year", metadata.Year?.ToString() ?? "" },
                { "Quality", $"{metadata.Bitrate}kbps {metadata.Format}".Trim() },
                { "DiskNumber", diskNumber?.ToString() ?? metadata.DiscNumber?.ToString() ?? "" },
                { "ChapterNumber", chapterNumber?.ToString() ?? metadata.TrackNumber?.ToString() ?? "" }
            };

            // Apply the naming pattern
            var relativePath = ApplyNamingPattern(pattern, variables);
            
            // Ensure it has the correct extension
            if (!relativePath.EndsWith(originalExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += originalExtension;
            }

            // Combine with output path if configured
            var fullPath = string.IsNullOrWhiteSpace(outputPath) 
                ? relativePath 
                : Path.Combine(outputPath, relativePath);

            _logger.LogInformation("Generated file path: {FilePath}", fullPath);
            return fullPath;
        }

        /// <summary>
        /// Parse a naming pattern and replace variables with actual values
        /// </summary>
        public string ApplyNamingPattern(string pattern, Dictionary<string, object> variables)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return "Unknown";
            }

            var result = pattern;

            // Regex to match variables: {VariableName} or {VariableName:Format}
            var variableRegex = new Regex(@"\{(\w+)(?::([^}]+))?\}", RegexOptions.IgnoreCase);
            
            result = variableRegex.Replace(result, match =>
            {
                var variableName = match.Groups[1].Value;
                var format = match.Groups[2].Success ? match.Groups[2].Value : null;

                if (variables.TryGetValue(variableName, out var value))
                {
                    // Handle empty values
                    if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        return "";
                    }

                    // Apply formatting if specified
                    if (!string.IsNullOrEmpty(format))
                    {
                        // For numeric values with format (e.g., {DiskNumber:00})
                        if (value is int intValue)
                        {
                            return intValue.ToString(format);
                        }
                        else if (int.TryParse(value.ToString(), out var parsedInt))
                        {
                            return parsedInt.ToString(format);
                        }
                    }

                    return value.ToString() ?? "";
                }

                // Variable not found, return empty string
                _logger.LogWarning("Variable {VariableName} not found in naming pattern", variableName);
                return "";
            });

            // Clean up multiple consecutive slashes or spaces
            result = Regex.Replace(result, @"[\\/]{2,}", "/");
            result = Regex.Replace(result, @"\s{2,}", " ");
            
            // Remove leading/trailing slashes and spaces from each path component
            var parts = result.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            result = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Select(p => p.Trim()));

            return result;
        }

        /// <summary>
        /// Remove invalid characters from path components
        /// </summary>
        private string SanitizePathComponent(string pathComponent)
        {
            if (string.IsNullOrWhiteSpace(pathComponent))
            {
                return "Unknown";
            }

            // Get invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            
            // Replace invalid characters with underscore
            var sanitized = new StringBuilder();
            foreach (var c in pathComponent)
            {
                if (invalidChars.Contains(c))
                {
                    sanitized.Append('_');
                }
                else
                {
                    sanitized.Append(c);
                }
            }

            // Trim and handle edge cases
            var result = sanitized.ToString().Trim();
            
            // Ensure it's not empty after sanitization
            if (string.IsNullOrWhiteSpace(result))
            {
                return "Unknown";
            }

            return result;
        }
    }
}

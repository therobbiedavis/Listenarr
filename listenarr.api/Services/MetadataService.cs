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

using Listenarr.Api.Models;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.IO;

namespace Listenarr.Api.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly IFfmpegService _ffmpegService;
        private readonly ILogger<MetadataService> _logger;

        public MetadataService(HttpClient httpClient, IConfigurationService configurationService, ILogger<MetadataService> logger, IFfmpegService ffmpegService)
        {
            _httpClient = httpClient;
            _configurationService = configurationService;
            _ffmpegService = ffmpegService;
            _logger = logger;
        }

        public async Task<AudioMetadata?> GetMetadataAsync(string title, string? artist = null, string? isbn = null)
        {
            try
            {
                var settings = await _configurationService.GetApplicationSettingsAsync();
                var audnexusUrl = settings.AudnexusApiUrl;

                // Build search query for Audnexus API
                string searchQuery;
                if (!string.IsNullOrEmpty(isbn))
                {
                    searchQuery = $"{audnexusUrl}/books/{isbn}";
                }
                else
                {
                    var queryParams = new List<string>();
                    if (!string.IsNullOrEmpty(title)) queryParams.Add($"title={Uri.EscapeDataString(title)}");
                    if (!string.IsNullOrEmpty(artist)) queryParams.Add($"author={Uri.EscapeDataString(artist)}");
                    
                    searchQuery = $"{audnexusUrl}/search?" + string.Join("&", queryParams);
                }

                _logger.LogInformation($"Fetching metadata from Audnexus: {searchQuery}");

                var response = await _httpClient.GetAsync(searchQuery);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Audnexus API returned {response.StatusCode} for query: {searchQuery}");
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                // Parse Audnexus response and convert to AudioMetadata
                // This is a simplified implementation - you would need to adapt based on actual Audnexus API structure
                var audnexusData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                
                return ParseAudnexusResponse(audnexusData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching metadata for title: {title}, artist: {artist}");
                return null;
            }
        }

    public async Task<AudioMetadata> ExtractFileMetadataAsync(string filePath)
        {
            try
            {
                // Run the blocking ffprobe invocation off the calling thread
                var ffprobeResult = await Task.Run(() =>
                {
                    try
                    {
                        // Use the known bundled ffprobe path under the application's config directory.
                        // On Windows the binary is ffprobe.exe, otherwise ffprobe.
                        var ffprobeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";
                        var ffprobePath = Path.Combine(Directory.GetCurrentDirectory(), "config", "ffmpeg", ffprobeName);
                        var ffprobeCmd = ffprobePath; // use explicit bundled path

                        _logger.LogDebug("Attempting to run bundled ffprobe at '{Path}' for file {File}", ffprobePath, filePath);

                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = ffprobeCmd,
                            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = System.Diagnostics.Process.Start(startInfo);
                        if (proc != null)
                        {
                            var output = proc.StandardOutput.ReadToEnd();
                            var err = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(5000);
                            _logger.LogDebug("ffprobe finished for {File} with ExitCode={Exit} StdErrLength={ErrLen}", filePath, proc.ExitCode, err?.Length ?? 0);
                            if (!string.IsNullOrEmpty(err)) _logger.LogDebug("ffprobe stderr for {File}: {Err}", filePath, err);
                            if (!string.IsNullOrEmpty(output))
                            {
                                try
                                {
                                    var doc = JsonSerializer.Deserialize<JsonElement>(output);
                                    var metadata = new AudioMetadata();

                                    // Try to get format info
                                    if (doc.TryGetProperty("format", out var fmt))
                                    {
                                        if (fmt.TryGetProperty("duration", out var durEl) && durEl.ValueKind == JsonValueKind.String)
                                        {
                                            if (double.TryParse(durEl.GetString(), out var dur)) metadata.Duration = TimeSpan.FromSeconds(dur);
                                        }
                                        if (fmt.TryGetProperty("format_name", out var fmtName) && fmtName.ValueKind == JsonValueKind.String)
                                        {
                                            // ffprobe's format_name can be a comma-separated list (e.g. "mov,mp4,m4a,3gp,3g2,mj2").
                                            // Normalize to a single, sensible value. Prefer the file extension when it better represents
                                            // the actual container (for example .m4b should be reported as M4B rather than "mov").
                                            var rawFmt = fmtName.GetString() ?? string.Empty;
                                            var primary = rawFmt.Split(',')[0];

                                            // Determine extension from file path (without leading dot)
                                            var ext = Path.GetExtension(filePath)?.TrimStart('.')?.ToLowerInvariant();

                                            if (!string.IsNullOrEmpty(ext))
                                            {
                                                // If the extension is a more specific container (m4b) or differs from primary token,
                                                // prefer reporting the extension for clarity. Otherwise use the primary token.
                                                if (ext == "m4b")
                                                {
                                                    metadata.Format = ext.ToUpperInvariant();
                                                    metadata.Container = ext.ToUpperInvariant();
                                                }
                                                else
                                                {
                                                    metadata.Format = primary.ToUpperInvariant();
                                                    metadata.Container = primary.ToUpperInvariant();
                                                }
                                            }
                                            else
                                            {
                                                metadata.Format = primary.ToUpperInvariant();
                                                metadata.Container = primary.ToUpperInvariant();
                                            }
                                        }
                                        if (fmt.TryGetProperty("bit_rate", out var br) && br.ValueKind == JsonValueKind.String && int.TryParse(br.GetString(), out var bitRate))
                                        {
                                            metadata.Bitrate = bitRate;
                                        }
                                    }

                                    // Streams: look for audio stream for sample rate, channels
                                    if (doc.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var s in streams.EnumerateArray())
                                        {
                                            if (s.TryGetProperty("codec_type", out var codecType) && codecType.GetString() == "audio")
                                            {
                                                if (s.TryGetProperty("sample_rate", out var sr) && sr.ValueKind == JsonValueKind.String && int.TryParse(sr.GetString(), out var sampleRate))
                                                {
                                                    metadata.SampleRate = sampleRate;
                                                }
                                                if (s.TryGetProperty("channels", out var ch) && ch.ValueKind == JsonValueKind.Number)
                                                {
                                                    metadata.Channels = ch.GetInt32();
                                                }
                                                if (s.TryGetProperty("bit_rate", out var sbr) && sbr.ValueKind == JsonValueKind.String && int.TryParse(sbr.GetString(), out var sbit))
                                                {
                                                    metadata.Bitrate = metadata.Bitrate == 0 ? sbit : metadata.Bitrate;
                                                }
                                                // codec_name is the audio codec used by the stream (e.g., aac, opus, mp3)
                                                if (s.TryGetProperty("codec_name", out var codecName) && codecName.ValueKind == JsonValueKind.String)
                                                {
                                                    metadata.Codec = codecName.GetString();
                                                }
                                                break;
                                            }
                                        }
                                    }

                                    // Fallback: set title and format from filename if missing
                                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                                    if (string.IsNullOrEmpty(metadata.Title)) metadata.Title = fileName;
                                    if (string.IsNullOrEmpty(metadata.Format)) metadata.Format = Path.GetExtension(filePath).TrimStart('.').ToUpper();
                                    if (string.IsNullOrEmpty(metadata.Container)) metadata.Container = Path.GetExtension(filePath).TrimStart('.').ToUpper();

                                    _logger.LogInformation($"Extracted ffprobe metadata from file: {filePath}");
                                    _logger.LogDebug("Parsed metadata: Duration={Duration} seconds, Format={Format}, Bitrate={Bitrate}, SampleRate={SampleRate}, Channels={Channels}", metadata.Duration.TotalSeconds, metadata.Format, metadata.Bitrate, metadata.SampleRate, metadata.Channels);

                                    return metadata;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed parsing ffprobe JSON for file: {File}", filePath);
                                }
                            }
                        }

                        return null;
                    }
                    catch (Exception ex)
                    {
                        // Swallow inside Task.Run and return null so the outer method handles fallback
                        _logger.LogInformation(ex, "ffprobe not available or failed for file: {File}", filePath);
                        return null;
                    }
                });

                if (ffprobeResult != null)
                {
                    return ffprobeResult;
                }

                // Fallback: basic filename-based metadata
                var fallbackName = Path.GetFileNameWithoutExtension(filePath);
                var fallback = new AudioMetadata
                {
                    Title = fallbackName,
                    Format = Path.GetExtension(filePath).TrimStart('.').ToUpper()
                };

                _logger.LogInformation($"Extracted basic metadata from file: {filePath}");
                return fallback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting metadata from file: {filePath}");
                return new AudioMetadata();
            }
        }

        public async Task ApplyMetadataAsync(string filePath, AudioMetadata metadata)
        {
            try
            {
                // This would use a library like TagLib# to apply metadata to audio files
                _logger.LogInformation($"Applied metadata to file: {filePath}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying metadata to file: {filePath}");
            }
        }

        public async Task<byte[]?> DownloadCoverArtAsync(string coverArtUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(coverArtUrl);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading cover art from: {coverArtUrl}");
                return null;
            }
        }

        private AudioMetadata ParseAudnexusResponse(JsonElement audnexusData)
        {
            // This is a simplified parser - adapt based on actual Audnexus API response structure
            var metadata = new AudioMetadata();

            if (audnexusData.TryGetProperty("title", out var title))
                metadata.Title = title.GetString() ?? "";

            if (audnexusData.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            {
                var authorNames = authors.EnumerateArray().Select(a => a.GetString()).Where(s => !string.IsNullOrEmpty(s));
                metadata.Artist = string.Join(", ", authorNames);
            }

            if (audnexusData.TryGetProperty("series", out var series))
                metadata.Series = series.GetString();

            if (audnexusData.TryGetProperty("publishedYear", out var year))
                metadata.Year = year.GetInt32();

            if (audnexusData.TryGetProperty("description", out var description))
                metadata.Description = description.GetString();

            if (audnexusData.TryGetProperty("isbn", out var isbn))
                metadata.Isbn = isbn.GetString();

            if (audnexusData.TryGetProperty("coverUrl", out var coverUrl))
                metadata.CoverArtUrl = coverUrl.GetString();

            return metadata;
        }
    }
}
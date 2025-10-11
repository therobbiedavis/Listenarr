using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class FfmpegInstallerService : IFfmpegService
    {
        private readonly ILogger<FfmpegInstallerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IStartupConfigService _startupConfigService;

        // Allow disabling auto-download via environment variable
        private readonly bool _autoInstall;

        public FfmpegInstallerService(ILogger<FfmpegInstallerService> logger, IStartupConfigService startupConfigService)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _autoInstall = Environment.GetEnvironmentVariable("LISTENARR_AUTO_INSTALL_FFPROBE")?.ToLower() != "false"; // default true
            _startupConfigService = startupConfigService;
        }

        public async Task<string?> GetFfprobePathAsync(bool ensureInstalled = true)
        {
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "config", "ffmpeg");
            Directory.CreateDirectory(baseDir);

            var ffprobeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";
            var ffprobePath = Path.Combine(baseDir, ffprobeName);

            if (File.Exists(ffprobePath))
            {
                _logger.LogInformation("Found bundled ffprobe at {Path}", ffprobePath);
                return ffprobePath;
            }

            if (!ensureInstalled || !_autoInstall)
            {
                _logger.LogInformation("Auto-install of ffprobe is disabled or not requested");
                return null;
            }

            try
            {
                // Select platform-specific URL (prebuilt minimal ffprobe). These URLs may be updated over time.
                // Allow startup config to override provider/release
                string? downloadUrl = GetDownloadUrlForPlatform();
                string? discoveredChecksum = null;
                try
                {
                    var cfg = _startupConfigService.GetConfig();
                    if (cfg?.Ffmpeg?.Provider != null && cfg.Ffmpeg.Provider.StartsWith("github:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Format: github:owner/repo
                        var parts = cfg.Ffmpeg.Provider.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            var repo = parts[1];
                            // Try to discover an asset matching platform/arch and optional release override
                            var assetInfo = await TryDiscoverGithubAssetAsync(repo, cfg.Ffmpeg.ReleaseOverride, cfg.Ffmpeg.Arch);
                            if (!string.IsNullOrEmpty(assetInfo.assetUrl))
                            {
                                downloadUrl = assetInfo.assetUrl;
                                if (!string.IsNullOrEmpty(assetInfo.checksumContent))
                                {
                                    discoveredChecksum = assetInfo.checksumContent;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while reading startup ffmpeg provider config");
                }
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.LogWarning("No ffprobe download URL configured for this platform");
                    return null;
                }

                _logger.LogInformation("Downloading ffprobe from {Url}", downloadUrl);

                using var resp = await _httpClient.GetAsync(downloadUrl);
                resp.EnsureSuccessStatusCode();

                var tmpFile = Path.Combine(baseDir, "ffprobe-download.tmp");
                await using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write))
                {
                    await resp.Content.CopyToAsync(fs);
                }

                // Compute SHA256 for logging / future verification
                string? computedHash = null;
                try
                {
                    using var sha = SHA256.Create();
                    await using var fs2 = File.OpenRead(tmpFile);
                    var hash = await sha.ComputeHashAsync(fs2);
                    var hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    computedHash = hashHex;
                    _logger.LogInformation("Downloaded ffprobe archive SHA256={Hash}", hashHex);
                }
                catch { /* non-fatal */ }

                // If a known checksum is available for this platform (either pinned or discovered), verify it
                var expected = GetChecksumForPlatform();
                // Prefer discovered checksum content from GitHub release discovery
                if (string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(discoveredChecksum))
                {
                    var parsed = ParseChecksumFileForAsset(discoveredChecksum, Path.GetFileName(downloadUrl));
                    if (!string.IsNullOrEmpty(parsed)) expected = parsed;
                }

                // Try to discover checksum files downloaded alongside the archive as a fallback
                if (string.IsNullOrEmpty(expected))
                {
                    try
                    {
                        var checksumFiles = Directory.GetFiles(baseDir, "*checksum*", SearchOption.TopDirectoryOnly)
                            .Concat(Directory.GetFiles(baseDir, "SHA256*", SearchOption.TopDirectoryOnly));
                        foreach (var cf in checksumFiles)
                        {
                            try
                            {
                                var content = await File.ReadAllTextAsync(cf);
                                var parsed = ParseChecksumFileForAsset(content, Path.GetFileName(downloadUrl));
                                if (!string.IsNullOrEmpty(parsed)) { expected = parsed; break; }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(computedHash) && !string.Equals(expected, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Downloaded ffprobe checksum mismatch (expected {Expected} != actual {Actual}). Aborting install.", expected, computedHash);
                    try { File.Delete(tmpFile); } catch { }
                    return null;
                }

                // If it's an archive, attempt to extract using SharpCompress; otherwise move as executable
                if (downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || downloadUrl.EndsWith(".ffmpeg.zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var archive = SharpCompress.Archives.Zip.ZipArchive.Open(tmpFile);
                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        var key = entry.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                        var outPath = Path.Combine(baseDir, key);
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? baseDir);
                        entry.WriteToFile(outPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                    }
                    File.Delete(tmpFile);
                }
                else if (downloadUrl.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) || downloadUrl.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || downloadUrl.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
                {
                    // Use SharpCompress to extract tar(.xz) in managed code
                    try
                    {
                        using var stream = File.OpenRead(tmpFile);
                        var readerOptions = new ReaderOptions { LeaveStreamOpen = false };
                        using var reader = ReaderFactory.Open(stream, readerOptions);
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                var outPath = Path.Combine(baseDir, reader.Entry.Key.Replace('/', Path.DirectorySeparatorChar));
                                Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? baseDir);
                                using var entryStream = reader.OpenEntryStream();
                                await using var outFs = File.Create(outPath);
                                await entryStream.CopyToAsync(outFs);
                            }
                        }
                        File.Delete(tmpFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Managed extraction failed, attempting fallback to system tar for {Tmp}", tmpFile);
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "tar",
                                Arguments = $"-xf \"{tmpFile}\" -C \"{baseDir}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var p = System.Diagnostics.Process.Start(psi);
                            p?.WaitForExit(30000);
                            File.Delete(tmpFile);
                        }
                        catch { /* best-effort */ }
                    }
                }
                else
                {
                    // assume raw binary
                    File.Move(tmpFile, ffprobePath);
                }

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        // ensure any ffprobe under baseDir is executable
                        var candidates = Directory.GetFiles(baseDir, "ffprobe*", SearchOption.AllDirectories);
                        foreach (var cand in candidates)
                        {
                            try
                            {
                                var psiCh = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "chmod",
                                    Arguments = $"+x \"{cand}\"",
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using var p3 = System.Diagnostics.Process.Start(psiCh);
                                p3?.WaitForExit(3000);
                            }
                            catch { /* best effort */ }
                        }
                    }
                    catch { /* best effort */ }
                }

                // Try to locate the ffprobe binary in the extracted files
                    try
                    {
                        var found = new List<string>();
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            found.AddRange(Directory.GetFiles(baseDir, "ffprobe.exe", SearchOption.AllDirectories));
                        }
                        else
                        {
                            found.AddRange(Directory.GetFiles(baseDir, "ffprobe", SearchOption.AllDirectories));
                            found.AddRange(Directory.EnumerateFiles(baseDir, "*ffprobe*", SearchOption.AllDirectories));
                        }

                        if (found.Any())
                        {
                            var chosen = found.OrderBy(f => f.Length).First();
                            var dest = ffprobePath;
                            try { File.Copy(chosen, dest, overwrite: true); } catch { /* best effort */ }
                        }
                    }
                catch { /* non-fatal */ }

                // Write a license notice file
                var licensePath = Path.Combine(baseDir, "LICENSE_NOTICE.txt");
                await File.WriteAllTextAsync(licensePath, "ffprobe binaries downloaded. Review FFmpeg licensing (LGPL/GPL) at https://ffmpeg.org/legal.html\nSource: " + downloadUrl + "\n");

                _logger.LogInformation("ffprobe installed to {Path}", ffprobePath);
                return ffprobePath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download or install ffprobe");
                return null;
            }
        }

        private string? GetDownloadUrlForPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // johnvansickle static build (x86_64)
                return "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // evermeet/ffmpeg provides static macOS builds (note: keep an eye on licesning)
                return "https://evermeet.cx/ffmpeg/ffmpeg-6.0.zip"; // example; may need updating
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // gyan.dev builds
                return "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
            }

            return null;
        }

        private string? GetChecksumForPlatform()
        {
            // For production you should pin the checksums for each provider + archive
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return null; // placeholder - add SHA256 hex string
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return null;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            return null;
        }

        private async Task<(string? assetUrl, string? checksumContent)> TryDiscoverGithubAssetAsync(string repo, string? releaseOverride, string? arch)
        {
            try
            {
                // Use GitHub Releases API: https://api.github.com/repos/{owner}/{repo}/releases
                var releasesUrl = $"https://api.github.com/repos/{repo}/releases";
                var req = new HttpRequestMessage(HttpMethod.Get, releasesUrl);
                req.Headers.Add("User-Agent", "Listenarr-Installer");
                using var resp = await _httpClient.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadAsStringAsync();
                var docs = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
                if (docs.ValueKind != System.Text.Json.JsonValueKind.Array) return (null, null);

                foreach (var release in docs.EnumerateArray())
                {
                    var tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(releaseOverride) && !tag.Contains(releaseOverride, StringComparison.OrdinalIgnoreCase)) continue;
                    if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        string? checksumContent = null;
                        string? chosenUrl = null;
                        string? chosenName = null;
                        // First, attempt to find checksum asset(s)
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? string.Empty;
                            var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(url)) continue;
                            if (name.Contains("sha256", StringComparison.OrdinalIgnoreCase) || name.Contains("checksum", StringComparison.OrdinalIgnoreCase) || name.Contains("sha256sums", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    var c = await (await _httpClient.GetAsync(url)).Content.ReadAsStringAsync();
                                    if (!string.IsNullOrEmpty(c)) checksumContent = c;
                                }
                                catch { }
                            }
                        }

                        // Then find a matching asset for platform/arch
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? string.Empty;
                            var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(url)) continue;
                            if (!string.IsNullOrEmpty(arch) && !name.Contains(arch, StringComparison.OrdinalIgnoreCase)) continue;
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                            {
                                chosenUrl = url;
                                chosenName = name;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(chosenUrl)) return (chosenUrl, checksumContent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub asset discovery failed for repo {Repo}", repo);
            }

            return (null, null);
        }

        private static string? ParseChecksumFileForAsset(string checksumFileContent, string assetFileName)
        {
            if (string.IsNullOrEmpty(checksumFileContent) || string.IsNullOrEmpty(assetFileName)) return null;
            using var sr = new StringReader(checksumFileContent);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                // Common formats: "<checksum>  <filename>" or "<checksum>  *<filename>" or "<checksum> <filename>"
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var possibleHash = parts[0].Trim();
                    var possibleName = parts[^1].Trim();
                    if (possibleName.StartsWith("*")) possibleName = possibleName[1..];
                    if (possibleName.Equals(assetFileName, StringComparison.OrdinalIgnoreCase) || possibleName.EndsWith(assetFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return possibleHash;
                    }
                }
                else
                {
                    // Some checksum files list "filename: hash" or JSON; do simple contains
                    if (trimmed.Contains(assetFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var tokens = trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 2)
                        {
                            var candidate = tokens[1].Trim();
                            var candidateToken = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            if (!string.IsNullOrEmpty(candidateToken)) return candidateToken;
                        }
                    }
                }
            }

            return null;
        }
    }
}

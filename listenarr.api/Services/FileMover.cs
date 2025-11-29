using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Listenarr.Api.Services
{
    public class FileMover : IFileMover
    {
        private readonly ILogger<FileMover> _logger;
        private readonly IProcessRunner? _processRunner;
        private readonly FileMoverOptions _options;

        public FileMover(ILogger<FileMover> logger, IProcessRunner? processRunner = null, IOptions<FileMoverOptions>? options = null)
        {
            _logger = logger;
            _processRunner = processRunner;
            _options = options?.Value ?? new FileMoverOptions();
        }

        public async Task<bool> MoveDirectoryAsync(string sourceDir, string destDir)
        {
            // Try move with retries
            var attempt = 0;
            var delay = 1000;

            for (; attempt < _options.MaxRetries; attempt++)
            {
                try
                {
                    Directory.Move(sourceDir, destDir);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Directory.Move attempt {Attempt} failed: {Source} -> {Dest}", attempt + 1, sourceDir, destDir);
                    try
                    {
                        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                        _logger.LogWarning("Directory listing sample: {Sample}", string.Join(", ", files.Take(5).Select(f => Path.GetFileName(f))));
                    }
                    catch (Exception) { }

                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            var dirSec = new DirectoryInfo(sourceDir).GetAccessControl();
                            var owner = dirSec.GetOwner(typeof(NTAccount))?.ToString() ?? "unknown";
                            _logger.LogWarning("Directory owner: {Owner}", owner);
                        }
                    }
                    catch (Exception) { }

                    if (attempt < _options.MaxRetries - 1)
                    {
                        await Task.Delay(Math.Min(delay, _options.MaxBackoffMs));
                        delay = Math.Min(delay * 2, _options.MaxBackoffMs);
                    }
                }
            }

            // Fallback to copy+delete
                try
                {
                    CopyDirRecursive(sourceDir, destDir);
                    try { Directory.Delete(sourceDir, true); } catch { }
                    return true;
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy+delete fallback failed for directory {Source} -> {Dest}", sourceDir, destDir);

                // On Windows attempt robocopy as a final-resort atomic-ish fallback
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _options.EnableRobocopy && _processRunner != null)
                    {
                        _logger.LogWarning("Attempting robocopy fallback for directory move: {Source} -> {Dest}", sourceDir, destDir);
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "robocopy",
                            Arguments = $"\"{sourceDir}\" \"{destDir}\" /MOVE /E /NFL /NDL /NJH /NJS /NP",
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        };

                        var pr = await _processRunner.RunAsync(startInfo, _options.RobocopyTimeoutMs);
                        if (!pr.TimedOut && pr.ExitCode <= 7 && pr.ExitCode >= 0)
                        {
                            _logger.LogInformation("Robocopy fallback succeeded with exit code {Code}", pr.ExitCode);
                            _logger.LogDebug("Robocopy stdout: {Out}", Truncate(pr.Stdout, 2000));
                            return true;
                        }

                        _logger.LogWarning("Robocopy fallback failed or returned non-success code: {Code}. Stderr: {Err}", pr.ExitCode, Truncate(pr.Stderr, 2000));
                    }
                }
                catch (Exception rex)
                {
                    _logger.LogWarning(rex, "Robocopy fallback threw an exception");
                }

                return false;
            }
        }

        public async Task<bool> MoveFileAsync(string sourceFile, string destFile)
        {
            var attempt = 0;
            var delay = 1000;

            for (; attempt < _options.MaxRetries; attempt++)
            {
                try
                {
                    File.Move(sourceFile, destFile, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "File.Move attempt {Attempt} failed: {Source} -> {Dest}", attempt + 1, sourceFile, destFile);
                    try
                    {
                        using var stream = File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        _logger.LogDebug("Able to open source file for read during diagnostic: {File}", sourceFile);
                    }
                    catch (Exception) { }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            var fileSec = new FileInfo(sourceFile).GetAccessControl();
                            var owner = fileSec.GetOwner(typeof(NTAccount))?.ToString() ?? "unknown";
                            _logger.LogWarning("File owner for {File}: {Owner}", sourceFile, owner);
                        }
                        catch (Exception) { }
                    }

                    if (attempt < _options.MaxRetries - 1)
                    {
                        await Task.Delay(Math.Min(delay, _options.MaxBackoffMs));
                        delay = Math.Min(delay * 2, _options.MaxBackoffMs);
                    }
                }
            }

            // Fallback copy+delete
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    try { File.Delete(sourceFile); } catch { }
                    return true;
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy+delete fallback failed for file {Source} -> {Dest}", sourceFile, destFile);

                // On Windows attempt robocopy for single-file move as a last resort
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _options.EnableRobocopy && _processRunner != null)
                    {
                        _logger.LogWarning("Attempting robocopy fallback for file move: {Source} -> {Dest}", sourceFile, destFile);
                        var srcDir = Path.GetDirectoryName(sourceFile) ?? string.Empty;
                        var dstDir = Path.GetDirectoryName(destFile) ?? string.Empty;
                        var fileName = Path.GetFileName(sourceFile);
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "robocopy",
                            Arguments = $"\"{srcDir}\" \"{dstDir}\" {fileName} /MOV /E /NFL /NDL /NJH /NJS /NP",
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        };

                        var pr = await _processRunner.RunAsync(startInfo, _options.RobocopyTimeoutMs);
                        if (!pr.TimedOut && pr.ExitCode <= 7 && pr.ExitCode >= 0)
                        {
                            _logger.LogInformation("Robocopy fallback succeeded with exit code {Code}", pr.ExitCode);
                            _logger.LogDebug("Robocopy stdout: {Out}", Truncate(pr.Stdout, 2000));
                            return true;
                        }

                        _logger.LogWarning("Robocopy fallback failed or returned non-success code: {Code}. Stderr: {Err}", pr.ExitCode, Truncate(pr.Stderr, 2000));
                    }
                }
                catch (Exception rex)
                {
                    _logger.LogWarning(rex, "Robocopy fallback threw an exception");
                }

                return false;
            }
        }

        public Task<bool> CopyDirectoryAsync(string sourceDir, string destDir)
        {
            try
            {
                CopyDirRecursive(sourceDir, destDir);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy directory failed: {Source} -> {Dest}", sourceDir, destDir);
                return Task.FromResult(false);
            }
        }

        public Task<bool> CopyFileAsync(string sourceFile, string destFile)
        {
            try
            {
                File.Copy(sourceFile, destFile, true);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy file failed: {Source} -> {Dest}", sourceFile, destFile);
                return Task.FromResult(false);
            }
        }

        private void CopyDirRecursive(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.TopDirectoryOnly))
            {
                var sub = Path.Combine(dst, Path.GetFileName(dir));
                CopyDirRecursive(dir, sub);
            }

            foreach (var file in Directory.GetFiles(src, "*.*", SearchOption.TopDirectoryOnly))
            {
                var destFile = Path.Combine(dst, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
        }

        private async Task<int?> RunRobocopyAsync(string sourceDir, string destDir, bool move = false, string? filePattern = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

            if (!_options.EnableRobocopy) return null;

            // Ensure paths are quoted
            var args = new System.Text.StringBuilder();
            args.Append('"').Append(sourceDir).Append('"');
            args.Append(' ');
            args.Append('"').Append(destDir).Append('"');

            if (!string.IsNullOrWhiteSpace(filePattern))
            {
                args.Append(' ').Append(filePattern);
            }

            // Use /MOV for files or /MOVE for directories to move and delete source
            if (move)
            {
                // For directories, /MOVE is appropriate; for file pattern present, /MOV
                args.Append(' ').Append(string.IsNullOrWhiteSpace(filePattern) ? "/MOVE" : "/MOV");
            }

            // Mirror recursion for directories
            args.Append(" /E /NFL /NDL /NJH /NJS /NP");

            var startInfo = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = args.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                if (_processRunner != null)
                {
                    var pr = await _processRunner.RunAsync(startInfo, _options.RobocopyTimeoutMs);
                    if (pr.TimedOut)
                    {
                        _logger.LogWarning("Robocopy timed out after {Ms}ms. Stdout: {Out} Stderr: {Err}", _options.RobocopyTimeoutMs, Truncate(pr.Stdout, 2000), Truncate(pr.Stderr, 2000));
                        return null;
                    }

                    var exit = pr.ExitCode;
                    _logger.LogDebug("Robocopy exit code {Exit}. Stdout: {Out} Stderr: {Err}", exit, Truncate(pr.Stdout, 2000), Truncate(pr.Stderr, 2000));
                    return exit;
                }
                else
                {
                    _logger.LogWarning("Robocopy requested but no IProcessRunner is registered; skipping direct process start for safety.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while running robocopy");
                return null;
            }
        }

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }
}

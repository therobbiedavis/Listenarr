using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Listenarr.Api.Services
{
    public class PlaywrightInstallStatus
    {
        public bool IsInstalled { get; set; }
        public DateTimeOffset LastAttempt { get; set; }
        public string? LastError { get; set; }
        public string? LastOutput { get; set; }
    }

    public class PlaywrightInstallBackgroundService : BackgroundService
    {
        private readonly ILogger<PlaywrightInstallBackgroundService> _logger;
        private readonly IPlaywrightPageFetcher _fetcher;
        private readonly PlaywrightInstallStatus _status;
        private readonly IPlaywrightInstaller _installer;
        private readonly IProcessRunner? _processRunner;

        public PlaywrightInstallBackgroundService(ILogger<PlaywrightInstallBackgroundService> logger, IPlaywrightPageFetcher fetcher, PlaywrightInstallStatus status, IPlaywrightInstaller installer, IProcessRunner? processRunner = null)
        {
            _logger = logger;
            _fetcher = fetcher;
            _status = status;
            _installer = installer;
            _processRunner = processRunner;
        }

        private string? FindExecutableOnPath(string name)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var exts = Environment.GetEnvironmentVariable("PATHEXT")?.Split(';') ?? new[] { ".EXE", ".CMD", ".BAT", ".PS1" };

            foreach (var dir in paths)
            {
                try
                {
                    foreach (var ext in exts)
                    {
                        var candidate = Path.Combine(dir, name + ext);
                        if (File.Exists(candidate)) return candidate;
                    }
                    // Also check without extension (for *nix paths in WSL scenarios)
                    var candidateNoExt = Path.Combine(dir, name);
                    if (File.Exists(candidateNoExt)) return candidateNoExt;
                }
                catch { }
            }
            return null;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Try quickly at startup, then back off if it fails.
            var attempt = 0;
            var maxAttempts = 6; // try for a while, then stop

            while (!stoppingToken.IsCancellationRequested && attempt < maxAttempts)
            {
                attempt++;
                _status.LastAttempt = DateTimeOffset.UtcNow;
                try
                {
                    _logger.LogInformation("Playwright background installer attempt {Attempt}", attempt);

                    // Ask the installer to perform one install attempt (it will also try to initialize)
                    var (success, outText, errText, exitCode) = await _installer.InstallOnceAsync(stoppingToken).ConfigureAwait(false);
                    _status.LastOutput = outText + "\n" + errText;
                    _logger.LogDebug("installer stdout: {Out}", outText);
                    _logger.LogDebug("installer stderr: {Err}", errText);
                    if (success)
                    {
                        _logger.LogInformation("Playwright is available (background installer)");
                        _status.IsInstalled = true;
                        _status.LastError = null;
                        return; // done
                    }
                    else
                    {
                        // Lower severity to Debug in environments (tests/CI) where Playwright/browser
                        // installation may be unavailable; keep stdout/stderr available in _status.LastOutput.
                        _logger.LogDebug("Playwright installer attempt failed (ExitCode={ExitCode}). See debug logs for stdout/stderr.", exitCode);
                        _status.LastError = "installer failed; see LastOutput for details";
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Playwright background installer canceled");
                    return;
                }
                catch (Exception ex)
                {
                    // Use Debug level to avoid noisy warnings in test runs when Playwright isn't available
                    _logger.LogDebug(ex, "Playwright background installer encountered an error");
                    _status.LastError = ex.Message;
                }

                // Exponential backoff with jitter
                var delay = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, attempt - 1), 300));
                var jitter = TimeSpan.FromSeconds(new Random().NextDouble() * 5);
                var wait = delay + jitter;
                _logger.LogInformation("Playwright background installer will retry after {Delay}s", wait.TotalSeconds);
                await Task.Delay(wait, stoppingToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Playwright background installer finished attempts without success");
        }

        private async Task<(bool Success, string Out, string Err, int ExitCode)> RunNpxInstallChromiumAsync(CancellationToken ct)
        {
            // Provide richer diagnostics: log PATH, try `npx --version`, fall back to cmd.exe /c if direct start fails.
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            _logger.LogDebug("Playwright installer PATH: {PathSnippet}", path.Length > 200 ? path.Substring(0, 200) + "..." : path);

            try
            {
                // Quick check: npx availability
                try
                {
                    var check = new ProcessStartInfo
                    {
                        FileName = "npx",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    if (_processRunner != null)
                    {
                        var prCheck = await _processRunner.RunAsync(check, timeoutMs: 5000).ConfigureAwait(false);
                        _logger.LogDebug("npx --version stdout: {Out}, stderr: {Err}", prCheck.Stdout, prCheck.Stderr);
                    }
                    else
                    {
                        _logger.LogDebug("IProcessRunner is not available; skipping 'npx --version' check in Playwright installer.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "npx --version check failed");
                }

                // Attempt to resolve the full path to the npx executable (handles Windows .cmd/.exe wrappers)
                string? npxFullPath = FindExecutableOnPath("npx");
                if (string.IsNullOrEmpty(npxFullPath))
                {
                    _logger.LogDebug("Could not find 'npx' on PATH for the current process. Ensure Node.js/npx is available to the service environment.");
                    // Try cmd.exe fallback anyway to get a clearer error
                    var cmdPsi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C npx playwright install chromium",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    if (_processRunner != null)
                    {
                        var prCmd = await _processRunner.RunAsync(cmdPsi, timeoutMs: 10 * 60 * 1000, cancellationToken: ct).ConfigureAwait(false);
                        return (prCmd.ExitCode == 0 && !prCmd.TimedOut, prCmd.Stdout ?? string.Empty, prCmd.Stderr ?? string.Empty, prCmd.ExitCode);
                    }
                    else
                    {
                        _logger.LogDebug("IProcessRunner is not available; skipping cmd.exe npx Playwright install fallback.");
                        return (false, string.Empty, "IProcessRunner unavailable", -1);
                    }
                }

                var psiResolved = new ProcessStartInfo
                {
                    FileName = npxFullPath,
                    Arguments = "playwright install chromium",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (_processRunner != null)
                {
                    var pr = await _processRunner.RunAsync(psiResolved, timeoutMs: 10 * 60 * 1000, cancellationToken: ct).ConfigureAwait(false);
                    return (pr.ExitCode == 0 && !pr.TimedOut, pr.Stdout ?? string.Empty, pr.Stderr ?? string.Empty, pr.ExitCode);
                }
                else
                {
                    _logger.LogDebug("IProcessRunner is not available; skipping npx Playwright install fallback.");
                    return (false, string.Empty, "IProcessRunner unavailable", -1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception while running npx install");
                return (false, string.Empty, ex.ToString(), -1);
            }
        }
    }
}

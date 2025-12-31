using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace Listenarr.Api.Services
{
    public interface IPlaywrightInstaller
    {
        Task<(bool Success, string Out, string Err, int ExitCode)> InstallOnceAsync(CancellationToken ct = default);
        Task<bool> EnsurePlaywrightInitializedAsync(CancellationToken ct = default);
    }

    public class PlaywrightInstaller : IPlaywrightInstaller
    {
        private readonly ILogger<PlaywrightInstaller> _logger;
        private readonly IPlaywrightPageFetcher _fetcher;
        private readonly PlaywrightInstallStatus _status;
        private readonly string? _nodePathOverride;
        private readonly string? _httpProxyOverride;
        private readonly string? _httpsProxyOverride;
        private readonly IProcessRunner _processRunner;

        public PlaywrightInstaller(ILogger<PlaywrightInstaller> logger, IPlaywrightPageFetcher fetcher, PlaywrightInstallStatus status, IConfiguration config, IProcessRunner processRunner)
        {
            _logger = logger;
            _fetcher = fetcher;
            _status = status;
            _nodePathOverride = config["Playwright:NodePath"] ?? Environment.GetEnvironmentVariable("PLAYWRIGHT_NODE_PATH");
            _httpProxyOverride = config["ExternalRequests:HttpProxy"] ?? config["Playwright:HttpProxy"] ?? Environment.GetEnvironmentVariable("HTTP_PROXY");
            _httpsProxyOverride = config["ExternalRequests:HttpsProxy"] ?? config["Playwright:HttpsProxy"] ?? Environment.GetEnvironmentVariable("HTTPS_PROXY");
            _processRunner = processRunner;
        }

        public async Task<bool> EnsurePlaywrightInitializedAsync(CancellationToken ct = default)
        {
            try
            {
                return await _fetcher.TryEnsureInitializedAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EnsurePlaywrightInitializedAsync failed");
                return false;
            }
        }

        public async Task<(bool Success, string Out, string Err, int ExitCode)> InstallOnceAsync(CancellationToken ct = default)
        {
            _status.LastAttempt = DateTimeOffset.UtcNow;

            // First, try EnsurePlaywrightInitialized (reflection-based InstallAsync may have run)
            try
            {
                var initialized = await EnsurePlaywrightInitializedAsync(ct).ConfigureAwait(false);
                if (initialized)
                {
                    _status.IsInstalled = true;
                    _status.LastError = null;
                    return (true, string.Empty, string.Empty, 0);
                }
            }
            catch { }

            // Try calling Microsoft.Playwright.Playwright.InstallAsync() via reflection if available.
            try
            {
                var playwrightType = Type.GetType("Microsoft.Playwright.Playwright, Microsoft.Playwright");
                if (playwrightType != null)
                {
                    var installMethod = playwrightType.GetMethod("InstallAsync", BindingFlags.Public | BindingFlags.Static);
                    if (installMethod != null)
                    {
                        _logger.LogInformation("Detected Microsoft.Playwright.Playwright.InstallAsync - invoking to provision browsers.");
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromMinutes(2));

                        var installTaskObj = installMethod.Invoke(null, Array.Empty<object>());
                        if (installTaskObj is Task installTask)
                        {
                            try
                            {
                                var completed = await Task.WhenAny(installTask, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
                                if (completed != installTask)
                                {
                                    _logger.LogWarning("Playwright.InstallAsync timed out after 2 minutes.");
                                }
                                else
                                {
                                    await installTask.ConfigureAwait(false);
                                    // Ask Playwright to initialize now that browsers may be present
                                    var ok = await EnsurePlaywrightInitializedAsync(ct).ConfigureAwait(false);
                                    _status.IsInstalled = ok;
                                    if (ok)
                                    {
                                        _status.LastError = null;
                                        return (true, "InstallAsync invoked", string.Empty, 0);
                                    }
                                    _status.LastError = "Playwright failed to initialize after InstallAsync";
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Playwright.InstallAsync threw an exception");
                                _status.LastError = ex.Message;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reflection attempt to call Playwright.InstallAsync failed");
            }

            // Next, try to run npx (using override path if provided)
            string? resolvedNpx = null;
            if (!string.IsNullOrWhiteSpace(_nodePathOverride) && File.Exists(_nodePathOverride))
            {
                resolvedNpx = _nodePathOverride;
                _logger.LogInformation("Using configured Playwright NodePath: {Path}", _nodePathOverride);
            }
            else
            {
                // Try PATHEXT-aware lookup
                resolvedNpx = FindExecutableOnPath("npx");
            }

            if (string.IsNullOrEmpty(resolvedNpx))
            {
                _logger.LogInformation("No npx executable found (checked Playwright:NodePath and PATH). Skipping npx installer.");
                _status.LastError = "npx not found";
                return (false, string.Empty, "npx not found", -1);
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = resolvedNpx,
                    Arguments = "playwright install chromium",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // If proxy settings are provided via configuration or environment, forward them to the child
                var httpProxy = _httpProxyOverride ?? Environment.GetEnvironmentVariable("HTTP_PROXY") ?? Environment.GetEnvironmentVariable("http_proxy");
                var httpsProxy = _httpsProxyOverride ?? Environment.GetEnvironmentVariable("HTTPS_PROXY") ?? Environment.GetEnvironmentVariable("https_proxy");
                if (!string.IsNullOrWhiteSpace(httpProxy)) psi.Environment["HTTP_PROXY"] = httpProxy;
                if (!string.IsNullOrWhiteSpace(httpsProxy)) psi.Environment["HTTPS_PROXY"] = httpsProxy;

                var pr = await _processRunner.RunAsync(psi, timeoutMs: 10 * 60 * 1000, cancellationToken: ct).ConfigureAwait(false);
                var outText = pr.Stdout ?? string.Empty;
                var errText = pr.Stderr ?? string.Empty;

                _status.LastOutput = outText + "\n" + errText;

                if (!pr.TimedOut && pr.ExitCode == 0)
                {
                    var ok = await EnsurePlaywrightInitializedAsync(ct).ConfigureAwait(false);
                    _status.IsInstalled = ok;
                    if (!ok) _status.LastError = "Playwright failed to initialize after npx install";
                    return (ok, outText, errText, pr.ExitCode);
                }

                _status.LastError = "npx install failed; check LastOutput";
                return (false, outText, errText, pr.ExitCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while running configured npx");
                _status.LastError = ex.Message;
                return (false, string.Empty, ex.ToString(), -1);
            }
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
                    var candidateNoExt = Path.Combine(dir, name);
                    if (File.Exists(candidateNoExt)) return candidateNoExt;
                }
                catch { }
            }
            return null;
        }
    }
}

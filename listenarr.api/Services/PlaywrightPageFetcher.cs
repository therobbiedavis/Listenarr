using Microsoft.Playwright;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Listenarr.Api.Services
{
    public interface IPlaywrightPageFetcher
    {
        Task<string?> FetchAsync(string url, CancellationToken cancellationToken = default);
        /// <summary>
        /// Ensure Playwright and browsers are initialized/available. Returns true when a browser instance
        /// is available for use. This is a best-effort call intended for startup checks.
        /// </summary>
        Task<bool> TryEnsureInitializedAsync(CancellationToken cancellationToken = default);
    }

    public class PlaywrightPageFetcher : IPlaywrightPageFetcher, IAsyncDisposable
    {
        private readonly ILogger<PlaywrightPageFetcher> _logger;
        private readonly ExternalRequestOptions _options;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly IProcessRunner? _processRunner;

        public PlaywrightPageFetcher(ILogger<PlaywrightPageFetcher> logger, IOptions<ExternalRequestOptions> options, IProcessRunner? processRunner = null)
        {
            _logger = logger;
            _options = options?.Value ?? new ExternalRequestOptions();
            _processRunner = processRunner;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_browser != null && _playwright != null) return;
            await _initLock.WaitAsync();
            try
            {
                if (_browser != null && _playwright != null) return;
                // Ensure Playwright browser binaries are installed (first-run).
                try
                {
                    // Try to invoke Playwright.InstallAsync() via reflection (method may not exist in some package versions)
                    var playwrightType = typeof(Playwright);
                    var installMethod = playwrightType.GetMethod("InstallAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (installMethod != null)
                    {
                        var installTask = (Task)installMethod.Invoke(null, null)!;
                        await installTask.ConfigureAwait(false);
                        _logger.LogInformation("Playwright browsers installed/available via InstallAsync()");
                    }
                    else
                    {
                        // Fall back to calling the Playwright CLI via `npx playwright install` if available
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "npx",
                                Arguments = "playwright install",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            if (_processRunner != null)
                            {
                                var pr = await _processRunner.RunAsync(psi, timeoutMs: 2 * 60 * 1000).ConfigureAwait(false);
                                if (!pr.TimedOut && pr.ExitCode == 0)
                                {
                                    _logger.LogInformation("Playwright browsers installed via npx (process runner)");
                                }
                                else
                                {
                                    _logger.LogDebug("Playwright npx install output: {Out}\n{Err}", pr.Stdout, pr.Stderr);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("IProcessRunner is not available; skipping npx Playwright install fallback in PlaywrightPageFetcher.");
                            }
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogDebug(ex2, "Playwright CLI install fallback failed or not available");
                        }
                    }
                }
                catch (Exception iex)
                {
                    _logger.LogDebug(iex, "Playwright install step failed or skipped (may already be installed)");
                }

                // Create Playwright and launch a shared browser instance
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
                });
                _logger.LogInformation("Playwright browser launched");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Playwright");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<string?> FetchAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureInitializedAsync();
                if (_browser == null) return null;

                // Create a short-lived context to limit resource usage
                var contextOptions = new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    Locale = "en-US",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                };

                // Wire proxy settings from ExternalRequestOptions into Playwright context proxy
                if (!string.IsNullOrWhiteSpace(_options.UsProxyHost) && _options.UseUsProxy && _options.UsProxyPort > 0)
                {
                    var server = _options.UsProxyHost!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? $"{_options.UsProxyHost}:{_options.UsProxyPort}"
                        : $"http://{_options.UsProxyHost}:{_options.UsProxyPort}";
                    contextOptions.Proxy = new Proxy
                    {
                        Server = server,
                        Username = string.IsNullOrWhiteSpace(_options.UsProxyUsername) ? null : _options.UsProxyUsername,
                        Password = string.IsNullOrWhiteSpace(_options.UsProxyPassword) ? null : _options.UsProxyPassword
                    };
                }

                await using var context = await _browser.NewContextAsync(contextOptions);

                var page = await context.NewPageAsync();
                var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                if (response == null)
                {
                    _logger.LogDebug("Playwright navigation returned null for {Url}", url);
                }
                var content = await page.ContentAsync();
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Playwright fetch failed for {Url}", url);
                return null;
            }
        }

        public async Task<bool> TryEnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureInitializedAsync().ConfigureAwait(false);
                return _browser != null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Playwright TryEnsureInitializedAsync failed");
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_browser != null) await _browser.CloseAsync();
                if (_playwright is IAsyncDisposable a)
                {
                    await a.DisposeAsync();
                }
                else
                {
                    _playwright?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing Playwright resources");
            }
        }
    }
}


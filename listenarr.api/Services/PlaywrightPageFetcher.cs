using Microsoft.Playwright;
namespace Listenarr.Api.Services
{
    public interface IPlaywrightPageFetcher
    {
        Task<string?> RenderPageAsync(string url, int timeoutMs = 10000);
    }

    public class PlaywrightPageFetcher : IPlaywrightPageFetcher, IAsyncDisposable
    {
        private readonly ILogger<PlaywrightPageFetcher> _logger;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private readonly SemaphoreSlim _initLock = new(1,1);

        public PlaywrightPageFetcher(ILogger<PlaywrightPageFetcher> logger)
        {
            _logger = logger;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_browser != null && _playwright != null) return;
            await _initLock.WaitAsync();
            try
            {
                if (_browser != null && _playwright != null) return;
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                });
                _logger.LogInformation("Playwright browser launched");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<string?> RenderPageAsync(string url, int timeoutMs = 10000)
        {
            try
            {
                await EnsureInitializedAsync();
                if (_browser == null) return null;
                // Use await using because IBrowserContext is IAsyncDisposable
                await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    // Set UserAgent on the context options rather than calling SetUserAgentAsync on IPage
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                });
                var page = await context.NewPageAsync();
                var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = timeoutMs, WaitUntil = WaitUntilState.NetworkIdle });
                if (response == null || !response.Ok)
                {
                    _logger.LogDebug("Playwright failed to fetch {Url} (status: {Status})", url, response?.Status);
                }
                var content = await page.ContentAsync();
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Playwright RenderPageAsync failed for {Url}", url);
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_browser != null) await _browser.CloseAsync();
                if (_playwright != null)
                {
                    // Dispose in an API-compatible way: prefer IAsyncDisposable when available
                    if (_playwright is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else
                    {
                        try { _playwright.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing Playwright resources");
            }
        }
    }
}

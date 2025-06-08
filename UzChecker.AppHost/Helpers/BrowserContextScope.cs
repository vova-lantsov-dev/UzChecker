using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using UzChecker.AppHost.Services;

namespace UzChecker.AppHost.Helpers;

public static class BrowserContextExtensions
{
    public static async Task<BrowserContextScope> CreateBrowserScopeAsync(this IServiceScopeFactory serviceScopeFactory)
    {
        var serviceScope = serviceScopeFactory.CreateAsyncScope();
        var browser = serviceScope.ServiceProvider.GetRequiredService<IBrowser>();

        var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            DeviceScaleFactor = 1,
            IsMobile = false,
            HasTouch = false,
            Locale = "uk-UA"
        });

        var page = await browserContext.NewPageAsync();
        await page.GotoAsync("https://booking.uz.gov.ua/");

        return new BrowserContextScope(serviceScope, browserContext);
    }

    public readonly struct BrowserContextScope : IAsyncDisposable
    {
        private readonly IBrowserContext _browserContext;
        private readonly AsyncServiceScope _serviceScope;

        public IApiClient Api { get; }
        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        public BrowserContextScope(AsyncServiceScope serviceScope, IBrowserContext browserContext)
        {
            _browserContext = browserContext;
            _serviceScope = serviceScope;

            Api = ActivatorUtilities.CreateInstance<UzApiClient>(ServiceProvider, browserContext.APIRequest);
        }

        public async ValueTask DisposeAsync()
        {
            await _browserContext.DisposeAsync();
            await _serviceScope.DisposeAsync();
        }
    }
}
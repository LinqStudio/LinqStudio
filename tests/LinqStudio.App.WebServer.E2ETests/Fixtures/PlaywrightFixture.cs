using Microsoft.Playwright;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests.Fixtures;

public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        // Fail immediately if browsers aren't installed to avoid false positives
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Browser != null)
                await Browser.CloseAsync();
        }
        catch { }

        try
        {
            Playwright?.Dispose();
        }
        catch { }
    }
}

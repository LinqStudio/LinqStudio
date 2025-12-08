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
        // NOTE: If browsers are not installed, this will throw and tests will fail.
        // This is intentional - E2E tests require browsers to be installed.
        // To install: pwsh playwright.ps1 install --with-deps chromium
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
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

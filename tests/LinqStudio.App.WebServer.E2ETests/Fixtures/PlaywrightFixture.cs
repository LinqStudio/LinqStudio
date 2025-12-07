using Microsoft.Playwright;
using System.Diagnostics;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests.Fixtures;

public class PlaywrightFixture : IAsyncLifetime
{
	public IPlaywright? Playwright { get; private set; }
	public IBrowser? Browser { get; private set; }

	public async Task InitializeAsync()
	{
		Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
		try
		{
			Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !Debugger.IsAttached });
		}
		catch (Microsoft.Playwright.PlaywrightException)
		{
			// Browsers are probably not installed in this environment. Leave Browser null so tests can skip gracefully.
			Browser = null;
		}
	}

	public async Task DisposeAsync()
	{
		try
		{
			if (Browser != null)
			{
				await Browser.CloseAsync();
			}
		}
		catch { }

		try
		{
			Playwright?.Dispose();
		}
		catch { }
	}
}

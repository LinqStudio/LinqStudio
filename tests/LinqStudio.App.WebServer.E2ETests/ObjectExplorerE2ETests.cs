using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class ObjectExplorerE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task ObjectExplorer_Panel_IsVisible()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());

		// Wait for page to load
		await page.WaitForSelectorAsync("text=Hello, world!");

		// Check that object explorer panel is visible
		var objectExplorerTitle = await page.WaitForSelectorAsync("text=Object Explorer", new() { Timeout = 10000 });
		Assert.NotNull(objectExplorerTitle);

		// Check for Add Connection button
		var addConnectionButton = await page.QuerySelectorAsync("[data-testid='add-connection-button']");
		Assert.NotNull(addConnectionButton);

		// Check for Refresh All button
		var refreshAllButton = await page.QuerySelectorAsync("[data-testid='refresh-all-button']");
		Assert.NotNull(refreshAllButton);
	}

	[Fact(Timeout = 60_000)]
	public async Task ObjectExplorer_ShowsNoConnectionsMessage_Initially()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.WaitForSelectorAsync("text=Hello, world!");

		// Check for no connections message
		var noConnectionsMessage = await page.WaitForSelectorAsync("text=No connections", new() { Timeout = 10000 });
		Assert.NotNull(noConnectionsMessage);
	}
}

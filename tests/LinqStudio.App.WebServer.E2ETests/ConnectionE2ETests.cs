using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class ConnectionE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task Connection_ButtonInObjectExplorer_OpensDialog()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());

		// Wait for page to load
		await page.WaitForSelectorAsync("text=Hello, world!");

		// Find and click the Add Connection button in the object explorer
		var addConnectionButton = await page.WaitForSelectorAsync("[data-testid='add-connection-button']", new() { Timeout = 10000 });
		Assert.NotNull(addConnectionButton);
		
		await addConnectionButton.ClickAsync();

		// Wait for dialog to appear
		var dialog = await page.WaitForSelectorAsync("text=Connection Settings", new() { Timeout = 10000 });
		Assert.NotNull(dialog);
	}

	[Fact(Timeout = 60_000)]
	public async Task Connection_Dialog_HasAllRequiredFields()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.WaitForSelectorAsync("text=Hello, world!");

		// Open connection dialog via object explorer
		var addConnectionButton = await page.WaitForSelectorAsync("[data-testid='add-connection-button']", new() { Timeout = 10000 });
		Assert.NotNull(addConnectionButton);
		await addConnectionButton.ClickAsync();

		// Wait for dialog
		await page.WaitForSelectorAsync("text=Connection Settings", new() { Timeout = 10000 });

		// Check for connection name input
		var connectionNameInput = await page.QuerySelectorAsync("[data-testid='connection-name-input']");
		Assert.NotNull(connectionNameInput);

		// Check for database type select
		var databaseSelect = await page.QuerySelectorAsync("[data-testid='database-type-select']");
		Assert.NotNull(databaseSelect);

		// Check for connection string input
		var connectionStringInput = await page.QuerySelectorAsync("[data-testid='connection-string-input']");
		Assert.NotNull(connectionStringInput);

		// Check for timeout select
		var timeoutSelect = await page.QuerySelectorAsync("[data-testid='timeout-select']");
		Assert.NotNull(timeoutSelect);

		// Check for validate button
		var validateButton = await page.QuerySelectorAsync("[data-testid='validate-button']");
		Assert.NotNull(validateButton);

		// Check for save button
		var saveButton = await page.QuerySelectorAsync("[data-testid='save-button']");
		Assert.NotNull(saveButton);

		// Check for close button
		var closeButton = await page.QuerySelectorAsync("[data-testid='close-button']");
		Assert.NotNull(closeButton);
	}

	[Fact(Timeout = 60_000)]
	public async Task Connection_TimeoutDropdown_Exists()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate and open dialog
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.WaitForSelectorAsync("text=Hello, world!");

		var addConnectionButton = await page.WaitForSelectorAsync("[data-testid='add-connection-button']", new() { Timeout = 10000 });
		Assert.NotNull(addConnectionButton);
		await addConnectionButton.ClickAsync();

		await page.WaitForSelectorAsync("text=Connection Settings", new() { Timeout = 10000 });

		// Verify timeout dropdown exists
		var timeoutSelect = await page.QuerySelectorAsync("[data-testid='timeout-select']");
		Assert.NotNull(timeoutSelect);
	}

	[Fact(Timeout = 60_000)]
	public async Task Connection_CloseButton_ClosesDialog()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate and open dialog
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.WaitForSelectorAsync("text=Hello, world!");

		var addConnectionButton = await page.WaitForSelectorAsync("[data-testid='add-connection-button']", new() { Timeout = 10000 });
		Assert.NotNull(addConnectionButton);
		await addConnectionButton.ClickAsync();

		await page.WaitForSelectorAsync("text=Connection Settings", new() { Timeout = 10000 });

		// Click close button
		var closeButton = await page.QuerySelectorAsync("[data-testid='close-button']");
		Assert.NotNull(closeButton);
		await closeButton.ClickAsync();

		// Dialog should be closed - check that title is no longer visible
		await Task.Delay(500); // Small delay for dialog animation
		var dialogAfterClose = await page.QuerySelectorAsync("text=Connection Settings");
		Assert.Null(dialogAfterClose);
	}
}

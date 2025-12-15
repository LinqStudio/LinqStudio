using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AppServerFixture>, ICollectionFixture<PlaywrightFixture>
{
	// collection shared between tests
}

[Collection("E2E")]
public class EditorE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	/// <summary>
	/// Helper method to create a new project for testing
	/// </summary>
	private async Task CreateNewProjectAsync(IPage page)
	{
		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());

		// Click "New" in the Project menu
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Wait for project to be created (redirect to home happens automatically)
		await page.WaitForURLAsync(_app.BaseUrl.ToString());
	}

	/// <summary>
	/// Helper method to create a new project and navigate to the editor
	/// </summary>
	private async Task SetupEditorAsync(IPage page)
	{
		await CreateNewProjectAsync(page);

		// Create a new query
		await page.GetByTestId("nav-query-create").ClickAsync();

		// Wait for editor page to load
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");
		await Expect(page.GetByTestId("monaco-editor-container")).ToBeVisibleAsync();

		await WaitEditorAndFocusAsync(page);
	}

	private async Task WaitEditorAndFocusAsync(IPage page)
	{
		// Wait for Monaco container to appear
		var monacoEditor = page.Locator("#editor-top .monaco-editor");
		await Expect(monacoEditor).ToBeVisibleAsync();

		// Click to focus the editor
		await monacoEditor.ClickAsync();
	}

	private async Task ClearAndWriteQueryAsync(IPage page, string query)
	{
		// Clear the editor first
		await page.Keyboard.PressAsync("Control+A");
		// Type the provided query
		await page.Keyboard.TypeAsync(query);
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_ShowsCompletions_WhenTyping()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Trigger suggestions via Ctrl+Space
		await page.Keyboard.PressAsync("Control+Space");

		// Wait for suggest widget to appear
		var suggestRow = page.Locator(".suggest-widget .monaco-list-row").First;
		await Expect(suggestRow).ToBeVisibleAsync(new() { Timeout = 10000 });
		await Expect(suggestRow).Not.ToBeEmptyAsync();

		// Ensure we have some likely completions
		var suggestions = page.Locator(".suggest-widget .monaco-list-row");
		await Expect(suggestions).Not.ToHaveCountAsync(0);
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_Hover_ShowsSymbolInfo()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Clear the editor first and type some code
		await ClearAndWriteQueryAsync(page, "context.People.Where(");

		// Wait for the editor content to be rendered
		var viewLine = page.Locator(".view-lines .view-line");
		await Expect(viewLine.First).ToBeVisibleAsync();

		// Find a Monaco token element containing "Where" and hover over it
		var whereToken = page.Locator("span").Filter(new() { HasText = "Where", HasNotText = "context" });
		await Expect(whereToken.First).ToBeVisibleAsync();

		// Hover over the token
		await whereToken.First.HoverAsync();

		// Wait for the hover widget to appear and verify its content
		var hoverContent = page.Locator(".monaco-hover .hover-contents");
		await Expect(hoverContent).ToBeVisibleAsync();
		await Expect(hoverContent).ToContainTextAsync("Where", new() { IgnoreCase = true });
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_AutoTriggers_CompletionOnDot()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Clear the editor first and type some code
		await ClearAndWriteQueryAsync(page, "context.People");

		// Type a dot - this should trigger completion automatically
		await page.Keyboard.TypeAsync(".");

		// Wait for suggest widget to appear automatically (without Ctrl+Space)
		var suggestRow = page.Locator(".suggest-widget .monaco-list-row");
		await Expect(suggestRow.First).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Verify we have completions (properties/methods on IQueryable<Person>)
		await Expect(suggestRow).Not.ToHaveCountAsync(0);

		// Check for typical LINQ methods - verify at least one exists
		var addSuggestions = suggestRow.Filter(new() { HasText = "Add" });
		var allSuggestions = suggestRow.Filter(new() { HasText = "All" });
		var combinedCount = await addSuggestions.CountAsync() + await allSuggestions.CountAsync();
		Assert.True(combinedCount > 0, "Expected at least one LINQ method (Add or All) in completions");
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_AutoTriggers_CompletionOnOpenParen()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Clear and type code
		await ClearAndWriteQueryAsync(page, "context.People.Where");

		// Type an open paren - '(' is a trigger character that shows parameter hints
		await page.Keyboard.TypeAsync("(");

		// Verify parameter hints widget appears (Monaco shows parameter info widget for '(' trigger)
		var parameterHintsLocator = page.Locator(".suggest-widget .monaco-list-row");
		await Expect(parameterHintsLocator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_AutoTriggers_CompletionOnSpace()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Clear and type code ending with a space to trigger completion
		await ClearAndWriteQueryAsync(page, "context.People.Where( x =>");
		await page.Keyboard.TypeAsync(" ");

		// Wait for completion widget to appear after typing space
		var suggestRow = page.Locator(".suggest-widget .monaco-list-row");
		await Expect(suggestRow.First).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Check that completion suggestions are present
		await Expect(suggestRow).Not.ToHaveCountAsync(0);
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_DisplaysQueryName_InInfoBar()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Verify query info bar is displayed
		var queryInfoBar = page.GetByTestId("query-info-bar");
		await Expect(queryInfoBar).ToBeVisibleAsync();

		// Verify query name is displayed and has text
		var queryName = page.GetByTestId("query-name-display");
		await Expect(queryName).ToBeVisibleAsync();
		await Expect(queryName).Not.ToBeEmptyAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_RenameQuery_UpdatesQueryName()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Click rename button
		var renameBtn = page.GetByTestId("query-rename-btn");
		await Expect(renameBtn).ToBeVisibleAsync();
		await renameBtn.ClickAsync();

		// Wait for rename input to appear
		var renameInput = page.GetByTestId("query-name-input");
		await Expect(renameInput).ToBeVisibleAsync();

		// Clear and type new name
		await renameInput.FillAsync("My Test Query");

		// Click save button
		var saveBtn = page.GetByTestId("query-rename-save-btn");
		await Expect(saveBtn).ToBeVisibleAsync();
		await saveBtn.ClickAsync();

		// Verify the name was updated
		var queryName = page.GetByTestId("query-name-display");
		await Expect(queryName).ToBeVisibleAsync();
		await Expect(queryName).ToContainTextAsync("My Test Query");
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_RenameQuery_PreventsDuplicateNames()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await SetupEditorAsync(page);

		// Get the first query name
		var firstQueryName = await page.GetByTestId("query-name-display").InnerTextAsync();

		// Create a second query
		await page.GetByTestId("nav-query-create").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");
		await Expect(page.GetByTestId("monaco-editor-container")).ToBeVisibleAsync();

		// Try to rename the second query to the same name as the first
		var renameBtn = page.GetByTestId("query-rename-btn");
		await Expect(renameBtn).ToBeVisibleAsync();
		await renameBtn.ClickAsync();

		// Wait for rename input to appear
		var renameInput = page.GetByTestId("query-name-input");
		await Expect(renameInput).ToBeVisibleAsync();

		// Type the duplicate name
		await renameInput.FillAsync(firstQueryName);

		// Verify the save button is disabled (validation should fail)
		var saveBtn = page.GetByTestId("query-rename-save-btn");
		await Expect(saveBtn).ToBeVisibleAsync();
		await Expect(saveBtn).ToBeDisabledAsync();

		// Cancel the rename
		var cancelBtn = page.GetByTestId("query-rename-cancel-btn");
		await cancelBtn.ClickAsync();

		// Verify we're still on the second query with its original name
		var queryName = page.GetByTestId("query-name-display");
		await Expect(queryName).ToBeVisibleAsync();
		await Expect(queryName).Not.ToHaveTextAsync(firstQueryName);
	}
}

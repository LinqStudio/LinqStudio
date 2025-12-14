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
		await page.WaitForSelectorAsync("#editor-top .monaco-editor");

		// Click to focus the editor
		await page.ClickAsync("#editor-top .monaco-editor");
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
		var suggest = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 })
			?? throw new InvalidOperationException("Unable to find suggest widget");
		var text = await suggest.InnerTextAsync();
		Assert.False(string.IsNullOrWhiteSpace(text));

		// Ensure we have some likely completion
		var suggestions = await page.QuerySelectorAllAsync(".suggest-widget .monaco-list-row");
		var any = false;
		foreach (var s in suggestions)
		{
			var t = await s.InnerTextAsync();
			if (t.Length > 0)
			{
				any = true;
				break;
			}
		}
		Assert.True(any, "Expected at least one completion suggestion");
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
		await page.WaitForSelectorAsync(".view-lines .view-line");

		// Find a Monaco token element containing "Where" and hover over it
		var whereToken = page.Locator("span").Filter(new() { HasText = "Where", HasNotText = "context" });

		// Hover over the token
		await Task.Delay(500);
		await whereToken.First.HoverAsync();

		// Wait for the hover widget to appear and get its content
		var hoverContent = await page.Locator(".monaco-hover .hover-contents").InnerTextAsync();

		// Verify hover content exists and contains "Where"
		Assert.False(string.IsNullOrWhiteSpace(hoverContent));
		Assert.Contains("Where", hoverContent, System.StringComparison.OrdinalIgnoreCase);
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
		await Task.Delay(1000);
		await page.Keyboard.TypeAsync(".");

		// Wait for suggest widget to appear automatically (without Ctrl+Space)
		var suggest = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 });
		Assert.NotNull(suggest);

		// Verify we have completions (properties/methods on IQueryable<Person>)
		var suggestions = await page.QuerySelectorAllAsync(".suggest-widget .monaco-list-row");
		Assert.True(suggestions.Count > 0, "Expected completion suggestions to appear after typing '.'");

		// Check for typical LINQ methods
		var innerTexts = await Task.WhenAll(suggestions.Select(s => s.InnerTextAsync()));
		var hasLinqMethod = innerTexts.Any(t =>
			t.Contains("Add") || t.Contains("All"));
		Assert.True(hasLinqMethod, "Expected at least one LINQ method in completions");
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
		await Task.Delay(1000);
		await page.Keyboard.TypeAsync("(");

		// Verify parameter hints widget appears (Monaco shows parameter info widget for '(' trigger)
		var parameterHints = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 });
		Assert.NotNull(parameterHints);
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
		await Task.Delay(1000);
		await page.Keyboard.TypeAsync(" ");

		// Wait for completion widget to appear after typing space
		var suggest = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 });
		Assert.NotNull(suggest);

		// Check that completion suggestions are present
		var suggestions = await page.QuerySelectorAllAsync(".suggest-widget .monaco-list-row");
		Assert.True(suggestions.Count > 0, "Expected completion suggestions to appear after typing space");
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
}

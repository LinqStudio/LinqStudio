using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class EditorE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task Editor_ShowsCompletions_WhenTyping()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Trigger suggestions via Ctrl+Space
		await page.Keyboard.PressAsync("Control+Space");

		// Wait for suggest widget to appear (with .visible CSS class)
		var suggestRow = page.Get_QueryEditor_SuggestRows().First;
		await Expect(suggestRow).ToBeVisibleAsync(new() { Timeout = 20000 });
		await Expect(suggestRow).Not.ToBeEmptyAsync();

		// Ensure we have some likely completions
		var suggestions = page.Get_QueryEditor_SuggestRows();
		await Expect(suggestions).Not.ToHaveCountAsync(0);
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_Hover_ShowsSymbolInfo()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Clear the editor first and type some code
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Where(");

		// Wait for the editor content to be rendered
		var viewLine = page.Get_QueryEditor_ViewLine();
		await Expect(viewLine.First).ToBeVisibleAsync();

		// Find a Monaco token element containing "Where" and hover over it
		var whereToken = page.Locator(E2ESelectors.MonacoToken).Filter(new() { HasText = "Where", HasNotText = "context" });
		await Expect(whereToken.First).ToBeVisibleAsync();

		// Hover over the token
		await whereToken.First.HoverAsync();

		// Wait for the hover widget to appear and verify its content.
		// Use a longer timeout because Roslyn initialization from the SQLite schema takes additional
		// time compared to the demo model, and the hover widget briefly shows "Loading..." first.
		var hoverContent = page.Get_QueryEditor_HoverContent();
		await Expect(hoverContent).ToBeVisibleAsync();
		await Expect(hoverContent).ToContainTextAsync("Where", new() { IgnoreCase = true, Timeout = 20_000 });
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_AutoTriggers_CompletionOnDot()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Clear the editor first and type some code
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People");

		// Type a dot - this should trigger completion automatically
		await page.Keyboard.TypeAsync(".");

		// Wait for suggest widget to appear automatically (without Ctrl+Space)
		var suggestRow = page.Get_QueryEditor_SuggestRows();
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

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Clear and type code
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Where");

		// Type an open paren - this should auto-trigger completion suggestions
		await page.Keyboard.TypeAsync("(");

		// Wait for suggest widget to auto-appear (Monaco completion auto-trigger on '(')
		var suggestRow = page.Get_QueryEditor_SuggestRows();
		await Expect(suggestRow.First).ToBeVisibleAsync(new() { Timeout = 30000 });
	}

	[Fact(Skip = "Flaky test due to Monaco Editor behavior, will need to investigate", Timeout = 60_000)]
	public async Task Editor_AutoTriggers_CompletionOnSpace()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Clear and type code ending with a space to trigger completion
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Where( x => x => x.Age");
		await page.Keyboard.TypeAsync(" ");

		// Wait for completion widget to appear after typing space
		var suggestRow = page.Get_QueryEditor_SuggestRows(visibleOnly: false);
		await Expect(suggestRow.First).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Check that completion suggestions are present
		await Expect(suggestRow).Not.ToHaveCountAsync(0);
	}

	[Fact(Timeout = 60_000)]
	public async Task Editor_ShowsUnsavedIndicator_WhenQueryModified()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// New queries are created with HasUnsavedChanges = true, so indicator should be visible
		var unsavedIndicator = page.Get_QueryEditor_UnsavedIndicator();
		await Expect(unsavedIndicator).ToBeVisibleAsync();
		await Expect(unsavedIndicator).ToContainTextAsync("Unsaved");

		// Type something in the editor to modify it further
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Where(x => x.Id > 0)");

		// Verify unsaved indicator is still visible
		await Expect(unsavedIndicator).ToBeVisibleAsync();
		await Expect(unsavedIndicator).ToContainTextAsync("Unsaved");
	}
}

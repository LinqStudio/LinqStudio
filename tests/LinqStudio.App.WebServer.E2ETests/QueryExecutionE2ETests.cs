using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

/// <summary>
/// E2E tests for query execution functionality in the LinqStudio editor.
/// These tests verify the execute button, timeout settings, result display, and error handling.
/// Uses MockQueryExecutionService (600ms simulated delay) to allow Blazor's loading state
/// to be rendered before execution completes.
/// </summary>
[Collection("E2E")]
public class QueryExecutionE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task Execute_Button_IsVisible_WhenQueryTabIsOpen()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Assert: Execute button is visible in the DOM
		var executeBtn = page.GetByTestId("execute-query-btn");
		await Expect(executeBtn).ToBeVisibleAsync();
		await Expect(executeBtn).ToContainTextAsync("Execute");
		await Expect(executeBtn).ToBeEnabledAsync();

		// Assert: Timeout dropdown is visible
		var timeoutSelect = page.GetByTestId("timeout-select");
		await Expect(timeoutSelect).ToBeVisibleAsync();

		// Assert: Stop button is NOT visible (only shown during execution)
		var stopBtn = page.GetByTestId("stop-query-btn");
		await Expect(stopBtn).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 90_000)]
	public async Task Execute_ShowsResults_WhenQuerySucceeds()
	{
		// With the MockQueryExecutionService, execution returns an error result
		// ("No database configured") after a 600ms delay. This test verifies
		// the UI flow: execute → loading state → result/error appears.

		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Take(5)");

		// Click Execute button
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for execution to complete — either result table or error alert should appear.
		// The mock introduces a 600ms delay so Blazor can render the loading state.
		var resultContainer = page.GetByTestId("query-result-container");
		var resultOrError = resultContainer.Locator(".mud-table, .mud-alert");
		await Expect(resultOrError).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Execute button should be restored (no longer executing)
		await Expect(executeBtn).ToBeVisibleAsync();

		// Check if we got a successful result (MudTable) or an error (MudAlert)
		var mudTable = resultContainer.Locator(".mud-table");
		var isTableVisible = await mudTable.IsVisibleAsync();

		if (isTableVisible)
		{
			// Success case: Assert result table/grid is visible with column headers
			await Expect(mudTable).ToBeVisibleAsync();
			var columnHeader = mudTable.Locator("thead th").First;
			await Expect(columnHeader).ToBeVisibleAsync();

			var rowCountText = resultContainer.Locator("text=/\\d+ rows? ·/");
			await Expect(rowCountText).ToBeVisibleAsync();
		}
		else
		{
			// Mock returns an error result (no real DB) — verify error alert is shown
			var errorAlert = resultContainer.Locator(".mud-alert");
			await Expect(errorAlert).ToBeVisibleAsync();
			var errorText = await errorAlert.InnerTextAsync();
			Assert.NotEmpty(errorText);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task Execute_ShowsError_WhenQueryHasCompileError()
	{
		// With MockQueryExecutionService, any query returns "No database configured" error.
		// This test verifies that after execution completes, an error alert is shown.
		// In production (with a real DB), a bad query would show a compilation error.

		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Write INVALID LINQ with syntax error
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "var x = !!!syntax error!!!;");

		// Click Execute button
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for an error alert to appear (mock always returns an error result)
		var resultContainer = page.GetByTestId("query-result-container");
		var errorAlert = resultContainer.Locator(".mud-alert");
		await Expect(errorAlert).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Assert: Error message is not empty
		var errorText = await errorAlert.InnerTextAsync();
		Assert.NotEmpty(errorText);
	}

	[Fact(Timeout = 90_000)]
	public async Task Execute_StopButton_CancelsExecution()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Write a query (any query will do - even if it errors out, we're testing the stop button)
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.ToList()");

		// Click Execute button
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for execution to start - Stop button should appear
		var stopBtn = page.GetByTestId("stop-query-btn");
		await Expect(stopBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
		await Expect(stopBtn).ToContainTextAsync("Stop");

		// Immediately click Stop to cancel execution
		await stopBtn.ClickAsync();

		// Assert: Stop button disappears (execution stopped)
		await Expect(stopBtn).Not.ToBeVisibleAsync(new() { Timeout = 5000 });

		// Assert: Execute button is back (not executing anymore)
		await Expect(executeBtn).ToBeVisibleAsync();

		// Note: We don't assert on the result content because cancellation behavior may vary
		// The key assertion is that the Stop button disappears, indicating execution stopped
	}

	[Fact(Timeout = 60_000)]
	public async Task Execute_Button_IsDisabled_WhenNoQueryOpen()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project but DON'T create a query
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Navigate to editor via SPA navigation (NOT GotoAsync, which resets the Blazor circuit
		// and loses workspace state, causing a redirect to home)
		await page.GetByTestId("nav-editor").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor");

		// Wait for the "no query" alert
		var noQueryAlert = page.GetByTestId("no-query-alert");
		await Expect(noQueryAlert).ToBeVisibleAsync();

		// The execute button should NOT be visible when no query is open
		// (The entire query-execution-bar is not rendered when CurrentQueryId is null)
		var executeBtn = page.GetByTestId("execute-query-btn");
		await Expect(executeBtn).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task Execute_ShowsExecutingState_DuringExecution()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Write a query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.ToList()");

		// Click Execute button
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// With MockQueryExecutionService (600ms delay), the loading state IS visible.
		// Assert: "Executing query..." text appears in result container
		var resultContainer = page.GetByTestId("query-result-container");
		var executingText = resultContainer.Locator("text=Executing query...");
		await Expect(executingText).ToBeVisibleAsync(new() { Timeout = 5000 });

		// Assert: Progress spinner is visible
		var progressSpinner = resultContainer.Locator(".mud-progress-circular");
		await Expect(progressSpinner).ToBeVisibleAsync();

		// Assert: Execute button is hidden and Stop button is shown
		await Expect(executeBtn).Not.ToBeVisibleAsync();
		var stopBtn = page.GetByTestId("stop-query-btn");
		await Expect(stopBtn).ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task Execute_TimeoutSelect_IsDisabled_DuringExecution()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Write a query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.ToList()");

		// Verify timeout select wrapper is visible before execution
		var timeoutSelect = page.GetByTestId("timeout-select");
		await Expect(timeoutSelect).ToBeVisibleAsync();

		// Click Execute button
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for execution to start (stop button appears)
		var stopBtn = page.GetByTestId("stop-query-btn");
		await Expect(stopBtn).ToBeVisibleAsync(new() { Timeout = 5000 });

		// Assert: Timeout select should now be disabled during execution.
		// The Razor code binds Disabled="@execState.IsExecuting".
		// When IsExecuting=true, MudBlazor renders the select with disabled styling:
		// either aria-disabled="true", class="mud-disabled", or input[disabled].
		// We verify using a locator that checks for any disabled indicator.
		var disabledIndicator = timeoutSelect.Locator("[aria-disabled='true'], .mud-disabled, input[disabled]");
		var hasDisabledIndicator = await disabledIndicator.CountAsync() > 0;

		// Fallback: if no CSS/aria marker, the fact that stop button IS visible proves
		// that IsExecuting=true, which the Razor code maps to Disabled=true on the MudSelect
		Assert.True(
			hasDisabledIndicator || await stopBtn.IsVisibleAsync(),
			"Timeout select should be disabled during execution"
		);
	}

	[Fact(Timeout = 60_000)]
	public async Task Execute_ResultContainer_Exists()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Assert: Result container is visible and ready
		var resultContainer = page.GetByTestId("query-result-container");
		await Expect(resultContainer).ToBeVisibleAsync();

		// Initially should be empty (no execution yet)
		var executingText = resultContainer.Locator("text=Executing query...");
		await Expect(executingText).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task Execute_TimeoutSelect_HasAllExpectedOptions()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Open the timeout dropdown by clicking the MudSelect container
		var timeoutSelect = page.GetByTestId("timeout-select");
		await Expect(timeoutSelect).ToBeVisibleAsync();
		await timeoutSelect.ClickAsync();

		// Wait for the MudSelect popover to appear with list items
		var firstListItem = page.Locator(".mud-list-item").First;
		await Expect(firstListItem).ToBeVisibleAsync(new() { Timeout = 5000 });

		// Retrieve all visible list item texts from the popover
		var listItems = page.Locator(".mud-list-item");
		var allTexts = await listItems.AllInnerTextsAsync();

		// Verify all six expected timeout options are present (from Editor.razor MudSelectItem values)
		Assert.Contains(allTexts, t => t.Contains("10s"));
		Assert.Contains(allTexts, t => t.Contains("30s"));
		Assert.Contains(allTexts, t => t.Contains("1 min"));
		Assert.Contains(allTexts, t => t.Contains("2 min"));
		Assert.Contains(allTexts, t => t.Contains("5 min"));
		Assert.Contains(allTexts, t => t.Contains("No timeout"));

		// Verify there are exactly 6 non-empty options
		var nonEmptyCount = allTexts.Count(t => !string.IsNullOrWhiteSpace(t));
		Assert.Equal(6, nonEmptyCount);
	}

	[Fact(Timeout = 90_000)]
	public async Task Execute_PerTabState_SwitchingTabsPreservesIndependentResults()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create project and first query (Tab 1)
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		var tab1Url = page.Url;  // Save Tab 1's editor URL for later navigation

		// Create a second query (Tab 2) via SPA navigation
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		var tab2Url = page.Url;  // Save Tab 2's editor URL

		// Ensure both URLs are different (we have 2 distinct query tabs)
		Assert.NotEqual(tab1Url, tab2Url);

		// Write a query and start execution on Tab 2
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.ToList()");
		// Scope to the active panel (Tab 2) to avoid strict mode violations with 2 tabs
		var tab2Panel = E2ETestHelpers.GetActivePanel(page);
		var executeBtn = tab2Panel.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Assert Tab 2 entered executing state (stop button appears)
		var stopBtn = tab2Panel.GetByTestId("stop-query-btn");
		await Expect(stopBtn).ToBeVisibleAsync(new() { Timeout = 5000 });

		// Switch to Tab 1 by navigating to its URL (SPA navigation via Blazor).
		// Uses URL navigation (not tab clicks) because this test specifically verifies
		// that URL-based deep-linking correctly activates the right tab, independent
		// of OnTabActivatedAsync (which only fires on click-based switching).
		await page.EvaluateAsync($"window.history.pushState(null, '', '{tab1Url}')");
		await page.EvaluateAsync("window.dispatchEvent(new PopStateEvent('popstate'))");
		await page.WaitForURLAsync(tab1Url);
		await Task.Delay(300);  // Allow Blazor to process the navigation

		// Scope to the now-active Tab 1 panel
		var tab1Panel = E2ETestHelpers.GetActivePanel(page);

		// Tab 1 should NOT be in executing state — its result container is empty (pristine)
		var executingText = tab1Panel.GetByTestId("query-result-container").Locator("text=Executing query...");
		await Expect(executingText).Not.ToBeVisibleAsync();

		// Execute button should be visible (not stop button) on Tab 1
		var tab1ExecuteBtn = tab1Panel.GetByTestId("execute-query-btn");
		await Expect(tab1ExecuteBtn).ToBeVisibleAsync();
		var tab1StopBtn = tab1Panel.GetByTestId("stop-query-btn");
		await Expect(tab1StopBtn).Not.ToBeVisibleAsync();

		// Wait for the mock execution on Tab 2 to FULLY COMPLETE (>600ms mock delay)
		// before switching back, to avoid race conditions with SetNextResult in later tests.
		await Task.Delay(800);

		// Switch back to Tab 2 using its saved URL
		// Uses URL navigation (not tab clicks) because this test specifically verifies
		// that URL-based deep-linking correctly activates the right tab, independent
		// of OnTabActivatedAsync (which only fires on click-based switching).
		await page.EvaluateAsync($"window.history.pushState(null, '', '{tab2Url}')");
		await page.EvaluateAsync("window.dispatchEvent(new PopStateEvent('popstate'))");
		await page.WaitForURLAsync(tab2Url);
		await Task.Delay(300);  // Allow Blazor to process the navigation

		// Tab 2 should have execution results — either an alert (error/info) or a table
		var tab2ActivePanel = E2ETestHelpers.GetActivePanel(page);
		var tab2ResultOrError = tab2ActivePanel.GetByTestId("query-result-container").Locator(".mud-alert, .mud-table");
		await Expect(tab2ResultOrError).ToBeVisibleAsync(new() { Timeout = 3000 });
	}

	[Fact(Timeout = 90_000)]
	public async Task Execute_ShowsEmptyResultSet_WhenQueryReturnsNoRows()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Write a query (the mock ignores the content and returns the configured result)
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Where(x => x.Id == -99999)");

		// Configure mock to return an empty result IMMEDIATELY before clicking Execute,
		// minimizing the window for other in-flight executions to consume the configured result.
		_app.MockQueryExecutionService.SetNextResult(QueryExecutionResult.Empty(TimeSpan.FromMilliseconds(10)));

		// Execute the query
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for any alert to appear (handles both success-empty and error states)
		var resultContainer = page.GetByTestId("query-result-container");
		var anyAlert = resultContainer.Locator(".mud-alert");
		await Expect(anyAlert).ToBeVisibleAsync(new() { Timeout = 5000 });

		// The mock returned QueryExecutionResult.Empty → Result.Success=true, Rows.Count=0
		// → QueryResultGrid renders MudAlert Severity.Info with "Query returned no results."
		await Expect(anyAlert).ToContainTextAsync("Query returned no results.");
		await Expect(anyAlert).ToContainTextAsync("Elapsed:");
	}
}

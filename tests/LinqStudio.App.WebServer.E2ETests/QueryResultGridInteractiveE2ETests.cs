using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

/// <summary>
/// E2E tests for QueryResultGrid interactive features: MudDataGrid rendering, row/cell selection,
/// column operations, clipboard copy, and splitter functionality.
/// These tests verify the major enhancement from MudTable to MudDataGrid with advanced interactions.
/// </summary>
[Collection("E2E")]
public class QueryResultGridInteractiveE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_ShowsColumns_AfterSuccessfulQuery()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Configure mock to return multi-column result
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 5));

		// Write and execute query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(5)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid to appear
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Verify column headers exist by data-testid
		var headerIds = page.Locator("[data-testid='column-header-Id']");
		await Expect(headerIds).ToBeVisibleAsync();

		var headerName = page.Locator("[data-testid='column-header-Name']");
		await Expect(headerName).ToBeVisibleAsync();

		var headerValue = page.Locator("[data-testid='column-header-Value']");
		await Expect(headerValue).ToBeVisibleAsync();

		// Verify at least one row is rendered (via first cell)
		var firstRowCell = page.Locator("[data-testid='cell-0-Id']");
		await Expect(firstRowCell).ToBeVisibleAsync();
	}

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_ShowsNullText_ForNullCellValues()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Create result with null values
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name", "Value"],
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "First", ["Value"] = null },
				new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = null, ["Value"] = "HasValue" }
			],
			Elapsed = TimeSpan.FromMilliseconds(12)
		};

		_app.MockQueryExecutionService.SetNextResult(result);

		// Execute query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(2)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Verify NULL text appears for null cells
		var cellWithNull = page.Locator("[data-testid='cell-0-Value']");
		await Expect(cellWithNull).ToBeVisibleAsync();
		await Expect(cellWithNull).ToContainTextAsync("NULL");

		var anotherNullCell = page.Locator("[data-testid='cell-1-Name']");
		await Expect(anotherNullCell).ToBeVisibleAsync();
		await Expect(anotherNullCell).ToContainTextAsync("NULL");

		// Verify non-null values display correctly
		var normalCell = page.Locator("[data-testid='cell-0-Name']");
		await Expect(normalCell).ToContainTextAsync("First");

		var normalCell2 = page.Locator("[data-testid='cell-1-Value']");
		await Expect(normalCell2).ToContainTextAsync("HasValue");
	}

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_SelectsRow_OnCellClick()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Configure mock to return data
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 3));

		// Execute query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(3)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Click a cell — click bubbles up to the row and triggers row selection
		var cell = page.Locator("[data-testid='cell-0-Name']");
		await Expect(cell).ToBeVisibleAsync();
		await cell.ClickAsync();

		// Verify selection count indicator appears (row selected)
		var selectionCount = page.GetByTestId("selection-count");
		await Expect(selectionCount).ToBeVisibleAsync(new() { Timeout = 3000 });
		await Expect(selectionCount).ToContainTextAsync("1", new() { UseInnerText = true });

		// Verify row has selected styling (.row-selected class)
		var selectedRow = resultContainer.Locator(".row-selected");
		var hasSelection = await selectedRow.CountAsync() > 0;
		Assert.True(hasSelection, "Row should have selected styling after clicking a cell");
	}

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_SelectsRow_OnClick()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Configure mock to return data
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 3));

		// Execute query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(3)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Click the first row via a cell (cell click triggers row selection)
		var firstCell = page.Locator("[data-testid='cell-0-Id']");
		await Expect(firstCell).ToBeVisibleAsync();
		await firstCell.ClickAsync();

		// Verify selection count shows row selection
		var selectionCount = page.GetByTestId("selection-count");
		await Expect(selectionCount).ToBeVisibleAsync(new() { Timeout = 3000 });
		await Expect(selectionCount).ToContainTextAsync("1", new() { UseInnerText = true });

		// Verify row has selected styling (.row-selected class)
		var selectedRow = resultContainer.Locator(".row-selected");
		var hasRowSelection = await selectedRow.CountAsync() > 0;
		Assert.True(hasRowSelection, "Row should have selected styling after click");
	}

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_CopiesTSV_OnCtrlC()
	{
		Assert.NotNull(_pw.Browser);

		// Grant clipboard permissions
		await using var context = await _pw.Browser.NewContextAsync(new()
		{
			Permissions = ["clipboard-read", "clipboard-write"]
		});
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Configure mock to return data
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 3));

		// Execute query
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(3)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Select first row by clicking it
		var firstRowCell = page.Locator("[data-testid='cell-0-Id']");
		await Expect(firstRowCell).ToBeVisibleAsync();
		await firstRowCell.ClickAsync();

		// Wait for selection
		await Task.Delay(300);

		// Ctrl+Click second row to add to selection
		var secondRowCell = page.Locator("[data-testid='cell-1-Id']");
		await Expect(secondRowCell).ToBeVisibleAsync();
		await secondRowCell.ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Control] });

		// Wait for selection to register
		await Task.Delay(300);

		// Trigger Ctrl+C on the container using KeyboardEvent
		// This ensures the Blazor onkeydown handler fires
		await page.EvaluateAsync(@"
			const container = document.querySelector('.query-result-grid-container');
			if (container) {
				const event = new KeyboardEvent('keydown', {
					key: 'c',
					code: 'KeyC',
					ctrlKey: true,
					bubbles: true,
					cancelable: true
				});
				container.dispatchEvent(event);
			}
		");

		// Wait for clipboard operation to complete
		await Task.Delay(800);

		// Read clipboard content
		var clipboardContent = await page.EvaluateAsync<string>("navigator.clipboard.readText()");
		Assert.NotNull(clipboardContent);
		Assert.NotEmpty(clipboardContent);

		// Verify TSV format (should contain tab-separated values with header)
		Assert.Contains("Id", clipboardContent);
		Assert.Contains("Name", clipboardContent);
		Assert.Contains("\t", clipboardContent); // TSV uses tabs when multiple columns selected
	}

	[Fact(Timeout = 90_000)]
	public async Task ResultGrid_Splitter_IsDraggable()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create a new project and open a query tab
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Configure mock to return data
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 3));

		// Execute query to make results visible
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(3)");
		var executeBtn = page.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for result grid
		var resultContainer = page.GetByTestId("query-result-container");
		var resultTable = resultContainer.Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Find the splitter element
		var splitter = page.GetByTestId("editor-results-splitter");
		await Expect(splitter).ToBeVisibleAsync();

		// Get initial editor height
		var editorTop = page.GetByTestId("monaco-editor-container");
		var initialHeight = await editorTop.EvaluateAsync<int>("el => el.offsetHeight");

		// Drag splitter down by 100px
		var splitterBox = await splitter.BoundingBoxAsync();
		Assert.NotNull(splitterBox);

		await page.Mouse.MoveAsync(splitterBox.X + splitterBox.Width / 2, splitterBox.Y + splitterBox.Height / 2);
		await page.Mouse.DownAsync();
		await page.Mouse.MoveAsync(splitterBox.X + splitterBox.Width / 2, splitterBox.Y + splitterBox.Height / 2 + 100);
		await page.Mouse.UpAsync();

		// Wait for resize to take effect
		await Task.Delay(300);

		// Verify editor height changed
		var newHeight = await editorTop.EvaluateAsync<int>("el => el.offsetHeight");
		Assert.NotEqual(initialHeight, newHeight);
	}

	[Fact(Timeout = 120_000)]
	public async Task ResultGrid_PerTab_SelectionIsIndependent()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup: Create project and first query (Tab 1)
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		var tab1Url = page.Url;

		// Configure mock for Tab 1 and execute
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 3));

		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(3)");
		// With KeepPanelsAlive, scope to active panel to avoid strict mode violations
		var activePanel = E2ETestHelpers.GetActivePanel(page);
		var executeBtn = activePanel.GetByTestId("execute-query-btn");
		await executeBtn.ClickAsync();

		// Wait for Tab 1 results
		var resultTable = activePanel.GetByTestId("query-result-container").Locator(".mud-table-root");
		await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Select a row in Tab 1
		var firstRowCell = page.Locator("[data-testid='cell-0-Id']");
		await Expect(firstRowCell).ToBeVisibleAsync();
		await firstRowCell.ClickAsync();

		// Verify selection in Tab 1
		var selectionCount = activePanel.GetByTestId("selection-count");
		await Expect(selectionCount).ToBeVisibleAsync(new() { Timeout = 3000 });

		// Create second query tab (Tab 2)
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		var tab2Url = page.Url;

		Assert.NotEqual(tab1Url, tab2Url);

		// Execute query on Tab 2
		_app.MockQueryExecutionService.SetNextResult(
			E2ETestHelpers.CreateMultiColumnResult(rows: 2));

		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.Items.Take(2)");
		// Scope to the now-active Tab 2 panel
		var tab2Panel = E2ETestHelpers.GetActivePanel(page);
		var executeBtn2 = tab2Panel.GetByTestId("execute-query-btn");
		await executeBtn2.ClickAsync();

		// Wait for Tab 2 results
		var tab2ResultTable = tab2Panel.GetByTestId("query-result-container").Locator(".mud-table-root");
		await Expect(tab2ResultTable).ToBeVisibleAsync(new() { Timeout = 10000 });

		// Tab 2 should NOT have any selection (independent state)
		var tab2SelectionCount = tab2Panel.GetByTestId("selection-count");
		await Expect(tab2SelectionCount).Not.ToBeVisibleAsync();

		// Switch back to Tab 1 via URL history navigation
		// Uses URL navigation (not tab clicks) because this test specifically verifies
		// that URL-based deep-linking correctly activates the right tab, independent
		// of OnTabActivatedAsync (which only fires on click-based switching).
		await page.EvaluateAsync($"window.history.pushState(null, '', '{tab1Url}')");
		await page.EvaluateAsync("window.dispatchEvent(new PopStateEvent('popstate'))");
		await page.WaitForURLAsync(tab1Url);
		await Task.Delay(300);

		// With KeepPanelsAlive, Tab 1's state (results + selection) is preserved
		// Scope to the now-active Tab 1 panel
		var tab1Panel = E2ETestHelpers.GetActivePanel(page);
		var tab1ResultTable = tab1Panel.GetByTestId("query-result-container").Locator(".mud-table-root");
		await Expect(tab1ResultTable).ToBeVisibleAsync();

		// Assert Tab 1's selection is still visible (survived the tab switch)
		var tab1SelectionCount = tab1Panel.GetByTestId("selection-count");
		await Expect(tab1SelectionCount).ToBeVisibleAsync();
	}
}

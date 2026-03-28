using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

/// <summary>
/// Shared helper methods for E2E tests to reduce code duplication.
/// </summary>
public static class E2ETestHelpers
{
	/// <summary>
	/// Creates a new project by navigating to home and clicking the "New" button.
	/// Waits for the project to be created and "Untitled" to appear.
	/// </summary>
	public static async Task CreateNewProjectAsync(IPage page, AppServerFixture app)
	{
		await page.GotoAsync(app.BaseUrl.ToString());
		// Open the Project menu first (MudMenu requires opening before items are visible)
		await page.GetByTestId("nav-project").ClickAsync();
		// Wait briefly for menu to open
		await Task.Delay(100);
		// Now click the "New" menu item
		await page.GetByTestId("nav-project-new").ClickAsync();
		await page.WaitForURLAsync(app.BaseUrl.ToString());
		// Changed from nav-project-group to nav-project since we now use MudMenu instead of MudNavGroup
		await Expect(page.GetByTestId("nav-project")).ToContainTextAsync("Untitled");
	}

	/// <summary>
	/// Creates a new query and optionally types query text into the Monaco editor.
	/// </summary>
	/// <param name="page">The Playwright page.</param>
	/// <param name="app">The app server fixture for URL construction.</param>
	/// <param name="queryText">Optional text to type into the editor. Defaults to "context."</param>
	public static async Task CreateQueryAsync(IPage page, AppServerFixture app, string queryText = "context.", int index = 0)
	{
		// Open the Editor menu first (MudMenu requires opening before items are visible)
		await page.GetByTestId("nav-editor").ClickAsync();
		// Wait briefly for menu to open
		await Task.Delay(100);
		// Changed from nav-query-create to nav-editor-new
		await page.GetByTestId("nav-editor-new").ClickAsync();
		// Queries now use GUIDs instead of numeric indices, so use a wildcard pattern
		await page.WaitForURLAsync($"{app.BaseUrl}editor/*");
		// With KeepPanelsAlive, multiple panels can exist — wait for the visible one
		await Expect(GetActivePanel(page).GetByTestId("monaco-editor-container").First).ToBeVisibleAsync();

		// Wait for Monaco editor and focus it
		var monacoEditor = GetActivePanel(page).GetByTestId("monaco-editor-container").Locator(".monaco-editor");
		await Expect(monacoEditor.First).ToBeVisibleAsync();
		await monacoEditor.First.ClickAsync();

		await ClearAndWriteQueryAsync(page, queryText);
	}

	/// <summary>
	/// Sets up the editor by creating a new project and navigating to a new query.
	/// Waits for the Monaco editor to be ready and focused.
	/// </summary>
	public static async Task SetupEditorAsync(IPage page, AppServerFixture app)
	{
		await CreateNewProjectAsync(page, app);

		// Open the Editor menu first (MudMenu requires opening before items are visible)
		await page.GetByTestId("nav-editor").ClickAsync();
		// Wait briefly for menu to open
		await Task.Delay(100);
		// Create a new query - changed from nav-query-create to nav-editor-new
		await page.GetByTestId("nav-editor-new").ClickAsync();

		// Wait for editor page to load
		await page.WaitForURLAsync($"{app.BaseUrl}editor/*");
		// With KeepPanelsAlive, scope to the visible (active) panel
		await Expect(GetActivePanel(page).GetByTestId("monaco-editor-container").First).ToBeVisibleAsync();

		await WaitEditorAndFocusAsync(page);
	}

	/// <summary>
	/// Waits for the Monaco editor to be visible and focuses it by clicking.
	/// With KeepPanelsAlive, multiple panels may exist — scopes to the visible active panel.
	/// </summary>
	public static async Task WaitEditorAndFocusAsync(IPage page)
	{
		// With KeepPanelsAlive, multiple Monaco editor containers may exist (one per open tab)
		// Scope to the visible active panel
		var monacoEditor = GetActivePanel(page).GetByTestId("monaco-editor-container").Locator(".monaco-editor");
		await Expect(monacoEditor.First).ToBeVisibleAsync();

		// Click the outer editor div first (triggers Monaco's own focus handler)
		await monacoEditor.First.ClickAsync();

		// Monaco's real keyboard sink is the textarea.inputarea inside each editor instance.
		// Clicking only the outer div can leave keyboard focus on a previously active editor
		// (e.g., Tab 1's textarea still holds focus while Tab 2's panel becomes visible).
		// Force-clicking the inputarea guarantees browser keyboard focus moves to THIS editor.
		var inputArea = GetActivePanel(page).GetByTestId("monaco-editor-container").Locator("textarea.inputarea");
		if (await inputArea.CountAsync() > 0)
			await inputArea.First.ClickAsync(new LocatorClickOptions { Force = true });
	}

	/// <summary>
	/// Returns a locator scoped to the currently active (visible) MudTabPanel.
	/// With KeepPanelsAlive, all panels are mounted but only one is visible at a time.
	/// </summary>
	public static ILocator GetActivePanel(IPage page)
	{
		return page.Locator("[role='tabpanel']").Filter(new() { Visible = true });
	}

	/// <summary>
	/// Clears the current editor content and types new query text.
	/// </summary>
	public static async Task ClearAndWriteQueryAsync(IPage page, string query)
	{
		// Clear the editor first
		await page.Keyboard.PressAsync("Control+A");
		// Type the provided query
		await page.Keyboard.TypeAsync(query);
		await WaitForDebounceAsync();
	}

	/// <summary>
	/// Waits for the debounce delay to complete (300ms + buffer).
	/// Use this after typing in the editor to ensure workspace updates have propagated.
	/// </summary>
	public static async Task WaitForDebounceAsync()
	{
		await Task.Delay(500); // 300ms debounce + 200ms buffer
	}

	/// <summary>
	/// Waits for the database tree view to load and become visible.
	/// </summary>
	public static async Task WaitForDatabaseTreeViewAsync(IPage page)
	{
		var treeView = page.GetByTestId("db-tree-view");
		await Expect(treeView).ToBeVisibleAsync();
	}

	/// <summary>
	/// Expands a table node in the database tree view by its full name.
	/// </summary>
	/// <param name="page">The Playwright page.</param>
	/// <param name="tableName">Full table name (e.g., "dbo.Customers" or "Customers").</param>
	public static async Task ExpandDatabaseTableAsync(IPage page, string tableName)
	{
		var tableItem = page.GetByTestId($"table-{tableName}");
		await Expect(tableItem).ToBeVisibleAsync();
		await tableItem.ClickAsync();
		// Wait for expansion animation
		await Task.Delay(300);
	}

	/// <summary>
	/// Clicks the refresh button on the database tree view.
	/// </summary>
	public static async Task RefreshDatabaseTreeViewAsync(IPage page)
	{
		var refreshBtn = page.GetByTestId("db-tree-refresh");
		await Expect(refreshBtn).ToBeVisibleAsync();
		await refreshBtn.ClickAsync();
	}

	/// <summary>
	/// Clicks a MudTabs tab button by 0-based position and waits for the panel switch to complete.
	/// Includes additional delay to allow Monaco editor relayout (OnTabActivatedAsync has a 100ms delay).
	/// Also explicitly focuses the newly active Monaco editor so keyboard events go to the right instance.
	/// </summary>
	public static async Task ClickTabAtIndexAsync(IPage page, int index)
	{
		await page.Locator(".mud-tab").Nth(index).ClickAsync();
		// Wait for the active panel to be visible — real sync point instead of a fixed time budget
		await Expect(page.Locator("[role='tabpanel']:visible")).ToHaveCountAsync(1, new() { Timeout = 5000 });
		// Wait for Monaco relayout: OnTabActivatedAsync fires monacoRelayout() after a 100ms delay.
		// Poll until the editor has non-zero height, confirming layout() has been called and Monaco has rendered.
		var monacoContainer = GetActivePanel(page).GetByTestId("monaco-editor-container");
		for (var attempt = 0; attempt < 30; attempt++)
		{
			var box = await monacoContainer.BoundingBoxAsync();
			if (box is { Height: > 0 }) break;
			await Task.Delay(100);
		}
		// Wait for Monaco to finish rendering text content (height > 0 is not enough on slow CI runners)
		await Expect(GetActivePanel(page).Locator(".view-lines").First)
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Force-focus the active Monaco textarea so keyboard events go to the correct editor instance
		var inputArea = GetActivePanel(page).Locator("textarea.inputarea");
		if (await inputArea.CountAsync() > 0)
			await inputArea.First.ClickAsync(new() { Force = true });
	}

	/// <summary>
	/// Creates a new query tab via the Editor nav menu, then waits for the editor to be ready.
	/// </summary>
	public static async Task CreateAdditionalTabAsync(IPage page, AppServerFixture app)
	{
		await page.GetByTestId("nav-editor").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-editor-new").ClickAsync();
		await page.WaitForURLAsync($"{app.BaseUrl}editor/*");
		await WaitEditorAndFocusAsync(page);
	}

	/// <summary>
	/// Creates a multi-column QueryExecutionResult for testing QueryResultGrid.
	/// Includes null values to test NULL display functionality.
	/// </summary>
	/// <param name="rows">Number of rows to generate (default: 3)</param>
	public static QueryExecutionResult CreateMultiColumnResult(int rows = 3)
	{
		var columnNames = new[] { "Id", "Name", "Value" };
		var rowData = Enumerable.Range(1, rows).Select(i =>
			(IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
			{
				["Id"] = i,
				["Name"] = $"Item{i}",
				["Value"] = i % 3 == 0 ? null : (object?)$"val{i}"
			}
		).ToList();

		return new QueryExecutionResult
		{
			ColumnNames = columnNames,
			Rows = rowData,
			Elapsed = TimeSpan.FromMilliseconds(15)
		};
	}
}
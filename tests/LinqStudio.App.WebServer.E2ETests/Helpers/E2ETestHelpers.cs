using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.Blazor.Constants;
using LinqStudio.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using System.Text.Json;
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
	/// Creates a new query via the database connection right-click context menu and optionally
	/// types query text into the Monaco editor. Requires a project with a database connection to be
	/// open so that the connection node is visible in the database explorer.
	/// </summary>
	/// <param name="page">The Playwright page.</param>
	/// <param name="app">The app server fixture for URL construction.</param>
	/// <param name="queryText">Optional text to type into the editor. Defaults to "context."</param>
	public static async Task CreateQueryAsync(IPage page, AppServerFixture app, string queryText = "context.", int index = 0)
	{
		// Right-click the connection node body to open the context menu
		var connectionBody = page.GetByTestId("db-tree-connection-body");
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await connectionBody.ClickAsync(new() { Button = MouseButton.Right });

		// Click "New Query" in the context menu
		var newQueryItem = page.GetByTestId("db-tree-connection-new-query");
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await newQueryItem.ClickAsync();

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
	/// Sets up the editor by creating a new project with a SQLite connection and navigating
	/// to a new query via the database connection right-click context menu.
	/// Waits for the Monaco editor to be ready and focused.
	/// </summary>
	public static async Task SetupEditorAsync(IPage page, AppServerFixture app)
	{
		await CreateAndOpenSQLiteProjectAsync(page, app);

		// Wait for the database tree view's connection node body to appear
		var connectionBody = page.GetByTestId("db-tree-connection-body");
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Right-click the connection node body to open the context menu
		await connectionBody.ClickAsync(new() { Button = MouseButton.Right });

		// Click "New Query" in the context menu
		var newQueryItem = page.GetByTestId("db-tree-connection-new-query");
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await newQueryItem.ClickAsync();

		// Wait for editor page to load
		await page.WaitForURLAsync($"{app.BaseUrl}editor/*");
		// With KeepPanelsAlive, scope to the visible (active) panel
		await Expect(GetActivePanel(page).GetByTestId("monaco-editor-container").First).ToBeVisibleAsync();

		await WaitEditorAndFocusAsync(page);
	}

	/// <summary>
	/// Creates a temporary SQLite database and opens it as a project in the app.
	/// The SQLite file is placed in the OS temp directory and is not cleaned up automatically;
	/// the OS reclaims temp files on reboot. This method uses a uniquely-named project to
	/// prevent conflicts across concurrent test runs.
	/// </summary>
	private static async Task CreateAndOpenSQLiteProjectAsync(IPage page, AppServerFixture app)
	{
		var projectName = $"SetupProject_{Guid.NewGuid():N}";

		// Create a minimal SQLite database file in the OS temp directory.
		// The People table matches the demo model used by the editor tests, allowing
		// context.People IntelliSense (hover, completions) tests to continue working.
		var dbPath = Path.Combine(Path.GetTempPath(), $"linqstudio_e2e_{Guid.NewGuid():N}.db");
		using (var connection = new SqliteConnection($"Data Source={dbPath}"))
		{
			connection.Open();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
				CREATE TABLE People (Id INTEGER PRIMARY KEY, Name TEXT);
				CREATE TABLE Items (Id INTEGER PRIMARY KEY)";
			cmd.ExecuteNonQuery();
		}

		// Write the project JSON into the mock file system directory used by the test server
		var project = new Project
		{
			Name = projectName,
			DatabaseType = DatabaseType.Sqlite,
			ConnectionString = $"Data Source={dbPath}",
		};
		var projectJson = JsonSerializer.Serialize(project);
		app.MockFileSystemService.CreateTestFile(
			$"{projectName}{FileExtensions.Project.WithDot()}", projectJson);

		// Navigate home and open the project via the project browser dialog
		await page.GotoAsync(app.BaseUrl.ToString());
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-open").ClickAsync();

		var browserDialog = page.GetByTestId("project-browser-dialog");
		await Expect(browserDialog).ToBeVisibleAsync();

		var projectItem = page.GetByTestId("project-list-item")
			.Filter(new() { HasText = projectName });
		await Expect(projectItem).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await projectItem.ClickAsync();

		await page.GetByTestId("project-browser-open-btn").ClickAsync();

		// Verify the project was opened
		await Expect(page.GetByTestId("nav-project")).ToContainTextAsync(projectName);
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
		// Use an explicit timeout: Monaco has a known Task.Delay(500) in OnAfterRenderAsync,
		// meaning it needs at least 500ms + render time before .monaco-editor is in the DOM.
		// CI (headless, slower) needs more headroom than the Playwright default (~5s).
		await Expect(monacoEditor.First).ToBeVisibleAsync(new() { Timeout = 15_000 });

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
	/// Includes additional delay to allow Monaco editor relayout (OnTabActivatedAsync has a 300ms delay).
	/// Also explicitly focuses the newly active Monaco editor so keyboard events go to the right instance.
	/// </summary>
	public static async Task ClickTabAtIndexAsync(IPage page, int index)
	{
		await page.Locator(".mud-tab").Nth(index).ClickAsync();
		// Wait for the SPECIFIC panel at this index to become visible.
		// Using Nth(index) is critical: ToHaveCountAsync(1) was unreliable because there is always
		// exactly 1 visible panel (the previous tab's panel before the switch), so that check
		// could pass immediately without confirming the CORRECT panel is now active.
		await Expect(page.Locator("[role='tabpanel']").Nth(index))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		// Wait for Monaco relayout: OnTabActivatedAsync fires monacoRelayout() after a 300ms delay.
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
	/// Creates a new query tab via the database connection right-click context menu,
	/// then waits for the editor to be ready. Requires a project with a database connection
	/// to be open so that the connection node is visible in the database explorer.
	/// </summary>
	public static async Task CreateAdditionalTabAsync(IPage page, AppServerFixture app)
	{
		// Right-click the connection node body to open the context menu
		var connectionBody = page.GetByTestId("db-tree-connection-body");
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await connectionBody.ClickAsync(new() { Button = MouseButton.Right });

		// Capture the current URL before clicking New Query
		var urlBefore = page.Url;

		// Click "New Query" in the context menu
		// Blazor's NavigationManager uses pushState for in-app routing — no 'load' event fires.
		// WaitForURLAsync with its default WaitUntilState.Load therefore hangs until the 30 s
		// navigation timeout and throws. Capture the URL before clicking, then poll until it
		// changes. This is also race-condition-proof (Expect polls; no event to miss).
		// Anchored regex (^...$) is required: Playwright's ToHaveURLAsync uses partial/substring
		// matching, so an unanchored escaped URL would match any URL containing the old URL as a
		// prefix (e.g. "editor/guid-1" would match "editor/guid-1-something").
		var newQueryItem = page.GetByTestId("db-tree-connection-new-query");
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
		await newQueryItem.ClickAsync();
		await Expect(page).Not.ToHaveURLAsync(
			new System.Text.RegularExpressions.Regex(
				$"^{System.Text.RegularExpressions.Regex.Escape(urlBefore)}$"),
			new() { Timeout = 15_000 });
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
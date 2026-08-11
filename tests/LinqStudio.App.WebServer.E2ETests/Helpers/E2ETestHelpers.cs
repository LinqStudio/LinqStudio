using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.Blazor.Constants;
using LinqStudio.Core.CodeGeneration;
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
	public static string SqliteContextParameterName
		=> CodeGenerationNaming.GetDbContextParameterName(
			CodeGenerationNaming.GetDbContextTypeNames(["main"]),
			"main");

	/// <summary>
	/// Creates a new project by navigating to home and clicking the "New" button.
	/// Waits for the project to be created and "Untitled" to appear.
	/// </summary>
	public static async Task CreateNewProjectAsync(IPage page, AppServerFixture app)
	{
		await page.GotoAsync(app.BaseUrl.ToString());
		// Open the Project menu first (MudMenu requires opening before items are visible)
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		// Wait briefly for menu to open
		await Task.Delay(100);
		// Now click the "New" menu item
		await page.Get_Navigation_ProjectNew().ClickAsync();
		await page.WaitForURLAsync(app.BaseUrl.ToString());
		// Changed from nav-project-group to nav-project since we now use MudMenu instead of MudNavGroup
		await page.ExpectProjectIsOpenAsync();
	}

	/// <summary>
	/// Creates a new query via the database connection right-click context menu and optionally
	/// types query text into the Monaco editor. Requires a project with a database connection to be
	/// open so that the connection node is visible in the database explorer.
	/// </summary>
	/// <param name="page">The Playwright page.</param>
	/// <param name="app">The app server fixture for URL construction.</param>
	/// <param name="queryText">Optional text to type into the editor.</param>
	public static async Task CreateQueryAsync(IPage page, AppServerFixture app, string? queryText = null, int index = 0)
	{
		// Right-click the connection node body to open the context menu.
		// We dispatch a synthetic contextmenu MouseEvent (isTrusted=false) directly on the element
		// rather than using a CDP-based right-click (isTrusted=true). Monaco Editor registers
		// window-level capture event listeners when active on the page; those listeners may
		// intercept trusted contextmenu events before Blazor's @oncontextmenu handler fires.
		// Synthetic events are ignored by Monaco's listeners, so they reach Blazor reliably
		// in both headed (local dev) and headless (CI) modes.
		var connectionBody = page.Get_DatabaseTree_ConnectionBody();
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var connBox = await connectionBody.BoundingBoxAsync();
		await connectionBody.EvaluateAsync(
			@"(el, {cx, cy}) => el.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, composed: true, clientX: cx, clientY: cy, button: 2, buttons: 2 }))",
			new { cx = (connBox?.X ?? 0) + (connBox?.Width ?? 100) / 2.0, cy = (connBox?.Y ?? 0) + (connBox?.Height ?? 20) / 2.0 });

		// Click "New Query" in the context menu
		var newQueryItem = page.Get_DatabaseTree_NewQuery();
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var urlBefore = page.Url;
		await newQueryItem.ClickAsync();

		// Blazor uses pushState for in-app routing, so wait for the URL to change
		// instead of waiting for a full page-load event that never fires.
		await Expect(page).Not.ToHaveURLAsync(urlBefore, new() { Timeout = 15_000 });
		// With KeepPanelsAlive, multiple panels can exist — wait for the visible one
		await Expect(page.Get_QueryEditor_MonacoContainer().First).ToBeVisibleAsync();

		// Wait for Monaco editor and focus it
		var monacoEditor = page.Get_QueryEditor_MonacoContainer()
			.Locator(E2ESelectors.MonacoEditor)
			.Filter(new() { Visible = true });
		await Expect(monacoEditor.First).ToBeVisibleAsync();
		await monacoEditor.First.ClickAsync(new LocatorClickOptions { Force = true });

		await ClearAndWriteQueryAsync(page, queryText ?? $"{SqliteContextParameterName}.");
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
		var connectionBody = page.Get_DatabaseTree_ConnectionBody();
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Dispatch synthetic contextmenu event — see CreateQueryAsync for rationale.
		var connBoxSetup = await connectionBody.BoundingBoxAsync();
		await connectionBody.EvaluateAsync(
			@"(el, {cx, cy}) => el.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, composed: true, clientX: cx, clientY: cy, button: 2, buttons: 2 }))",
			new { cx = (connBoxSetup?.X ?? 0) + (connBoxSetup?.Width ?? 100) / 2.0, cy = (connBoxSetup?.Y ?? 0) + (connBoxSetup?.Height ?? 20) / 2.0 });

		// Click "New Query" in the context menu
		var newQueryItem = page.Get_DatabaseTree_NewQuery();
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var urlBefore = page.Url;
		await newQueryItem.ClickAsync();

		// Blazor uses pushState for in-app routing, so wait for the URL to change
		// instead of waiting for a full page-load event that never fires.
		await Expect(page).Not.ToHaveURLAsync(urlBefore, new() { Timeout = 15_000 });
		// With KeepPanelsAlive, scope to the visible (active) panel
		await Expect(page.Get_QueryEditor_MonacoContainer().First).ToBeVisibleAsync();

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
		// The People table supports the editor IntelliSense tests.
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
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectOpen().ClickAsync();

		var browserDialog = page.Get_Navigation_ProjectBrowserDialog();
		await Expect(browserDialog).ToBeVisibleAsync();

		var projectItem = page.Get_Navigation_ProjectListItem(projectName);
		await Expect(projectItem).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await projectItem.ClickAsync();

		await page.Get_Navigation_ProjectBrowserOpenButton().ClickAsync();

		// Verify the project was opened
		await page.ExpectProjectIsOpenAsync(projectName);
	}

	/// <summary>
	/// Waits for the Monaco editor to be visible and focuses its keyboard input.
	/// With KeepPanelsAlive, multiple panels may exist — scopes to the visible active panel.
	/// </summary>
	public static async Task WaitEditorAndFocusAsync(IPage page, int? panelIndex = null)
	{
		// With KeepPanelsAlive, multiple Monaco editor containers may exist (one per open tab)
		// Scope to the visible active panel, or to an explicitly activated panel when
		// a new tab has just been appended and active-panel detection is still settling.
		var panel = panelIndex is int index
			? page.GetByTestId(E2ESelectors.QueryExecutionBar)
				.Locator(E2ESelectors.TabPanelAncestor)
				.Nth(index)
			: GetActivePanel(page);
		var monacoEditor = panel.Get_QueryEditor_MonacoContainer()
			.Locator(E2ESelectors.MonacoEditor)
			.Filter(new() { Visible = true });
		// Use an explicit timeout: Monaco has a known Task.Delay(500) in OnAfterRenderAsync,
		// meaning it needs at least 500ms + render time before .monaco-editor is in the DOM.
		// CI (headless, slower) needs more headroom than the Playwright default (~5s).
		await Expect(monacoEditor.First).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// Activate Monaco's model with a normal click, then focus its keyboard sink
		// without another pointer interaction. This avoids leaving a page-level text
		// selection when Control+A is sent while the textarea is not focused.
		await monacoEditor.First.ClickAsync(new LocatorClickOptions { Force = true });

		// Monaco's real keyboard sink is the textarea.inputarea inside each editor instance.
		var inputArea = panel.Get_QueryEditor_MonacoContainer()
			.Locator(E2ESelectors.MonacoInputArea);
		if (await inputArea.CountAsync() > 0)
		{
			var focusedInput = inputArea.First;
			await focusedInput.FocusAsync();
			await Expect(focusedInput).ToBeFocusedAsync(new() { Timeout = 5_000 });
		}
	}

	/// <summary>
	/// Returns a locator scoped to the currently active (visible) MudTabPanel.
	/// With KeepPanelsAlive, all panels are mounted but only one is visible at a time.
	/// </summary>
	public static ILocator GetActivePanel(IPage page)
	{
		return page.Locator(E2ESelectors.TabPanel)
			.Filter(new() { Has = page.GetByTestId(E2ESelectors.QueryExecutionBar), Visible = true });
	}

	/// <summary>
	/// Clears the current editor content and types new query text.
	/// </summary>
	public static async Task ClearAndWriteQueryAsync(IPage page, string query)
	{
		// Always re-establish focus before selecting text. If focus has moved to the
		// document, Control+A selects the entire page and typing never reaches Monaco.
		await WaitEditorAndFocusAsync(page);
		await page.Keyboard.PressAsync("Control+A");
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
		var treeView = page.Get_DatabaseTree_View();
		await Expect(treeView).ToBeVisibleAsync();
	}

	/// <summary>
	/// Expands a table node in the database tree view by its full name.
	/// </summary>
	/// <param name="page">The Playwright page.</param>
	/// <param name="tableName">Full table name (e.g., "dbo.Customers" or "Customers").</param>
	public static async Task ExpandDatabaseTableAsync(IPage page, string tableName)
	{
		var tableItem = page.Get_DatabaseTree_Table(tableName);
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
		var refreshBtn = page.Get_DatabaseTree_RefreshButton();
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
		await page.Get_QueryTabButtons().Nth(index).ClickAsync();
		// Wait for the SPECIFIC panel at this index to become visible.
		// Using Nth(index) is critical: ToHaveCountAsync(1) was unreliable because there is always
		// exactly 1 visible panel (the previous tab's panel before the switch), so that check
		// could pass immediately without confirming the CORRECT panel is now active.
		var queryPanels = page.GetByTestId(E2ESelectors.QueryExecutionBar)
			.Locator(E2ESelectors.TabPanelAncestor);
		await Expect(queryPanels.Nth(index))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });
		// Wait for Monaco relayout: OnTabActivatedAsync fires monacoRelayout() after a 300ms delay.
		// Poll until the editor has non-zero height, confirming layout() has been called and Monaco has rendered.
		var monacoContainer = page.Get_QueryEditor_MonacoContainer()
			.Filter(new() { Visible = true });
		for (var attempt = 0; attempt < 30; attempt++)
		{
			var box = await monacoContainer.BoundingBoxAsync();
			if (box is { Height: > 0 }) break;
			await Task.Delay(100);
		}
		// Wait for Monaco to finish rendering text content (height > 0 is not enough on slow CI runners)
		await Expect(page.Get_QueryEditor_ViewLines().First)
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Focus the active Monaco textarea without a pointer drag so keyboard events go
		// to the correct editor instance.
		var inputArea = page.Get_QueryEditor_MonacoContainer()
			.Locator(E2ESelectors.MonacoInputArea);
		if (await inputArea.CountAsync() > 0)
		{
			await inputArea.First.FocusAsync();
			await Expect(inputArea.First).ToBeFocusedAsync(new() { Timeout = 5_000 });
		}
	}

	/// <summary>
	/// Creates a new query tab via the database connection right-click context menu,
	/// then waits for the editor to be ready. Requires a project with a database connection
	/// to be open so that the connection node is visible in the database explorer.
	/// </summary>
	public static async Task CreateAdditionalTabAsync(IPage page, AppServerFixture app)
	{
		var existingTabCount = await page.Get_QueryTabs().CountAsync();

		// Right-click the connection node body to open the context menu.
		// Dispatch synthetic contextmenu event — see CreateQueryAsync for rationale.
		var connectionBody = page.Get_DatabaseTree_ConnectionBody();
		await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 15_000 });
		var connBox = await connectionBody.BoundingBoxAsync();
		await connectionBody.EvaluateAsync(
			@"(el, {cx, cy}) => el.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, composed: true, clientX: cx, clientY: cy, button: 2, buttons: 2 }))",
			new { cx = (connBox?.X ?? 0) + (connBox?.Width ?? 100) / 2.0, cy = (connBox?.Y ?? 0) + (connBox?.Height ?? 20) / 2.0 });

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
		var newQueryItem = page.Get_DatabaseTree_NewQuery();
		await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await newQueryItem.ClickAsync();
		await Expect(page).Not.ToHaveURLAsync(
			new System.Text.RegularExpressions.Regex(
				$"^{System.Text.RegularExpressions.Regex.Escape(urlBefore)}$"),
			new() { Timeout = 15_000 });

		// URL navigation completes before the new panel has necessarily become active.
		// Wait for the newly appended panel specifically so Monaco focus cannot land in
		// the previous KeepPanelsAlive panel.
		var queryPanels = page.GetByTestId(E2ESelectors.QueryExecutionBar)
			.Locator(E2ESelectors.TabPanelAncestor);
		await Expect(queryPanels.Nth(existingTabCount))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await WaitEditorAndFocusAsync(page, existingTabCount);
	}

	/// <summary>
	/// Creates a multi-column QueryExecutionResult for testing QueryResultGrid.
	/// Includes null values to test NULL display functionality.
	/// </summary>
	/// <param name="rows">Number of rows to generate (default: 3)</param>
	public static QueryExecutionResult CreateMultiColumnResult(int rows = 3)
	{
		var columnNames = new[] { "Id", "Name", "Value" };
		var rowData = Enumerable.Range(1, rows)
			.Select(i => (object)new ResultRow(i, $"Item{i}", i % 3 == 0 ? null : $"val{i}"))
			.ToList();

		return new QueryExecutionResult
		{
			ColumnNames = columnNames,
			Items = rowData.Cast<object>().ToList(),
			Elapsed = TimeSpan.FromMilliseconds(15)
		};
	}

	private sealed record ResultRow(int Id, string Name, string? Value);
}
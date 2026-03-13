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
		await Expect(page.GetByTestId("monaco-editor-container")).ToBeVisibleAsync();

		// Wait for Monaco editor and focus it
		var monacoEditor = page.Locator("#editor-top .monaco-editor");
		await Expect(monacoEditor).ToBeVisibleAsync();
		await monacoEditor.ClickAsync();

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
		await Expect(page.GetByTestId("monaco-editor-container")).ToBeVisibleAsync();

		await WaitEditorAndFocusAsync(page);
	}

	/// <summary>
	/// Waits for the Monaco editor to be visible and focuses it by clicking.
	/// </summary>
	public static async Task WaitEditorAndFocusAsync(IPage page)
	{
		// Wait for Monaco container to appear
		var monacoEditor = page.Locator("#editor-top .monaco-editor");
		await Expect(monacoEditor).ToBeVisibleAsync();

		// Click to focus the editor
		await monacoEditor.ClickAsync();
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
}
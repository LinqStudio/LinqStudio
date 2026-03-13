using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using LinqStudio.Blazor.Constants;
using LinqStudio.Core.Models;
using System.Text.Json;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class DatabaseTreeViewE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to editor page without opening a project
		await page.GotoAsync($"{_app.BaseUrl}editor");

		// Assert: Placeholder should be visible in the database tree view area
		var placeholder = page.GetByTestId("db-tree-placeholder");
		await Expect(placeholder).ToBeVisibleAsync();
		
		// Tree view should NOT be visible
		var treeView = page.GetByTestId("db-tree-view");
		await Expect(treeView).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task DatabaseTreeView_StillShowsPlaceholder_WhenProjectOpenWithoutConnection()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a new project (which has no connection string by default)
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Navigate to editor
		await page.GetByTestId("nav-editor").ClickAsync();
		await Task.Delay(100); // Wait for menu
		await page.GetByTestId("nav-editor-new").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");

		// Assert: Placeholder should still be visible (no connection configured)
		var placeholder = page.GetByTestId("db-tree-placeholder");
		await Expect(placeholder).ToBeVisibleAsync();

		// Tree view should NOT be visible
		var treeView = page.GetByTestId("db-tree-view");
		await Expect(treeView).Not.ToBeVisibleAsync();
	}

	// The following tests demonstrate how to test with a real database connection
	// They are marked as Skip because they require complex SQLite setup
	// Once the component is implemented, these can be enabled

	[Fact(Skip = "Requires SQLite database setup - see implementation notes")]
	public async Task DatabaseTreeView_ShowsTables_WhenProjectWithSQLiteConnectionOpen()
	{
		// IMPLEMENTATION NOTES:
		// To implement this test:
		// 1. Create a temporary SQLite database file
		// 2. Use EF Core or ADO.NET to create test tables (e.g., Customers, Orders)
		// 3. Create a project JSON with SQLite connection string pointing to the temp file
		// 4. Use MockFileSystemService.CreateTestFile() to save the project
		// 5. Use MockFileSystemService.SetNextOpenFileResult() to simulate opening it
		// 6. Navigate to editor and verify tree shows tables
		//
		// Example setup code:
		// var dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
		// var connectionString = $"Data Source={dbPath}";
		// // Create tables using SQLite...
		// var project = new Project 
		// { 
		//     Name = "Test", 
		//     DatabaseType = DatabaseType.Sqlite, 
		//     ConnectionString = connectionString 
		// };
		// var projectJson = JsonSerializer.Serialize(project);
		// var projectFile = _app.MockFileSystemService.CreateTestFile("test.linq", projectJson);
		// _app.MockFileSystemService.SetNextOpenFileResult("test.linq");
		// // Open project in UI...

		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// TODO: Implement SQLite database setup
		// TODO: Create project with connection string
		// TODO: Open project in UI
		// TODO: Navigate to editor

		// Assert: Tree view should be visible
		var treeView = page.GetByTestId("db-tree-view");
		await Expect(treeView).ToBeVisibleAsync();

		// Assert: At least one table should be visible
		var tableItems = page.Locator("[data-testid^='table-']");
		var count = await tableItems.CountAsync();
		Assert.True(count > 0, "Expected at least one table to be visible");
	}

	[Fact(Skip = "Requires SQLite database setup - see implementation notes")]
	public async Task DatabaseTreeView_ShowsColumns_WhenTableExpanded()
	{
		// IMPLEMENTATION NOTES:
		// This test builds on the previous one and additionally:
		// 1. Locates a specific table item by data-testid (e.g., "table-Customers")
		// 2. Clicks on the expand icon or the table item itself to trigger expansion
		// 3. Waits for column items to appear
		// 4. Verifies column items have correct data-testid format: "column-{tableName}-{columnName}"
		// 5. Checks that column display shows type information (e.g., "Id: int", "Name: nvarchar(50)?")
		//
		// Example assertions:
		// var customersTable = page.GetByTestId("table-Customers");
		// await customersTable.ClickAsync();
		// var idColumn = page.GetByTestId("column-Customers-Id");
		// await Expect(idColumn).ToBeVisibleAsync();
		// await Expect(idColumn).ToContainTextAsync("int");

		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// TODO: Setup as in previous test
		// TODO: Find and expand a table node

		// Assert: Column items should appear
		var columnItems = page.Locator("[data-testid^='column-']");
		var count = await columnItems.CountAsync();
		Assert.True(count > 0, "Expected at least one column to be visible after expanding table");
	}

	[Fact(Skip = "Requires SQLite database setup - see implementation notes")]
	public async Task DatabaseTreeView_RefreshButton_ReloadsTableList()
	{
		// IMPLEMENTATION NOTES:
		// This test verifies the refresh functionality:
		// 1. Setup database and open project (as in previous tests)
		// 2. Verify initial table list is loaded
		// 3. Optionally modify the database (add/remove table) - or just test reload behavior
		// 4. Click the refresh button (data-testid="db-tree-refresh")
		// 5. Verify loading indicator appears briefly
		// 6. Verify table list is reloaded (count or content matches expectations)
		//
		// Example assertions:
		// var refreshBtn = page.GetByTestId("db-tree-refresh");
		// await refreshBtn.ClickAsync();
		// var loadingIndicator = page.GetByTestId("db-tree-loading");
		// await Expect(loadingIndicator).ToBeVisibleAsync();
		// await Expect(loadingIndicator).Not.ToBeVisibleAsync(); // Wait for load to complete

		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// TODO: Setup as in previous tests
		// TODO: Click refresh button

		// Assert: Refresh should trigger reload
		var refreshBtn = page.GetByTestId("db-tree-refresh");
		await Expect(refreshBtn).ToBeVisibleAsync();
	}
}

using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using LinqStudio.Blazor.Constants;
using LinqStudio.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
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

		// The database tree view is visible in the sidebar on all pages.
		// With no connection configured, it should show a placeholder instead of the tree.
		var placeholder = page.GetByTestId("db-tree-placeholder");
		await Expect(placeholder).ToBeVisibleAsync();

		// Tree view should NOT be visible
		var treeView = page.GetByTestId("db-tree-view");
		await Expect(treeView).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ShowsConnectionNode_WhenSQLiteProjectOpened()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeConnTest");

			// Tree view div should be visible now that a project with a connection is open
			var treeView = page.GetByTestId("db-tree-view");
			await Expect(treeView).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Connection root node should be visible and show project name
			var connectionNode = page.GetByTestId("db-tree-connection");
			await Expect(connectionNode).ToBeVisibleAsync(new() { Timeout = 10_000 });
			// DisplayName = "{project.Name} ({DatabaseType})" → "DbTreeConnTest (Sqlite)"
			await Expect(connectionNode).ToContainTextAsync("DbTreeConnTest");

			// Placeholder should NOT be shown
			await Expect(page.GetByTestId("db-tree-placeholder")).Not.ToBeVisibleAsync();
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ShowsTableNodes_AfterExpandingTree()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeTablesTest");

			// Wait for tree view
			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Connection node visible
			var connectionNode = page.GetByTestId("db-tree-connection");
			await Expect(connectionNode).ToBeVisibleAsync(new() { Timeout = 10_000 });

			// Expand connection node via its expand button → tables folder becomes visible
			await ExpandTreeItemAsync(connectionNode);

			var tablesFolder = page.GetByTestId("db-tree-tables-folder");
			await Expect(tablesFolder).ToBeVisibleAsync(new() { Timeout = 10_000 });
			await Expect(tablesFolder).ToContainTextAsync("Tables");

			// Expand tables folder via its expand button → table nodes become visible
			await ExpandTreeItemAsync(tablesFolder);

			// SQLiteGenerator returns schema "main" → FullName = "main.Customers"
			var customersTable = page.GetByTestId("table-main.Customers");
			var ordersTable = page.GetByTestId("table-main.Orders");

			await Expect(customersTable).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(ordersTable).ToBeVisibleAsync(new() { Timeout = 5_000 });
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ShowsColumnNodes_AfterExpandingTableNode()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeColumnsTest");

			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Expand connection → tables folder
			await ExpandTreeItemAsync(page.GetByTestId("db-tree-connection"));
			var tablesFolder = page.GetByTestId("db-tree-tables-folder");
			await Expect(tablesFolder).ToBeVisibleAsync(new() { Timeout = 10_000 });

			// Expand tables folder → table nodes
			await ExpandTreeItemAsync(tablesFolder);
			var customersTable = page.GetByTestId("table-main.Customers");
			await Expect(customersTable).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Expand the Customers table → triggers lazy column load (ExpandedChanged callback)
			await ExpandTreeItemAsync(customersTable);

			// Column nodes should appear with correct testids
			// testid format: "column-{tableNode.TableName.FullName}-{colNode.ColumnDetail.Name}"
			var idColumn = page.GetByTestId("column-main.Customers-Id");
			var nameColumn = page.GetByTestId("column-main.Customers-Name");

			await Expect(idColumn).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(nameColumn).ToBeVisibleAsync(new() { Timeout = 5_000 });

			// Column type information should be shown (Name is TEXT NOT NULL → displays as "TEXT")
			await Expect(nameColumn).ToContainTextAsync("TEXT");
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ContextMenu_ShowsRefreshOnTablesFolder()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeCtxMenuTest1");

			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Expand connection node to make tables folder visible
			await ExpandTreeItemAsync(page.GetByTestId("db-tree-connection"));
			var tablesFolder = page.GetByTestId("db-tree-tables-folder");
			await Expect(tablesFolder).ToBeVisibleAsync(new() { Timeout = 10_000 });

			// Right-click the "Tables" label inside the tables folder BodyContent.
			// The @oncontextmenu handler is on a div wrapping the label text — right-clicking
			// the text element triggers contextmenu which bubbles up to that div.
			await tablesFolder.GetByText("Tables").ClickAsync(new() { Button = MouseButton.Right });

			// Context menu with Refresh option should appear
			var refreshItem = page.GetByTestId("db-tree-tables-folder-refresh");
			await Expect(refreshItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
			await Expect(refreshItem).ToContainTextAsync("Refresh");
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ContextMenu_ShowsRefreshOnTableNode()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeCtxMenuTest2");

			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Expand connection → tables folder → table nodes
			await ExpandTreeItemAsync(page.GetByTestId("db-tree-connection"));
			var tablesFolder = page.GetByTestId("db-tree-tables-folder");
			await Expect(tablesFolder).ToBeVisibleAsync(new() { Timeout = 10_000 });
			await ExpandTreeItemAsync(tablesFolder);

			var customersTable = page.GetByTestId("table-main.Customers");
			await Expect(customersTable).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Right-click on the table label text → triggers context menu
			await customersTable.GetByText("main.Customers").ClickAsync(new() { Button = MouseButton.Right });

			// Context menu with Refresh option for this specific table
			// testid: "db-tree-table-refresh-{table.FullName}" = "db-tree-table-refresh-main.Customers"
			var refreshItem = page.GetByTestId("db-tree-table-refresh-main.Customers");
			await Expect(refreshItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
			await Expect(refreshItem).ToContainTextAsync("Refresh");
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_TablesFolder_RefreshReloadsTableList()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeRefreshTest");

			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Expand connection → tables folder → table nodes
			await ExpandTreeItemAsync(page.GetByTestId("db-tree-connection"));
			var tablesFolder = page.GetByTestId("db-tree-tables-folder");
			await Expect(tablesFolder).ToBeVisibleAsync(new() { Timeout = 10_000 });
			await ExpandTreeItemAsync(tablesFolder);

			// Confirm initial tables loaded
			await Expect(page.GetByTestId("table-main.Customers")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Right-click tables folder and click Refresh
			await tablesFolder.GetByText("Tables").ClickAsync(new() { Button = MouseButton.Right });
			var refreshItem = page.GetByTestId("db-tree-tables-folder-refresh");
			await Expect(refreshItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
			await refreshItem.ClickAsync();

			// Context menu should close
			await Expect(refreshItem).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });

			// After refresh: tables folder children are cleared and reloaded.
			// Re-expand the tables folder to verify both tables are still present.
			await ExpandTreeItemAsync(tablesFolder);
			await Expect(page.GetByTestId("table-main.Customers")).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(page.GetByTestId("table-main.Orders")).ToBeVisibleAsync(new() { Timeout = 5_000 });
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	[Fact(Timeout = 90_000)]
	public async Task DatabaseTreeView_ConnectionContextMenu_NewQuery_OpensNewEditorTab()
	{
		Assert.NotNull(_pw.Browser);

		var dbPath = CreateTestSQLiteDatabase();
		try
		{
			await using var context = await _pw.Browser.NewContextAsync();
			var page = await context.NewPageAsync();

			await OpenSQLiteProjectAsync(page, dbPath, "DbTreeNewQueryTest");

			// Wait for tree view to be visible
			await Expect(page.GetByTestId("db-tree-view")).ToBeVisibleAsync(new() { Timeout = 15_000 });

			// Connection node should be visible
			var connectionNode = page.GetByTestId("db-tree-connection");
			await Expect(connectionNode).ToBeVisibleAsync(new() { Timeout = 10_000 });

			// Right-click the connection node BodyContent div to open the context menu
			var connectionBody = page.GetByTestId("db-tree-connection-body");
			await Expect(connectionBody).ToBeVisibleAsync(new() { Timeout = 10_000 });
			await connectionBody.ClickAsync(new() { Button = MouseButton.Right });

			// Context menu "New Query" item should appear
			var newQueryItem = page.GetByTestId("db-tree-connection-new-query");
			await Expect(newQueryItem).ToBeVisibleAsync(new() { Timeout = 5_000 });
			await Expect(newQueryItem).ToContainTextAsync("New Query");

			// Click "New Query"
			await newQueryItem.ClickAsync();

			// Should navigate to a new editor URL matching /editor/{guid}
			await page.WaitForURLAsync(
				new System.Text.RegularExpressions.Regex($@"^{_app.BaseUrl}editor/[0-9a-f-]{{36}}$"),
				new() { Timeout = 10_000 });

			// Editor page should be visible
			await Expect(page.GetByTestId("editor-page")).ToBeVisibleAsync();
		}
		finally
		{
			TryDeleteFile(dbPath);
		}
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	/// <summary>
	/// Creates a temporary SQLite database file with Customers and Orders tables.
	/// The caller is responsible for deleting the file after the test completes.
	/// </summary>
	private static string CreateTestSQLiteDatabase()
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"linqstudio_e2e_{Guid.NewGuid():N}.db");
		using var connection = new SqliteConnection($"Data Source={dbPath}");
		connection.Open();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = @"
			CREATE TABLE Customers (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				Name TEXT NOT NULL,
				Email TEXT
			);
			CREATE TABLE Orders (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				CustomerId INTEGER NOT NULL,
				Amount REAL
			);
		";
		cmd.ExecuteNonQuery();
		return dbPath;
	}

	/// <summary>
	/// Creates a project JSON file in the test directory with the given SQLite connection
	/// string, then opens it in the app via the project browser dialog.
	/// After this method returns, the current page is on the home page with the project open.
	/// </summary>
	private async Task OpenSQLiteProjectAsync(IPage page, string dbPath, string projectName)
	{
		var project = new Project
		{
			Name = projectName,
			DatabaseType = DatabaseType.Sqlite,
			ConnectionString = $"Data Source={dbPath}",
		};
		var projectJson = JsonSerializer.Serialize(project);
		_app.MockFileSystemService.CreateTestFile(
			$"{projectName}{FileExtensions.Project.WithDot()}", projectJson);

		// Navigate to home and open project via the project browser dialog
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for MudMenu to open
		await page.GetByTestId("nav-project-open").ClickAsync();

		var browserDialog = page.GetByTestId("project-browser-dialog");
		await Expect(browserDialog).ToBeVisibleAsync();

		var projectItem = page.GetByTestId("project-list-item")
			.Filter(new() { HasText = projectName });
		await Expect(projectItem).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await projectItem.ClickAsync();

		await page.GetByTestId("project-browser-open-btn").ClickAsync();

		// Verify project name appears in the nav
		await Expect(page.GetByTestId("nav-project")).ToContainTextAsync(projectName);
	}

	/// <summary>
	/// Expands a MudTreeViewItem by clicking its expand button (the chevron/arrow icon).
	/// This is more reliable than clicking the item body, which may have conflicting handlers.
	/// </summary>
	private static async Task ExpandTreeItemAsync(ILocator treeItem)
	{
		// MudBlazor renders the expand button as the first <button> inside the item content.
		// It has the CSS class "mud-treeview-item-expand-button".
		var expandBtn = treeItem.Locator("button").First;
		await expandBtn.ClickAsync();
		await Task.Delay(200); // brief pause for collapse/expand animation
	}

	/// <summary>
	/// Silently tries to delete a file, ignoring IOException if the file is still open
	/// (e.g., SQLite database still held by a live connection in the server process).
	/// </summary>
	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch (IOException)
		{
			// File still locked — SQLite connection is still open in the server process.
			// The OS will clean up temp files; test cleanup does not require deletion.
		}
	}
}


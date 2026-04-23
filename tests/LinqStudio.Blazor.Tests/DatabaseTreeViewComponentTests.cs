using Bunit;
using Xunit;
using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Components.Layout;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Models;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Reflection;

namespace LinqStudio.Blazor.Tests;

public class DatabaseTreeViewComponentTests : BunitContext
{
	private readonly string _testDirectory;

	public DatabaseTreeViewComponentTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioDbTreeTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
	}

	private void SetupServices()
	{
		Services
			.AddLinqStudio()
			.AddFileSystemRepositories(_testDirectory)
			.AddLinqStudioBlazor();

		Services.AddLogging();

		// Set JSInterop to Loose — MudBlazor tree view components make JS calls
		// (e.g. key interceptor setup). Loose mode accepts them without explicit configuration.
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	/// <summary>
	/// Uses reflection to inject a mock <see cref="IDatabaseQueryGenerator"/> directly into
	/// the <see cref="Project.QueryGenerator"/> backing field. This avoids needing a real
	/// database connection string while still satisfying the component's
	/// <c>QueryGenerator != null</c> guard.
	/// </summary>
	/// <remarks>
	/// C# 13's <c>field</c> keyword generates a backing field named
	/// <c>&lt;PropertyName&gt;k__BackingField</c> — the same convention as auto-properties.
	/// </remarks>
	private static void SetQueryGenerator(Project project, IDatabaseQueryGenerator generator)
	{
		var fieldInfo = typeof(Project).GetField(
			"<QueryGenerator>k__BackingField",
			BindingFlags.NonPublic | BindingFlags.Instance);

		Assert.True(fieldInfo != null,
			"Reflection could not find the QueryGenerator backing field on Project. " +
			"The field name may have changed — check the C# 13 'field' keyword backing field convention.");

		fieldInfo!.SetValue(project, generator);
	}

	/// <summary>
	/// Creates a <see cref="Mock{IDatabaseQueryGenerator}"/> where <c>GetTablesAsync</c>
	/// returns the supplied table list (by reference — mutate the list to change future returns).
	/// <c>CallBase = true</c> ensures that the default interface implementation of
	/// <c>GetTableAsync(DatabaseTableName, …)</c> delegates to the mocked
	/// <c>GetTableAsync(string, …)</c> overload rather than returning null.
	/// </summary>
	private static Mock<IDatabaseQueryGenerator> CreateMockGenerator(
		List<DatabaseTableName> tables)
	{
		var mock = new Mock<IDatabaseQueryGenerator> { CallBase = true };
		mock.Setup(g => g.GetTablesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => (IReadOnlyList<DatabaseTableName>)tables);
		return mock;
	}

	/// <summary>
	/// Creates a <see cref="DatabaseTableDetail"/> with the specified columns, for use
	/// when mocking <c>GetTableAsync</c>.
	/// </summary>
	private static DatabaseTableDetail MakeTableDetail(
		DatabaseTableName table, IEnumerable<TableColumn> columns) =>
		new()
		{
			Schema = table.Schema,
			Name = table.Name,
			Columns = columns.ToList(),
			ForeignKeys = [],
		};

	// ── Existing passing tests ───────────────────────────────────────────────

	[Fact]
	public void DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen()
	{
		// Arrange
		SetupServices();

		// Act - Render without opening a project
		var cut = Render<DatabaseTreeView>();

		// Assert - Placeholder should be visible
		var placeholder = cut.Find("[data-testid='db-tree-placeholder']");
		Assert.NotNull(placeholder);
		Assert.Contains("open a project", placeholder.TextContent, StringComparison.OrdinalIgnoreCase);

		// Tree view should NOT be present
		var treeView = cut.FindAll("[data-testid='db-tree-view']");
		Assert.Empty(treeView);
	}

	[Fact]
	public async Task DatabaseTreeView_ShowsPlaceholder_WhenProjectOpenButNoConnection()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();

		// Create project without connection string
		await workspace.CreateNewAsync("Test");

		// Act
		var cut = Render<DatabaseTreeView>();

		// Assert - Should show placeholder when no connection configured
		var placeholder = cut.Find("[data-testid='db-tree-placeholder']");
		Assert.NotNull(placeholder);
	}

	// ── New tests ────────────────────────────────────────────────────────────

	[Fact]
	public async Task DatabaseTreeView_ShowsConnectionRootNode_WhenProjectOpen()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var mockGen = CreateMockGenerator([]);
		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		// Act
		var cut = Render<DatabaseTreeView>();

		// Assert
		cut.WaitForAssertion(() =>
		{
			var connNode = cut.Find("[data-testid='db-tree-connection']");
			Assert.NotNull(connNode);
			Assert.Contains("MyApp", connNode.TextContent, StringComparison.OrdinalIgnoreCase);
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_ShowsTablesFolder_WhenProjectOpen()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var mockGen = CreateMockGenerator([]);
		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		// Act
		var cut = Render<DatabaseTreeView>();

		// Assert
		cut.WaitForAssertion(() =>
		{
			var folderNode = cut.Find("[data-testid='db-tree-tables-folder']");
			Assert.NotNull(folderNode);
			Assert.Contains("Tables", folderNode.TextContent, StringComparison.OrdinalIgnoreCase);
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_ShowsTableList_AfterTablesLoad()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var tables = new List<DatabaseTableName>
		{
			new() { Schema = "dbo", Name = "Users" },
			new() { Schema = "dbo", Name = "Orders" },
		};
		var mockGen = CreateMockGenerator(tables);
		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		// Act
		var cut = Render<DatabaseTreeView>();

		// Assert - table nodes with correct FullName testids
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Users']"));
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Orders']"));
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_ShowsTableName_WhenNoSchema()
	{
		// Arrange — table without a schema: FullName = "Users" (not "null.Users")
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var tables = new List<DatabaseTableName>
		{
			new() { Schema = null, Name = "Users" },
		};
		var mockGen = CreateMockGenerator(tables);
		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		// Act
		var cut = Render<DatabaseTreeView>();

		// Assert - data-testid uses FullName which is just "Users" when schema is null
		cut.WaitForAssertion(() =>
		{
			var tableNode = cut.Find("[data-testid='table-Users']");
			Assert.NotNull(tableNode);
			// Label text should be just "Users" without a schema prefix
			Assert.Contains("Users", tableNode.TextContent, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("null.", tableNode.TextContent, StringComparison.OrdinalIgnoreCase);
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_LoadsColumns_WhenTableExpanded()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var usersTable = new DatabaseTableName { Schema = "dbo", Name = "Users" };
		var mockGen = CreateMockGenerator([usersTable]);
		var columns = new List<TableColumn>
		{
			new()
			{
				Name = "Id",
				DataType = "int",
				GenericType = DbColumnType.Int32,
				IsNullable = false,
				IsPrimaryKey = true,
				IsIdentity = true,
			},
			new()
			{
				Name = "UserName",
				DataType = "nvarchar",
				GenericType = DbColumnType.String,
				IsNullable = true,
				IsPrimaryKey = false,
				IsIdentity = false,
				MaxLength = 100,
			},
		};
		mockGen.Setup(g => g.GetTableAsync("dbo.Users", It.IsAny<CancellationToken>()))
			.ReturnsAsync(MakeTableDetail(usersTable, columns));

		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		// Act
		var cut = Render<DatabaseTreeView>();

		// Wait for table list to load
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Users']"));
		}, TimeSpan.FromSeconds(3));

		// Simulate table expansion by calling RefreshTableNodeAsync on the table node.
		// This exercises the same column-loading code path that ExpandedChanged triggers.
		var tableNode = cut.Instance.TablesFolderNode?.Children.FirstOrDefault();
		Assert.NotNull(tableNode);
		await cut.InvokeAsync(() => cut.Instance.RefreshTableNodeAsync(tableNode!));

		// Assert - column nodes should appear
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='column-dbo.Users-Id']"));
			Assert.NotNull(cut.Find("[data-testid='column-dbo.Users-UserName']"));
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_ShowsColumnType_Correctly()
	{
		// Arrange
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var usersTable = new DatabaseTableName { Schema = "dbo", Name = "Users" };
		var mockGen = CreateMockGenerator([usersTable]);
		var columns = new List<TableColumn>
		{
			new()
			{
				Name = "Email",
				DataType = "nvarchar",
				GenericType = DbColumnType.String,
				IsNullable = true,
				IsPrimaryKey = false,
				IsIdentity = false,
				MaxLength = 255,
			},
		};
		mockGen.Setup(g => g.GetTableAsync("dbo.Users", It.IsAny<CancellationToken>()))
			.ReturnsAsync(MakeTableDetail(usersTable, columns));

		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		var cut = Render<DatabaseTreeView>();

		// Wait for table list to load
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Users']"));
		}, TimeSpan.FromSeconds(3));

		// Load columns by calling RefreshTableNodeAsync (same code path as expand event)
		var tableNode = cut.Instance.TablesFolderNode?.Children.FirstOrDefault();
		Assert.NotNull(tableNode);
		await cut.InvokeAsync(() => cut.Instance.RefreshTableNodeAsync(tableNode!));

		// Assert - column type display "nvarchar(255)?" is shown in the column node
		cut.WaitForAssertion(() =>
		{
			var colEl = cut.Find("[data-testid='column-dbo.Users-Email']");
			Assert.NotNull(colEl);
			Assert.Contains("nvarchar(255)?", colEl.TextContent, StringComparison.OrdinalIgnoreCase);
		}, TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task DatabaseTreeView_RefreshTablesFolder_ReloadsTableList()
	{
		// Arrange — initial state: one table
		SetupServices();
		var workspace = Services.GetRequiredService<ProjectWorkspace>();
		await workspace.CreateNewAsync("MyApp");

		var tables = new List<DatabaseTableName>
		{
			new() { Schema = "dbo", Name = "Users" },
		};
		var mockGen = CreateMockGenerator(tables);
		SetQueryGenerator(workspace.CurrentProject!, mockGen.Object);

		var cut = Render<DatabaseTreeView>();

		// Wait for initial load
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Users']"));
		}, TimeSpan.FromSeconds(3));

		// Verify Orders is not yet shown
		Assert.Empty(cut.FindAll("[data-testid='table-dbo.Orders']"));

		// Act — add a new table to the mock's return list and refresh
		tables.Add(new DatabaseTableName { Schema = "dbo", Name = "Orders" });
		await cut.InvokeAsync(cut.Instance.RefreshTablesFolderAsync);

		// Assert — both tables now visible
		cut.WaitForAssertion(() =>
		{
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Users']"));
			Assert.NotNull(cut.Find("[data-testid='table-dbo.Orders']"));
		}, TimeSpan.FromSeconds(3));
	}
}

using Bunit;
using Xunit;
using LinqStudio.Blazor.Components.Layout;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;
using LinqStudio.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

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
	}

	private ProjectWorkspace CreateMockWorkspaceWithProject(Mock<IDatabaseQueryGenerator> mockGenerator)
	{
		var projectService = new ProjectService();
		var queryService = new QueryService();
		var options = new FileSystemStorageOptions { BasePath = _testDirectory };
		var projectRepository = new FileSystemProjectRepository(projectService, options);
		var queryRepository = new FileSystemQueryRepository(queryService, options);
		var queriesWorkspace = new QueriesWorkspace(queryRepository, NullLogger<QueriesWorkspace>.Instance);
		var workspace = new ProjectWorkspace(projectRepository, queriesWorkspace, NullLogger<ProjectWorkspace>.Instance);
		return workspace;
	}

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

	// Note: The following tests require the actual DatabaseTreeView component to exist
	// and would test more advanced scenarios with mocked database connections.
	// Since the component doesn't exist yet, these serve as documentation
	// of what should be tested once the component is implemented:

	// TODO: Once DatabaseTreeView is implemented, add these tests:
	// - DatabaseTreeView_ShowsTableList_AfterTablesLoad()
	// - DatabaseTreeView_ShowsTableName_WhenNoSchema()
	// - DatabaseTreeView_LoadsColumns_WhenTableExpanded()
	// - DatabaseTreeView_ShowsColumnType_Correctly()
	// - DatabaseTreeView_RefreshButton_ReloadsTableList()
}

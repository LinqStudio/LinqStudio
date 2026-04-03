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
using MudBlazor;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Tests;

public class ConnectionTreeViewComponentTests : BunitContext, IAsyncLifetime
{
	private readonly string _testDirectory;

	public ConnectionTreeViewComponentTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioConnTreeTests_{Guid.NewGuid()}");
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

	[Fact]
	public void ConnectionTreeView_ShowsPlaceholder_WhenNoProjectOpen()
	{
		// Arrange
		SetupServices();

		// Act - Render without opening a project
		var cut = Render<ConnectionTreeView>();

		// Assert - Placeholder should be visible
		var placeholder = cut.Find("[data-testid='db-tree-placeholder']");
		Assert.NotNull(placeholder);
		Assert.Contains("open a project", placeholder.TextContent, StringComparison.OrdinalIgnoreCase);

		// Connection tree container should be present
		var container = cut.Find("[data-testid='connection-tree-view-container']");
		Assert.NotNull(container);
	}

	[Fact]
	public async Task ConnectionTreeView_ShowsNoConnectionsPlaceholder_WhenProjectOpenButNoConnections()
	{
		// Arrange
		SetupServices();
		JSInterop.Mode = JSRuntimeMode.Loose;
		var workspace = Services.GetRequiredService<ProjectWorkspace>();

		// Create project without connections
		await workspace.CreateNewAsync("Test");

		// Render the popover provider first so MudMenu works
		Render<MudPopoverProvider>();

		// Act
		var cut = Render<ConnectionTreeView>();

		// Assert - Should show "no connections" placeholder when no connections configured
		var placeholder = cut.Find("[data-testid='db-tree-placeholder']");
		Assert.NotNull(placeholder);
	}

	// IAsyncLifetime - required to properly dispose async MudBlazor services
	Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
	async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

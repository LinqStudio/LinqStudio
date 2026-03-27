using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinqStudio.Blazor.Tests.Services;

public class QueriesWorkspaceTests : IDisposable
{
	private readonly QueryService _queryService;
	private readonly QueriesWorkspace _workspace;
	private readonly string _testDirectory;
	private readonly string _testProjectFilePath;

	public QueriesWorkspaceTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
		_testProjectFilePath = Path.Combine(_testDirectory, "TestProject.linq");

		_queryService = new QueryService();
		_workspace = new QueriesWorkspace(_queryService, NullLogger<QueriesWorkspace>.Instance);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			Directory.Delete(_testDirectory, recursive: true);
		}
	}

	#region Initialize Tests

	[Fact]
	public async Task InitializeAsync_WithNoQueries_SetsCurrentIndexToNull()
	{
		// Act
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Assert
		Assert.Null(_workspace.CurrentQueryId);
		Assert.Empty(_workspace.OpenQueries);
		Assert.Empty(_workspace.AllQueries);
	}

	[Fact]
	public async Task InitializeAsync_WithQueries_OpensFirstQuery()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(_testProjectFilePath, q1);

		// Act
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Assert
		Assert.Equal(q1.Id, _workspace.CurrentQueryId);
		Assert.Single(_workspace.OpenQueries);
		Assert.Single(_workspace.AllQueries);
	}

	#endregion

	#region OpenQuery Tests

	[Fact]
	public async Task OpenQuery_OpensQueryAndSetsAsCurrent()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "context.Orders", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(_testProjectFilePath, q1);
		await _queryService.SaveQueryAsync(_testProjectFilePath, q2);
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Act
		_workspace.OpenQuery(q2.Id);

		// Assert
		Assert.Equal(q2.Id, _workspace.CurrentQueryId);
		Assert.True(_workspace.OpenQueries.ContainsKey(q2.Id));
	}

	#endregion

	#region CreateNewQuery Tests

	[Fact]
	public async Task CreateNewQuery_CreatesAndOpensQuery()
	{
		// Arrange
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Act
		var newId = _workspace.CreateNewQuery();

		// Assert
		Assert.Single(_workspace.AllQueries);
		Assert.Equal("Query", _workspace.AllQueries[0].Name);
		Assert.Equal(newId, _workspace.CurrentQueryId);
	}

	#endregion

	#region UpdateQueryText Tests

	[Fact]
	public async Task UpdateQueryText_UpdatesCurrentText()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(_testProjectFilePath, q1);
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Act
		_workspace.UpdateQueryText(q1.Id, "updated");

		// Assert
		Assert.Equal("updated", _workspace.CurrentQueryState!.CurrentText);
		Assert.True(_workspace.CurrentQueryState.HasUnsavedChanges);
	}

	#endregion

	#region SaveQuery Tests

	[Fact]
	public async Task SaveQueryAsync_SavesQueryToDisk()
	{
		// Arrange
		await _workspace.InitializeAsync(_testProjectFilePath);
		var newId = _workspace.CreateNewQuery();
		_workspace.UpdateQueryText(newId, "new query text");

		// Act
		await _workspace.SaveQueryAsync(newId);

		// Assert - reload and verify
		var reloaded = await _queryService.LoadQueriesAsync(_testProjectFilePath);
		Assert.Single(reloaded);
		Assert.Equal("new query text", reloaded[0].QueryText);
	}

	#endregion

	#region RenameQuery Tests

	[Fact]
	public async Task RenameQuery_UpdatesQueryName()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(_testProjectFilePath, q1);
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Act
		_workspace.RenameQuery(q1.Id, "NewName");

		// Assert
		Assert.Equal("NewName", _workspace.GetCurrentQuery()!.Name);
	}

	#endregion

	#region DeleteQuery Tests

	[Fact]
	public async Task DeleteQueryAsync_RemovesQueryFromWorkspace()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(_testProjectFilePath, q1);
		await _workspace.InitializeAsync(_testProjectFilePath);

		// Act
		await _workspace.DeleteQueryAsync(q1.Id);

		// Assert
		Assert.Empty(_workspace.AllQueries);
		Assert.Null(_workspace.CurrentQueryId);
	}

	#endregion
}

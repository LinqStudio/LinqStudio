using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests.Repositories;

public class FileSystemQueryRepositoryTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly FileSystemQueryRepository _repository;
	private readonly QueryService _queryService;

	public FileSystemQueryRepositoryTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioQueryRepoTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
		_queryService = new QueryService();
		_repository = new FileSystemQueryRepository(
			_queryService,
			new FileSystemStorageOptions { BasePath = _testDirectory });
	}

	#region LoadQueriesAsync

	[Fact]
	public async Task LoadQueriesAsync_ReturnsEmpty_WhenQueriesDirDoesNotExist()
	{
		// Act
		var queries = await _repository.LoadQueriesAsync("NonExistentProject");

		// Assert
		Assert.Empty(queries);
	}

	[Fact]
	public async Task LoadQueriesAsync_ReturnsAllSavedQueries()
	{
		// Arrange
		var query1 = new SavedQuery { Name = "Q1", QueryText = "context.People" };
		var query2 = new SavedQuery { Name = "Q2", QueryText = "context.Orders" };
		await _repository.SaveQueryAsync("MyProject", query1);
		await _repository.SaveQueryAsync("MyProject", query2);

		// Act
		var queries = await _repository.LoadQueriesAsync("MyProject");

		// Assert
		Assert.Equal(2, queries.Count);
		Assert.Contains(queries, q => q.Id == query1.Id);
		Assert.Contains(queries, q => q.Id == query2.Id);
	}

	#endregion

	#region SaveQueryAsync

	[Fact]
	public async Task SaveQueryAsync_CreatesQueryFile()
	{
		// Arrange
		var query = new SavedQuery { Name = "TestQuery", QueryText = "context.Users" };

		// Act
		await _repository.SaveQueryAsync("ProjectA", query);

		// Assert
		var queriesDir = Path.Combine(_testDirectory, "ProjectA.linq.queries");
		Assert.True(Directory.Exists(queriesDir));
		var files = Directory.GetFiles(queriesDir, "*.linq.query");
		Assert.Single(files);
	}

	[Fact]
	public async Task SaveQueryAsync_CanRoundtripQuery()
	{
		// Arrange
		var query = new SavedQuery
		{
			Name = "Roundtrip",
			QueryText = "context.People.Where(p => p.IsActive)"
		};

		// Act
		await _repository.SaveQueryAsync("ProjectB", query);
		var queries = await _repository.LoadQueriesAsync("ProjectB");

		// Assert
		Assert.Single(queries);
		Assert.Equal(query.Id, queries[0].Id);
		Assert.Equal("Roundtrip", queries[0].Name);
		Assert.Equal("context.People.Where(p => p.IsActive)", queries[0].QueryText);
	}

	#endregion

	#region DeleteQueryAsync

	[Fact]
	public async Task DeleteQueryAsync_RemovesQueryFile()
	{
		// Arrange
		var query = new SavedQuery { Name = "DeleteMe", QueryText = "context.Users" };
		await _repository.SaveQueryAsync("ProjectC", query);

		// Act
		await _repository.DeleteQueryAsync("ProjectC", query.Id);

		// Assert
		var queries = await _repository.LoadQueriesAsync("ProjectC");
		Assert.Empty(queries);
	}

	[Fact]
	public async Task DeleteQueryAsync_DoesNotThrow_WhenQueryNotExists()
	{
		// Act & Assert — should not throw
		await _repository.DeleteQueryAsync("ProjectD", Guid.NewGuid());
	}

	[Fact]
	public async Task DeleteQueryAsync_OnlyRemovesSpecifiedQuery()
	{
		// Arrange
		var query1 = new SavedQuery { Name = "Keep", QueryText = "context.A" };
		var query2 = new SavedQuery { Name = "Remove", QueryText = "context.B" };
		await _repository.SaveQueryAsync("ProjectE", query1);
		await _repository.SaveQueryAsync("ProjectE", query2);

		// Act
		await _repository.DeleteQueryAsync("ProjectE", query2.Id);

		// Assert
		var queries = await _repository.LoadQueriesAsync("ProjectE");
		Assert.Single(queries);
		Assert.Equal(query1.Id, queries[0].Id);
	}

	#endregion

	#region IDisposable

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			try { Directory.Delete(_testDirectory, recursive: true); }
			catch { /* ignore cleanup errors */ }
		}
	}

	#endregion
}

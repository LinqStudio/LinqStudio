using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using System.Text.Json;

namespace LinqStudio.Core.Tests;

public class QueryServiceTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly QueryService _service;
	private readonly string _testProjectPath;

	public QueryServiceTests()
	{
		// Create unique test directory for each test run
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		_service = new QueryService();
		_testProjectPath = Path.Combine(_testDirectory, "TestProject.linq");
	}

	#region GetQueriesDirectory Tests

	[Fact]
	public void GetQueriesDirectory_ReturnsCorrectPath()
	{
		// Arrange
		var projectPath = @"C:\Projects\MyProject.linq";

		// Act
		var queriesDir = _service.GetQueriesDirectory(projectPath);

		// Assert
		Assert.Equal(@"C:\Projects\MyProject.linq.queries", queriesDir);
	}

	[Fact]
	public void GetQueriesDirectory_ThrowsException_WhenPathIsNull()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.GetQueriesDirectory(null!));
	}

	[Fact]
	public void GetQueriesDirectory_ThrowsException_WhenPathIsEmpty()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.GetQueriesDirectory(string.Empty));
	}

	#endregion

	#region GetQueryFilePath Tests

	[Fact]
	public void GetQueryFilePath_ReturnsCorrectPath()
	{
		// Arrange
		var projectPath = @"C:\Projects\MyProject.linq";
		var queryId = Guid.NewGuid();

		// Act
		var queryPath = _service.GetQueryFilePath(projectPath, queryId);

		// Assert
		Assert.Equal($@"C:\Projects\MyProject.linq.queries\{queryId}.linq.query", queryPath);
	}

	[Fact]
	public void GetQueryFilePath_HandlesEmptyGuid()
	{
		// Arrange
		var projectPath = @"C:\Projects\MyProject.linq";

		// Act
		var queryPath = _service.GetQueryFilePath(projectPath, Guid.Empty);

		// Assert
		Assert.Contains("00000000-0000-0000-0000-000000000000", queryPath);
	}

	#endregion

	#region SaveQueryAsync Tests

	[Fact]
	public async Task SaveQueryAsync_CreatesQueryFile()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test Query",
			QueryText = "context.People.ToList()",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query.Id);
		Assert.True(File.Exists(queryPath));
	}

	[Fact]
	public async Task SaveQueryAsync_CreatesQueriesDirectory_IfNotExists()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test Query",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var queriesDir = _service.GetQueriesDirectory(_testProjectPath);
		Assert.True(Directory.Exists(queriesDir));
	}

	[Fact]
	public async Task SaveQueryAsync_SavesCorrectContent()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "My Query",
			QueryText = "context.Users.Where(u => u.IsActive)",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query.Id);
		var loaded = await _service.LoadQueryFromFileAsync(queryPath);
		Assert.NotNull(loaded);
		Assert.Equal("My Query", loaded.Name);
		Assert.Equal("context.Users.Where(u => u.IsActive)", loaded.QueryText);
	}

	[Fact]
	public async Task SaveQueryAsync_OverwritesExistingQuery()
	{
		// Arrange
		var query1 = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Original",
			QueryText = "Original Query",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query1);

		var query2 = new SavedQuery
		{
			Id = query1.Id, // Same ID
			Name = "Updated",
			QueryText = "Updated Query",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query2);

		// Assert
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query1.Id);
		var content = await File.ReadAllTextAsync(queryPath);
		Assert.Contains("Updated", content);
		Assert.DoesNotContain("Original", content);
	}

	[Fact]
	public async Task SaveQueryAsync_ThrowsException_WhenQueryIdIsEmpty()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.Empty,
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.SaveQueryAsync(_testProjectPath, query)
		);
	}

	[Fact]
	public async Task SaveQueryAsync_ThrowsException_WhenQueryIsNull()
	{
		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(
			() => _service.SaveQueryAsync(_testProjectPath, null!)
		);
	}

	[Fact]
	public async Task SaveQueryAsync_HandlesSpecialCharacters()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test \"Query\" with 'quotes'",
			QueryText = "context.People.Where(p => p.Name == \"John\")",
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var loaded = await _service.LoadQueryFromFileAsync(_service.GetQueryFilePath(_testProjectPath, query.Id));
		Assert.NotNull(loaded);
		Assert.Equal(query.Name, loaded.Name);
		Assert.Equal(query.QueryText, loaded.QueryText);
	}

	[Fact]
	public async Task SaveQueryAsync_HandlesEmptyQueryText()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Empty Query",
			QueryText = string.Empty,
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var loaded = await _service.LoadQueryFromFileAsync(_service.GetQueryFilePath(_testProjectPath, query.Id));
		Assert.NotNull(loaded);
		Assert.Equal(string.Empty, loaded.QueryText);
	}

	[Fact]
	public async Task SaveQueryAsync_PreservesAllProperties()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Full Query",
			QueryText = "context.People.ToList()",
			CreatedDate = DateTimeOffset.UtcNow.AddDays(-1)
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var loaded = await _service.LoadQueryFromFileAsync(_service.GetQueryFilePath(_testProjectPath, query.Id));
		Assert.NotNull(loaded);
		Assert.Equal(query.Id, loaded.Id);
		Assert.Equal(query.Name, loaded.Name);
		Assert.Equal(query.QueryText, loaded.QueryText);
		Assert.Equal(query.CreatedDate, loaded.CreatedDate);
	}

	#endregion

	#region LoadQueriesAsync Tests

	[Fact]
	public async Task LoadQueriesAsync_ReturnsEmptyList_WhenDirectoryNotExists()
	{
		// Act
		var queries = await _service.LoadQueriesAsync(_testProjectPath);

		// Assert
		Assert.NotNull(queries);
		Assert.Empty(queries);
	}

	[Fact]
	public async Task LoadQueriesAsync_ReturnsEmptyList_WhenNoQueryFiles()
	{
		// Arrange
		var queriesDir = _service.GetQueriesDirectory(_testProjectPath);
		Directory.CreateDirectory(queriesDir);

		// Act
		var queries = await _service.LoadQueriesAsync(_testProjectPath);

		// Assert
		Assert.NotNull(queries);
		Assert.Empty(queries);
	}

	[Fact]
	public async Task LoadQueriesAsync_LoadsAllQueries()
	{
		// Arrange
		var query1 = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Query 1",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		var query2 = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Query 2",
			QueryText = "context.Orders",
			CreatedDate = DateTimeOffset.UtcNow
		};

		await _service.SaveQueryAsync(_testProjectPath, query1);
		await _service.SaveQueryAsync(_testProjectPath, query2);

		// Act
		var queries = await _service.LoadQueriesAsync(_testProjectPath);

		// Assert
		Assert.Equal(2, queries.Count);
		Assert.Contains(queries, q => q.Id == query1.Id);
		Assert.Contains(queries, q => q.Id == query2.Id);
	}

	[Fact]
	public async Task LoadQueriesAsync_SkipsCorruptedFiles_AndContinuesLoading()
	{
		// Arrange
		var validQuery = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Valid Query",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, validQuery);

		// Create corrupted file
		var queriesDir = _service.GetQueriesDirectory(_testProjectPath);
		var corruptedPath = Path.Combine(queriesDir, $"{Guid.NewGuid()}.linq.query");
		await File.WriteAllTextAsync(corruptedPath, "{ corrupted json !!!");

		// Act
		var queries = await _service.LoadQueriesAsync(_testProjectPath);

		// Assert - Should load the valid query and skip the corrupted one
		Assert.Single(queries);
		Assert.Equal(validQuery.Id, queries[0].Id);
	}

	#endregion

	#region LoadQueryFromFileAsync Tests

	[Fact]
	public async Task LoadQueryFromFileAsync_ReturnsNull_WhenFileNotExists()
	{
		// Arrange
		var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.linq.query");

		// Act
		var query = await _service.LoadQueryFromFileAsync(nonExistentPath);

		// Assert
		Assert.Null(query);
	}

	[Fact]
	public async Task LoadQueryFromFileAsync_LoadsQuery()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test Query",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query);
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query.Id);

		// Act
		var loaded = await _service.LoadQueryFromFileAsync(queryPath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(query.Id, loaded.Id);
		Assert.Equal(query.Name, loaded.Name);
		Assert.Equal(query.QueryText, loaded.QueryText);
	}

	[Fact]
	public async Task LoadQueryFromFileAsync_ReturnsNull_WhenFileIsCorrupted()
	{
		// Arrange
		var corruptedPath = Path.Combine(_testDirectory, "corrupted.linq.query");
		await File.WriteAllTextAsync(corruptedPath, "{ invalid json !!!");

		// Act
		var query = await _service.LoadQueryFromFileAsync(corruptedPath);

		// Assert
		Assert.Null(query);
	}

	[Fact]
	public async Task LoadQueryFromFileAsync_SetsFilePath()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query);
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query.Id);

		// Act
		var loaded = await _service.LoadQueryFromFileAsync(queryPath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(queryPath, loaded.FilePath);
	}

	#endregion

	#region SaveQueryToFileAsync Tests

	[Fact]
	public async Task SaveQueryToFileAsync_CreatesFile()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Standalone Query",
			QueryText = "context.Orders",
			CreatedDate = DateTimeOffset.UtcNow
		};
		var filePath = Path.Combine(_testDirectory, "standalone.linq.query");

		// Act
		await _service.SaveQueryToFileAsync(filePath, query);

		// Assert
		Assert.True(File.Exists(filePath));
	}

	[Fact]
	public async Task SaveQueryToFileAsync_CreatesDirectory_IfNotExists()
	{
		// Arrange
		var subDir = Path.Combine(_testDirectory, "subdir");
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		var filePath = Path.Combine(subDir, "query.linq.query");

		// Act
		await _service.SaveQueryToFileAsync(filePath, query);

		// Assert
		Assert.True(Directory.Exists(subDir));
		Assert.True(File.Exists(filePath));
	}

	[Fact]
	public async Task SaveQueryToFileAsync_SetsFilePath()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		var filePath = Path.Combine(_testDirectory, "test.linq.query");

		// Act
		await _service.SaveQueryToFileAsync(filePath, query);

		// Assert
		Assert.Equal(filePath, query.FilePath);
	}

	[Fact]
	public async Task SaveQueryToFileAsync_ThrowsException_WhenQueryIdIsEmpty()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.Empty,
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		var filePath = Path.Combine(_testDirectory, "test.linq.query");

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.SaveQueryToFileAsync(filePath, query)
		);
	}

	#endregion

	#region DeleteQuery Tests

	[Fact]
	public async Task DeleteQuery_RemovesQueryFile()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "To Delete",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query);
		var queryPath = _service.GetQueryFilePath(_testProjectPath, query.Id);

		// Act
		_service.DeleteQuery(_testProjectPath, query.Id);

		// Assert
		Assert.False(File.Exists(queryPath));
	}

	[Fact]
	public void DeleteQuery_DoesNotThrow_WhenFileNotExists()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid();

		// Act & Assert - Should not throw
		_service.DeleteQuery(_testProjectPath, nonExistentId);
	}

	#endregion

	#region DeleteAllQueries Tests

	[Fact]
	public async Task DeleteAllQueries_RemovesQueriesDirectory()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query);
		var queriesDir = _service.GetQueriesDirectory(_testProjectPath);

		// Act
		_service.DeleteAllQueries(_testProjectPath);

		// Assert
		Assert.False(Directory.Exists(queriesDir));
	}

	[Fact]
	public void DeleteAllQueries_DoesNotThrow_WhenDirectoryNotExists()
	{
		// Act & Assert - Should not throw
		_service.DeleteAllQueries(_testProjectPath);
	}

	#endregion

	#region QueryExists Tests

	[Fact]
	public async Task QueryExists_ReturnsTrue_WhenFileExists()
	{
		// Arrange
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			QueryText = "context.People",
			CreatedDate = DateTimeOffset.UtcNow
		};
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Act
		var exists = _service.QueryExists(_testProjectPath, query.Id);

		// Assert
		Assert.True(exists);
	}

	[Fact]
	public void QueryExists_ReturnsFalse_WhenFileNotExists()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid();

		// Act
		var exists = _service.QueryExists(_testProjectPath, nonExistentId);

		// Assert
		Assert.False(exists);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task SaveQueryAsync_HandlesConcurrentCalls()
	{
		// Arrange
		var tasks = new List<Task>();

		// Act - Save multiple queries concurrently
		for (int i = 0; i < 10; i++)
		{
			var index = i;
			tasks.Add(Task.Run(async () =>
			{
				var query = new SavedQuery
				{
					Id = Guid.NewGuid(),
					Name = $"Concurrent {index}",
					QueryText = $"context.Table{index}",
					CreatedDate = DateTimeOffset.UtcNow
				};
				await _service.SaveQueryAsync(_testProjectPath, query);
			}));
		}

		await Task.WhenAll(tasks);

		// Assert - All queries should be saved
		var queries = await _service.LoadQueriesAsync(_testProjectPath);
		Assert.Equal(10, queries.Count);
	}

	[Fact]
	public async Task SaveQueryAsync_HandlesVeryLongQueryText()
	{
		// Arrange
		var longQueryText = new string('A', 100000); // 100KB of text
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Long Query",
			QueryText = longQueryText,
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var loaded = await _service.LoadQueryFromFileAsync(_service.GetQueryFilePath(_testProjectPath, query.Id));
		Assert.NotNull(loaded);
		Assert.Equal(longQueryText, loaded.QueryText);
	}

	[Fact]
	public async Task SaveQueryAsync_HandlesMultilineQueryText()
	{
		// Arrange
		var multilineQuery = """
		context.People
			.Where(p => p.IsActive)
			.OrderBy(p => p.Name)
			.ToList()
		""";
		var query = new SavedQuery
		{
			Id = Guid.NewGuid(),
			Name = "Multiline",
			QueryText = multilineQuery,
			CreatedDate = DateTimeOffset.UtcNow
		};

		// Act
		await _service.SaveQueryAsync(_testProjectPath, query);

		// Assert
		var loaded = await _service.LoadQueryFromFileAsync(_service.GetQueryFilePath(_testProjectPath, query.Id));
		Assert.NotNull(loaded);
		Assert.Equal(multilineQuery, loaded.QueryText);
	}

	#endregion

	#region IDisposable Implementation

	public void Dispose()
	{
		// Cleanup test directory
		if (Directory.Exists(_testDirectory))
		{
			try
			{
				Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}

	#endregion
}

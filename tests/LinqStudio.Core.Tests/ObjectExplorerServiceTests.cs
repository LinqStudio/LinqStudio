using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Services;
using Xunit;

namespace LinqStudio.Core.Tests;

public class ObjectExplorerServiceTests
{
	[Fact]
	public void AddConnection_AddsConnectionToList()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();

		// Act
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);

		// Assert
		Assert.Single(service.Connections);
		Assert.Equal("Test Connection", service.Connections[0].Name);
		Assert.Equal(DatabaseType.Mssql, service.Connections[0].DatabaseType);
		Assert.Equal("test-connection-string", service.Connections[0].ConnectionString);
		Assert.Same(fakeGenerator, service.Connections[0].QueryGenerator);
	}

	[Fact]
	public void AddConnection_RaisesConnectionsChangedEvent()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var eventRaised = false;
		service.ConnectionsChanged += () => eventRaised = true;

		// Act
		service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public void RemoveConnection_RemovesConnectionFromList()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);

		// Act
		service.RemoveConnection(connection.Id);

		// Assert
		Assert.Empty(service.Connections);
	}

	[Fact]
	public void RemoveConnection_RaisesConnectionsChangedEvent()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);
		var eventRaised = false;
		service.ConnectionsChanged += () => eventRaised = true;

		// Act
		service.RemoveConnection(connection.Id);

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public async Task GetTablesAsync_CachesResult()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" },
			new() { Name = "Table2", Schema = "dbo" }
		};
		fakeGenerator.Tables = tables;

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);

		// Act
		var result1 = await service.GetTablesAsync(connection);
		var result2 = await service.GetTablesAsync(connection);

		// Assert
		Assert.Same(result1, result2); // Should return the same cached instance
		Assert.Equal(1, fakeGenerator.GetTablesCallCount); // Should only call once
	}

	[Fact]
	public async Task GetTableDetailAsync_CachesResult()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var table = new DatabaseTableName { Name = "Table1", Schema = "dbo" };
		var detail = new DatabaseTableDetail
		{
			Name = "Table1",
			Schema = "dbo",
			Columns = new List<TableColumn>(),
			ForeignKeys = new List<ForeignKey>()
		};
		fakeGenerator.TableDetails[table.FullName] = detail;

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);

		// Act
		var result1 = await service.GetTableDetailAsync(connection, table);
		var result2 = await service.GetTableDetailAsync(connection, table);

		// Assert
		Assert.Same(result1, result2); // Should return the same cached instance
		Assert.Equal(1, fakeGenerator.GetTableCallCount); // Should only call once
	}

	[Fact]
	public async Task RefreshConnectionAsync_ClearsCache()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator = new FakeDatabaseQueryGenerator();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" }
		};
		fakeGenerator.Tables = tables;

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", fakeGenerator);
		
		// Cache the tables
		await service.GetTablesAsync(connection);

		// Act
		await service.RefreshConnectionAsync(connection);

		// Assert
		Assert.Equal(2, fakeGenerator.GetTablesCallCount); // Should call twice (initial + after refresh)
	}

	[Fact]
	public async Task RefreshAllAsync_ClearsAllCaches()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var fakeGenerator1 = new FakeDatabaseQueryGenerator();
		var fakeGenerator2 = new FakeDatabaseQueryGenerator();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" }
		};
		fakeGenerator1.Tables = tables;
		fakeGenerator2.Tables = tables;

		var connection1 = service.AddConnection("Connection 1", DatabaseType.Mssql, "test-connection-string-1", fakeGenerator1);
		var connection2 = service.AddConnection("Connection 2", DatabaseType.Mssql, "test-connection-string-2", fakeGenerator2);
		
		// Cache the tables for both
		await service.GetTablesAsync(connection1);
		await service.GetTablesAsync(connection2);

		// Act
		await service.RefreshAllAsync();

		// Assert
		Assert.Equal(2, fakeGenerator1.GetTablesCallCount); // Initial + refresh
		Assert.Equal(2, fakeGenerator2.GetTablesCallCount); // Initial + refresh
	}

	/// <summary>
	/// Fake implementation of IDatabaseQueryGenerator for testing.
	/// </summary>
	private class FakeDatabaseQueryGenerator : IDatabaseQueryGenerator
	{
		public List<DatabaseTableName> Tables { get; set; } = new();
		public Dictionary<string, DatabaseTableDetail> TableDetails { get; set; } = new();
		public int GetTablesCallCount { get; private set; }
		public int GetTableCallCount { get; private set; }

		public Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken cancellationToken = default)
		{
			GetTablesCallCount++;
			return Task.FromResult<IReadOnlyList<DatabaseTableName>>(Tables);
		}

		public Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
		{
			GetTableCallCount++;
			return Task.FromResult(TableDetails[tableName]);
		}

		public Task TestConnectionAsync(CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}
	}
}

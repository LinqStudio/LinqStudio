using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Services;
using Moq;
using Xunit;

namespace LinqStudio.Core.Tests;

public class ObjectExplorerServiceTests
{
	[Fact]
	public void AddConnection_AddsConnectionToList()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();

		// Act
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);

		// Assert
		Assert.Single(service.Connections);
		Assert.Equal("Test Connection", service.Connections[0].Name);
		Assert.Equal(DatabaseType.Mssql, service.Connections[0].DatabaseType);
		Assert.Equal("test-connection-string", service.Connections[0].ConnectionString);
		Assert.Same(mockGenerator.Object, service.Connections[0].QueryGenerator);
	}

	[Fact]
	public void AddConnection_RaisesConnectionsChangedEvent()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var eventRaised = false;
		service.ConnectionsChanged += () => eventRaised = true;

		// Act
		service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public void RemoveConnection_RemovesConnectionFromList()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);

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
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);
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
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" },
			new() { Name = "Table2", Schema = "dbo" }
		};
		mockGenerator.Setup(g => g.GetTablesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(tables);

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);

		// Act
		var result1 = await service.GetTablesAsync(connection);
		var result2 = await service.GetTablesAsync(connection);

		// Assert
		Assert.Same(result1, result2); // Should return the same cached instance
		mockGenerator.Verify(g => g.GetTablesAsync(It.IsAny<CancellationToken>()), Times.Once); // Should only call once
	}

	[Fact]
	public async Task GetTableDetailAsync_CachesResult()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var table = new DatabaseTableName { Name = "Table1", Schema = "dbo" };
		var detail = new DatabaseTableDetail
		{
			Name = "Table1",
			Schema = "dbo",
			Columns = new List<TableColumn>(),
			ForeignKeys = new List<ForeignKey>()
		};
		mockGenerator.Setup(g => g.GetTableAsync(It.IsAny<DatabaseTableName>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(detail);

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);

		// Act
		var result1 = await service.GetTableDetailAsync(connection, table);
		var result2 = await service.GetTableDetailAsync(connection, table);

		// Assert
		Assert.Same(result1, result2); // Should return the same cached instance
		mockGenerator.Verify(g => g.GetTableAsync(It.IsAny<DatabaseTableName>(), It.IsAny<CancellationToken>()), Times.Once); // Should only call once
	}

	[Fact]
	public async Task RefreshConnectionAsync_ClearsCache()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator = new Mock<IDatabaseQueryGenerator>();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" }
		};
		mockGenerator.Setup(g => g.GetTablesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(tables);

		var connection = service.AddConnection("Test Connection", DatabaseType.Mssql, "test-connection-string", mockGenerator.Object);
		
		// Cache the tables
		await service.GetTablesAsync(connection);

		// Act
		await service.RefreshConnectionAsync(connection);

		// Assert
		mockGenerator.Verify(g => g.GetTablesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); // Should call twice (initial + after refresh)
	}

	[Fact]
	public async Task RefreshAllAsync_ClearsAllCaches()
	{
		// Arrange
		var service = new ObjectExplorerService();
		var mockGenerator1 = new Mock<IDatabaseQueryGenerator>();
		var mockGenerator2 = new Mock<IDatabaseQueryGenerator>();
		var tables = new List<DatabaseTableName>
		{
			new() { Name = "Table1", Schema = "dbo" }
		};
		mockGenerator1.Setup(g => g.GetTablesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(tables);
		mockGenerator2.Setup(g => g.GetTablesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(tables);

		var connection1 = service.AddConnection("Connection 1", DatabaseType.Mssql, "test-connection-string-1", mockGenerator1.Object);
		var connection2 = service.AddConnection("Connection 2", DatabaseType.Mssql, "test-connection-string-2", mockGenerator2.Object);
		
		// Cache the tables for both
		await service.GetTablesAsync(connection1);
		await service.GetTablesAsync(connection2);

		// Act
		await service.RefreshAllAsync();

		// Assert
		mockGenerator1.Verify(g => g.GetTablesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); // Initial + refresh
		mockGenerator2.Verify(g => g.GetTablesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); // Initial + refresh
	}
}

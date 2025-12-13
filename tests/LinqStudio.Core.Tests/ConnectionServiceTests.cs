using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Services;
using Xunit;

namespace LinqStudio.Core.Tests;

public class ConnectionServiceTests
{
	[Fact]
	public void ConnectionService_InitialState_HasNullConnectionString()
	{
		// Arrange & Act
		var service = new ConnectionService();

		// Assert
		Assert.Null(service.ConnectionString);
		Assert.Null(service.QueryGenerator);
	}

	[Fact]
	public void UpdateConnection_WithMssql_SetsConnectionStringAndGenerator()
	{
		// Arrange
		var service = new ConnectionService();
		var connectionString = "Server=localhost;Database=Test;Trusted_Connection=True;";

		// Act
		service.UpdateConnection(DatabaseType.Mssql, connectionString);

		// Assert
		Assert.Equal(connectionString, service.ConnectionString);
		Assert.NotNull(service.QueryGenerator);
	}

	[Fact]
	public void UpdateConnection_WithMySql_SetsConnectionStringAndGenerator()
	{
		// Arrange
		var service = new ConnectionService();
		var connectionString = "Server=localhost;Database=Test;Uid=root;Pwd=password;";

		// Act
		service.UpdateConnection(DatabaseType.MySql, connectionString);

		// Assert
		Assert.Equal(connectionString, service.ConnectionString);
		Assert.NotNull(service.QueryGenerator);
	}

	[Fact]
	public async Task TestConnectionAsync_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Arrange
		var service = new ConnectionService();

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			async () => await service.TestConnectionAsync(DatabaseType.Mssql, "", 10));
	}

	[Fact]
	public async Task TestConnectionAsync_WithInvalidConnectionString_ThrowsException()
	{
		// Arrange
		var service = new ConnectionService();
		var invalidConnectionString = "InvalidConnectionString";

		// Act & Assert - Should throw some kind of exception
		await Assert.ThrowsAnyAsync<Exception>(
			async () => await service.TestConnectionAsync(DatabaseType.Mssql, invalidConnectionString, 5));
	}
}

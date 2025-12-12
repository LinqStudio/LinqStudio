using Xunit;
using LinqStudio.Core.Services;

namespace LinqStudio.Blazor.Tests;

public class ConnectionServiceTests
{
	[Fact]
	public void ConnectionService_CanBeCreated()
	{
		// Arrange & Act
		var service = new ConnectionService();

		// Assert
		Assert.NotNull(service);
	}

	[Fact]
	public void ConnectionService_ConnectionString_StartsAsNull()
	{
		// Arrange
		var service = new ConnectionService();

		// Act & Assert
		Assert.Null(service.ConnectionString);
	}

	[Fact]
	public void ConnectionService_ConnectionString_CanBeSet()
	{
		// Arrange
		var service = new ConnectionService();
		var testConnectionString = "Server=localhost;Database=Test;";

		// Act
		service.ConnectionString = testConnectionString;

		// Assert
		Assert.Equal(testConnectionString, service.ConnectionString);
	}

	[Fact]
	public void ConnectionService_ConnectionString_CanBeUpdated()
	{
		// Arrange
		var service = new ConnectionService();
		var firstConnectionString = "Server=localhost;Database=Test1;";
		var secondConnectionString = "Server=localhost;Database=Test2;";

		// Act
		service.ConnectionString = firstConnectionString;
		Assert.Equal(firstConnectionString, service.ConnectionString);

		service.ConnectionString = secondConnectionString;

		// Assert
		Assert.Equal(secondConnectionString, service.ConnectionString);
	}
}

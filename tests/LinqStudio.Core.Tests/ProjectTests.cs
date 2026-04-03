using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.Tests;

public class ProjectTests
{
	[Fact]
	public void UpdateConnection_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		var conn = new ServerConnection();
		Assert.Throws<ArgumentException>(() => conn.UpdateConnection(DatabaseType.Mssql, string.Empty));
	}

	[Fact]
	public void UpdateConnection_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		var conn = new ServerConnection();
		Assert.Throws<ArgumentException>(() => conn.UpdateConnection(DatabaseType.Mssql, "   "));
	}

	[Fact]
	public void UpdateConnection_SetsConnectionStringAndType()
	{
		var conn = new ServerConnection();
		conn.UpdateConnection(DatabaseType.Sqlite, "Data Source=test.db");
		Assert.Equal(DatabaseType.Sqlite, conn.DatabaseType);
		Assert.Equal("Data Source=test.db", conn.ConnectionString);
	}

	[Fact]
	public void GetServerDisplayName_ReturnsMssqlServer_WhenMssqlConnectionString()
	{
		var conn = new ServerConnection
		{
			DatabaseType = DatabaseType.Mssql,
			ConnectionString = "Server=127.0.0.1,1433;Database=AdventureWorks;Integrated Security=true;"
		};
		var name = conn.GetServerDisplayName();
		Assert.Contains("127.0.0.1,1433", name);
	}

	[Fact]
	public void GetDatabaseName_ReturnsDatabaseName_WhenInitialCatalogPresent()
	{
		var conn = new ServerConnection
		{
			DatabaseType = DatabaseType.Mssql,
			ConnectionString = "Server=localhost;Initial Catalog=TestDb;Integrated Security=true;"
		};
		Assert.Equal("TestDb", conn.GetDatabaseName());
	}

	[Fact]
	public void GetDatabaseName_ReturnsDatabaseName_WhenDatabaseKeyPresent()
	{
		var conn = new ServerConnection
		{
			DatabaseType = DatabaseType.MySql,
			ConnectionString = "Server=localhost;Database=mydb;User=root;"
		};
		Assert.Equal("mydb", conn.GetDatabaseName());
	}

	[Fact]
	public void Project_HasEmptyConnectionsByDefault()
	{
		var project = new Project();
		Assert.NotNull(project.Connections);
		Assert.Empty(project.Connections);
	}
}

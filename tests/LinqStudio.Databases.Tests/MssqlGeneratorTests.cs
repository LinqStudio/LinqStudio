using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace LinqStudio.Databases.Tests;

public class MssqlGeneratorCreateTests
{
	[Fact]
	public void Create_ThrowsArgumentException_WhenConnectionStringIsEmpty()
		=> Assert.Throws<ArgumentException>(() => MssqlGenerator.Create(string.Empty));

	[Fact]
	public void Create_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
		=> Assert.Throws<ArgumentException>(() => MssqlGenerator.Create("   "));

	[Fact]
	public void Create_AllowsConnectionString_WhenNoDatabaseSpecified()
		=> Assert.NotNull(MssqlGenerator.Create("Server=myServer;User Id=sa;Password=secret;"));
}

public class MssqlGeneratorTests : BaseGeneratorTests, IClassFixture<MssqlDatabaseFixture>
{
	private readonly MssqlDatabaseFixture _fixture;

	protected override IDatabaseQueryGenerator Generator { get; }
	protected override IDatabaseQueryGenerator GeneratorWithoutDatabase
		=> new MssqlGenerator(new SqlConnection(_fixture.MasterConnectionString));

	public MssqlGeneratorTests(MssqlDatabaseFixture fixture)
	{
		_fixture = fixture;
		Generator = new MssqlGenerator(_fixture.DbContext.Database.GetDbConnection());
	}

	private static void AssertExpectedTablesExist(IReadOnlyList<DatabaseTableName> tables)
	{
		Assert.NotEmpty(tables);
		Assert.Contains(tables, t => t.Name == "Customers");
		Assert.Contains(tables, t => t.Name == "Orders");
		Assert.Contains(tables, t => t.Name == "Products");
		Assert.Contains(tables, t => t.Name == "OrderItems");
	}

	[Fact]
	public async Task GetTablesAsync_ShouldReturnTables_WhenConnectedToNamedDatabase()
	{
		using var connection = new SqlConnection(_fixture.ConnectionString);
		var generator = new MssqlGenerator(connection);
		var tables = await generator.GetTablesAsync();
		AssertExpectedTablesExist(tables);
		Assert.All(tables, table => Assert.Equal("TestLinqStudio", table.DatabaseName));
		Assert.DoesNotContain(tables, table => table.Name == "OtherOnlyTable");
	}

	[Fact]
	public async Task GetTablesAsync_ShouldNotReturnTablesFromOtherDatabases_WhenConnectedToMaster()
	{
		using var connection = new SqlConnection(_fixture.MasterConnectionString);
		var generator = new MssqlGenerator(connection);
		var tables = await generator.GetTablesAsync();
		Assert.DoesNotContain(tables, t => t.Name is "Customers" or "Orders" or "Products" or "OrderItems");
		Assert.DoesNotContain(tables, t => t.DatabaseName == "TestLinqStudio");
	}

	[Fact]
	public async Task GetDatabasesAsync_ShouldEnumerateDatabases_WhenNoDatabaseIsSpecified()
	{
		using var connection = new SqlConnection(_fixture.MasterConnectionString);
		var generator = new MssqlGenerator(connection);

		var databases = await generator.GetDatabasesAsync();

		Assert.Contains(databases, database => database.Name == "TestLinqStudio");
		Assert.Contains(databases, database => database.Name == _fixture.OtherDatabaseName);
	}

	[Fact]
	public async Task GetTablesAsync_WithDatabaseName_ShouldLoadSelectedDatabase_WhenConnectionHasNoDatabase()
	{
		using var connection = new SqlConnection(_fixture.MasterConnectionString);
		var generator = new MssqlGenerator(connection);

		var tables = await generator.GetTablesAsync(_fixture.OtherDatabaseName);

		Assert.Contains(tables, table => table.Name == "OtherOnlyTable");
		Assert.All(tables, table => Assert.Equal(
			_fixture.OtherDatabaseName,
			table.DatabaseName,
			StringComparer.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GetDatabasesAsync_ThenGetTablesAsync_ShouldLoadTablesForEveryDatabase()
	{
		IDatabaseQueryGenerator generator =
			new MssqlGenerator(new SqlConnection(_fixture.MasterConnectionString));

		var databases = await generator.GetDatabasesAsync();

		Assert.Contains(databases, database => database.Name == "TestLinqStudio");
		Assert.Contains(databases, database => database.Name == _fixture.OtherDatabaseName);

		foreach (var database in databases)
		{
			var tables = await generator.GetTablesAsync(database.Name);

			Assert.NotEmpty(tables);
			Assert.All(tables, table => Assert.Equal(
				database.Name,
				table.DatabaseName,
				StringComparer.OrdinalIgnoreCase));
		}
	}
}

using LinqStudio.Abstractions;
using LinqStudio.Databases.SQLite;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Unit tests for SqliteGenerator.Create() — no live database required.
/// </summary>
public class SqliteGeneratorCreateTests
{
	[Fact]
	public void Create_DoesNotThrow_WithValidInMemoryConnectionString()
	{
		var generator = SqliteGenerator.Create("DataSource=:memory:");
		Assert.NotNull(generator);
	}

	[Fact]
	public void Create_DoesNotThrow_WithValidFileConnectionString()
	{
		var generator = SqliteGenerator.Create("DataSource=test.db");
		Assert.NotNull(generator);
	}

	[Fact]
	public void Create_DoesNotThrow_WithEmptyConnectionString()
	{
		// SqliteGenerator.Create() has no validation - it passes through to SqliteConnection constructor
		// which will fail later during actual connection attempt
		var generator = SqliteGenerator.Create(string.Empty);
		Assert.NotNull(generator);
	}
}

/// <summary>
/// Tests for SQLite database generator.
/// </summary>
public class SqliteGeneratorTests : BaseGeneratorTests, IClassFixture<SqliteDatabaseFixture>
{
	private readonly SqliteDatabaseFixture _fixture;

	protected override IDatabaseQueryGenerator Generator { get; }

	public SqliteGeneratorTests(SqliteDatabaseFixture fixture)
	{
		_fixture = fixture;
		Generator = new SqliteGenerator(_fixture.DbContext.Database.GetDbConnection());
	}

	[Fact]
	public async Task GetTablesAsync_ReturnsTablesFromInMemoryDatabase()
	{
		// SQLite in-memory database specific test - verify tables persist while connection is open
		var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();

		try
		{
			// Create a simple table using raw SQL
			using var createCmd = connection.CreateCommand();
			createCmd.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT);";
			await createCmd.ExecuteNonQueryAsync();

			var generator = new SqliteGenerator(connection);
			var tables = await generator.GetTablesAsync();

			Assert.NotEmpty(tables);
			Assert.Contains(tables, t => t.Name == "TestTable");
		}
		finally
		{
			await connection.CloseAsync();
		}
	}
}

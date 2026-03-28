using LinqStudio.Abstractions;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Unit tests for MySqlGenerator.Create() — no live database required.
/// </summary>
public class MySqlGeneratorCreateTests
{
	[Fact]
	public void Create_DoesNotThrow_WithValidConnectionString()
	{
		var generator = MySqlGenerator.Create("Server=localhost;Database=test;User=root;Password=pwd;");
		Assert.NotNull(generator);
	}

	[Fact]
	public void Create_DoesNotThrow_WithEmptyConnectionString()
	{
		// MySqlGenerator.Create() has no validation - it passes through to MySqlConnection constructor
		// which will fail later during actual connection attempt
		var generator = MySqlGenerator.Create(string.Empty);
		Assert.NotNull(generator);
	}
}

/// <summary>
/// Tests for MySQL database generator using Testcontainers.
/// </summary>
public class MySqlGeneratorTests : BaseGeneratorTests, IClassFixture<MySqlDatabaseFixture>
{
	private readonly MySqlDatabaseFixture _fixture;

	protected override IDatabaseQueryGenerator Generator { get; }

	public MySqlGeneratorTests(MySqlDatabaseFixture fixture)
	{
		_fixture = fixture;
		Generator = new MySqlGenerator(_fixture.DbContext.Database.GetDbConnection());
	}

	[Fact]
	public async Task GetTablesAsync_ReturnsTablesFromCorrectDatabase()
	{
		// Verify that MySQL generator correctly scopes to the connected database
		using var connection = new MySqlConnection(_fixture.ConnectionString);
		var generator = new MySqlGenerator(connection);

		var tables = await generator.GetTablesAsync();

		// Should return our test tables
		Assert.NotEmpty(tables);
		Assert.Contains(tables, t => t.Name == "Customers");
		Assert.Contains(tables, t => t.Name == "Orders");
		Assert.Contains(tables, t => t.Name == "Products");
		Assert.Contains(tables, t => t.Name == "OrderItems");
	}
}

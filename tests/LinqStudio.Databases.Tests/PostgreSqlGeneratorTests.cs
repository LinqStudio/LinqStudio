using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Unit tests for PostgreSqlGenerator.Create() — no live database required.
/// </summary>
public class PostgreSqlGeneratorCreateTests
{
	[Fact]
	public void Create_DoesNotThrow_WithValidConnectionString()
	{
		var generator = PostgreSqlGenerator.Create("Host=localhost;Database=test;Username=postgres;Password=pwd;");
		Assert.NotNull(generator);
	}

	[Fact]
	public void Create_DoesNotThrow_WithEmptyConnectionString()
	{
		// PostgreSqlGenerator.Create() has no validation - it passes through to NpgsqlConnection constructor
		// which will fail later during actual connection attempt
		var generator = PostgreSqlGenerator.Create(string.Empty);
		Assert.NotNull(generator);
	}
}

/// <summary>
/// Tests for PostgreSQL database generator using Testcontainers.
/// </summary>
public class PostgreSqlGeneratorTests : BaseGeneratorTests, IClassFixture<PostgreSqlDatabaseFixture>
{
	private readonly PostgreSqlDatabaseFixture _fixture;

	protected override IDatabaseQueryGenerator Generator { get; }

	public PostgreSqlGeneratorTests(PostgreSqlDatabaseFixture fixture)
	{
		_fixture = fixture;
		Generator = new PostgreSqlGenerator(_fixture.DbContext.Database.GetDbConnection());
	}

	[Fact]
	public async Task GetTablesAsync_ReturnsTablesFromPublicSchema()
	{
		// Verify that PostgreSQL generator returns tables from the correct database/schema
		// PostgreSQL uses schemas (typically 'public' by default)
		using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		var generator = new PostgreSqlGenerator(connection);

		var tables = await generator.GetTablesAsync();

		// Should return our test tables
		Assert.NotEmpty(tables);
		Assert.Contains(tables, t => t.Name == "Customers");
		Assert.Contains(tables, t => t.Name == "Orders");
		Assert.Contains(tables, t => t.Name == "Products");
		Assert.Contains(tables, t => t.Name == "OrderItems");

		// PostgreSQL tables should have schema information
		Assert.All(tables, t => Assert.False(string.IsNullOrWhiteSpace(t.Schema)));
	}
}

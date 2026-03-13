using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Unit tests for MssqlGenerator.Create() validation — no live database required.
/// </summary>
public class MssqlGeneratorCreateTests
{
	[Fact]
	public void Create_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		Assert.Throws<ArgumentException>(() => MssqlGenerator.Create(string.Empty));
	}

	[Fact]
	public void Create_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		Assert.Throws<ArgumentException>(() => MssqlGenerator.Create("   "));
	}

	[Fact]
	public void Create_ThrowsArgumentException_WhenNoDatabaseSpecified()
	{
		Assert.Throws<ArgumentException>(() => MssqlGenerator.Create("Server=myServer;User Id=sa;Password=pwd;"));
	}

	[Fact]
	public void Create_DoesNotThrow_WhenValidConnectionStringWithDatabase()
	{
		// Should not throw — just creates the generator (no actual connection made)
		var generator = MssqlGenerator.Create("Server=myServer;Database=MyDb;User Id=sa;Password=pwd;TrustServerCertificate=True;");
		Assert.NotNull(generator);
	}
}

/// <summary>
/// Tests for MSSQL database generator using Testcontainers.
/// </summary>
public class MssqlGeneratorTests : BaseGeneratorTests, IClassFixture<MssqlDatabaseFixture>
{
	private readonly MssqlDatabaseFixture _fixture;

	protected override IDatabaseQueryGenerator Generator { get; }

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
		// This test explicitly verifies behavior against a named database (not master)
		// to catch bugs where OBJECTPROPERTY returns NULL in non-master database context.
		// Regression test for: MssqlGenerator returning 0 tables for Aspire-seeded DB.
		using var connection = new SqlConnection(_fixture.ConnectionString);
		var generator = new MssqlGenerator(connection);

		var tables = await generator.GetTablesAsync();

		AssertExpectedTablesExist(tables);
	}

	[Fact]
	public async Task GetTablesAsync_ShouldReturnAllUserDatabaseTables_WhenConnectedToMaster()
	{
		// GetTablesAsync uses a server-level cross-database query (FROM sys.databases with dynamic SQL),
		// so it returns tables from all user databases regardless of the current connection database.
		// This test bypasses Create() intentionally to validate the underlying SQL behavior directly.
		using var connection = new SqlConnection(_fixture.MasterConnectionString);
		var generator = new MssqlGenerator(connection);

		var tables = await generator.GetTablesAsync();

		AssertExpectedTablesExist(tables);
	}
}

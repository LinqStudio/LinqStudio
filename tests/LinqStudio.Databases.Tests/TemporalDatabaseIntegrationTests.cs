using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.Tests.Fixtures;
using LinqStudio.Databases.Tests.TestData;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

public abstract class TemporalDatabaseIntegrationTests
{
	protected abstract IDatabaseQueryGenerator Generator { get; }
	protected abstract TestDbContext DbContext { get; }

	[Fact]
	public async Task GetTableAsync_MapsNativeTemporalColumnsToGenericTypes()
	{
		var table = await Generator.GetTableAsync("Customers");

		var birthDate = table.Columns.First(c => c.Name == "BirthDate");
		var preferredTime = table.Columns.First(c => c.Name == "PreferredTime");
		var createdDate = table.Columns.First(c => c.Name == "CreatedDate");

		Assert.Equal(DbColumnType.DateOnly, birthDate.GenericType);
		Assert.Equal(DbColumnType.TimeSpan, preferredTime.GenericType);
		Assert.Equal(DbColumnType.DateTime, createdDate.GenericType);
		Assert.False(string.IsNullOrWhiteSpace(birthDate.DataType));
		Assert.False(string.IsNullOrWhiteSpace(preferredTime.DataType));
		Assert.False(string.IsNullOrWhiteSpace(createdDate.DataType));
	}

	[Fact]
	public async Task QueryAsync_MaterializesNativeTemporalColumnsAsExpectedClrTypes()
	{
		var customer = await DbContext.Customers
			.AsNoTracking()
			.Select(c => new
			{
				c.BirthDate,
				c.PreferredTime,
				c.CreatedDate
			})
			.FirstAsync();

		Assert.NotEqual(default, customer.BirthDate);
		Assert.NotEqual(default, customer.PreferredTime);
		Assert.NotEqual(default, customer.CreatedDate);
	}
}

public sealed class MssqlTemporalDatabaseIntegrationTests(
	MssqlDatabaseFixture fixture) : TemporalDatabaseIntegrationTests, IClassFixture<MssqlDatabaseFixture>
{
	protected override IDatabaseQueryGenerator Generator { get; } =
		new MssqlGenerator(fixture.DbContext.Database.GetDbConnection());

	protected override TestDbContext DbContext => fixture.DbContext;
}

public sealed class PostgreSqlTemporalDatabaseIntegrationTests(
	PostgreSqlDatabaseFixture fixture) : TemporalDatabaseIntegrationTests, IClassFixture<PostgreSqlDatabaseFixture>
{
	protected override IDatabaseQueryGenerator Generator { get; } =
		new PostgreSqlGenerator(fixture.DbContext.Database.GetDbConnection());

	protected override TestDbContext DbContext => fixture.DbContext;
}

public sealed class MySqlTemporalDatabaseIntegrationTests(
	MySqlDatabaseFixture fixture) : TemporalDatabaseIntegrationTests, IClassFixture<MySqlDatabaseFixture>
{
	protected override IDatabaseQueryGenerator Generator { get; } =
		new MySqlGenerator(fixture.DbContext.Database.GetDbConnection());

	protected override TestDbContext DbContext => fixture.DbContext;
}

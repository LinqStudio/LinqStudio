using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

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
}

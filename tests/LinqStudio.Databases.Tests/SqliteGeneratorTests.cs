using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.SQLite;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

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
}

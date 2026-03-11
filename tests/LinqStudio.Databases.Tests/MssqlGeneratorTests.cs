using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

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
}

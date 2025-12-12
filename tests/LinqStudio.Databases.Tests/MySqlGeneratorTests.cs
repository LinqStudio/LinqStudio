using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.MySQL;
using LinqStudio.Databases.Tests.Fixtures;

namespace LinqStudio.Databases.Tests;

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
		Generator = new MySqlGenerator(_fixture.DbContext.Database);
	}

}

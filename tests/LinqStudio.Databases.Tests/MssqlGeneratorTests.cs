using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.MSSQL;
using LinqStudio.Databases.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.MsSql;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for MSSQL database generator using Testcontainers.
/// </summary>
public class MssqlGeneratorTests : DatabaseGeneratorTestsBase
{
	private MsSqlContainer? _container;

	protected override async Task<string> StartDatabaseContainerAsync()
	{
		_container = new MsSqlBuilder()
			.WithPassword("StrongPassword123!")
			.Build();

		await _container.StartAsync();

		return _container.GetConnectionString();
	}

	protected override async Task StopDatabaseContainerAsync()
	{
		if (_container != null)
		{
			await _container.StopAsync();
			await _container.DisposeAsync();
		}
	}

	protected override DbContextOptions<TestDbContext> CreateDbContextOptions(string connectionString)
	{
		return new DbContextOptionsBuilder<TestDbContext>()
			.UseSqlServer(connectionString)
			.Options;
	}

	protected override IDatabaseQueryGenerator CreateGenerator(DatabaseFacade database)
	{
		return new MssqlGenerator(database);
	}
}

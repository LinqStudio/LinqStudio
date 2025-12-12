using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.MySQL;
using LinqStudio.Databases.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for MySQL database generator using Testcontainers.
/// </summary>
public class MySqlGeneratorTests : DatabaseGeneratorTestsBase
{
	private MySqlContainer? _container;

	protected override async Task<string> StartDatabaseContainerAsync()
	{
		_container = new MySqlBuilder()
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
			.UseMySQL(connectionString)
			.Options;
	}

	protected override IDatabaseQueryGenerator CreateGenerator(string connectionString)
	{
		return new MySqlGenerator(connectionString);
	}
}

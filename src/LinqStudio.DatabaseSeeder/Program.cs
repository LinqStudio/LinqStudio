using LinqStudio.Demo;
using Microsoft.EntityFrameworkCore;

try
{
	// Read connection strings from environment (injected by Aspire)
	var mssqlConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DemoMssql");
	var mysqlConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DemoMysql");

	var tasks = new List<Task>();

	if (!string.IsNullOrEmpty(mssqlConnectionString))
	{
		tasks.Add(SeedDatabaseAsync(mssqlConnectionString, DatabaseProvider.SqlServer, "MSSQL"));
	}

	if (!string.IsNullOrEmpty(mysqlConnectionString))
	{
		tasks.Add(SeedDatabaseAsync(mysqlConnectionString, DatabaseProvider.MySql, "MySQL"));
	}

	await Task.WhenAll(tasks);
	Console.WriteLine("Demo seeding complete.");
	Environment.Exit(0);
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Fatal seeder error: {ex}");
	Environment.Exit(1);
}

static async Task SeedDatabaseAsync(string connectionString, DatabaseProvider provider, string name)
{
	var retries = 10;
	while (retries-- > 0)
	{
		try
		{
			var options = provider switch
			{
				DatabaseProvider.SqlServer => new DbContextOptionsBuilder<DemoDbContext>()
					.UseSqlServer(connectionString).Options,
				DatabaseProvider.MySql => new DbContextOptionsBuilder<DemoDbContext>()
					.UseMySQL(connectionString).Options,
				_ => throw new NotSupportedException()
			};
			await using var ctx = new DemoDbContext(options);
			await using var tx = await ctx.Database.BeginTransactionAsync();

			await DemoSeeder.SeedAsync(ctx);

			await tx.CommitAsync();
			Console.WriteLine($"[{name}] Seeded successfully.");
			return;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{name}] Retry {10 - retries}/10: {ex.ToString()}");
			await Task.Delay(3000);
		}
	}
	throw new Exception($"[{name}] Failed to seed after 10 retries.");
}

enum DatabaseProvider { SqlServer, MySql }

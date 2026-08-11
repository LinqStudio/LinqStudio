using LinqStudio.Demo;
using Microsoft.EntityFrameworkCore;

try
{
	// Read connection strings from environment (injected by Aspire)
	var targets = new[]
	{
		new SeedTarget("DemoMssql1", DatabaseProvider.SqlServer, "MSSQL Demo 1", new DateTime(2024, 1, 1)),
		new SeedTarget("DemoMssql2", DatabaseProvider.SqlServer, "MSSQL Demo 2", new DateTime(2025, 1, 1)),
		new SeedTarget("DemoMysql1", DatabaseProvider.MySql, "MySQL Demo 1", new DateTime(2024, 1, 1)),
		new SeedTarget("DemoMysql2", DatabaseProvider.MySql, "MySQL Demo 2", new DateTime(2025, 1, 1))
	};

	var tasks = targets.Select(SeedTargetAsync).ToList();

	await Task.WhenAll(tasks);
	Console.WriteLine("Demo seeding complete.");
	Environment.Exit(0);
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Fatal seeder error: {ex}");
	Environment.Exit(1);
}

static async Task SeedTargetAsync(SeedTarget target)
{
	var connectionString = Environment.GetEnvironmentVariable($"ConnectionStrings__{target.ConfigurationName}");
	if (string.IsNullOrEmpty(connectionString))
	{
		return;
	}

	var retries = 10;
	while (retries-- > 0)
	{
		try
		{
			var options = target.Provider switch
			{
				DatabaseProvider.SqlServer => new DbContextOptionsBuilder<DemoDbContext>()
					.UseSqlServer(connectionString).Options,
				DatabaseProvider.MySql => new DbContextOptionsBuilder<DemoDbContext>()
					.UseMySQL(connectionString).Options,
				_ => throw new NotSupportedException()
			};
			await using var ctx = new DemoDbContext(options);
			await using var tx = await ctx.Database.BeginTransactionAsync();

			await DemoSeeder.SeedAsync(ctx, target.SnapshotDate);

			await tx.CommitAsync();
			Console.WriteLine($"[{target.DisplayName}] Seeded successfully.");
			return;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{target.DisplayName}] Retry {10 - retries}/10: {ex}");
			await Task.Delay(3000);
		}
	}
	throw new Exception($"[{target.DisplayName}] Failed to seed after 10 retries.");
}

record SeedTarget(string ConfigurationName, DatabaseProvider Provider, string DisplayName, DateTime SnapshotDate);

enum DatabaseProvider { SqlServer, MySql }

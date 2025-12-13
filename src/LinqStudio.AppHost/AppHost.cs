using LinqStudio.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server for testing
var sqlServer = builder.AddSqlServer("sqlserver")
	.WithDataVolume()
	.AddDatabase("testdb");

// Add a resource to seed the database
builder.Services.AddHostedService<DatabaseSeederHostedService>();

builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")
	.WithReference(sqlServer);

builder.Build().Run();

/// <summary>
/// Hosted service to seed test database with sample data.
/// </summary>
file class DatabaseSeederHostedService : IHostedService
{
	private readonly IServiceProvider _serviceProvider;

	public DatabaseSeederHostedService(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		// Wait a bit for SQL Server to start
		await Task.Delay(5000, cancellationToken);

		try
		{
			var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__testdb");
			if (string.IsNullOrEmpty(connectionString))
			{
				return;
			}

			var options = new DbContextOptionsBuilder<TestDbContext>()
				.UseSqlServer(connectionString)
				.Options;

			await using var dbContext = new TestDbContext(options);
			
			// Create database
			await dbContext.Database.EnsureCreatedAsync(cancellationToken);

			// Check if already seeded
			if (await dbContext.Customers.AnyAsync(cancellationToken))
			{
				return;
			}

			// Seed data
			var customers = BogusDataGenerator.GenerateCustomers(10);
			await dbContext.Customers.AddRangeAsync(customers, cancellationToken);
			await dbContext.SaveChangesAsync(cancellationToken);

			var products = BogusDataGenerator.GenerateProducts(20);
			await dbContext.Products.AddRangeAsync(products, cancellationToken);
			await dbContext.SaveChangesAsync(cancellationToken);

			var orders = BogusDataGenerator.GenerateOrders(customers, 3);
			await dbContext.Orders.AddRangeAsync(orders, cancellationToken);
			await dbContext.SaveChangesAsync(cancellationToken);

			var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);
			await dbContext.OrderItems.AddRangeAsync(orderItems, cancellationToken);
			await dbContext.SaveChangesAsync(cancellationToken);
		}
		catch
		{
			// Ignore seeding errors
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

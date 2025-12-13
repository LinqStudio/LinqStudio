using LinqStudio.App.WebServer.TestData;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.App.WebServer.Services;

/// <summary>
/// Service to seed test database with sample data on startup.
/// </summary>
public class DatabaseSeederService : IHostedService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<DatabaseSeederService> _logger;

	public DatabaseSeederService(IServiceProvider serviceProvider, ILogger<DatabaseSeederService> logger)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

		try
		{
			// Apply migrations or create database
			await dbContext.Database.EnsureCreatedAsync(cancellationToken);

			// Check if data already exists
			if (await dbContext.Customers.AnyAsync(cancellationToken))
			{
				_logger.LogInformation("Test database already seeded");
				return;
			}

			_logger.LogInformation("Seeding test database...");

			// Generate and insert test data
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

			_logger.LogInformation("Test database seeded successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error seeding test database");
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

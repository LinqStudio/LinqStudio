using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using LinqStudio.TestData;

namespace LinqStudio.Databases.Tests.Fixtures;

/// <summary>
/// Shared fixture for MySQL database container.
/// Creates one container for all MySQL tests.
/// </summary>
public class MySqlDatabaseFixture : IAsyncLifetime
{
	private MySqlContainer? _container;
	public string ConnectionString { get; private set; } = null!;
	public TestDbContext DbContext { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		_container = new MySqlBuilder()
			.WithPassword("StrongPassword123!")
			.Build();

		await _container.StartAsync();
		ConnectionString = _container.GetConnectionString();

		// Create DbContext and seed data
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseMySQL(ConnectionString)
			.Options;

		DbContext = new TestDbContext(options);
		await SeedTestDataAsync();
	}

	public async Task DisposeAsync()
	{
		if (DbContext != null)
			await DbContext.DisposeAsync();

		if (_container != null)
		{
			await _container.StopAsync();
			await _container.DisposeAsync();
		}
	}

	private async Task SeedTestDataAsync()
	{
		// Create database and apply migrations
		await DbContext.Database.EnsureCreatedAsync();

		// Generate and insert test data - IDs will be auto-generated
		var customers = BogusDataGenerator.GenerateCustomers(10);
		await DbContext.Customers.AddRangeAsync(customers);
		await DbContext.SaveChangesAsync(); // Save to get IDs

		var products = BogusDataGenerator.GenerateProducts(20);
		await DbContext.Products.AddRangeAsync(products);
		await DbContext.SaveChangesAsync(); // Save to get IDs

		var orders = BogusDataGenerator.GenerateOrders(customers, 3);
		await DbContext.Orders.AddRangeAsync(orders);
		await DbContext.SaveChangesAsync(); // Save to get IDs

		var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);
		await DbContext.OrderItems.AddRangeAsync(orderItems);
		await DbContext.SaveChangesAsync();
	}
}

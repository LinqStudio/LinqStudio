using Microsoft.EntityFrameworkCore;
using LinqStudio.Databases.Tests.TestData;

namespace LinqStudio.Databases.Tests.Fixtures;

/// <summary>
/// Shared fixture for SQLite database.
/// Uses in-memory SQLite database for all SQLite tests.
/// </summary>
public class SqliteDatabaseFixture : IAsyncLifetime
{
	public string ConnectionString { get; private set; } = null!;
	public TestDbContext DbContext { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		// Use in-memory SQLite database
		ConnectionString = "DataSource=:memory:";

		// Create DbContext and seed data
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseSqlite(ConnectionString)
			.Options;

		DbContext = new TestDbContext(options);
		
		// Open connection to keep in-memory database alive
		await DbContext.Database.OpenConnectionAsync();
		
		await SeedTestDataAsync();
	}

	public async Task DisposeAsync()
	{
		if (DbContext != null)
		{
			await DbContext.Database.CloseConnectionAsync();
			await DbContext.DisposeAsync();
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

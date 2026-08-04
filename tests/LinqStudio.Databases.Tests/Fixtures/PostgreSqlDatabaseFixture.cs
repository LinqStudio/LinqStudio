using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using LinqStudio.Databases.Tests.TestData;

namespace LinqStudio.Databases.Tests.Fixtures;

/// <summary>
/// Shared fixture for PostgreSQL database container.
/// Creates one container for all PostgreSQL tests.
/// </summary>
public class PostgreSqlDatabaseFixture : IAsyncLifetime
{
	private PostgreSqlContainer? _container;
	public string ConnectionString { get; private set; } = null!;
	public string ServerConnectionString { get; private set; } = null!;
	public string OtherDatabaseName { get; } = "other_linqstudio_database";
	public TestDbContext DbContext { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		// Configure Npgsql to use legacy timestamp behavior
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

		_container = new PostgreSqlBuilder("postgres:latest")
			.WithPassword("StrongPassword123!")
			.Build();

		await _container.StartAsync();
		ConnectionString = _container.GetConnectionString();
		var serverConnection = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString);
		serverConnection.Remove("Database");
		ServerConnectionString = serverConnection.ConnectionString;

		var adminConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
		{
			Database = "postgres"
		}.ConnectionString;
		await using (var adminConnection = new Npgsql.NpgsqlConnection(adminConnectionString))
		{
			await adminConnection.OpenAsync();
			await using var command = adminConnection.CreateCommand();
			command.CommandText = $"CREATE DATABASE \"{OtherDatabaseName}\"";
			await command.ExecuteNonQueryAsync();
		}

		var otherConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
		{
			Database = OtherDatabaseName
		}.ConnectionString;
		await using (var otherConnection = new Npgsql.NpgsqlConnection(otherConnectionString))
		{
			await otherConnection.OpenAsync();
			await using var command = otherConnection.CreateCommand();
			command.CommandText = "CREATE TABLE IF NOT EXISTS \"OtherOnlyTable\" (\"Id\" integer NOT NULL PRIMARY KEY)";
			await command.ExecuteNonQueryAsync();
		}

		// Create DbContext and seed data
		var options = new DbContextOptionsBuilder<TestDbContext>()
			.UseNpgsql(ConnectionString)
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

using FluentAssertions;
using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.Tests.TestData;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Abstract base class for database generator tests.
/// Provides common test logic that works across different database types.
/// </summary>
public abstract class DatabaseGeneratorTestsBase : IAsyncLifetime
{
	protected string ConnectionString { get; private set; } = null!;
	protected IDatabaseQueryGenerator Generator { get; private set; } = null!;

	/// <summary>
	/// Initialize the database container and setup test data.
	/// </summary>
	public async Task InitializeAsync()
	{
		// Start the database container
		ConnectionString = await StartDatabaseContainerAsync();

		// Create the generator
		Generator = CreateGenerator(ConnectionString);

		// Setup EF Core and insert test data
		await SeedTestDataAsync();
	}

	/// <summary>
	/// Cleanup the database container.
	/// </summary>
	public async Task DisposeAsync()
	{
		await StopDatabaseContainerAsync();
	}

	/// <summary>
	/// Start the database container and return the connection string.
	/// </summary>
	protected abstract Task<string> StartDatabaseContainerAsync();

	/// <summary>
	/// Stop the database container.
	/// </summary>
	protected abstract Task StopDatabaseContainerAsync();

	/// <summary>
	/// Create a DbContext options for the specific database type.
	/// </summary>
	protected abstract DbContextOptions<TestDbContext> CreateDbContextOptions(string connectionString);

	/// <summary>
	/// Create the database generator for the specific database type.
	/// </summary>
	protected abstract IDatabaseQueryGenerator CreateGenerator(string connectionString);

	/// <summary>
	/// Seed test data into the database using EF Core.
	/// </summary>
	private async Task SeedTestDataAsync()
	{
		var options = CreateDbContextOptions(ConnectionString);
		await using var context = new TestDbContext(options);

		// Create database and apply migrations
		await context.Database.EnsureCreatedAsync();

		// Generate and insert test data
		var customers = BogusDataGenerator.GenerateCustomers(10);
		var products = BogusDataGenerator.GenerateProducts(20);
		var orders = BogusDataGenerator.GenerateOrders(customers, 3);
		var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);

		await context.Customers.AddRangeAsync(customers);
		await context.Products.AddRangeAsync(products);
		await context.Orders.AddRangeAsync(orders);
		await context.OrderItems.AddRangeAsync(orderItems);

		await context.SaveChangesAsync();
	}

	[Fact]
	public async Task GetTablesAsync_ShouldReturnAllTables()
	{
		// Act
		var tables = await Generator.GetTablesAsync();

		// Assert
		tables.Should().NotBeNull();
		tables.Should().NotBeEmpty();
		
		// Should have at least our 4 tables
		tables.Should().Contain(t => t.Name == "Customers");
		tables.Should().Contain(t => t.Name == "Orders");
		tables.Should().Contain(t => t.Name == "Products");
		tables.Should().Contain(t => t.Name == "OrderItems");

		// Tables should have schema and name
		tables.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Name));
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnTableWithColumns()
	{
		// Arrange
		var tables = await Generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await Generator.GetTableAsync(customersTable.FullName);

		// Assert
		table.Should().NotBeNull();
		table.Name.Should().Be("Customers");
		table.Columns.Should().NotBeNull();
		table.Columns.Should().NotBeEmpty();

		// Verify expected columns
		table.Columns.Should().Contain(c => c.Name == "Id");
		table.Columns.Should().Contain(c => c.Name == "FirstName");
		table.Columns.Should().Contain(c => c.Name == "LastName");
		table.Columns.Should().Contain(c => c.Name == "Email");
		table.Columns.Should().Contain(c => c.Name == "CreatedDate");

		// Verify Id is primary key
		var idColumn = table.Columns.First(c => c.Name == "Id");
		idColumn.IsPrimaryKey.Should().BeTrue();
		idColumn.IsNullable.Should().BeFalse();
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnTableWithForeignKeys()
	{
		// Arrange
		var tables = await Generator.GetTablesAsync();
		var ordersTable = tables.First(t => t.Name == "Orders");

		// Act
		var table = await Generator.GetTableAsync(ordersTable.FullName);

		// Assert
		table.Should().NotBeNull();
		table.ForeignKeys.Should().NotBeNull();
		table.ForeignKeys.Should().NotBeEmpty();

		// Should have foreign key to Customers table
		table.ForeignKeys.Should().Contain(fk => 
			fk.ColumnName == "CustomerId" && 
			fk.ReferencedTable.Contains("Customers"));
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnTableWithMultipleForeignKeys()
	{
		// Arrange
		var tables = await Generator.GetTablesAsync();
		var orderItemsTable = tables.First(t => t.Name == "OrderItems");

		// Act
		var table = await Generator.GetTableAsync(orderItemsTable.FullName);

		// Assert
		table.Should().NotBeNull();
		table.ForeignKeys.Should().NotBeNull();
		table.ForeignKeys.Should().HaveCountGreaterThanOrEqualTo(2);

		// Should have foreign keys to Orders and Products tables
		table.ForeignKeys.Should().Contain(fk => 
			fk.ColumnName == "OrderId" && 
			fk.ReferencedTable.Contains("Orders"));
		
		table.ForeignKeys.Should().Contain(fk => 
			fk.ColumnName == "ProductId" && 
			fk.ReferencedTable.Contains("Products"));
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnColumnDataTypes()
	{
		// Arrange
		var tables = await Generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await Generator.GetTableAsync(customersTable.FullName);

		// Assert
		table.Columns.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.DataType));
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnNullableInformation()
	{
		// Arrange
		var tables = await Generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await Generator.GetTableAsync(customersTable.FullName);

		// Assert
		var idColumn = table.Columns!.First(c => c.Name == "Id");
		idColumn.IsNullable.Should().BeFalse();

		var firstNameColumn = table.Columns.First(c => c.Name == "FirstName");
		firstNameColumn.IsNullable.Should().BeFalse();
	}
}

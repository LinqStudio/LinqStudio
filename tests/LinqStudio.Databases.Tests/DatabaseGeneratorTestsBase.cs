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
	protected TestDbContext DbContext { get; private set; } = null!;

	/// <summary>
	/// Initialize the database container and setup test data.
	/// </summary>
	public async Task InitializeAsync()
	{
		// Start the database container
		ConnectionString = await StartDatabaseContainerAsync();

		// Create DbContext
		var options = CreateDbContextOptions(ConnectionString);
		DbContext = new TestDbContext(options);

		// Create the generator with DbFacade
		Generator = CreateGenerator(DbContext.Database);

		// Setup EF Core and insert test data
		await SeedTestDataAsync();
	}

	/// <summary>
	/// Cleanup the database container.
	/// </summary>
	public async Task DisposeAsync()
	{
		if (DbContext != null)
			await DbContext.DisposeAsync();
		
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
	protected abstract IDatabaseQueryGenerator CreateGenerator(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database);

	/// <summary>
	/// Seed test data into the database using EF Core.
	/// </summary>
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

	[Fact]
	public async Task GetTablesAsync_ShouldReturnAllTables()
	{
		// Act
		var tables = await Generator.GetTablesAsync();

		// Assert
		Assert.NotNull(tables);
		Assert.NotEmpty(tables);
		
		// Should have at least our 4 tables
		Assert.Contains(tables, t => t.Name == "Customers");
		Assert.Contains(tables, t => t.Name == "Orders");
		Assert.Contains(tables, t => t.Name == "Products");
		Assert.Contains(tables, t => t.Name == "OrderItems");

		// Tables should have schema and name
		Assert.All(tables, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
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
		Assert.NotNull(table);
		Assert.Equal("Customers", table.Name);
		Assert.NotNull(table.Columns);
		Assert.NotEmpty(table.Columns);

		// Verify expected columns
		Assert.Contains(table.Columns, c => c.Name == "Id");
		Assert.Contains(table.Columns, c => c.Name == "FirstName");
		Assert.Contains(table.Columns, c => c.Name == "LastName");
		Assert.Contains(table.Columns, c => c.Name == "Email");
		Assert.Contains(table.Columns, c => c.Name == "CreatedDate");

		// Verify Id is primary key
		var idColumn = table.Columns.First(c => c.Name == "Id");
		Assert.True(idColumn.IsPrimaryKey);
		Assert.False(idColumn.IsNullable);
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
		Assert.NotNull(table);
		Assert.NotNull(table.ForeignKeys);
		Assert.NotEmpty(table.ForeignKeys);

		// Should have foreign key to Customers table
		Assert.Contains(table.ForeignKeys, fk => 
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
		Assert.NotNull(table);
		Assert.NotNull(table.ForeignKeys);
		Assert.True(table.ForeignKeys.Count >= 2);

		// Should have foreign keys to Orders and Products tables
		Assert.Contains(table.ForeignKeys, fk => 
			fk.ColumnName == "OrderId" && 
			fk.ReferencedTable.Contains("Orders"));
		
		Assert.Contains(table.ForeignKeys, fk => 
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
		Assert.All(table.Columns!, c => Assert.False(string.IsNullOrWhiteSpace(c.DataType)));
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
		Assert.False(idColumn.IsNullable);

		var firstNameColumn = table.Columns.First(c => c.Name == "FirstName");
		Assert.False(firstNameColumn.IsNullable);
	}
}

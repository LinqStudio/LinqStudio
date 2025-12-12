using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases.MySQL;
using LinqStudio.Databases.Tests.Fixtures;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for MySQL database generator using Testcontainers.
/// </summary>
public class MySqlGeneratorTests : IClassFixture<MySqlDatabaseFixture>
{
	private readonly MySqlDatabaseFixture _fixture;
	private readonly IDatabaseQueryGenerator _generator;

	public MySqlGeneratorTests(MySqlDatabaseFixture fixture)
	{
		_fixture = fixture;
		_generator = new MySqlGenerator(_fixture.DbContext.Database);
	}

	[Fact]
	public async Task GetTablesAsync_ShouldReturnAllTables()
	{
		// Act
		var tables = await _generator.GetTablesAsync();

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
		var tables = await _generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await _generator.GetTableAsync(customersTable.FullName);

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
		var tables = await _generator.GetTablesAsync();
		var ordersTable = tables.First(t => t.Name == "Orders");

		// Act
		var table = await _generator.GetTableAsync(ordersTable.FullName);

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
		var tables = await _generator.GetTablesAsync();
		var orderItemsTable = tables.First(t => t.Name == "OrderItems");

		// Act
		var table = await _generator.GetTableAsync(orderItemsTable.FullName);

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
		var tables = await _generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await _generator.GetTableAsync(customersTable.FullName);

		// Assert
		Assert.All(table.Columns!, c => Assert.False(string.IsNullOrWhiteSpace(c.DataType)));
	}

	[Fact]
	public async Task GetTableAsync_ShouldReturnNullableInformation()
	{
		// Arrange
		var tables = await _generator.GetTablesAsync();
		var customersTable = tables.First(t => t.Name == "Customers");

		// Act
		var table = await _generator.GetTableAsync(customersTable.FullName);

		// Assert
		var idColumn = table.Columns!.First(c => c.Name == "Id");
		Assert.False(idColumn.IsNullable);

		var firstNameColumn = table.Columns.First(c => c.Name == "FirstName");
		Assert.False(firstNameColumn.IsNullable);
	}
}

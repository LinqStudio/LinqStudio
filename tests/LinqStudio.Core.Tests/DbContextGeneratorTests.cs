using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests;

public class DbContextGeneratorTests
{
	private readonly DbContextGenerator _generator = new();

	private sealed class FakeGenerator : IDatabaseQueryGenerator
	{
		private readonly List<DatabaseTableName> _tables;
		private readonly Dictionary<string, DatabaseTableDetail> _details;

		public FakeGenerator(List<DatabaseTableName> tables, Dictionary<string, DatabaseTableDetail> details)
		{
			_tables = tables;
			_details = details;
		}

		public Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<DatabaseTableName>>(_tables);

		public Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken ct = default)
			=> Task.FromResult(_details[tableName]);

		public Task TestConnectionAsync(CancellationToken ct = default) => Task.CompletedTask;

		public DbColumnType MapToGenericType(string dataType) => DbColumnType.String;
	}

	[Fact]
	public async Task GenerateAsync_SingleTable_NoForeignKeys_ProducesModelFile()
	{
		var table = new DatabaseTableName { Name = "Orders" };
		var detail = new DatabaseTableDetail
		{
			Name = "Orders",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true },
				new TableColumn { Name = "Description", DataType = "nvarchar", GenericType = DbColumnType.String, IsNullable = false, IsPrimaryKey = false, IsIdentity = false, MaxLength = 200 }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Orders"] = detail });
		var result = await _generator.GenerateAsync(fake);

		Assert.True(result.ModelFiles.ContainsKey("Orders.cs"));
		var code = result.ModelFiles["Orders.cs"];
		Assert.Contains("[Key]", code);
		Assert.Contains("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]", code);
		Assert.Contains("[Required]", code);
		Assert.Contains("[MaxLength(200)]", code);
		Assert.Equal("GeneratedDbContext", result.ContextTypeName);
		Assert.Equal("GeneratedModels", result.Namespace);
	}

	[Fact]
	public async Task GenerateAsync_NullableStringColumn_NoRequiredAnnotation()
	{
		var table = new DatabaseTableName { Name = "Products" };
		var detail = new DatabaseTableDetail
		{
			Name = "Products",
			Columns =
			[
				new TableColumn { Name = "Notes", DataType = "nvarchar", GenericType = DbColumnType.String, IsNullable = true, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Products"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Products.cs"];
		Assert.DoesNotContain("[Required]", code);
		Assert.Contains("string?", code);
	}

	[Fact]
	public async Task GenerateAsync_NonNullableStringColumn_HasRequiredAnnotation()
	{
		var table = new DatabaseTableName { Name = "Products" };
		var detail = new DatabaseTableDetail
		{
			Name = "Products",
			Columns =
			[
				new TableColumn { Name = "Name", DataType = "nvarchar", GenericType = DbColumnType.String, IsNullable = false, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Products"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Products.cs"];
		Assert.Contains("[Required]", code);
		Assert.DoesNotContain("string?", code);
		Assert.Contains("= string.Empty;", code);
	}

	[Fact]
	public async Task GenerateAsync_NullableValueType_UsesNullableType()
	{
		var table = new DatabaseTableName { Name = "Items" };
		var detail = new DatabaseTableDetail
		{
			Name = "Items",
			Columns =
			[
				new TableColumn { Name = "Quantity", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = true, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Items"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Items.cs"];
		Assert.Contains("int?", code);
	}

	[Fact]
	public async Task GenerateAsync_MaxLength_Minus1_NoAnnotation()
	{
		var table = new DatabaseTableName { Name = "Items" };
		var detail = new DatabaseTableDetail
		{
			Name = "Items",
			Columns =
			[
				new TableColumn { Name = "Notes", DataType = "nvarchar(max)", GenericType = DbColumnType.String, IsNullable = false, IsPrimaryKey = false, IsIdentity = false, MaxLength = -1 }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Items"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Items.cs"];
		Assert.DoesNotContain("[MaxLength(", code);
		Assert.Contains("[Required]", code);
	}

	[Fact]
	public async Task GenerateAsync_SnakeCaseColumnName_ConvertsToPascalCase()
	{
		var table = new DatabaseTableName { Name = "Events" };
		var detail = new DatabaseTableDetail
		{
			Name = "Events",
			Columns =
			[
				new TableColumn { Name = "created_at", DataType = "datetime", GenericType = DbColumnType.DateTime, IsNullable = true, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Events"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Events.cs"];
		Assert.Contains("public DateTime? CreatedAt", code);
		Assert.DoesNotContain("created_at", code);
	}

	[Fact]
	public async Task GenerateAsync_SchemaPrefix_StrippedFromClassName()
	{
		var table = new DatabaseTableName { Schema = "dbo", Name = "OrderItems" };
		var detail = new DatabaseTableDetail
		{
			Schema = "dbo",
			Name = "OrderItems",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true }
			],
			ForeignKeys = []
		};

		// Key must match FullName: "dbo.OrderItems"
		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["dbo.OrderItems"] = detail });
		var result = await _generator.GenerateAsync(fake);

		Assert.True(result.ModelFiles.ContainsKey("OrderItems.cs"));
		var code = result.ModelFiles["OrderItems.cs"];
		Assert.Contains("public class OrderItems", code);
	}

	[Fact]
	public async Task GenerateAsync_ForeignKey_AddsReferenceNavPropOnChildSide()
	{
		var ordersTable = new DatabaseTableName { Name = "Orders" };
		var customersTable = new DatabaseTableName { Name = "Customers" };

		var ordersDetail = new DatabaseTableDetail
		{
			Name = "Orders",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true },
				new TableColumn { Name = "CustomerId", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys =
			[
				new ForeignKey { Name = "FK_Orders_Customers", ColumnName = "CustomerId", ReferencedTable = "Customers", ReferencedColumn = "Id" }
			]
		};
		var customersDetail = new DatabaseTableDetail
		{
			Name = "Customers",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator(
			[ordersTable, customersTable],
			new Dictionary<string, DatabaseTableDetail>
			{
				["Orders"] = ordersDetail,
				["Customers"] = customersDetail
			});

		var result = await _generator.GenerateAsync(fake);

		var ordersCode = result.ModelFiles["Orders.cs"];
		var customersCode = result.ModelFiles["Customers.cs"];

		// Child side (Orders): reference nav prop — type is class name "Customers", nav name is singularized "Customer"
		Assert.Contains("public virtual Customers? Customer", ordersCode);
		// Parent side (Customers): collection nav prop for child class "Orders"
		Assert.Contains("ICollection<Orders>", customersCode);
	}

	[Fact]
	public async Task GenerateAsync_MultipleTables_DbContextHasDbSetForEach()
	{
		var ordersTable = new DatabaseTableName { Name = "Orders" };
		var customersTable = new DatabaseTableName { Name = "Customers" };

		var ordersDetail = new DatabaseTableDetail
		{
			Name = "Orders",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true }
			],
			ForeignKeys = []
		};
		var customersDetail = new DatabaseTableDetail
		{
			Name = "Customers",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "int", GenericType = DbColumnType.Int32, IsNullable = false, IsPrimaryKey = true, IsIdentity = true }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator(
			[ordersTable, customersTable],
			new Dictionary<string, DatabaseTableDetail>
			{
				["Orders"] = ordersDetail,
				["Customers"] = customersDetail
			});

		var result = await _generator.GenerateAsync(fake);

		Assert.Contains("DbSet<Orders>", result.DbContextCode);
		Assert.Contains("DbSet<Customers>", result.DbContextCode);
		Assert.Contains("GeneratedDbContext", result.DbContextCode);
		Assert.Contains("UseInMemoryDatabase", result.DbContextCode);
		Assert.Contains("namespace GeneratedModels", result.DbContextCode);
	}

	[Fact]
	public async Task GenerateAsync_EmptyTableList_ProducesEmptyModelFiles()
	{
		var fake = new FakeGenerator([], new Dictionary<string, DatabaseTableDetail>());
		var result = await _generator.GenerateAsync(fake);

		Assert.Empty(result.ModelFiles);
		Assert.Contains("GeneratedDbContext", result.DbContextCode);
	}

	[Fact]
	public async Task GenerateAsync_BinaryColumn_UsesCorrectType()
	{
		var table = new DatabaseTableName { Name = "Files" };
		var detail = new DatabaseTableDetail
		{
			Name = "Files",
			Columns =
			[
				new TableColumn { Name = "Data", DataType = "varbinary", GenericType = DbColumnType.Binary, IsNullable = false, IsPrimaryKey = false, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Files"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Files.cs"];
		Assert.Contains("byte[]", code);
		Assert.Contains("= [];", code);
	}

	[Fact]
	public async Task GenerateAsync_GuidPrimaryKey_NoIdentityAnnotation_WhenNotIdentity()
	{
		var table = new DatabaseTableName { Name = "Entities" };
		var detail = new DatabaseTableDetail
		{
			Name = "Entities",
			Columns =
			[
				new TableColumn { Name = "Id", DataType = "uniqueidentifier", GenericType = DbColumnType.Guid, IsNullable = false, IsPrimaryKey = true, IsIdentity = false }
			],
			ForeignKeys = []
		};

		var fake = new FakeGenerator([table], new Dictionary<string, DatabaseTableDetail> { ["Entities"] = detail });
		var result = await _generator.GenerateAsync(fake);

		var code = result.ModelFiles["Entities.cs"];
		Assert.Contains("[Key]", code);
		Assert.DoesNotContain("[DatabaseGenerated(", code);
	}
}

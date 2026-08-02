using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Components.Dialogs;

namespace LinqStudio.Blazor.Tests;

public class RelationshipKeyPairDetectorTests
{
	[Fact]
	public void Detect_NamingConventionCustomerIdToCustomerId_ReturnsCustomerIdAndId()
	{
		var dependent = Table("Orders",
			Column("Id", DbColumnType.Int32, isPrimaryKey: true),
			Column("CustomerId", DbColumnType.Int32));
		var principal = Table("Customer",
			Column("Id", DbColumnType.Int32, isPrimaryKey: true));

		var result = RelationshipKeyPairDetector.Detect(dependent, principal);

		var pair = Assert.Single(result);
		Assert.Equal("CustomerId", pair.DependentColumn);
		Assert.Equal("Id", pair.PrincipalColumn);
	}

	[Fact]
	public void Detect_ForeignKeyMetadata_ReturnsReferencedColumns()
	{
		var dependent = Table("Orders", [Column("BuyerKey", DbColumnType.Guid)],
		[
			new ForeignKey
			{
				Name = "FK_Orders_Customers",
				ColumnName = "BuyerKey",
				ReferencedTable = "dbo.Customers",
				ReferencedColumn = "CustomerKey",
			},
		]);
		var principal = Table("Customers", [Column("CustomerKey", DbColumnType.Guid, isPrimaryKey: true)]);

		var result = RelationshipKeyPairDetector.Detect(dependent, principal);

		var pair = Assert.Single(result);
		Assert.Equal("BuyerKey", pair.DependentColumn);
		Assert.Equal("CustomerKey", pair.PrincipalColumn);
	}

	[Fact]
	public void Detect_MismatchedTypes_DoesNotSuggestPair()
	{
		var dependent = Table("Orders", Column("CustomerId", DbColumnType.String));
		var principal = Table("Customers", Column("Id", DbColumnType.Int32, isPrimaryKey: true));

		Assert.Empty(RelationshipKeyPairDetector.Detect(dependent, principal));
	}

	private static DatabaseTableDetail Table(
		string name,
		IReadOnlyList<TableColumn> columns,
		IReadOnlyList<ForeignKey>? foreignKeys = null)
		=> new()
		{
			Name = name,
			Columns = columns,
			ForeignKeys = foreignKeys ?? [],
		};

	private static DatabaseTableDetail Table(string name, params TableColumn[] columns)
		=> Table(name, (IReadOnlyList<TableColumn>)columns);

	private static TableColumn Column(
		string name,
		DbColumnType genericType,
		bool isPrimaryKey = false)
		=> new()
		{
			Name = name,
			DataType = genericType.ToString(),
			GenericType = genericType,
			IsNullable = false,
			IsPrimaryKey = isPrimaryKey,
			IsIdentity = false,
		};
}

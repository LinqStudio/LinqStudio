using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.SQLite;
using LinqStudio.Databases.Tests.Fixtures;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for SQLite type mapping to ensure all SQLite types map correctly to generic DbColumnType.
/// </summary>
public class SqliteTypeMapperTests : IClassFixture<SqliteDatabaseFixture>
{
	private readonly SqliteGenerator _generator;

	public SqliteTypeMapperTests(SqliteDatabaseFixture fixture)
	{
		_generator = new SqliteGenerator(fixture.DbContext.Database);
	}

	[Theory]
	[InlineData("INTEGER", DbColumnType.Int32)]
	[InlineData("integer", DbColumnType.Int32)]
	[InlineData("INT", DbColumnType.Int32)]
	[InlineData("int", DbColumnType.Int32)]
	public void MapToGenericType_IntegerTypes_ReturnsInt32(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("TINYINT", DbColumnType.SByte)]
	[InlineData("tinyint", DbColumnType.SByte)]
	public void MapToGenericType_TinyInt_ReturnsSByte(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("SMALLINT", DbColumnType.Int16)]
	[InlineData("smallint", DbColumnType.Int16)]
	public void MapToGenericType_SmallInt_ReturnsInt16(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("BIGINT", DbColumnType.Int64)]
	[InlineData("bigint", DbColumnType.Int64)]
	public void MapToGenericType_BigInt_ReturnsInt64(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("TEXT", DbColumnType.String)]
	[InlineData("text", DbColumnType.String)]
	[InlineData("VARCHAR", DbColumnType.String)]
	[InlineData("varchar", DbColumnType.String)]
	[InlineData("CHAR", DbColumnType.String)]
	[InlineData("char", DbColumnType.String)]
	[InlineData("CLOB", DbColumnType.String)]
	[InlineData("clob", DbColumnType.String)]
	[InlineData("VARCHAR(100)", DbColumnType.String)]
	[InlineData("CHARACTER(50)", DbColumnType.String)]
	public void MapToGenericType_StringTypes_ReturnsString(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("BLOB", DbColumnType.Binary)]
	[InlineData("blob", DbColumnType.Binary)]
	public void MapToGenericType_Blob_ReturnsBinary(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("REAL", DbColumnType.Double)]
	[InlineData("real", DbColumnType.Double)]
	[InlineData("DOUBLE", DbColumnType.Double)]
	[InlineData("double", DbColumnType.Double)]
	public void MapToGenericType_RealTypes_ReturnsDouble(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("FLOAT", DbColumnType.Float)]
	[InlineData("float", DbColumnType.Float)]
	public void MapToGenericType_Float_ReturnsFloat(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("NUMERIC", DbColumnType.Decimal)]
	[InlineData("numeric", DbColumnType.Decimal)]
	[InlineData("DECIMAL", DbColumnType.Decimal)]
	[InlineData("decimal", DbColumnType.Decimal)]
	[InlineData("DECIMAL(10,2)", DbColumnType.Decimal)]
	[InlineData("MONEY", DbColumnType.Decimal)]
	public void MapToGenericType_DecimalTypes_ReturnsDecimal(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("DATETIME", DbColumnType.DateTime)]
	[InlineData("datetime", DbColumnType.DateTime)]
	[InlineData("DATE", DbColumnType.DateTime)]
	[InlineData("date", DbColumnType.DateTime)]
	[InlineData("TIMESTAMP", DbColumnType.DateTime)]
	public void MapToGenericType_DateTimeTypes_ReturnsDateTime(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("TIME", DbColumnType.TimeSpan)]
	[InlineData("time", DbColumnType.TimeSpan)]
	public void MapToGenericType_Time_ReturnsTimeSpan(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("BOOLEAN", DbColumnType.Boolean)]
	[InlineData("boolean", DbColumnType.Boolean)]
	[InlineData("BOOL", DbColumnType.Boolean)]
	[InlineData("bool", DbColumnType.Boolean)]
	public void MapToGenericType_Boolean_ReturnsBoolean(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("GUID", DbColumnType.Guid)]
	[InlineData("guid", DbColumnType.Guid)]
	[InlineData("UUID", DbColumnType.Guid)]
	[InlineData("uuid", DbColumnType.Guid)]
	public void MapToGenericType_Guid_ReturnsGuid(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("unknown_type", DbColumnType.Unknown)]
	[InlineData("CUSTOM", DbColumnType.Unknown)]
	public void MapToGenericType_UnknownTypes_ReturnsUnknown(string sqliteType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqliteType);
		Assert.Equal(expected, result);
	}
}

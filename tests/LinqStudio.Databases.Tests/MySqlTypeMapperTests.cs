using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for MySQL type mapping to ensure all MySQL types map correctly to generic DbColumnType.
/// </summary>
public class MySqlTypeMapperTests : IClassFixture<MySqlDatabaseFixture>
{
	private readonly MySqlGenerator _generator;

	public MySqlTypeMapperTests(MySqlDatabaseFixture fixture)
	{
		_generator = new MySqlGenerator(fixture.DbContext.Database.GetDbConnection());
	}

	[Theory]
	[InlineData("bool", DbColumnType.Boolean)]
	[InlineData("boolean", DbColumnType.Boolean)]
	[InlineData("BOOL", DbColumnType.Boolean)]
	public void MapToGenericType_BooleanTypes_ReturnsBoolean(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("tinyint", DbColumnType.SByte)]
	[InlineData("TINYINT", DbColumnType.SByte)]
	public void MapToGenericType_TinyInt_ReturnsSByte(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("tinyint unsigned", DbColumnType.Byte)]
	[InlineData("TINYINT UNSIGNED", DbColumnType.Byte)]
	public void MapToGenericType_TinyIntUnsigned_ReturnsByte(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("smallint", DbColumnType.Int16)]
	[InlineData("SMALLINT", DbColumnType.Int16)]
	public void MapToGenericType_SmallInt_ReturnsInt16(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("smallint unsigned", DbColumnType.UInt16)]
	[InlineData("SMALLINT UNSIGNED", DbColumnType.UInt16)]
	public void MapToGenericType_SmallIntUnsigned_ReturnsUInt16(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("mediumint", DbColumnType.Int32)]
	[InlineData("int", DbColumnType.Int32)]
	[InlineData("integer", DbColumnType.Int32)]
	[InlineData("INT", DbColumnType.Int32)]
	public void MapToGenericType_Int_ReturnsInt32(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("mediumint unsigned", DbColumnType.UInt32)]
	[InlineData("int unsigned", DbColumnType.UInt32)]
	[InlineData("integer unsigned", DbColumnType.UInt32)]
	[InlineData("INT UNSIGNED", DbColumnType.UInt32)]
	public void MapToGenericType_IntUnsigned_ReturnsUInt32(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bigint", DbColumnType.Int64)]
	[InlineData("BIGINT", DbColumnType.Int64)]
	public void MapToGenericType_BigInt_ReturnsInt64(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bigint unsigned", DbColumnType.UInt64)]
	[InlineData("BIGINT UNSIGNED", DbColumnType.UInt64)]
	public void MapToGenericType_BigIntUnsigned_ReturnsUInt64(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("float", DbColumnType.Float)]
	[InlineData("FLOAT", DbColumnType.Float)]
	public void MapToGenericType_Float_ReturnsFloat(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("double", DbColumnType.Double)]
	[InlineData("double precision", DbColumnType.Double)]
	[InlineData("real", DbColumnType.Double)]
	[InlineData("DOUBLE", DbColumnType.Double)]
	public void MapToGenericType_Double_ReturnsDouble(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("decimal", DbColumnType.Decimal)]
	[InlineData("numeric", DbColumnType.Decimal)]
	[InlineData("dec", DbColumnType.Decimal)]
	[InlineData("fixed", DbColumnType.Decimal)]
	[InlineData("DECIMAL", DbColumnType.Decimal)]
	public void MapToGenericType_DecimalTypes_ReturnsDecimal(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("char", DbColumnType.String)]
	[InlineData("varchar", DbColumnType.String)]
	[InlineData("tinytext", DbColumnType.String)]
	[InlineData("text", DbColumnType.String)]
	[InlineData("mediumtext", DbColumnType.String)]
	[InlineData("longtext", DbColumnType.String)]
	[InlineData("enum", DbColumnType.String)]
	[InlineData("set", DbColumnType.String)]
	[InlineData("CHAR", DbColumnType.String)]
	[InlineData("VARCHAR", DbColumnType.String)]
	[InlineData("TEXT", DbColumnType.String)]
	public void MapToGenericType_StringTypes_ReturnsString(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("date", DbColumnType.DateTime)]
	[InlineData("datetime", DbColumnType.DateTime)]
	[InlineData("timestamp", DbColumnType.DateTime)]
	[InlineData("year", DbColumnType.DateTime)]
	[InlineData("DATE", DbColumnType.DateTime)]
	[InlineData("DATETIME", DbColumnType.DateTime)]
	public void MapToGenericType_DateTimeTypes_ReturnsDateTime(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("time", DbColumnType.TimeSpan)]
	[InlineData("TIME", DbColumnType.TimeSpan)]
	public void MapToGenericType_Time_ReturnsTimeSpan(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("binary", DbColumnType.Binary)]
	[InlineData("varbinary", DbColumnType.Binary)]
	[InlineData("tinyblob", DbColumnType.Binary)]
	[InlineData("blob", DbColumnType.Binary)]
	[InlineData("mediumblob", DbColumnType.Binary)]
	[InlineData("longblob", DbColumnType.Binary)]
	[InlineData("bit", DbColumnType.Binary)]
	[InlineData("BINARY", DbColumnType.Binary)]
	[InlineData("BLOB", DbColumnType.Binary)]
	public void MapToGenericType_BinaryTypes_ReturnsBinary(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("json", DbColumnType.Json)]
	[InlineData("JSON", DbColumnType.Json)]
	public void MapToGenericType_Json_ReturnsJson(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("geometry", DbColumnType.Binary)]
	[InlineData("point", DbColumnType.Binary)]
	[InlineData("linestring", DbColumnType.Binary)]
	[InlineData("polygon", DbColumnType.Binary)]
	[InlineData("multipoint", DbColumnType.Binary)]
	[InlineData("multilinestring", DbColumnType.Binary)]
	[InlineData("multipolygon", DbColumnType.Binary)]
	[InlineData("geometrycollection", DbColumnType.Binary)]
	public void MapToGenericType_GeometricTypes_ReturnsBinary(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("unknown_type", DbColumnType.Unknown)]
	public void MapToGenericType_UnknownTypes_ReturnsUnknown(string mysqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(mysqlType);
		Assert.Equal(expected, result);
	}
}

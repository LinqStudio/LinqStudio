using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.MSSQL;
using LinqStudio.Databases.Tests.Fixtures;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for MSSQL type mapping to ensure all SQL Server types map correctly to generic DbColumnType.
/// </summary>
public class MssqlTypeMapperTests : IClassFixture<MssqlDatabaseFixture>
{
	private readonly MssqlGenerator _generator;

	public MssqlTypeMapperTests(MssqlDatabaseFixture fixture)
	{
		_generator = new MssqlGenerator(fixture.DbContext.Database);
	}

	[Theory]
	[InlineData("bit", DbColumnType.Boolean)]
	public void MapToGenericType_BooleanTypes_ReturnsBoolean(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("tinyint", DbColumnType.SByte)]
	[InlineData("TINYINT", DbColumnType.SByte)]
	public void MapToGenericType_TinyInt_ReturnsSByte(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("smallint", DbColumnType.Int16)]
	[InlineData("SMALLINT", DbColumnType.Int16)]
	public void MapToGenericType_SmallInt_ReturnsInt16(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("int", DbColumnType.Int32)]
	[InlineData("INT", DbColumnType.Int32)]
	public void MapToGenericType_Int_ReturnsInt32(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bigint", DbColumnType.Int64)]
	[InlineData("BIGINT", DbColumnType.Int64)]
	public void MapToGenericType_BigInt_ReturnsInt64(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("real", DbColumnType.Float)]
	[InlineData("REAL", DbColumnType.Float)]
	public void MapToGenericType_Real_ReturnsFloat(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("float", DbColumnType.Double)]
	[InlineData("FLOAT", DbColumnType.Double)]
	public void MapToGenericType_Float_ReturnsDouble(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("decimal", DbColumnType.Decimal)]
	[InlineData("numeric", DbColumnType.Decimal)]
	[InlineData("money", DbColumnType.Decimal)]
	[InlineData("smallmoney", DbColumnType.Decimal)]
	[InlineData("DECIMAL", DbColumnType.Decimal)]
	[InlineData("NUMERIC", DbColumnType.Decimal)]
	public void MapToGenericType_DecimalTypes_ReturnsDecimal(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("char", DbColumnType.String)]
	[InlineData("nchar", DbColumnType.String)]
	[InlineData("varchar", DbColumnType.String)]
	[InlineData("nvarchar", DbColumnType.String)]
	[InlineData("text", DbColumnType.String)]
	[InlineData("ntext", DbColumnType.String)]
	[InlineData("CHAR", DbColumnType.String)]
	[InlineData("VARCHAR", DbColumnType.String)]
	[InlineData("NVARCHAR", DbColumnType.String)]
	public void MapToGenericType_StringTypes_ReturnsString(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("date", DbColumnType.DateTime)]
	[InlineData("datetime", DbColumnType.DateTime)]
	[InlineData("datetime2", DbColumnType.DateTime)]
	[InlineData("smalldatetime", DbColumnType.DateTime)]
	[InlineData("DATE", DbColumnType.DateTime)]
	[InlineData("DATETIME", DbColumnType.DateTime)]
	public void MapToGenericType_DateTimeTypes_ReturnsDateTime(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("time", DbColumnType.TimeSpan)]
	[InlineData("TIME", DbColumnType.TimeSpan)]
	public void MapToGenericType_Time_ReturnsTimeSpan(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("datetimeoffset", DbColumnType.DateTimeOffset)]
	[InlineData("DATETIMEOFFSET", DbColumnType.DateTimeOffset)]
	public void MapToGenericType_DateTimeOffset_ReturnsDateTimeOffset(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("uniqueidentifier", DbColumnType.Guid)]
	[InlineData("UNIQUEIDENTIFIER", DbColumnType.Guid)]
	public void MapToGenericType_UniqueIdentifier_ReturnsGuid(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("binary", DbColumnType.Binary)]
	[InlineData("varbinary", DbColumnType.Binary)]
	[InlineData("image", DbColumnType.Binary)]
	[InlineData("timestamp", DbColumnType.Binary)]
	[InlineData("rowversion", DbColumnType.Binary)]
	[InlineData("BINARY", DbColumnType.Binary)]
	[InlineData("VARBINARY", DbColumnType.Binary)]
	public void MapToGenericType_BinaryTypes_ReturnsBinary(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("xml", DbColumnType.Xml)]
	[InlineData("XML", DbColumnType.Xml)]
	public void MapToGenericType_Xml_ReturnsXml(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("geography", DbColumnType.Binary)]
	[InlineData("geometry", DbColumnType.Binary)]
	[InlineData("hierarchyid", DbColumnType.Binary)]
	public void MapToGenericType_SpatialTypes_ReturnsBinary(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("sql_variant", DbColumnType.Unknown)]
	[InlineData("unknown_type", DbColumnType.Unknown)]
	public void MapToGenericType_UnknownTypes_ReturnsUnknown(string sqlType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(sqlType);
		Assert.Equal(expected, result);
	}
}

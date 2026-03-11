using LinqStudio.Abstractions.Models;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.Tests.Fixtures;

namespace LinqStudio.Databases.Tests;

/// <summary>
/// Tests for PostgreSQL type mapping to ensure all PostgreSQL types map correctly to generic DbColumnType.
/// </summary>
public class PostgreSqlTypeMapperTests : IClassFixture<PostgreSqlDatabaseFixture>
{
	private readonly PostgreSqlGenerator _generator;

	public PostgreSqlTypeMapperTests(PostgreSqlDatabaseFixture fixture)
	{
		_generator = new PostgreSqlGenerator(fixture.DbContext.Database);
	}

	[Theory]
	[InlineData("boolean", DbColumnType.Boolean)]
	[InlineData("bool", DbColumnType.Boolean)]
	[InlineData("BOOLEAN", DbColumnType.Boolean)]
	public void MapToGenericType_BooleanTypes_ReturnsBoolean(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("smallint", DbColumnType.Int16)]
	[InlineData("int2", DbColumnType.Int16)]
	[InlineData("smallserial", DbColumnType.Int16)]
	[InlineData("serial2", DbColumnType.Int16)]
	[InlineData("SMALLINT", DbColumnType.Int16)]
	public void MapToGenericType_SmallInt_ReturnsInt16(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("integer", DbColumnType.Int32)]
	[InlineData("int", DbColumnType.Int32)]
	[InlineData("int4", DbColumnType.Int32)]
	[InlineData("serial", DbColumnType.Int32)]
	[InlineData("serial4", DbColumnType.Int32)]
	[InlineData("INTEGER", DbColumnType.Int32)]
	public void MapToGenericType_Int_ReturnsInt32(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bigint", DbColumnType.Int64)]
	[InlineData("int8", DbColumnType.Int64)]
	[InlineData("bigserial", DbColumnType.Int64)]
	[InlineData("serial8", DbColumnType.Int64)]
	[InlineData("BIGINT", DbColumnType.Int64)]
	public void MapToGenericType_BigInt_ReturnsInt64(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("real", DbColumnType.Float)]
	[InlineData("float4", DbColumnType.Float)]
	[InlineData("REAL", DbColumnType.Float)]
	public void MapToGenericType_Real_ReturnsFloat(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("double precision", DbColumnType.Double)]
	[InlineData("float8", DbColumnType.Double)]
	[InlineData("DOUBLE PRECISION", DbColumnType.Double)]
	public void MapToGenericType_Double_ReturnsDouble(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("numeric", DbColumnType.Decimal)]
	[InlineData("decimal", DbColumnType.Decimal)]
	[InlineData("money", DbColumnType.Decimal)]
	[InlineData("NUMERIC", DbColumnType.Decimal)]
	[InlineData("DECIMAL", DbColumnType.Decimal)]
	public void MapToGenericType_DecimalTypes_ReturnsDecimal(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("character varying", DbColumnType.String)]
	[InlineData("varchar", DbColumnType.String)]
	[InlineData("character", DbColumnType.String)]
	[InlineData("char", DbColumnType.String)]
	[InlineData("text", DbColumnType.String)]
	[InlineData("name", DbColumnType.String)]
	[InlineData("VARCHAR", DbColumnType.String)]
	[InlineData("TEXT", DbColumnType.String)]
	public void MapToGenericType_StringTypes_ReturnsString(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("timestamp", DbColumnType.DateTime)]
	[InlineData("timestamp without time zone", DbColumnType.DateTime)]
	[InlineData("date", DbColumnType.DateTime)]
	[InlineData("TIMESTAMP", DbColumnType.DateTime)]
	[InlineData("DATE", DbColumnType.DateTime)]
	public void MapToGenericType_DateTimeTypes_ReturnsDateTime(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("timestamp with time zone", DbColumnType.DateTimeOffset)]
	[InlineData("timestamptz", DbColumnType.DateTimeOffset)]
	public void MapToGenericType_DateTimeOffset_ReturnsDateTimeOffset(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("time", DbColumnType.TimeSpan)]
	[InlineData("time without time zone", DbColumnType.TimeSpan)]
	[InlineData("interval", DbColumnType.TimeSpan)]
	[InlineData("TIME", DbColumnType.TimeSpan)]
	public void MapToGenericType_TimeSpan_ReturnsTimeSpan(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("uuid", DbColumnType.Guid)]
	[InlineData("UUID", DbColumnType.Guid)]
	public void MapToGenericType_Uuid_ReturnsGuid(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bytea", DbColumnType.Binary)]
	[InlineData("BYTEA", DbColumnType.Binary)]
	public void MapToGenericType_Bytea_ReturnsBinary(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("xml", DbColumnType.Xml)]
	[InlineData("XML", DbColumnType.Xml)]
	public void MapToGenericType_Xml_ReturnsXml(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("json", DbColumnType.Json)]
	[InlineData("jsonb", DbColumnType.Json)]
	[InlineData("JSON", DbColumnType.Json)]
	[InlineData("JSONB", DbColumnType.Json)]
	public void MapToGenericType_Json_ReturnsJson(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("inet", DbColumnType.String)]
	[InlineData("cidr", DbColumnType.String)]
	[InlineData("macaddr", DbColumnType.String)]
	[InlineData("macaddr8", DbColumnType.String)]
	public void MapToGenericType_NetworkTypes_ReturnsString(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("bit", DbColumnType.Binary)]
	[InlineData("bit varying", DbColumnType.Binary)]
	public void MapToGenericType_BitTypes_ReturnsBinary(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("tsvector", DbColumnType.String)]
	[InlineData("tsquery", DbColumnType.String)]
	public void MapToGenericType_TextSearchTypes_ReturnsString(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("point", DbColumnType.Binary)]
	[InlineData("line", DbColumnType.Binary)]
	[InlineData("lseg", DbColumnType.Binary)]
	[InlineData("box", DbColumnType.Binary)]
	[InlineData("path", DbColumnType.Binary)]
	[InlineData("polygon", DbColumnType.Binary)]
	[InlineData("circle", DbColumnType.Binary)]
	public void MapToGenericType_GeometricTypes_ReturnsBinary(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("int4range", DbColumnType.String)]
	[InlineData("int8range", DbColumnType.String)]
	[InlineData("numrange", DbColumnType.String)]
	[InlineData("tsrange", DbColumnType.String)]
	[InlineData("tstzrange", DbColumnType.String)]
	[InlineData("daterange", DbColumnType.String)]
	public void MapToGenericType_RangeTypes_ReturnsString(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData("integer[]", DbColumnType.Unknown)]
	[InlineData("text[]", DbColumnType.Unknown)]
	[InlineData("user-defined", DbColumnType.Unknown)]
	[InlineData("unknown_type", DbColumnType.Unknown)]
	public void MapToGenericType_UnknownTypes_ReturnsUnknown(string pgType, DbColumnType expected)
	{
		var result = _generator.MapToGenericType(pgType);
		Assert.Equal(expected, result);
	}
}

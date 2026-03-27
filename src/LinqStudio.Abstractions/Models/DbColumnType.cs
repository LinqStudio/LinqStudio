namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Generic database column type that maps to C# types.
/// Abstracts database-specific types (e.g., nvarchar, varchar -> String).
/// </summary>
public enum DbColumnType
{
	/// <summary>
	/// Unknown or unsupported type.
	/// </summary>
	Unknown,

	/// <summary>
	/// Boolean type (C# bool).
	/// Maps to: SQL Server bit, PostgreSQL boolean, MySQL tinyint(1), SQLite boolean/integer.
	/// </summary>
	Boolean,

	/// <summary>
	/// 8-bit signed integer (C# sbyte).
	/// Maps to: SQL Server tinyint, PostgreSQL "char".
	/// </summary>
	SByte,

	/// <summary>
	/// 8-bit unsigned integer (C# byte).
	/// Maps to: MySQL tinyint unsigned.
	/// </summary>
	Byte,

	/// <summary>
	/// 16-bit signed integer (C# short).
	/// Maps to: SQL Server smallint, PostgreSQL smallint, MySQL smallint, SQLite (stored as integer).
	/// </summary>
	Int16,

	/// <summary>
	/// 16-bit unsigned integer (C# ushort).
	/// Maps to: MySQL smallint unsigned.
	/// </summary>
	UInt16,

	/// <summary>
	/// 32-bit signed integer (C# int).
	/// Maps to: SQL Server int, PostgreSQL integer, MySQL int, SQLite integer.
	/// </summary>
	Int32,

	/// <summary>
	/// 32-bit unsigned integer (C# uint).
	/// Maps to: MySQL int unsigned.
	/// </summary>
	UInt32,

	/// <summary>
	/// 64-bit signed integer (C# long).
	/// Maps to: SQL Server bigint, PostgreSQL bigint, MySQL bigint, SQLite integer.
	/// </summary>
	Int64,

	/// <summary>
	/// 64-bit unsigned integer (C# ulong).
	/// Maps to: MySQL bigint unsigned.
	/// </summary>
	UInt64,

	/// <summary>
	/// Single-precision floating point (C# float).
	/// Maps to: SQL Server real, PostgreSQL real, MySQL float, SQLite real.
	/// </summary>
	Float,

	/// <summary>
	/// Double-precision floating point (C# double).
	/// Maps to: SQL Server float, PostgreSQL double precision, MySQL double, SQLite real.
	/// </summary>
	Double,

	/// <summary>
	/// Fixed-precision decimal (C# decimal).
	/// Maps to: SQL Server decimal/numeric/money, PostgreSQL decimal/numeric/money, MySQL decimal/numeric, SQLite (stored as text or real).
	/// </summary>
	Decimal,

	/// <summary>
	/// String type (C# string).
	/// Maps to: SQL Server varchar/nvarchar/char/nchar/text/ntext, PostgreSQL varchar/char/text, MySQL varchar/char/text, SQLite text.
	/// </summary>
	String,

	/// <summary>
	/// Date and time type (C# DateTime).
	/// Maps to: SQL Server datetime/datetime2/smalldatetime/date, PostgreSQL timestamp/date, MySQL datetime/timestamp/date, SQLite text/real/integer.
	/// </summary>
	DateTime,

	/// <summary>
	/// Time span type (C# TimeSpan).
	/// Maps to: SQL Server time, PostgreSQL time/interval, MySQL time.
	/// </summary>
	TimeSpan,

	/// <summary>
	/// Date and time with timezone (C# DateTimeOffset).
	/// Maps to: SQL Server datetimeoffset, PostgreSQL timestamptz.
	/// </summary>
	DateTimeOffset,

	/// <summary>
	/// Globally unique identifier (C# Guid).
	/// Maps to: SQL Server uniqueidentifier, PostgreSQL uuid, MySQL char(36)/binary(16), SQLite text/blob.
	/// </summary>
	Guid,

	/// <summary>
	/// Binary data (C# byte[]).
	/// Maps to: SQL Server binary/varbinary/image, PostgreSQL bytea, MySQL binary/varbinary/blob, SQLite blob.
	/// </summary>
	Binary,

	/// <summary>
	/// XML data (C# string).
	/// Maps to: SQL Server xml, PostgreSQL xml.
	/// </summary>
	Xml,

	/// <summary>
	/// JSON data (C# string).
	/// Maps to: PostgreSQL json/jsonb, MySQL json.
	/// </summary>
	Json
}

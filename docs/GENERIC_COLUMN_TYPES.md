# Generic Database Column Types

## Overview

LinqStudio implements a generic column type system that maps database-specific types to common C# types. This abstraction makes it easier to generate C# code from database schemas without dealing with vendor-specific type names.

## DbColumnType Enum

The `DbColumnType` enum (in `LinqStudio.Abstractions.Models`) defines generic types that map one-to-one with C# types:

- `Boolean` → `bool`
- `SByte`, `Byte` → `sbyte`, `byte`
- `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, `UInt64` → `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- `Float`, `Double`, `Decimal` → `float`, `double`, `decimal`
- `String` → `string`
- `DateTime`, `DateTimeOffset`, `TimeSpan` → `DateTime`, `DateTimeOffset`, `TimeSpan`
- `Guid` → `Guid`
- `Binary` → `byte[]`
- `Xml`, `Json` → `string` (specialized)
- `Unknown` → unsupported or unmappable types

## Type Mapping

Each database generator implements the abstract `MapToGenericType(string dataType)` method in `AdoNetDatabaseGeneratorBase` to convert database-specific type names to `DbColumnType`.

### MSSQL Type Mappings

- `bit` → `Boolean`
- `tinyint` → `SByte`
- `smallint` → `Int16`
- `int` → `Int32`
- `bigint` → `Int64`
- `real` → `Float`
- `float` → `Double`
- `decimal`, `numeric`, `money`, `smallmoney` → `Decimal`
- `char`, `nchar`, `varchar`, `nvarchar`, `text`, `ntext` → `String`
- `date`, `datetime`, `datetime2`, `smalldatetime` → `DateTime`
- `time` → `TimeSpan`
- `datetimeoffset` → `DateTimeOffset`
- `uniqueidentifier` → `Guid`
- `binary`, `varbinary`, `image`, `timestamp`, `rowversion` → `Binary`
- `xml` → `Xml`
- `geography`, `geometry`, `hierarchyid` → `Binary`

### PostgreSQL Type Mappings

- `boolean`, `bool` → `Boolean`
- `smallint`, `int2` → `Int16`
- `integer`, `int`, `int4` → `Int32`
- `bigint`, `int8` → `Int64`
- `real`, `float4` → `Float`
- `double precision`, `float8` → `Double`
- `numeric`, `decimal`, `money` → `Decimal`
- `varchar`, `char`, `text` → `String`
- `timestamp`, `date` → `DateTime`
- `timestamptz` → `DateTimeOffset`
- `time`, `interval` → `TimeSpan`
- `uuid` → `Guid`
- `bytea` → `Binary`
- `xml` → `Xml`
- `json`, `jsonb` → `Json`

### MySQL Type Mappings

- `bool`, `boolean` → `Boolean`
- `tinyint` → `SByte`
- `tinyint unsigned` → `Byte`
- `smallint` → `Int16`
- `smallint unsigned` → `UInt16`
- `int`, `integer`, `mediumint` → `Int32`
- `int unsigned` → `UInt32`
- `bigint` → `Int64`
- `bigint unsigned` → `UInt64`
- `float` → `Float`
- `double`, `real` → `Double`
- `decimal`, `numeric` → `Decimal`
- `char`, `varchar`, `text`, `enum`, `set` → `String`
- `date`, `datetime`, `timestamp`, `year` → `DateTime`
- `time` → `TimeSpan`
- `binary`, `varbinary`, `blob` → `Binary`
- `json` → `Json`

### SQLite Type Mappings

SQLite uses type affinity, so mappings are based on type name patterns:

- Types containing `int` → `Int32` (with size hints for `tinyint`, `smallint`, `bigint`)
- Types containing `char`, `text`, `clob` → `String`
- Types containing `blob` → `Binary`
- Types containing `real`, `floa`, `doub` → `Double` or `Float`
- Types containing `numeric`, `decimal` → `Decimal`
- Types containing `date` or `time` → `DateTime` or `TimeSpan`
- Types containing `bool` → `Boolean`
- Types containing `guid`, `uuid` → `Guid`

## Usage

The `GenericType` property on `TableColumn` is automatically populated when retrieving table schemas:

```csharp
var generator = new MssqlGenerator(database);
var table = await generator.GetTableAsync("MyTable");

foreach (var column in table.Columns)
{
    Console.WriteLine($"{column.Name}: {column.DataType} → {column.GenericType}");
    // Example: "Id: int → Int32"
    // Example: "Name: nvarchar → String"
}
```

## Testing

Comprehensive unit tests validate all type mappings for each database:

- `MssqlTypeMapperTests` - 60+ tests covering all SQL Server types
- `PostgreSqlTypeMapperTests` - 70+ tests covering all PostgreSQL types
- `MySqlTypeMapperTests` - 80+ tests covering all MySQL types
- `SqliteTypeMapperTests` - 50+ tests covering SQLite type affinity

Run tests: `dotnet test --filter "FullyQualifiedName~TypeMapperTests"`

## Extending for New Databases

To add support for a new database:

1. Create a new generator inheriting from `AdoNetDatabaseGeneratorBase`
2. Implement the abstract `MapToGenericType(string dataType)` method
3. Map database-specific types to `DbColumnType` values
4. Add comprehensive unit tests for all type mappings

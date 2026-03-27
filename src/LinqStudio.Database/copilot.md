# LinqStudio.Databases

This project contains database-specific code for generating metadata queries (tables, columns, schemas, foreign keys) for different database types.

## Architecture

All database generators inherit from `AdoNetDatabaseGeneratorBase` which uses raw ADO.NET (`DbConnection`) rather than EF Core's `DatabaseFacade`. This design choice provides:

- **Simplicity:** Only ADO.NET functionality is needed for metadata queries
- **Flexibility:** Generators can be created from connection strings or existing connections
- **Independence:** No dependency on EF Core infrastructure in the generators themselves

## Database Generators

- **MssqlGenerator** - Microsoft SQL Server support
- **MySqlGenerator** - MySQL/MariaDB support  
- **PostgreSqlGenerator** - PostgreSQL support
- **SqliteGenerator** - SQLite support (overrides GetTablesAsync with SQLite-specific query)

## Usage Pattern

### From Connection String
```csharp
var generator = MssqlGenerator.Create(connectionString);
var tables = await generator.GetTablesAsync();
```

### From DbContext
```csharp
var generator = new MssqlGenerator(dbContext.Database.GetDbConnection());
var tables = await generator.GetTablesAsync();
```

### From Raw Connection
```csharp
using var connection = new SqlConnection(connectionString);
var generator = new MssqlGenerator(connection);
var tables = await generator.GetTablesAsync();
```

## Connection Lifecycle

All generators follow a consistent pattern for managing connection state:

1. Check if connection is already open
2. Open connection if needed
3. Execute query
4. Close connection only if we opened it (in finally block)

This ensures generators can work with both new connections and existing open connections without interfering with the caller's connection management.

## Key Implementation Details

- **Base class property:** `protected DbConnection Connection` - raw ADO.NET connection
- **Factory methods:** `Create(string connectionString)` static methods for easy instantiation
- **Table metadata:** Uses ADO.NET `GetSchemaAsync("Tables")` or database-specific queries
- **Column metadata:** Database-specific queries against INFORMATION_SCHEMA or system catalogs
- **Foreign keys:** Database-specific queries (SQL Server uses sys.foreign_keys, others use INFORMATION_SCHEMA)

## MSSQL: System Table Filtering

`MssqlGenerator` overrides `GetTablesAsync()` with a direct SQL query using `OBJECTPROPERTY(..., 'IsMSShipped') = 0` to filter out all Microsoft-shipped system objects. This prevents system tables (`spt_*`, `MS*`, replication/CDC objects) from appearing in the database explorer — even when accidentally connecting to the `master` database.

The query used:
```sql
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND OBJECTPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), 'IsMSShipped') = 0
ORDER BY TABLE_SCHEMA, TABLE_NAME
```

## Recent Changes (2025-01-09)

Refactored from using `DatabaseFacade` to `DbConnection` throughout to fix compilation errors and simplify the architecture. See `.squad/decisions/inbox/simon-dbconnection-fix.md` for details.

# Simon's History

## Learnings

### 2025-01-09: Fixed DbConnection vs DatabaseFacade Mismatch

**Problem:**
The database generator code had a mismatch between `DbConnection` (ADO.NET) and `DatabaseFacade` (EF Core abstraction). 

- `AdoNetDatabaseGeneratorBase` was accepting a `DatabaseFacade` parameter but immediately calling `Database.GetDbConnection()` to get the underlying raw `DbConnection`
- Some methods in the base and derived classes were trying to use `DbConnection` as a type/property name instead of an instance
- `TestConnectionAsync` in the base class incorrectly referenced `DbConnection` directly
- Derived classes (`MssqlGenerator`, `MySqlGenerator`, `PostgreSqlGenerator`, `SqliteGenerator`) were inconsistent - some constructors expected `DbConnection`, others expected `DatabaseFacade`

**Solution:**
Refactored the entire hierarchy to consistently use `DbConnection`:

1. Changed `AdoNetDatabaseGeneratorBase` constructor to accept `DbConnection` instead of `DatabaseFacade`
2. Changed protected property from `Database` (DatabaseFacade) to `Connection` (DbConnection)
3. Updated `GetTablesAsync` and `TestConnectionAsync` to use `Connection` directly instead of calling `Database.GetDbConnection()`
4. Updated all derived classes to accept `DbConnection` in their constructors
5. Updated all `GetTableAsync` methods in derived classes to use `Connection` instead of `Database.GetDbConnection()`
6. Removed unnecessary EF Core using directives (Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Infrastructure) from files that don't need them
7. Restored static `Create` factory methods in `MssqlGenerator` and `MySqlGenerator` that accept connection strings for direct instantiation

**Rationale:**
The generators only need raw ADO.NET functionality to query database metadata (tables, columns, foreign keys). They don't need the full EF Core `DatabaseFacade` abstraction. Working directly with `DbConnection` simplifies the code, removes unnecessary dependencies, and allows creating generators from connection strings without requiring a full DbContext.

**Pattern to follow:**
When implementing database generators:
- Accept `DbConnection` in constructor
- Store it in protected `Connection` property
- Always check if connection is already open before opening it
- Close connection in finally block only if we opened it
- All methods follow the pattern: check state → open if needed → execute → close if we opened

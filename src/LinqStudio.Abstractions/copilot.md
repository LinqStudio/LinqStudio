# LinqStudio.Abstractions Notes

## Interfaces (`Abstractions/`)

- `IDatabaseQueryGenerator` — live DB introspection (tables, columns, foreign keys)
- `IDbContextGenerator` — generates EF Core C# model files + DbContext from a live schema via `IDatabaseQueryGenerator`. Returns `DbContextGeneratorResult`.
- `IUserSettingsSection` — marker interface for auto-discovered user settings

## Services (`Services/`)

- `IQueryExecutionService` — executes user LINQ queries and returns results (rows, columns, timing, errors)

## Models (`Models/`)

- `DbContextGeneratorResult` — return type of `IDbContextGenerator.GenerateAsync()`: model files dict, DbContext code, context type name, namespace
- `QueryExecutionResult` — result of query execution with data rows, column names, elapsed time, and error info

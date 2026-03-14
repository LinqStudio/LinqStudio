# LinqStudio.Abstractions Notes

## Interfaces (`Abstractions/`)

- `IDatabaseQueryGenerator` — live DB introspection (tables, columns, foreign keys)
- `IDbContextGenerator` — generates EF Core C# model files + DbContext from a live schema via `IDatabaseQueryGenerator`. Returns `DbContextGeneratorResult`.
- `IUserSettingsSection` — marker interface for auto-discovered user settings

## Models (`Models/`)

- `DbContextGeneratorResult` — return type of `IDbContextGenerator.GenerateAsync()`: model files dict, DbContext code, context type name, namespace

# LinqStudio.Core Notes

## Bug Fixes

### QueryGenerator Property - Missing Database Type Cases (2026-03-11)
Fixed: `Project.cs` QueryGenerator property switch expression was missing PostgreSQL and SQLite cases, causing NotSupportedException at runtime. Added the missing cases to match the TestConnectionAsync implementation which already had all four database types properly handled.

## Services

### DbContextGenerator (2026-03-14)
`DbContextGenerator` implements `IDbContextGenerator` and converts live DB schema (via `IDatabaseQueryGenerator`) into C# model classes + `GeneratedDbContext`. Registered as `AddScoped<IDbContextGenerator, DbContextGenerator>()`. Used by `CompilerServiceFactory.CreateFromProjectAsync()` to power real IntelliSense against the user's actual database. Fixed namespace: `GeneratedModels`, fixed context type: `GeneratedDbContext`.

## Repositories

### FileSystemProjectRepository & FileSystemQueryRepository (2026-03-20)
`IProjectRepository` and `IQueryRepository` interfaces live in `LinqStudio.Core.Repositories` (moved from Blazor to avoid circular deps). `ProjectSummary` record lives in `LinqStudio.Core.Models`. Implementations:
- `FileSystemProjectRepository` — stores projects as `{BasePath}/{projectName}.linq` files. Project ID = file name without extension.
- `FileSystemQueryRepository` — stores queries in `{BasePath}/{projectId}.linq.queries/` directories.
- `FileSystemStorageOptions` — configures the `BasePath`.
- Registered via `services.AddFileSystemRepositories(basePath)` extension in `LinqStudio.Core.Extensions.ServiceCollectionExtensions`.
- WebServer configures basePath from `LinqStudio:ProjectsPath` config key, defaulting to `~/Documents/LinqStudio/Projects`.

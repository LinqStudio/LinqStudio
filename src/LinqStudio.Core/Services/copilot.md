# LinqStudio Core Services

## RoslynWorkspaceService
**NEW** - Singleton service that centralizes Roslyn workspace creation and query wrapping logic (extracted from CompilerService and QueryExecutionService).

### Key Features
- Creates `AdhocWorkspace` instances pre-configured with all EF Core metadata references
- Provides complete assembly list including SQL Server, SQLite, PostgreSQL, and MySQL providers
- Wraps user LINQ queries in `QueryContainer` class for Roslyn analysis and compilation
- Stateless and thread-safe - creates fresh workspaces on each call

### Public Methods
```csharp
// Create a new workspace with project and all metadata references
(AdhocWorkspace Workspace, ProjectId ProjectId, Solution Solution) CreateWorkspace(string projectName);

// Get complete list of metadata references (EF Core + DB providers)
IReadOnlyList<MetadataReference> GetMetadataReferences();

// Wrap user query in QueryContainer class
string WrapQuery(string userQuery, string contextTypeName, string projectNamespace, string beforeReturn = "return");
```

### Usage
Both `CompilerService` and `QueryExecutionService` now depend on `RoslynWorkspaceService`:
```csharp
// Injected via constructor
public CompilerService(string contextTypeName, string projectNamespace, RoslynWorkspaceService roslynWorkspaceService, ...)
{
    (_workspace, _projectId, _solution) = roslynWorkspaceService.CreateWorkspace("EFCoreModelsProject");
    // CompilerService manages its own document lifecycle and parse options
}

public QueryExecutionService(IDbContextGenerator generator, RoslynWorkspaceService roslynWorkspaceService, ...)
{
    // QueryExecutionService creates fresh workspace per compilation
    var (workspace, projectId, solution) = _roslynWorkspaceService.CreateWorkspace("QueryExecution");
}
```

### Assembly Loading Strategy
Uses the **comprehensive** assembly list from QueryExecutionService:
1. Priority assemblies (EF Core, providers, System.Linq): Try `AppDomain.CurrentDomain.GetAssemblies()` first, fall back to `Assembly.Load()`
2. Remaining assemblies: Add all non-dynamic assemblies from AppDomain

This ensures all database providers (MSSQL, SQLite, PostgreSQL, MySQL) are available.

### Design Notes
- **Stateless**: No shared mutable state, thread-safe by design
- **CompilerService retains**: `SemaphoreSlim` for document updates, `CSharpParseOptions` with documentation mode
- **Cursor position calculation**: `WrapQuery()` output is byte-for-byte identical to original implementations to preserve CompilerService's cursor position math
- Registered as singleton in `ServiceCollectionExtensions.AddLinqStudio()` (before services that depend on it)

## ConnectionService
Singleton service that manages database connection information and provides connection testing capabilities.

### Key Features
- Stores current connection string and database type
- Creates appropriate query generators (MSSQL, MySQL) based on database type
- Tests database connections with configurable timeout (5s, 10s, 15s, 30s, 60s)
- Integrates with `IDatabaseQueryGenerator` for schema operations

### Usage
```csharp
// Registered as singleton in DI
services.AddSingleton<ConnectionService>();

// Update connection
connectionService.UpdateConnection(DatabaseType.Mssql, connectionString);

// Test connection with 10 second timeout
await connectionService.TestConnectionAsync(DatabaseType.Mssql, connectionString, 10);
```

### Testing
- Unit tests in `tests/LinqStudio.Core.Tests/ConnectionServiceTests.cs`
- Integration tests via database generator tests
- E2E tests in `tests/LinqStudio.App.WebServer.E2ETests/ConnectionE2ETests.cs`

## QueryExecutionService
Scoped service that executes user LINQ queries against a database and returns results (Phase 1b).

### Key Features
- Compiles LINQ queries to IL using Roslyn
- Loads compiled assemblies and executes via reflection
- Instantiates DbContext with real database connection
- Materializes query results with proper column extraction
- Handles primitive types, anonymous types, and EF entities
- Configurable timeout via QueryExecutionSettings
- Comprehensive error handling (compile vs runtime errors)

### Architecture
Implements 7-step execution pipeline:
1. Collect source files (models + DbContext from generator)
2. Wrap user query in QueryContainer class
3. Compile to IL using `CSharpCompilation.Emit()`
4. Load assembly from memory
5. Instantiate DbContext with `DbContextOptions` (real connection)
6. Invoke `QueryContainer.Query(dbContext)` via reflection
7. Materialize results with `ToListAsync()` and extract columns

### Usage
```csharp
// Registered as scoped in DI
services.AddScoped<IQueryExecutionService, QueryExecutionService>();

// Execute query (internal method - interface will be updated in Phase 2)
var result = await queryExecutionService.ExecuteQueryInternalAsync(
    userQuery: "context.Users.Where(u => u.Age > 18)",
    project: currentProject,
    cancellationToken);

// Check result
if (result.Success)
{
    foreach (var row in result.Rows)
    {
        // Access columns by name
    }
}
```

### Dependencies
- `IDbContextGenerator`: Generates models and DbContext code
- `IOptionsMonitor<QueryExecutionSettings>`: Timeout configuration
- EF Core providers: SQL Server, SQLite, PostgreSQL, MySQL

### Error Handling
- Compile errors: Returns `IsCompileError = true` with diagnostic messages
- Runtime errors: Returns `IsCompileError = false` with exception message
- Timeout: Returns error if `TimeoutSeconds` exceeded
- Cancellation: Properly handles `CancellationToken`

### Notes
- Current implementation has internal `ExecuteQueryInternalAsync` method
- Public interface method throws `NotImplementedException` 
- Phase 2 will update `IQueryExecutionService` interface to include `Project` parameter
- Uses same query wrapping pattern as `CompilerService` for consistency

## AssemblyLoadContext Pattern (QueryExecutionService)

Each query execution loads a dynamically compiled assembly into a **collectible** `AssemblyLoadContext`:

```csharp
var alc = new AssemblyLoadContext("query-exec", isCollectible: true);
try
{
    ms.Position = 0;
    var assembly = alc.LoadFromStream(ms);
    // ... execute using assembly — materialize all results before leaving this block ...
}
finally
{
    alc.Unload();
}
```

**Why this matters:**
- Without `isCollectible: true`, compiled assemblies are loaded into the default AppDomain and never freed — every query execution permanently grows memory.
- After `alc.Unload()`, the assembly and all its types are invalidated. All results (rows, column names) **must be fully materialized** (via `ExtractResults`) before the `finally` block runs.
- `DbContext` is wrapped in `await using` to ensure EF Core releases its connection before the ALC is unloaded.
- `AssemblyLoadContext` is in `System.Runtime.Loader` — add the using when referencing it.

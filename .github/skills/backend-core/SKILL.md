---
name: backend-core
description: Patterns and conventions for LinqStudio's backend projects (Core, Databases, Abstractions): settings pattern, service registration, DB introspection, query execution pipeline, and C# conventions. Use this when working on src/LinqStudio.Core, src/LinqStudio.Databases, or src/LinqStudio.Abstractions.
---

# SKILL: Backend Core Patterns

## When to Use This Skill

Load this skill before working on:
- `src/LinqStudio.Core/` — services, settings, service registration, query execution
- `src/LinqStudio.Database/` — database introspection generators (MSSQL, MySQL, PostgreSQL, SQLite)
- `src/LinqStudio.Abstractions/` — shared interfaces and models
- Any code that touches `IUserSettingsSection`, `IDatabaseQueryGenerator`, `IDbContextGenerator`, or `QueryExecutionResult`

For Roslyn-specific workspace work (`AdhocWorkspace`, completions, hover), also load the `roslyn-workspace-management` skill.

---

## Project Map

```
src/
├── LinqStudio.Abstractions/          # Shared interfaces and models — no implementation
│   ├── Abstractions/
│   │   ├── IUserSettingsSection.cs   # Contract for all user settings
│   │   ├── IDatabaseQueryGenerator.cs# Contract for DB introspection
│   │   └── IDbContextGenerator.cs   # Contract for EF Core code generation
│   └── Models/
│       ├── DatabaseType.cs           # Mssql | MySql | PostgreSql | Sqlite enum
│       ├── DatabaseTableName.cs      # Schema + Name, FullName helper
│       ├── DatabaseTableDetail.cs    # TableName + Columns + ForeignKeys
│       ├── TableColumn.cs            # Column metadata incl. DbColumnType, IsPrimaryKey, IsIdentity
│       ├── ForeignKey.cs             # FK name, column, referenced table/column
│       ├── DbColumnType.cs           # Generic type enum (database-agnostic)
│       ├── DbContextGeneratorResult.cs# ModelFiles dict + DbContextCode + ContextTypeName + Namespace
│       └── QueryExecutionResult.cs  # Rows, ColumnNames, Elapsed, ErrorMessage, IsCompileError
│
├── LinqStudio.Core/
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs  # AddLinqStudio() — main DI entry point
│   ├── Settings/
│   │   ├── UISettings.cs             # IUserSettingsSection impl (dark mode, etc.)
│   │   └── QueryExecutionSettings.cs # IUserSettingsSection impl (TimeoutSeconds)
│   └── Services/
│       ├── SettingsService.cs        # Singleton; serializes writes to usersettings.json
│       ├── ISettingsService.cs       # Write-only interface (Save)
│       ├── RoslynWorkspaceService.cs # Singleton; stateless workspace/doc creation
│       ├── CompilerService.cs        # Scoped; stateful IntelliSense workspace
│       ├── CompilerServiceFactory.cs # Scoped; creates + initializes CompilerService
│       ├── ICompilerServiceFactory.cs
│       ├── DbContextGenerator.cs     # Scoped; schema → C# code generation
│       ├── QueryExecutionService.cs  # Scoped; compile + run user LINQ query
│       ├── IQueryExecutionService.cs
│       ├── QueryService.cs           # Singleton; query CRUD
│       └── ProjectService.cs        # Singleton; project CRUD
│
└── LinqStudio.Database/              # NOTE: folder is "Database" (singular), not "Databases"
    ├── AdoNetDatabaseGeneratorBase.cs# Abstract base; manages connection lifecycle
    ├── MssqlGenerator.cs             # SQL Server introspection
    ├── MySqlGenerator.cs             # MySQL/MariaDB introspection
    ├── PostgreSqlGenerator.cs        # PostgreSQL introspection
    └── SqliteGenerator.cs            # SQLite introspection (overrides GetTablesAsync)
```

---

## Pattern 1 — Settings (`IUserSettingsSection`)

### Contract

```csharp
public interface IUserSettingsSection
{
    public string SectionName { get; }
}
```

### Implementing a new settings section

```csharp
// LinqStudio.Core/Settings/MyFeatureSettings.cs
using LinqStudio.Abstractions;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Settings;

public record class MyFeatureSettings : IUserSettingsSection
{
    [JsonIgnore]
    public string SectionName => nameof(MyFeatureSettings);

    public bool IsEnabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}
```

**Rules:**
- Use `record class`, not `class` or `struct`
- `SectionName` must be `[JsonIgnore]` — it is the JSON key, not a JSON value
- `SectionName` must equal `nameof(TheClass)` — the DI binding uses it to find the config section
- All properties need defaults (the file may not exist yet on first run)
- DO NOT add a constructor — the auto-registration reflection calls `new TSettings()`

### Auto-registration (no manual wiring needed)

`AddLinqStudioOptions()` in `ServiceCollectionExtensions.cs` reflects over the assembly and registers every `IUserSettingsSection` implementation automatically:

```csharp
services.AddOptions<MyFeatureSettings>()
    .BindConfiguration("MyFeatureSettings");  // auto-derived from SectionName
```

**This means:** adding a new settings class to `LinqStudio.Core/Settings/` is the ONLY step required. No edits to `ServiceCollectionExtensions.cs`.

### Reading settings (consumption)

Inject `IOptionsMonitor<TSettings>` (live-reload) or `IOptions<TSettings>` (startup snapshot):

```csharp
public class MyService(IOptionsMonitor<QueryExecutionSettings> settings)
{
    public void DoWork()
    {
        var timeout = settings.CurrentValue.TimeoutSeconds; // always fresh
    }
}
```

### Writing settings (persistence)

Inject `ISettingsService` and call `Save()`. The service uses a `SemaphoreSlim` to serialize concurrent writes:

```csharp
await _settingsService.Save(new MyFeatureSettings { IsEnabled = false });
// Merges into usersettings.json without overwriting other sections
```

### Localization requirement

Every settings class and every property needs a localization key. Pattern:
- Section: `UserSettings.MyFeatureSettings` = "My Feature"
- Property: `UserSettings.MyFeatureSettings.IsEnabled` = "Enable My Feature"

---

## Pattern 2 — Service Registration (`AddLinqStudio`)

The single entry point is `services.AddLinqStudio()` in `LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs`.

### Lifetime rules

| Service | Lifetime | Why |
|---------|----------|-----|
| `ProjectService` | Singleton | In-memory project list, shared across sessions |
| `QueryService` | Singleton | In-memory query cache |
| `SettingsService` | Singleton | File lock (`SemaphoreSlim`) must be shared |
| `RoslynWorkspaceService` | Singleton | Stateless; shared workspace factory |
| `CompilerServiceFactory` | Scoped | Creates one per user session/tab |
| `DbContextGenerator` | Scoped | One schema-gen pipeline per session |
| `QueryExecutionService` | Scoped | Holds references to scoped services |

### Adding a new service

1. Implement the interface
2. Add `services.AddSingleton<IFoo, Foo>()` or `AddScoped<>()` in `AddLinqStudio()`
3. Respect the lifetime rules: scoped services may NOT be injected into singletons

---

## Pattern 3 — Database Introspection (`IDatabaseQueryGenerator`)

### Interface contract

```csharp
public interface IDatabaseQueryGenerator
{
    Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken ct = default);
    Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken ct = default);
    Task<DatabaseTableDetail> GetTableAsync(DatabaseTableName table, CancellationToken ct = default); // default impl
    Task TestConnectionAsync(CancellationToken ct = default);
    DbColumnType MapToGenericType(string dataType);
}
```

### Connection lifecycle (mandatory for all generators)

Every generator method that touches the database must follow this exact pattern:

```csharp
var wasOpen = Connection.State == ConnectionState.Open;
if (!wasOpen)
    await Connection.OpenAsync(cancellationToken);
try
{
    // ... query work ...
}
finally
{
    if (!wasOpen)
        await Connection.CloseAsync();
}
```

**Why:** Generators may be called with an already-open connection (from `DbContext.Database.GetDbConnection()`). Closing a connection the caller owns breaks the caller.

### Base class (`AdoNetDatabaseGeneratorBase`)

Provides:
- `GetTablesAsync()` — uses `Connection.GetSchemaAsync("Tables")` + `ParseTableFromSchemaRow()`
- `TestConnectionAsync()` — opens connection, runs `SELECT 1`
- `ParseTableName(string)` — splits "schema.name" or "name"

Subclasses must implement:
- `GetTableAsync(string tableName, ...)` — columns + foreign keys
- `MapToGenericType(string dataType)` — DB-native type → `DbColumnType`
- `ParseTableFromSchemaRow(DataRow row)` — interpret the ADO.NET schema row

### Adding a new database type

1. Add the value to `DatabaseType` enum in `LinqStudio.Abstractions/Models/DatabaseType.cs`
2. Create `MyDbGenerator : AdoNetDatabaseGeneratorBase` in `LinqStudio.Database/`
3. Implement `GetTableAsync`, `MapToGenericType`, `ParseTableFromSchemaRow`
4. Add a `static Create(string connectionString)` factory method
5. Add a `case DatabaseType.MyDb:` in `QueryExecutionService.CreateDbContextOptions()`
6. Add the matching EF Core provider package call (`builder.UseMyDb(connectionString)`)

> ⚠️ The `CreateDbContextOptions` switch is the only place where a new `DatabaseType` requires a manual edit outside the new generator class. There is a TODO comment there flagging this as a known design gap.

### `DbColumnType` mapping

All generators map their native types to the common `DbColumnType` enum before returning column metadata. `DbContextGenerator` then maps `DbColumnType` to C# type names (`GetCSharpTypeName`) and initializers (`GetInitializer`). Never hardcode C# type strings in generators — always go through `DbColumnType`.

---

## Pattern 4 — DbContext Code Generation (`IDbContextGenerator`)

`DbContextGenerator` is the only implementation. It:
1. Calls `IDatabaseQueryGenerator.GetTablesAsync()` to get the table list
2. Calls `GetTableAsync()` for each table to get columns + foreign keys
3. Generates a C# file per entity model (in namespace `GeneratedModels`)
4. Generates a `GeneratedDbContext : DbContext` with `DbSet<T>` properties and `OnModelCreating` with `HasKey()` calls
5. Returns `DbContextGeneratorResult` containing model files dict, DbContext code, context type name, and namespace

**The generated code is consumed by:**
- `CompilerServiceFactory.CreateFromProjectAsync()` → adds files to Roslyn workspace for IntelliSense
- `QueryExecutionService.ExecuteQueryAsync()` → compiles to IL for actual query execution

### Generated code conventions

- Namespace: always `GeneratedModels`
- Context class: always `GeneratedDbContext`
- Entity class names: `ToPascalCase(tableName)` (splits on `_`, capitalizes each part)
- Primary keys: configured via `HasKey()` in `OnModelCreating`, NOT `[Key]` attribute
- Navigation properties: reference navs use `virtual T? Nav`, collections use `virtual ICollection<T> Navs = []`
- String/non-nullable initializers: `= string.Empty;` for strings, `= [];` for collections, `= null!;` for object types

---

## Pattern 5 — Query Execution Pipeline

`QueryExecutionService.ExecuteQueryAsync()` compiles the user's LINQ string with Roslyn, loads the resulting IL into a **collectible `AssemblyLoadContext`**, instantiates the generated `DbContext` and query container, invokes the query method, materializes results via EF Core, then unloads the ALC.

**Critical invariants:**
- `alc.Unload()` MUST be in a `finally` block. The ALC is collectible — omitting this leaks assemblies permanently.
- `QueryExecutionSettings.TimeoutSeconds` is read via `IOptionsMonitor<T>.CurrentValue` at execution time (live value, not cached at construction). A linked `CancellationTokenSource` handles both caller cancellation and timeout.

---

## C# Conventions

These apply across all three projects:

| Convention | Detail |
|------------|--------|
| Nullable | `#nullable enable` (set in `Directory.Build.props`) — all reference types require nullability annotation |
| Usings | Implicit usings enabled — no need to add `using System;` etc. |
| Namespaces | File-scoped: `namespace LinqStudio.Core.Services;` — no braces |
| Expression bodies | Use where appropriate: `public bool Success => ErrorMessage is null;` |
| Records | Prefer `record class` for DTOs and settings; use `record` (positional) for simple value objects |
| Warnings as errors | Main projects treat warnings as errors — no suppressed warnings or nullable-ignore pragmas |
| `required` keyword | Used on record properties that have no meaningful default (e.g., `DatabaseTableDetail.Columns`) |
| Collection expressions | Prefer `[]` over `new List<T>()` for empty collections: `= []` |
| `await using` | Always use `await using` for `IAsyncDisposable` (connections, streams) |

---

## Common Pitfalls

### 1. Adding a settings class but not getting it auto-wired
**Symptom:** `IOptions<MySettings>` injection throws at startup  
**Cause:** Class is in a different assembly than `ServiceCollectionExtensions`  
**Fix:** Settings classes must be in `LinqStudio.Core` assembly (same as the extension method that reflects over it)

### 2. Writing directly to `usersettings.json` without `SettingsService`
**Symptom:** Concurrent requests corrupt the JSON file  
**Fix:** Always go through `ISettingsService.Save()` — it holds the semaphore

### 3. Closing a connection the caller already opened
**Symptom:** Caller's transaction or subsequent queries fail after generator runs  
**Fix:** Always check `wasOpen` before `OpenAsync` and only `CloseAsync` if you opened it (see Pattern 3)

### 4. Injecting a scoped service into a singleton
**Symptom:** `InvalidOperationException: Cannot consume scoped service from singleton`  
**Fix:** Check the lifetime table in Pattern 2. `QueryExecutionService`, `DbContextGenerator`, and `CompilerServiceFactory` are all scoped and cannot be injected into singletons.

### 5. Forgetting `alc.Unload()` after query execution
**Symptom:** Memory grows unboundedly; GC cannot collect generated assemblies  
**Fix:** Always call `alc.Unload()` in a `finally` block. See Pattern 5 — the pipeline wraps execution in `try/finally` precisely for this.

### 6. Adding a new `DatabaseType` without updating `CreateDbContextOptions`
**Symptom:** `NotSupportedException` at runtime when the new DB type is selected  
**Fix:** After adding the enum value and generator, add the matching `case` in `QueryExecutionService.CreateDbContextOptions()` with the correct EF Core provider call.

### 7. Using `[Key]` attribute on generated entity models
**Symptom:** EF Core may apply wrong key conventions; composite keys silently broken  
**Fix:** `DbContextGenerator` uses `modelBuilder.Entity<T>().HasKey(...)` in `OnModelCreating` for all tables. Never add `[Key]` to generated models — the generator intentionally omits it.

---

## Anti-Patterns

❌ **`SectionName` not matching `nameof(TheClass)`**  
The auto-registration calls `new TSettings().SectionName` to derive the config section key. If `SectionName` returns something other than the class name, the binding fails silently and the settings load as defaults.

❌ **Implementing `IUserSettingsSection` outside `LinqStudio.Core`**  
The reflection loop in `AddLinqStudioOptions` scans `typeof(ServiceCollectionExtensions).Assembly` — which is `LinqStudio.Core`. Settings in other assemblies are invisible to the auto-wiring.

❌ **Creating a `DbContextGenerator` or `QueryExecutionService` manually (outside DI)**  
These are scoped. They expect `IOptionsMonitor<T>` to be properly initialized. Newing them up directly will crash on the first call that touches settings.

❌ **Reading `SettingsService.FILE_NAME` directly from disk**  
The `IOptionsMonitor<T>` path (DI) is the authoritative read path. Reading the file directly bypasses the monitor and returns stale or unparsed JSON.

❌ **Using `DbColumnType.Unknown` as a "pass-through"**
`DbContextGenerator.GetCSharpTypeName()` maps `Unknown` to `object`. An entity model with `public object Foo { get; set; } = null!;` is legal C# but breaks EF Core conventions. When adding a new DB type, always map every native type to the closest `DbColumnType` — never leave gaps that produce `Unknown` for common types.

❌ **Forgetting the connection lifecycle guard in new generators**  
If a new generator calls `Connection.OpenAsync()` unconditionally, it will throw `InvalidOperationException` when called with an already-open connection (the standard usage from `DbContext.Database.GetDbConnection()`).

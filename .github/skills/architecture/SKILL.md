---
name: architecture
description: System architecture, layer boundaries, service lifetimes, data flow, and cross-cutting concerns for LinqStudio. Use this before making changes that span multiple projects or affect the Core → Blazor → WebServer layer hierarchy.
---

# LinqStudio Architecture

## When to Use This Skill

Read this skill when you:
- Are adding or modifying a service that touches more than one project
- Are unsure where a new class or interface should live
- Are wiring up DI registration (`Program.cs`, `ServiceCollectionExtensions`)
- Are working on the Roslyn compiler pipeline, query execution, or settings
- Are adding a new database provider or schema introspection feature
- Are about to introduce a pattern that already has an established equivalent elsewhere

---

## Project Layer Map

Dependency flow is **strictly one-way** — lower layers never reference higher ones.

```
LinqStudio.Abstractions       ← contracts, shared models, no logic
       ↓
LinqStudio.Core               ← business logic, Roslyn pipeline, services
       ↓
LinqStudio.Database           ← database introspection (ADO.NET, vendor-specific)
       ↓
LinqStudio.Blazor             ← Razor components, UI services, Monaco integration
       ↓
LinqStudio.App.WebServer      ← ASP.NET Core host, wires all DI, serves Blazor
LinqStudio.App.Maui           ← MAUI Blazor Hybrid host (Windows desktop)
       ↓
LinqStudio.AppHost            ← Aspire orchestration only
```

Supporting projects (no business logic):
- `LinqStudio.ServiceDefaults` — Aspire service defaults (health checks, telemetry)
- `LinqStudio.DatabaseSeeder` — Aspire console app; seeds demo DBs; must exit explicitly
- `LinqStudio.Demo` — demo/sample project

---

## What Each Layer Owns

### LinqStudio.Abstractions
- Contracts and interfaces: `IDatabaseQueryGenerator`, `IDbContextGenerator`, `IUserSettingsSection`
- Shared models: `DbColumnType`, `DatabaseTableDetail`, `DatabaseTableName`, `TableColumn`, `ForeignKey`, `QueryExecutionResult`, `DbContextGeneratorResult`, `DatabaseType`
- No dependencies on any other LinqStudio project
- **Rule**: Shared models and contracts only. No logic, no services.

### LinqStudio.Core
- All business services: `ProjectService`, `QueryService`, `SettingsService`, `CompilerService`, `QueryExecutionService`, `DbContextGenerator`, `RoslynWorkspaceService`
- Settings system (`IUserSettingsSection` auto-discovery via reflection)
- Roslyn compiler pipeline
- File system repository implementations (`FileSystemProjectRepository`, `FileSystemQueryRepository`)
- DI entry point: `AddLinqStudio()` and `AddFileSystemRepositories()` extension methods
- **Rule**: Pure C#, no Blazor or UI concerns. Depends on Abstractions only.

### LinqStudio.Database
- All database introspection uses **ADO.NET directly** (not EF Core) to avoid circular dependencies
- Each DB provider implements `IDatabaseQueryGenerator` (maps vendor types → `DbColumnType`)
- Providers: SQL Server, MySQL, PostgreSQL, SQLite
- **Rule**: Vendor-specific schema queries belong here, not in Core.

### LinqStudio.Blazor
- Razor components and pages
- Scoped UI services: `MonacoProvidersService`, `ErrorHandlingService`, `QueriesWorkspace`, `ProjectWorkspace`, `ClipboardService`
- Monaco editor integration (hover, completions, diagnostics)
- Error handling: `ErrorHandlingService` + `AppErrorBoundary` + `ErrorDialog`
- MudBlazor UI library
- DI entry point: `AddLinqStudioBlazor()` extension method
- **Rule**: UI state and Blazor-specific concerns only. No database access, no Roslyn calls outside of injected Core services.

### LinqStudio.App.WebServer
- `Program.cs` is the composition root — only place that calls all three `Add*` extensions
- Wires: `AddLinqStudio()` + `AddFileSystemRepositories(projectsBasePath)` + `AddLinqStudioBlazor()`
- Reads `LinqStudio:ProjectsPath` from config (defaults to `%APPDATA%/LinqStudio/Projects`)
- Loads `linqstudio.settings.json` with `reloadOnChange: true`
- Hosts Razor components; Blazor components come from `LinqStudio.Blazor` assembly
- **Rule**: No business logic here. Composition root only.

### LinqStudio.AppHost
- Aspire orchestration: starts SQL Server, MySQL, DatabaseSeeder, WebServer, MAUI
- Demo databases: SQL Server (`127.0.0.1:14330`), MySQL (`127.0.0.1:13306`) — persistent containers
- Feature flags in config control which apps Aspire starts (`LinqStudio:Apps:WebServer`, `LinqStudio:Apps:Maui`)
- WebServer waits for DatabaseSeeder to complete successfully
- **Rule**: No logic, no services. Infrastructure orchestration only.

---

## Service Lifetimes

### Singletons (one per application)
| Service | Type | Reason |
|---|---|---|
| `RoslynWorkspaceService` | `RoslynWorkspaceService` | Shared workspace creation + metadata references; expensive to construct |
| `ProjectService` | `ProjectService` | Application-wide project state |
| `QueryService` | `QueryService` | Application-wide query state |
| `ISettingsService` | `SettingsService` | Settings file state |
| `ProjectVersionConfig` | `ProjectVersionConfig` | Immutable version metadata |

### Scoped (one per user/connection in Blazor Server)
| Service | Interface | Reason |
|---|---|---|
| `ICompilerServiceFactory` | `CompilerServiceFactory` | Factory; vends per-editor `CompilerService` instances |
| `IQueryExecutionService` | `QueryExecutionService` | Per-session execution context |
| `IDbContextGenerator` | `DbContextGenerator` | Per-session code generation |
| `IProjectRepository` | `FileSystemProjectRepository` | Per-session file I/O |
| `IQueryRepository` | `FileSystemQueryRepository` | Per-session file I/O |
| `MonacoProvidersService` | *(concrete)* | Per-session Monaco provider registry |
| `ErrorHandlingService` | *(concrete)* | Per-session dialog service dependency |
| `QueriesWorkspace` | *(concrete)* | Per-session query UI state |
| `ProjectWorkspace` | *(concrete)* | Per-session project UI state |
| `IClipboardService` | `ClipboardService` | Per-session clipboard access |

### Not Registered in DI (created by factory)
- `CompilerService` — created by `ICompilerServiceFactory` per editor instance; holds its own `AdhocWorkspace`

---

## Data Flow: UI → Query Execution

```
Blazor Component
    │ injects QueriesWorkspace / ProjectWorkspace
    ↓
QueriesWorkspace / ProjectWorkspace   (scoped, Blazor layer)
    │ current query text, project connection settings
    ↓
ICompilerServiceFactory.Create(contextTypeName, projectNamespace)
    │ creates CompilerService with AdhocWorkspace per editor
    ↓
CompilerService   (Core layer, long-lived per editor tab)
    │ SemaphoreSlim(_lock) serializes all Roslyn operations
    │ Wraps user query in QueryContainer class (with "return" prefix)
    │ Uses RoslynWorkspaceService for workspace/metadata setup
    ↓
IQueryExecutionService.ExecuteQueryAsync(...)   (Core layer, scoped)
    │ Compiles wrapped query to assembly via Roslyn
    │ Uses RoslynWorkspaceService.AddDocuments() for fresh workspace
    ↓
DbContextGenerator   (Core layer, scoped)
    │ Generates EF Core DbContext + entity model C# source
    │ Driven by IDatabaseQueryGenerator (Database layer)
    ↓
IDatabaseQueryGenerator   (Abstractions → Database layer)
    │ ADO.NET introspection of schema
    │ Maps vendor types → DbColumnType → C# types
    ↓
Actual database (SQL Server / MySQL / PostgreSQL / SQLite)
```

---

## Roslyn Compiler Pipeline Details

**RoslynWorkspaceService** (singleton) owns workspace creation and the full set of EF Core + DB provider metadata references. Used by both `CompilerService` (for initialization) and `QueryExecutionService` (for fresh execution workspaces).

**CompilerService** (per-editor, created by factory) owns:
- Its own `AdhocWorkspace` — long-lived, updated incrementally as the user types
- A `SemaphoreSlim` lock — all workspace mutations are serialized to prevent Roslyn races
- Query wrapping — user query is embedded in a `QueryContainer` class; cursor offset is adjusted internally

**Key distinction**: `CompilerService` updates its workspace incrementally (for autocomplete). `QueryExecutionService` creates a fresh workspace per execution (for compilation).

---

## Settings System

All settings use the `IUserSettingsSection` pattern:

1. Create a `record class` in `LinqStudio.Core/Settings/` implementing `IUserSettingsSection`
2. Implement `string SectionName { get; }` returning the config section key
3. That's it — auto-discovered via assembly reflection at startup (`AddLinqStudioOptions`)
4. Registered as `IOptions<T>` and `IOptionsMonitor<T>` bound to `SectionName`
5. Settings file: `linqstudio.settings.json` loaded in `Program.cs` with `reloadOnChange: true`
6. Components and services consume settings via `IOptionsMonitor<TSettings>` for live reload

**Never** manually register a settings class in DI — the reflection loop handles it.

---

## Monaco Editor Integration

`MonacoProvidersService` (scoped, Blazor layer) manages global Monaco provider registration:

- Monaco registers providers globally (hover, completion, diagnostics)
- Multiple editor instances can exist per session (multi-tab)
- Pattern: single global registration per language, `ConcurrentDictionary<editorUri, delegate>` routes calls to the right editor
- Component lifecycle: **register on init, unregister on dispose**
- **Never** call `Global.RegisterHoverProvider()` or similar directly from components — always use `MonacoProvidersService`

---

## Error Handling

Three-layer strategy — always use all three layers together:

| Layer | Class | Scope | Purpose |
|---|---|---|---|
| 1 | `ErrorHandlingService` | Scoped | Manual try-catch in components; shows `ErrorDialog` |
| 2 | `AppErrorBoundary` | Component | Wraps `<Router>` in `Routes.razor`; catches unhandled exceptions globally |
| 3 | `ErrorDialog` | Component | MudBlazor dialog with collapsible stack trace |

Usage: inject `ErrorHandlingService`, wrap risky operations in try-catch, call `HandleErrorAsync(ex, optionalMessage)`. Unexpected exceptions are automatically caught by `AppErrorBoundary` — no need to try-catch everywhere.

---

## Aspire / AppHost Details

- Use `127.0.0.1` not `localhost` on Windows (IPv6 binding issue)
- `DatabaseSeeder` must exit with `Environment.Exit(0)` on success and `Environment.Exit(1)` on failure — Aspire's `WaitForCompletion()` relies on exit codes
- WebServer and MAUI startup is controlled by `LinqStudio:Apps:WebServer` and `LinqStudio:Apps:Maui` in `appsettings.json`

---

## Workspace UI State Pattern

`ProjectWorkspace` (orchestrator) + `QueriesWorkspace` (query state) — both scoped:

| Workspace | Tracks |
|---|---|
| `ProjectWorkspace` | Current project, file path, connection settings, aggregated unsaved-changes flag |
| `QueriesWorkspace` | Open queries, current query, in-memory edits, per-query unsaved-changes flag |

Both emit change events (`WorkspaceChanged`, `QueriesChanged`) for reactive UI. Unsaved changes are tracked independently at each level and aggregated at `ProjectWorkspace`.

---

## Anti-Patterns

**Never do these:**

### Layer violations
- ❌ UI logic in `LinqStudio.Core` (Blazor state, MudBlazor, `IDialogService`)
- ❌ Business logic in `LinqStudio.App.WebServer` (it is a composition root, nothing more)
- ❌ EF Core usage in `LinqStudio.Database` (ADO.NET only for schema introspection — EF circular dependency)
- ❌ Referencing `LinqStudio.Blazor` from `LinqStudio.Core` (breaks the dependency direction)
- ❌ Putting vendor-specific DB introspection in `LinqStudio.Core` (belongs in `LinqStudio.Database`)

### Service wiring
- ❌ Registering `CompilerService` directly in DI — it's created by `ICompilerServiceFactory` per editor
- ❌ Making `RoslynWorkspaceService` scoped — it's singleton because constructing Roslyn metadata is expensive
- ❌ Manually registering settings classes — the reflection loop auto-discovers all `IUserSettingsSection` implementations
- ❌ Making a service singleton that holds user session state — use scoped for anything per-connection

### Roslyn pipeline
- ❌ Calling Monaco global provider APIs directly from components — always go through `MonacoProvidersService`
- ❌ Skipping the `SemaphoreSlim` lock before mutating the `AdhocWorkspace` in `CompilerService` — races with Monaco callbacks

### Settings
- ❌ Reading config with `IConfiguration` directly in services — use `IOptionsMonitor<TSettings>` for reactive settings
- ❌ Hardcoding config section names inline — they come from `IUserSettingsSection.SectionName`

### Aspire / AppHost
- ❌ Console apps used as Aspire dependencies without explicit `Environment.Exit(0/1)` — Aspire cannot detect success/failure
- ❌ Connecting to demo databases with `localhost` on Windows — use `127.0.0.1`

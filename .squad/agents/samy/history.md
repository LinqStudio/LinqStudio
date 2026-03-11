# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

### 2026-03-11: Comprehensive Architectural Analysis

#### Solution Structure & Projects
- **10 projects** organized in modern .slnx format (LinqStudio.slnx) with legacy .sln fallback
- **Dependency hierarchy**: Abstractions → Core → Blazor → App.WebServer → AppHost
- **Database layer**: Separate LinqStudio.Databases project with ADO.NET-based query generators (MSSQL, MySQL)
- **5 test projects**: Core.Tests, Blazor.Tests, App.WebServer.Tests, App.WebServer.E2ETests, Databases.Tests
- **Build system**: Nuke build automation (build/_build.csproj) with targets: Clean, Restore, Compile, PlaywrightInstall, Test
- **All projects target .NET 10** with nullable enabled, implicit usings, warnings as errors, latest C# language version

#### Core Architecture Layers

**LinqStudio.Abstractions** (foundation layer)
- Pure interfaces and models, no dependencies
- `IUserSettingsSection` - extensible settings pattern
- `IDatabaseQueryGenerator` - database introspection abstraction
- Models: `DatabaseType` (enum: Mssql, MySql), `DatabaseTableName`, `DatabaseTableDetail`, `TableColumn`, `ForeignKey`

**LinqStudio.Databases** (database introspection)
- `AdoNetDatabaseGeneratorBase` - base class for DB generators
- `MssqlGenerator` - SQL Server schema queries using GetSchema() and sys tables
- `MySqlGenerator` - MySQL schema queries
- Implements `IDatabaseQueryGenerator` for fetching tables, columns, foreign keys
- Uses direct ADO.NET (`DbConnection`) not EF Core to avoid circular dependencies

**LinqStudio.Core** (business logic layer)
- **CompilerService**: Roslyn-based C# compilation and IntelliSense
  - Creates `AdhocWorkspace` with EF Core assemblies
  - Wraps user queries in synthetic `QueryContainer` class for Roslyn analysis
  - `GetCompletionsAsync()` - autocomplete at cursor position (cursor adjustment for wrapper)
  - `GetHoverAsync()` - hover information with symbol resolution
  - Thread-safe with `SemaphoreSlim` for concurrent Monaco callbacks
  - Initialization: `Initialize(models, dbContext)` - adds table models and DbContext to workspace
- **CompilerServiceFactory**: Creates pre-initialized CompilerService with test models (Person class)
- **ProjectService**: Project file I/O (.linq files), schema versioning, validation
- **QueryService**: Query file I/O (.linq.query files), directory structure management (project.linq.queries/)
- **SettingsService**: JSON-based user settings persistence (usersettings.json)
- **Extension methods**: 
  - `ServiceCollectionExtensions.AddLinqStudio()` - registers Core services, auto-discovers and binds all `IUserSettingsSection` via reflection
  - `JsonSerializerOptionsExtensions` - uses C# 11 extension syntax for static `.Indented` property
- **Models**: `Project` (connection string, DB type, models, DbContext), `SavedQuery`, `ProjectVersionConfig`, `ProjectConstants`
- **Settings**: `UISettings` (IsDarkMode, AlwaysReloadSettingsInSettingsPage) - record classes implementing `IUserSettingsSection`

**LinqStudio.Blazor** (UI components layer)
- **Services**:
  - `MonacoProvidersService` - prevents duplicate Monaco provider registrations, routes hover/completion to correct editor instances by URI, retry logic for Monaco initialization
  - `ErrorHandlingService` - centralized error handling with MudDialog integration
  - `ProjectWorkspace` - manages current project state, tracks unsaved changes, orchestrates QueriesWorkspace
  - `QueriesWorkspace` - manages open queries (dictionary by Guid), current query, save states, file I/O delegation
- **Components/Pages**:
  - `Home.razor` - landing page
  - `Editor/Editor.razor` - main query editor with Monaco integration, tab management, IntelliSense via CompilerService
  - `Settings/SettingsEditor.razor` - JSON editor for settings with Monaco, hover tooltips from localized descriptions
  - `Error.razor`, `NotFound.razor` - error pages
- **Dialogs**: `EditorMenuDialog`, `EditProjectDialog`, `UnsavedChangesDialog`, `ErrorDialog`
- **Layout**: `MainLayout.razor` (dark/light theme toggle, MudDrawer navigation), `NavMenu.razor`
- **Abstractions**: `IFileSystemService` - platform-agnostic file dialogs (abstraction for Server vs WASM)
- **Extension methods**: `ServiceCollectionExtensions.AddLinqStudioBlazor()` - registers MudBlazor, Monaco, error handling, workspaces

**LinqStudio.App.WebServer** (entry point)
- **Program.cs**: 
  - `builder.AddServiceDefaults()` - Aspire defaults (health checks, OpenTelemetry, service discovery)
  - `AddJsonFile(SettingsService.FILE_NAME)` - loads usersettings.json with reload on change
  - `AddRazorComponents().AddInteractiveServerComponents()` - Blazor Server mode
  - `AddLinqStudio()` + `AddLinqStudioBlazor()` - registers all layers
  - `AddScoped<IFileSystemService, ServerFileSystemService>` - native file dialogs via NativeFileDialogSharp
  - `MapRazorComponents<App>().AddAdditionalAssemblies(Blazor assembly)` - component discovery
- **ServerFileSystemService**: Uses NativeFileDialogSharp for cross-platform file dialogs, default path ~/Documents/LinqStudio/

**LinqStudio.ServiceDefaults** (Aspire shared configuration)
- `AddServiceDefaults<TBuilder>()` extension - configures OpenTelemetry (metrics, traces), health checks, service discovery, HTTP resilience
- Health endpoints: /health, /alive (dev only)
- OTLP exporter configuration

**LinqStudio.AppHost** (Aspire orchestration)
- Minimal orchestration: single `AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")`
- Aspire SDK version 9.5.0, Aspire.Hosting.AppHost 13.0.2

#### Key Data Flows

**User Query → IntelliSense Flow**:
1. User types in Monaco editor (Editor.razor)
2. Monaco triggers completion/hover via registered providers (MonacoProvidersService)
3. MonacoProvidersService routes to Editor component's provider delegate
4. Editor calls `CompilerService.GetCompletionsAsync(userQuery, cursorPosition)`
5. CompilerService wraps query in `QueryContainer` class, adjusts cursor position
6. Roslyn CompletionService analyzes syntax tree, returns completions with descriptions
7. Results converted to Monaco CompletionList, displayed in editor

**Project Load → Editor Flow**:
1. User opens project via IFileSystemService (native dialog)
2. ProjectService.LoadProjectAsync() deserializes .linq file
3. ProjectWorkspace.OpenProject() sets current project, initializes QueriesWorkspace
4. QueriesWorkspace loads all queries from project.linq.queries/ directory
5. First query auto-opened, content loaded into Monaco editor
6. CompilerService initialized with project's models and DbContext

**Settings Modification Flow**:
1. Settings page loads all IUserSettingsSection implementations via reflection
2. Monaco editor displays current settings JSON (one tab per section)
3. User edits JSON, changes tracked in component state
4. Save → SettingsService.Save() writes entire usersettings.json atomically
5. IOptionsMonitor<T> notifies subscribers, UI updates reactively

#### Technology Stack Details

**Exact Versions** (from .csproj files):
- .NET 10.0 (all projects)
- Microsoft.CodeAnalysis.CSharp 5.0.0 (Roslyn)
- Microsoft.EntityFrameworkCore 10.0.1
- BlazorMonaco 3.4.0
- MudBlazor 8.15.0
- NativeFileDialogSharp 0.5.0 (file dialogs)
- Aspire SDK 9.5.0 + Aspire.Hosting.AppHost 13.0.2
- Microsoft.Extensions (DI, Options, Configuration, Logging) 10.0.1
- OpenTelemetry 1.14.0 packages
- Microsoft.Playwright 1.57.0 (E2E tests)
- XUnit 2.9.3 + Microsoft.NET.Test.Sdk 18.0.1
- FluentAssertions 8.8.0 (Core.Tests only - custom instruction says DO NOT use, use standard xUnit assertions)
- Nuke.Common 10.1.0 (build automation)

**Build & Test Infrastructure**:
- Nuke build with GitHub Actions integration (`[GitHubActions]` attribute)
- CI runs on Ubuntu, triggers on PR, invokes Test target
- Playwright install target: runs `playwright.ps1 install --with-deps` from E2E test output
- Unit test pattern: `*.Tests` projects
- E2E test pattern: `*.E2ETests` projects
- Test files as embedded resources: `<Compile Remove>` + `<EmbeddedResource Include>`

#### Settings System Architecture

**Pattern**: Auto-discovery via reflection
1. All `IUserSettingsSection` implementations in Core assembly automatically registered
2. Each setting has `SectionName` property (usually matches class name)
3. `ServiceCollectionExtensions.AddAndBindOptions()` uses reflection + generic method invocation
4. Settings bound to configuration via `BindConfiguration(sectionName)`
5. Reactive updates via `IOptionsMonitor<T>` (reload on file change)

**Localization**: 
- All setting descriptions in `SharedResource.resx` (English + French)
- Format: `UserSettings.{SectionName}` for section title, `UserSettings.{SectionName}.{PropertyName}` for property descriptions
- Monaco hover tooltips parse JSON property names, look up translations dynamically

**Persistence**: 
- Single `usersettings.json` file
- SettingsService opens file once (prevents concurrency issues)
- Atomic write: read JSON → update sections → truncate → write
- Built-in JSON reload support: `reloadOnChange: true` in Program.cs

#### Error Handling Architecture

**Three Layers** (from docs/ERROR_HANDLING.md):
1. **ErrorHandlingService** (scoped) - manual exception handling, displays MudDialog with error details + expandable stack trace
2. **AppErrorBoundary** - global unhandled exception catcher, wraps entire Router in Routes.razor
3. **ErrorDialog** - reusable dialog component with error icon, message alert, collapsible technical details (max-height: 400px, monospace)

**Features**: Custom error messages, automatic logging (ILogger), keyboard accessible (Escape to close), fallback UI if dialog fails

#### Code Conventions & Patterns

**C# Style**:
- File-scoped namespaces (`namespace X;`)
- Expression-bodied members preferred
- Record classes for immutable data models
- Primary constructors for DI (e.g., `MonacoProvidersService(IJSRuntime jSRuntime)`)
- Property initializers with field keyword (C# 13: `get; set { field = value; QueryGenerator = null; }`)
- Target-typed new (`new()` instead of `new Type()`)
- Nullable reference types enabled, all warnings as errors (except tests)

**DI Registration Pattern**:
- Extension methods in each layer: `AddLinqStudio()`, `AddLinqStudioBlazor()`, `AddServiceDefaults()`
- Scoped services for UI components (workspaces, Monaco, error handling)
- Singleton services for stateless business logic (ProjectService, QueryService, SettingsService)
- Factory pattern for per-request services (CompilerServiceFactory)

**Blazor Patterns**:
- Partial classes for code-behind (`Editor.razor` + `Editor.razor.cs`)
- Parameter binding: `[Parameter] public Guid? QueryIdParam { get; set; }`
- Reactive state: `IOptionsMonitor<T>`, `StateHasChanged()`, event subscriptions
- Monaco integration: delay rendering with `@if (_loaded)` workaround for initialization timing
- MudBlazor throughout: MudDrawer, MudDialog, MudTabs, MudAlert, MudSnackbar, MudChip

**Testing Patterns**:
- Embedded resources for test data: `TestFiles/*.cs` → `.EmbeddedResource` in .csproj
- `Assembly.GetManifestResourceStream()` to load embedded files
- WebApplicationFactory<T> for integration tests
- Playwright for E2E browser automation
- XUnit conventions: `[Fact]`, `Assert.*` (NO FluentAssertions per custom instructions)

#### Current Branch & Work in Progress

**Branch**: `copilot/add-connection-settings-ui`
- 1 commit ahead of origin
- Latest commit (6a0088c): "feat: hire team — Samy, EvilJosh, Simon, Jordan, Alice"
- Previous work: connection settings dialog, ConnectionService, error handling, project/query management
- Uncommitted changes: .gitignore modifications, new .squad/ infrastructure for team coordination

**Recent Features** (from copilot.md files):
- Connection settings dialog with database type selection, connection string input, timeout control (5s-60s)
- Error handling system with ErrorDialog, AppErrorBoundary, ErrorHandlingService
- ConnectionService for database connection testing and query generator factory
- Project/query workspace management with unsaved changes tracking
- File system abstraction for cross-platform file dialogs

#### Architecture Decisions & Patterns

**Key Decisions**:
1. **Layered architecture** - strict dependency flow, no circular dependencies
2. **Settings auto-discovery** - reflection-based, no manual registration, extensible
3. **Roslyn workspace isolation** - thread-safe with SemaphoreSlim, query wrapping for Roslyn analysis
4. **Monaco provider management** - global registration with per-editor routing to avoid duplicates
5. **Aspire for orchestration** - minimal AppHost, ServiceDefaults for cross-cutting concerns
6. **Native file dialogs** - NativeFileDialogSharp instead of web-based file inputs for better UX
7. **Atomic settings persistence** - single file open prevents concurrent writes
8. **ADO.NET for DB introspection** - avoids EF Core dependency in database generators

**Notable Patterns**:
- **Workspace pattern**: ProjectWorkspace orchestrates QueriesWorkspace, both emit change events
- **Factory pattern**: CompilerServiceFactory creates pre-initialized instances with test models
- **Extension method pattern**: Each layer has `ServiceCollectionExtensions` for DI registration
- **Dialog pattern**: MudBlazor DialogService for all modals (connection, errors, unsaved changes, editor menu)
- **Resource embedding**: Test models embedded as .cs files for CompilerService tests
- **Retry pattern**: MonacoProvidersService retries Monaco initialization up to 20 times (250ms delay) for timing issues

#### Database Support

**Current**: MSSQL, MySQL
**Future**: PostgreSQL, SQLite (DatabaseType enum suggests planned support)
**Implementation**: 
- Each database has dedicated generator class inheriting `AdoNetDatabaseGeneratorBase`
- Generators use DB-specific SQL queries for schema introspection
- MSSQL: uses `GetSchema()` + sys catalog queries for foreign keys
- MySQL: similar ADO.NET pattern with MySQL-specific system tables
- Connection testing with cancellation token support (timeout enforcement)

#### Known Limitations & Workarounds

1. **BlazorMonaco rendering**: `Task.Delay(500)` + `_loaded` flag before editor initialization
2. **Monaco provider accumulation**: MonacoProvidersService tracks by URI to prevent duplicate registrations
3. **Cursor position adjustment**: CompilerService wraps queries, must adjust cursor for Roslyn analysis
4. **Extension method syntax**: JsonSerializerOptionsExtensions uses C# 13 `extension` keyword (non-standard, may need refactoring)
5. **FluentAssertions in Core.Tests**: Contradicts custom instructions (should use standard xUnit assertions)

#### Testing Strategy

**Unit Tests** (LinqStudio.Core.Tests, Blazor.Tests):
- CompilerServiceTests - loads embedded test files, validates Roslyn completions
- SettingsServiceTests - validates JSON persistence
- ProjectServiceTests - validates project load/save
- ErrorHandlingServiceTests - validates error dialog creation

**Integration Tests** (LinqStudio.App.WebServer.Tests):
- WebApplicationFactory-based tests for full pipeline

**E2E Tests** (LinqStudio.App.WebServer.E2ETests):
- Playwright browser automation
- ConnectionE2ETests - connection dialog, validation, timeout
- ErrorHandlingE2ETests - error boundary, dialog display
- Requires `playwright.ps1 install --with-deps` (Linux: installs OS dependencies)

**Test Execution**: `./build.sh Test` or `./build.ps1 Test` runs all test types via Nuke

#### File & Folder Structure

```
src/
  LinqStudio.Abstractions/      # Interfaces, models
    Abstractions/                 # IUserSettingsSection, IDatabaseQueryGenerator
    Models/                       # DatabaseType, TableColumn, ForeignKey, etc.
  LinqStudio.Core/              # Business logic
    Services/                     # CompilerService, ProjectService, QueryService, SettingsService
    Settings/                     # UISettings and future setting classes
    Models/                       # Project, SavedQuery
    Extensions/                   # ServiceCollectionExtensions, JsonSerializerOptionsExtensions
    Resources/                    # SharedResource.resx (localization)
  LinqStudio.Databases/         # DB introspection
    AdoNetDatabaseGeneratorBase.cs
    MssqlGenerator.cs
    MySqlGenerator.cs
  LinqStudio.Blazor/            # UI components
    Components/
      Pages/                      # Home, Editor, Settings, Error, NotFound
      Dialogs/                    # EditorMenuDialog, EditProjectDialog, UnsavedChangesDialog
      Layout/                     # MainLayout, NavMenu
    Services/                     # MonacoProvidersService, ErrorHandlingService, workspaces
    Abstractions/                 # IFileSystemService
  LinqStudio.App.WebServer/     # Entry point
    Program.cs
    App.razor
    Routes.razor
    Services/                     # ServerFileSystemService
  LinqStudio.AppHost/           # Aspire orchestration
    AppHost.cs
  LinqStudio.ServiceDefaults/   # Aspire shared config
    Extensions.cs

tests/
  LinqStudio.Core.Tests/        # Unit tests for Core
    TestFiles/                    # Embedded .cs test models
  LinqStudio.Blazor.Tests/      # Unit tests for Blazor
  LinqStudio.App.WebServer.Tests/ # Integration tests
  LinqStudio.App.WebServer.E2ETests/ # Playwright E2E tests
  LinqStudio.Databases.Tests/   # Database generator tests

build/
  _build.csproj                 # Nuke build project
  Build.cs                      # Nuke build script

docs/
  ERROR_HANDLING.md             # Error handling documentation
```

#### Domain Concepts

**Project** (.linq file):
- Container for connection settings, database type, models, DbContext code
- Metadata: Id (Guid), Name, Created/Modified dates, SchemaVersion
- Directory structure: `project.linq` + `project.linq.queries/` folder for associated queries
- Lazy initialization of QueryGenerator from connection string + database type

**SavedQuery** (.linq.query file):
- Individual LINQ query with Id (Guid), Name, QueryText, CreatedDate
- Stored in `project.linq.queries/` directory
- FilePath property tracks persistence state

**QueryContainer** (synthetic class):
- Wrapper for user queries to enable Roslyn analysis
- Format: `public async Task<IQueryable<object>> Query({ContextType} context) { return {userQuery}; }`
- Cursor position calculation: insert `__THIS_HERE__` placeholder, find offset

**Workspace** (UI state):
- ProjectWorkspace: current project, file path, unsaved changes detection
- QueriesWorkspace: open queries (dictionary), current query, save states
- Both emit WorkspaceChanged/QueriesChanged events for reactive UI updates

#### Next Steps & Considerations

**Architectural Observations**:
1. Clean separation of concerns, but Abstractions layer is thin (only 2 interfaces)
2. CompilerService is stateful (AdhocWorkspace), requires careful lifecycle management
3. Monaco provider routing is complex - consider per-editor CompilerService instances?
4. Settings auto-discovery is elegant but requires Core assembly scan
5. Aspire orchestration is minimal - room to add more infrastructure (databases, message queues, etc.)

**Potential Future Work** (based on code structure):
- PostgreSQL/SQLite support (DatabaseType enum ready, generators not implemented)
- Query execution (currently only IntelliSense, no actual DB query execution visible)
- DbContext code generation from database introspection
- Multiple DbContext support in single project
- Query result visualization (CompilerService returns IQueryable<object>, needs materialization)
- WASM support (IFileSystemService abstraction ready, needs WASM implementation)

**Team Coordination Points**:
- EvilJosh owns Blazor UI changes (Monaco, MudBlazor components)
- Simon owns Core services (CompilerService, database generators)
- Jordan owns all testing (unit, integration, E2E)
- Alice owns live browser testing with Playwright
- Samy (me) coordinates architecture, reviews cross-cutting changes

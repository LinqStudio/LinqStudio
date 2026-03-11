# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-11 - Backend/Core Architecture Deep Analysis

#### CompilerService (`src/LinqStudio.Core/Services/CompilerService.cs`)
**Complete Roslyn workspace management for LINQ query compilation and IntelliSense:**

**Constructor:**
- Initializes `AdhocWorkspace` with `SolutionInfo` and `ProjectInfo` (named "EFCoreModelsProject")
- Loads EF Core assemblies first: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Microsoft.EntityFrameworkCore.SqlServer`, `System.Linq`, `System.Linq.Queryable`
- Assembly loading strategy: Try `AppDomain.CurrentDomain.GetAssemblies()` first, fall back to `Assembly.Load()` for EF Core namespaces
- Then loads ALL remaining assemblies from `AppDomain.CurrentDomain.GetAssemblies()` (skipping dynamic assemblies and those without location)
- Adds all assemblies as `MetadataReference.CreateFromFile(asm.Location)` to solution
- Configures C# parse options with `DocumentationMode.Diagnose` to enable XML doc comments for hover tooltips
- Uses `SemaphoreSlim _lock = new(1, 1)` to serialize all access and prevent concurrent workspace mutations

**Method: `Initialize(Dictionary<string, string> tableModelFiles, string dbContextCode)` (async)**
- Adds EF Core model files: each table model passed as (tableName, modelCode) → DocumentName = tableName + ".cs"
- Adds DbContext file as "DbContext.cs"
- Calls `AddOrUpdateFile()` for each, which creates new documents in the workspace or updates existing ones

**Method: `AddUserQuery(string content)` (async)**
- Wraps user query via `WrapUserQuery()` and adds as "UserQuery.cs"

**Method: `WrapUserQuery(string userQuery)` (private)**
- Ensures user query ends with semicolon
- Wraps in synthetic class structure:
```csharp
using System;
using System.Linq;
using System.Threading.Tasks;

namespace {_projectNamespace};

public class QueryContainer
{
    public async Task<IQueryable<object>> Query({_contextTypeName} context)
    {
        return {userQuery};
    }
}
```
- **Critical**: `_beforeUserQuery = "return"` and `_afterUserQuery = ""` are hardcoded wrapper components

**Method: `GetCompletionsAsync(string userQueryContent, int cursorPosition)` (async)**
- **CRITICAL CURSOR OFFSET CALCULATION**: Uses marker technique to compute wrapper overhead
  - Creates `thisHere = "__THIS_HERE__"` marker
  - Wraps marker: `prefix = WrapUserQuery(thisHere)`
  - Finds marker position: `wrappedCursorPosition = prefix.IndexOf(thisHere)`
  - Absolute position in wrapped document: `absolutePos = wrappedCursorPosition + safeCursor`
- Clamps cursor to valid range: `Math.Clamp(cursorPosition, 0, userQueryContent.Length)`
- Gets `CompletionService` from document, calls `GetCompletionsAsync(document, wrappedCursorPosition + safeCursor)`
- For each `CompletionItem`, retrieves description via `completionService.GetDescriptionAsync()`
- Returns `IReadOnlyList<(CompletionItem Item, string? Description)>`

**Method: `GetHoverAsync(string userQueryContent, int cursorPosition)` (async) → `HoverInfo?`**
- **Record type: `HoverInfo(string? Markdown, int StartOffset, int Length)`**
- Uses same cursor offset calculation as completions
- Gets syntax root and semantic model from document
- Finds token at cursor position: `root.FindToken(absolutePos)`
- **Symbol resolution strategy (prioritized):**
  1. `SimpleNameSyntax` (identifiers)
  2. `MemberAccessExpressionSyntax` (e.g., `context.People`)
  3. `InvocationExpressionSyntax` (method calls)
  4. `ExpressionSyntax` (fallback)
- If inside invocation argument (e.g., lambda), resolves to the invoked method instead
- **Extension method fallback**: If symbol not found, searches `Compilation.GetSymbolsWithName()` and specifically `System.Linq.Queryable` type members
- Filters extension methods by checking if receiver type is compatible with first parameter type
- Uses `SymbolEqualityComparer` and interface checks for compatibility
- If still not found, uses `semanticModel.LookupSymbols(absolutePos, name: token.ValueText)`
- Final fallback: `semanticModel.GetTypeInfo(candidateNode)`
- Displays symbol with `SymbolDisplayFormat.MinimallyQualifiedFormat`
- Includes XML documentation via `symbol.GetDocumentationCommentXml()`
- Returns hover info with **user-space coordinates**: `userStart = span.Start - wrappedCursorPosition`

**Method: `Dispose()`**
- Disposes `_workspace` and `_lock` (both in try/catch to ignore exceptions)

#### CompilerServiceFactory (`src/LinqStudio.Core/Services/CompilerServiceFactory.cs`)
- **Scoped service** used by UI to create `CompilerService` instances
- `CreateAsync()` method creates a new `CompilerService` with:
  - `_defaultContextTypeName = "TestDbContext"`
  - `_defaultProjectNamespace = "LinqStudio.TestModels"`
  - Hard-coded test model: `Person` class with `Id`, `Name`, `Age` properties
  - Hard-coded `TestDbContext` with `DbSet<Person> People`
  - Uses EF Core in-memory database: `UseInMemoryDatabase("LinqStudioTestDb")`

#### SettingsService (`src/LinqStudio.Core/Services/SettingsService.cs`)
- **Constant: `FILE_NAME = "usersettings.json"`**
- **Method: `Save(params IEnumerable<IUserSettingsSection> settings)` (async)**
  - Opens file with `FileMode.OpenOrCreate` and `FileAccess.ReadWrite` (single file handle to prevent concurrency issues)
  - Parses existing JSON as `JsonNode` (or creates empty `JsonObject` if file empty)
  - For each setting: `document[setting.SectionName] = JsonNode.Parse(JsonSerializer.Serialize((object)setting))`
  - Rewrites entire file: `file.Position = 0; file.SetLength(0); await JsonSerializer.SerializeAsync(file, document, JsonSerializerOptions.Indented)`
- **Pattern**: Merges changes into existing JSON structure, preserves other sections

#### Settings System
**Interface: `IUserSettingsSection` (`src/LinqStudio.Abstractions/Abstractions/IUserSettingsSection.cs`)**
- Single property: `string SectionName { get; }`

**Implementation: `UISettings` (`src/LinqStudio.Core/Settings/UISettings.cs`)**
- `record class UISettings : IUserSettingsSection`
- `[JsonIgnore] public string SectionName => nameof(UISettings);`
- `public bool IsDarkMode { get; set; } = true;`
- `public bool AlwaysReloadSettingsInSettingsPage { get; set; } = true;`

**Auto-registration pattern (`ServiceCollectionExtensions.AddLinqStudio()`):**
- Uses reflection to find all types implementing `IUserSettingsSection` (not interface, not abstract)
- For each type, invokes generic `AddOptions<TSettings>()` method via reflection
- `AddOptions<TSettings>()` calls `services.AddOptions<TSettings>().BindConfiguration(new TSettings().SectionName)`
- **Result**: All settings auto-registered and bound to configuration sections matching their `SectionName`

#### ProjectService (`src/LinqStudio.Core/Services/ProjectService.cs`)
**Constructor dependencies:**
- `ProjectVersionConfig _versionConfig` (default: `CurrentSchemaVersion = 1`, `MinSupportedSchemaVersion = 1`)

**Method: `CreateNew(string name, string connectionString = "")`**
- Returns new `Project` with default values, `SchemaVersion = _versionConfig.CurrentSchemaVersion`

**Method: `LoadProjectAsync(string filePath)` (async) → `Project?`**
- Reads JSON file via `JsonSerializer.DeserializeAsync<Project>`
- Validates schema version via `ValidateSchemaVersion()`: throws if `project.SchemaVersion > CurrentSchemaVersion` or `< MinSupportedSchemaVersion`
- Validates project data via `ValidateProject()`: auto-generates `Id`, `CreatedDate`, `ModifiedDate` if missing

**Method: `SaveProjectAsync(Project project, string filePath)` (async)**
- **Atomic save pattern**: Writes to temp file `{filePath}.{Guid:N}.tmp`, then `File.Move(tempFilePath, filePath, overwrite: true)`
- Updates `project.ModifiedDate = DateTimeOffset.UtcNow` and `project.SchemaVersion = _versionConfig.CurrentSchemaVersion` before saving
- Uses `JsonSerializer.SerializeAsync(stream, project, JsonSerializerOptions.Indented)`
- Cleans up temp file on failure

#### QueryService (`src/LinqStudio.Core/Services/QueryService.cs`)
**Constants:**
- `const string QueryFileExtension = ".linq.query"`

**Method: `GetQueriesDirectory(string projectFilePath)`**
- Returns `$"{projectFilePath}.queries"` (e.g., `MyProject.linq.queries/`)

**Method: `GetQueryFilePath(string projectFilePath, Guid queryId)`**
- Returns `Path.Combine(queriesDir, $"{queryId}{QueryFileExtension}")`

**Method: `LoadQueriesAsync(string projectFilePath)` (async)**
- Scans `GetQueriesDirectory()` for all `*.linq.query` files
- Deserializes each as `SavedQuery` (silently logs errors, continues loading others)

**Method: `SaveQueryAsync(string projectFilePath, SavedQuery query)` (async)**
- Same atomic save pattern as `ProjectService`: writes to temp file, then moves
- Validates `query.Id != Guid.Empty`

**Method: `DeleteQuery(string projectFilePath, Guid queryId)`**
- Deletes query file if exists

**Method: `LoadQueryFromFileAsync(string filePath)` / `SaveQueryToFileAsync(string filePath, SavedQuery query)`**
- Standalone mode methods for opening/saving individual query files outside of a project context
- `SaveQueryToFileAsync` updates `query.FilePath = filePath` after save

#### Models (`src/LinqStudio.Core/Models/`)
**Project.cs:**
- `int SchemaVersion` (default: `ProjectConstants.CurrentSchemaVersion = 1`)
- `Guid Id` (default: `Guid.NewGuid()`)
- `string Name`
- `DatabaseType DatabaseType` (enum: `Mssql`, `MySql`) with **field setter**: resets `QueryGenerator = null` when changed
- `string? ConnectionString` with **field setter**: resets `QueryGenerator = null` when changed
- `DateTimeOffset CreatedDate` / `ModifiedDate`
- `Dictionary<string, string>? Models` / `string? DbContextCode` (future properties)
- **`[JsonIgnore] IDatabaseQueryGenerator? QueryGenerator`**: Lazy-initialized based on `DatabaseType`:
  - `DatabaseType.Mssql => MssqlGenerator.Create(ConnectionString)`
  - `DatabaseType.MySql => MySqlGenerator.Create(ConnectionString)`
- `Task TestConnectionAsync(DatabaseType databaseType, string connectionString, int timeoutSeconds)`: Uses `CancellationTokenSource` with timeout, creates generator and calls `TestConnectionAsync()`

**SavedQuery.cs:**
- `Guid Id { get; init; }`
- `string Name { get; set; }`
- `string QueryText { get; set; }`
- `DateTimeOffset CreatedDate { get; init; }`
- `string? FilePath { get; set; }` (null if never saved)

**ProjectVersionConfig.cs:**
- Primary constructor: `ProjectVersionConfig(int currentVersion, int minVersion)`
- Default constructor: `this(currentVersion: 1, minVersion: 1)`
- `int CurrentSchemaVersion { get; }`
- `int MinSupportedSchemaVersion { get; }`

**ProjectConstants.cs:**
- `const int CurrentSchemaVersion = 1`
- `const int MinSupportedSchemaVersion = 1`

#### ServiceCollectionExtensions (`src/LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs`)
**Method: `AddLinqStudio(this IServiceCollection services)`**
- Registers **singletons**: `ProjectVersionConfig`, `ProjectService`, `QueryService`, `SettingsService`
- Registers **scoped**: `CompilerServiceFactory`
- Calls `AddAndBindOptions()` to auto-register all `IUserSettingsSection` implementations via reflection

**Private method: `AddAndBindOptions(IServiceCollection services)`**
- Finds all types in assembly implementing `IUserSettingsSection` (not interface, not abstract)
- Invokes `AddOptions<TSettings>()` via reflection for each type

**Private generic method: `AddOptions<TSettings>()` where TSettings : class, IUserSettingsSection, new()**
- `services.AddOptions<TSettings>().BindConfiguration(new TSettings().SectionName)`

#### JsonSerializerOptionsExtensions (`src/LinqStudio.Core/Extensions/JsonSerializerOptionsExtensions.cs`)
**⚠️ KNOWN ISSUE: Uses C# extension syntax (non-standard keyword `extension`)**
```csharp
private static readonly JsonSerializerOptions _indentedOptions = new() { WriteIndented = true };

extension(JsonSerializerOptions options)
{
    public static JsonSerializerOptions Indented => _indentedOptions;
}
```
- Provides `JsonSerializerOptions.Indented` static property
- **Note**: This is experimental C# syntax that may need refactoring to standard extension method pattern

#### LinqStudio.Abstractions Layer (`src/LinqStudio.Abstractions/`)
**IDatabaseQueryGenerator (`Abstractions/IDatabaseQueryGenerator.cs`):**
- `Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken = default)`
- `Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken = default)`
- Overload: `Task<DatabaseTableDetail> GetTableAsync(DatabaseTableName table, CancellationToken = default)` (default implementation: calls `GetTableAsync(table.FullName)`)
- `Task TestConnectionAsync(CancellationToken = default)`

**DatabaseType (Models/DatabaseType.cs):**
- `enum DatabaseType { Mssql, MySql }`

**DatabaseTableName (Models/DatabaseTableName.cs):**
- `record DatabaseTableName`
- `string? Schema { get; init; }` (e.g., "dbo", "public")
- `required string Name { get; init; }`
- `string FullName => Schema != null ? $"{Schema}.{Name}" : Name;`

**DatabaseTableDetail (Models/DatabaseTableDetail.cs):**
- `record DatabaseTableDetail : DatabaseTableName`
- `required IReadOnlyList<TableColumn> Columns { get; init; }`
- `required IReadOnlyList<ForeignKey> ForeignKeys { get; init; }`

**TableColumn (Models/TableColumn.cs):**
- `record TableColumn`
- Properties: `Name`, `DataType`, `IsNullable`, `IsPrimaryKey`, `IsIdentity`, `MaxLength?`, `Precision?`, `Scale?`

**ForeignKey (Models/ForeignKey.cs):**
- `record ForeignKey`
- Properties: `Name`, `ColumnName`, `ReferencedTable`, `ReferencedColumn`

#### LinqStudio.Database Layer (`src/LinqStudio.Database/` - note singular "Database" not "Databases")
**Namespace: `LinqStudio.Databases` (plural in namespace)**

**AdoNetDatabaseGeneratorBase (`AdoNetDatabaseGeneratorBase.cs`):**
- Abstract base class implementing `IDatabaseQueryGenerator`
- `protected DbConnection DbConnection { get; }`
- `GetTablesAsync()`: Uses `DbConnection.GetSchemaAsync("Tables")` and delegates row parsing to abstract `ParseTableFromSchemaRow(DataRow)`
- `TestConnectionAsync()`: Opens connection, executes `SELECT 1`, closes connection
- `protected static (string? schema, string name) ParseTableName(string tableName)`: Splits "schema.name" or returns "(null, name)"
- Abstract methods: `ParseTableFromSchemaRow(DataRow)`, `GetTableAsync(string tableName, CancellationToken)`

**MssqlGenerator (`MssqlGenerator.cs`):**
- Inherits `AdoNetDatabaseGeneratorBase`
- `static MssqlGenerator Create(string connectionString) => new(new SqlConnection(connectionString))`
- `ParseTableFromSchemaRow()`: Filters `TABLE_TYPE == "BASE TABLE"` (excludes views)
- `GetTableAsync()`: Defaults schema to "dbo"
  - Gets columns via `GetSchemaAsync("Columns")` with restrictions
  - Gets primary keys via `GetSchemaAsync("IndexColumns")`
  - Gets foreign keys via **SQL query** (sys.foreign_keys, sys.foreign_key_columns, sys.tables, sys.schemas)
  - Foreign key query uses `OBJECT_ID(@TableName)` with parameter `$"{schema}.{tableName}"`

**MySqlGenerator (`MySqlGenerator.cs`):**
- Inherits `AdoNetDatabaseGeneratorBase`
- `static MySqlGenerator Create(string connectionString) => new(new MySql.Data.MySqlClient.MySqlConnection(connectionString))`
- `ParseTableFromSchemaRow()`: Filters `TABLE_TYPE == "BASE TABLE"`
- `GetTableAsync()`: Defaults schema to `DbConnection.Database` (current database)
  - Gets columns via **INFORMATION_SCHEMA.COLUMNS query** (MySQL `GetSchema()` unreliable)
  - Detects primary keys via `COLUMN_KEY = "PRI"`
  - Detects identity via `EXTRA.Contains("auto_increment")`
  - Handles large values for LONGTEXT: clamps `maxLength` to `int.MaxValue` if exceeds
  - Gets foreign keys via **INFORMATION_SCHEMA.KEY_COLUMN_USAGE query** with `REFERENCED_TABLE_NAME IS NOT NULL`

#### LinqStudio.App.WebServer (`src/LinqStudio.App.WebServer/`)
**Program.cs startup pipeline:**
1. `builder.AddServiceDefaults()` — Aspire defaults (OpenTelemetry, health checks, service discovery)
2. `builder.Configuration.AddJsonFile(SettingsService.FILE_NAME, optional: true, reloadOnChange: true)` — Adds `usersettings.json` to configuration
3. `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
4. `builder.Services.AddLinqStudio().AddLinqStudioBlazor()`
5. `builder.Services.AddScoped<IFileSystemService, ServerFileSystemService>` — Server-specific file system service
6. `app.MapDefaultEndpoints()` — Maps health checks (development only)
7. `app.UseExceptionHandler("/Error")`, `app.UseHsts()` (non-development)
8. `app.UseStatusCodePagesWithReExecute("/not-found")`
9. `app.UseHttpsRedirection()`, `app.UseAntiforgery()`, `app.MapStaticAssets()`
10. `app.MapRazorComponents<App>().AddAdditionalAssemblies(typeof(LinqStudio.Blazor.Components.Pages.Home).Assembly).AddInteractiveServerRenderMode()`

**ServerFileSystemService (`Services/ServerFileSystemService.cs`):**
- Implements `IFileSystemService` (from `LinqStudio.Blazor.Abstractions`)
- Uses `NativeFileDialogSharp` library for cross-platform native file dialogs
- `PromptOpenFileAsync()` / `PromptSaveFileAsync()`: Run `Dialog.FileOpen()` / `Dialog.FileSave()` on background thread (`Task.Run`)
- Default path logic: `~/Documents/LinqStudio/` if exists, else `~/Documents/`, else `~/`, else current directory

#### LinqStudio.ServiceDefaults (`src/LinqStudio.ServiceDefaults/`)
**Extensions.cs:**
- `AddServiceDefaults<TBuilder>()`: Calls `ConfigureOpenTelemetry()`, `AddDefaultHealthChecks()`, `AddServiceDiscovery()`, `ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler().AddServiceDiscovery())`
- `ConfigureOpenTelemetry()`: Adds logging with formatted messages and scopes, metrics (ASP.NET Core, HttpClient, Runtime), tracing (ASP.NET Core, HttpClient), exports to OTLP if `OTEL_EXPORTER_OTLP_ENDPOINT` configured
- `AddDefaultHealthChecks()`: Adds "self" health check tagged "live"
- `MapDefaultEndpoints()`: Maps `/health` and `/alive` (development only, `/alive` filters to "live" tag)

#### LinqStudio.AppHost (`src/LinqStudio.AppHost/`)
**AppHost.cs:**
- Minimal Aspire orchestration: `builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")`
- Single project orchestration (no distributed services)

#### LinqStudio.Blazor Services (`src/LinqStudio.Blazor/`)
**MonacoProvidersService (`Services/MonacoProvidersService.cs`):**
- **Internal scoped service** to prevent duplicate Monaco provider registrations
- Tracks providers globally in `ConcurrentDictionary<string, HoverProvider.ProvideDelegate>` and `ConcurrentDictionary<string, CompletionItemProvider.ProvideDelegate>` keyed by `model.Uri`
- `RegisterHoverProviderAsync()` / `RegisterCompletionProviderAsync()`: Registers provider for editor, returns `IDisposable` to unregister
- **Lazy registration**: `RegisterAll()` called on first use, registers global providers once with `BlazorMonaco.Languages.Global.RegisterHoverProviderAsync()` / `RegisterCompletionItemProvider()`
- **Monaco readiness retry**: `RetryUntilMonacoReady()` retries up to 20 times with 250ms delay if Monaco JS not loaded yet (detects "monaco is not defined", "Cannot read properties of undefined")
- Delegates callbacks route to per-editor providers via `modelUri` lookup

**ProjectWorkspace (`Services/ProjectWorkspace.cs`):**
- **Scoped service** managing current open project state
- Properties: `Project? CurrentProject`, `string? CurrentFilePath`, `string CurrentProjectName`, `bool HasUnsavedChanges`, `bool IsProjectOpen`
- Tracks `_savedProject` (last persisted state) vs `_currentProject` (current state) to detect changes
- `HasUnsavedChanges`: Checks both project property changes AND query changes (`_queriesWorkspace.HasUnsavedChanges`)
- Methods: `CreateNewAsync(string name)`, `LoadAsync(string filePath)`, `SaveAsync()`, `SaveAsAsync(string filePath)`, `Update(Project updatedProject)`, `Close()`
- `SaveAsync()` / `SaveAsAsync()`: Calls `ProjectService.SaveProjectAsync()`, then reloads project to get updated `ModifiedDate`
- `CloneProject()`: Deep clones via `JsonSerializer.Serialize()` → `Deserialize()` to track changes
- Event: `WorkspaceChanged` raised on any state change

**QueriesWorkspace (`Services/QueriesWorkspace.cs`):**
- **Scoped service** managing query state for current project
- Tracks `Dictionary<Guid, OpenQueryState> _openQueries` (in-memory editor state) and `Dictionary<Guid, SavedQuery> _allQueries` (all queries for project)
- Properties: `Guid? CurrentQueryId`, `IReadOnlyDictionary<Guid, OpenQueryState> OpenQueries`, `IReadOnlyList<SavedQuery> AllQueries`, `bool HasUnsavedChanges`
- `InitializeAsync(string? projectFilePath)`: Loads all queries via `QueryService.LoadQueriesAsync()`, opens first query if any exist
- `CreateNewQuery(string? name)`: Generates unique name (appends counter if collision), creates query with default text `"// Write your LINQ query here\ncontext."`
- `UpdateQueryText(Guid queryId, string newText)`: Updates `OpenQueryState.CurrentText`, marks `HasUnsavedChanges = true` if differs from saved text
- `SaveQueryAsync(Guid queryId)`: Syncs `CurrentText` to `SavedQuery.QueryText`, calls `QueryService.SaveQueryAsync()`
- `SaveQueryWithDialogAsync()`: Uses callback to prompt for file path, saves to standalone file via `QueryService.SaveQueryToFileAsync()`
- `OpenQueryFromFileAsync(string filePath)`: Loads standalone query file via `QueryService.LoadQueryFromFileAsync()`
- Event: `QueriesChanged` raised on any query state change

**ErrorHandlingService (`Services/ErrorHandlingService.cs`):**
- Logs error via `ILogger<ErrorHandlingService>`
- Shows `ErrorDialog` via `IDialogService.ShowAsync<ErrorDialog>()`

**ServiceCollectionExtensions (`Extensions/ServiceCollectionExtensions.cs`):**
- `AddLinqStudioBlazor()`: Registers `MudServices()`, scoped services: `MonacoProvidersService`, `ErrorHandlingService`, `QueriesWorkspace`, `ProjectWorkspace`

#### Build Configuration
**Directory.Build.props:**
- `<TargetFramework>net10.0</TargetFramework>`
- `<LangVersion>latest</LangVersion>`
- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — **Warnings are errors in main projects**
- `<ImplicitUsings>enable</ImplicitUsings>`

**Project dependencies (NuGet):**
- **LinqStudio.Core**: `Microsoft.CodeAnalysis.CSharp` 5.0.0, `Microsoft.CodeAnalysis.CSharp.Features` 5.0.0, `Microsoft.EntityFrameworkCore` 10.0.1, `Microsoft.EntityFrameworkCore.Relational` 10.0.1
- **LinqStudio.Database**: `Microsoft.Data.SqlClient` 6.1.3, `MySql.Data` 9.5.0, `Microsoft.EntityFrameworkCore.Relational` 10.0.1
- **LinqStudio.App.WebServer**: `NativeFileDialogSharp` 0.5.0
- **LinqStudio.AppHost**: `Aspire.Hosting.AppHost` 13.0.2 (Aspire SDK 9.5.0)
- **LinqStudio.ServiceDefaults**: `Microsoft.Extensions.Http.Resilience` 10.1.0, `Microsoft.Extensions.ServiceDiscovery` 10.1.0, OpenTelemetry packages 1.14.0

**Project structure:**
- **LinqStudio.Abstractions**: Pure interfaces/models, no dependencies
- **LinqStudio.Core**: References Abstractions + Database, contains CompilerService, SettingsService, ProjectService, QueryService
- **LinqStudio.Database**: References Abstractions, contains MssqlGenerator, MySqlGenerator, AdoNetDatabaseGeneratorBase
- **LinqStudio.Blazor**: References Core, contains Razor components, MonacoProvidersService, ProjectWorkspace, QueriesWorkspace
- **LinqStudio.App.WebServer**: References Blazor + ServiceDefaults, ASP.NET Core host
- **LinqStudio.AppHost**: References App.WebServer, Aspire orchestration
- **LinqStudio.ServiceDefaults**: Shared Aspire configuration (health checks, OpenTelemetry, service discovery)

#### File Extensions & Constants
**FileExtensions (`src/LinqStudio.Blazor/Constants/FileExtensions.cs`):**
- `const string Project = "linq"`
- `const string Query = "linquery"`
- Extension methods: `WithDot(this string extension)`, `EnsureHasExtension(this string fileName, string extension)`

**Query file organization:**
- Project file: `MyProject.linq`
- Queries directory: `MyProject.linq.queries/`
- Query files: `MyProject.linq.queries/{Guid}.linquery`

#### Localization
- Resource files: `src/LinqStudio.Core/Resources/SharedResource.resx` (English), `SharedResource.fr.resx` (French)
- Auto-generated Designer class: `SharedResource.Designer.cs` (strongly-typed resource access)
- Settings descriptions stored in resource file with keys: `UserSettings.{SettingName}`, `UserSettings.{SettingName}.{PropertyName}`

#### Key Patterns
1. **Atomic file saves**: All file I/O uses temp file + move pattern to prevent corruption
2. **Cursor offset math**: All CompilerService cursor positions account for wrapper code via marker technique
3. **Auto-registration**: Settings auto-discovered via reflection, no manual DI registration
4. **Workspace pattern**: ProjectWorkspace and QueriesWorkspace manage in-memory state with change tracking
5. **Monaco provider isolation**: MonacoProvidersService prevents duplicate registrations across component instances
6. **Extension method resolution**: CompilerService fallback searches `Compilation.GetSymbolsWithName()` and `System.Linq.Queryable` type for LINQ extension methods
7. **Database abstraction**: IDatabaseQueryGenerator with ADO.NET base class, database-specific implementations for MSSQL and MySQL
8. **Aspire orchestration**: Single project orchestration with service defaults (OpenTelemetry, health checks, resilience)

#### No TODOs, FIXMEs, or HACKs
- Grep search found no `TODO`, `FIXME`, `HACK`, `XXX`, `TEMP`, or `WIP` comments in Core layer

#### Known Issues
1. **JsonSerializerOptionsExtensions.cs**: Uses experimental `extension()` syntax (may need refactoring to standard extension method pattern)
2. **BlazorMonaco rendering delay**: Known workaround with `Task.Delay(500)` before editor initialization (mentioned in project instructions)

### 2026-03-11 - Aspire Database Container Integration

**Task:** Added MSSQL and MySQL Docker containers via Aspire with demo data seeding using a shared library.

**Implementation:**

1. **Created LinqStudio.Demo** (src/LinqStudio.Demo/)
   - Shared library containing demo database models (Customer, Order, Product, OrderItem)
   - DemoDbContext with proper EF Core configuration (decimal precision, foreign keys, cascade rules)
   - BogusDataGenerator static class for generating fake data using Bogus library
   - DemoSeeder class with async seeding logic that checks for existing data before seeding
   - Models reused from existing test data structure (	ests/LinqStudio.Databases.Tests/TestData/)
   - Package refs: Bogus 35.6.5, EF Core 10.0.1, MSSQL + MySQL providers

2. **Created LinqStudio.DatabaseSeeder** (src/LinqStudio.DatabaseSeeder/)
   - Console app that reads connection strings from environment variables injected by Aspire
   - Seeds both MSSQL and MySQL in parallel using Task.WhenAll
   - Implements retry logic (10 retries, 3-second delay) to handle container startup timing
   - Detects provider type and configures DbContext accordingly

3. **Updated LinqStudio.AppHost** (src/LinqStudio.AppHost/)
   - Added Aspire.Hosting.SqlServer 9.5.0 and Aspire.Hosting.MySql 9.5.0 packages
   - Configured two containers with persistent lifetime: demo-mssql and demo-mysql
   - Seeder waits for both DBs with WaitFor() calls
   - Main app references both DBs with WithReference() (connection strings visible in Aspire dashboard)
   - App waits for seeder completion with WaitForCompletion() before starting
   - Used ContainerLifetime.Persistent to keep containers running across restarts

4. **Updated LinqStudio.slnx**
   - Added both new projects to solution file
   - Also enabled building of LinqStudio.Databases.Tests (was disabled with <Build Project="false" />), which fixed E2E test compilation errors that referenced TestData namespace

5. **Decision on Tests:**
   - Kept existing LinqStudio.Databases.Tests with its own copy of models/seeder unchanged
   - E2E tests depend on that namespace, so enabling the project build fixed compilation
   - Both Demo and Databases.Tests have duplicate code, but this is safe and avoids test fragility

**Learnings:**

- **Aspire 9.5 API:** WithLifetime(), WithReference(), WaitFor(), WaitForCompletion() all work as expected
- **Connection string injection:** Aspire injects as ConnectionStrings__<name> environment variable (double underscore)
- **Seeding pattern:** Retry + EnsureCreated + "check if already seeded" is robust for container startup timing
- **Project dependencies:** E2E tests reference Databases.Tests, so that project must build even if not run directly
- **Build validation:** Clean build + full test run confirmed no regressions (all 100 unit tests passed, E2E tests passed)

**Files created:**
- src/LinqStudio.Demo/LinqStudio.Demo.csproj
- src/LinqStudio.Demo/Models.cs
- src/LinqStudio.Demo/DemoDbContext.cs
- src/LinqStudio.Demo/BogusDataGenerator.cs
- src/LinqStudio.Demo/DemoSeeder.cs
- src/LinqStudio.Demo/copilot.md
- src/LinqStudio.DatabaseSeeder/LinqStudio.DatabaseSeeder.csproj
- src/LinqStudio.DatabaseSeeder/Program.cs
- src/LinqStudio.DatabaseSeeder/copilot.md

**Files modified:**
- src/LinqStudio.AppHost/LinqStudio.AppHost.csproj (added Aspire DB packages + seeder project reference)
- src/LinqStudio.AppHost/AppHost.cs (orchestration logic)
- LinqStudio.slnx (added two projects, enabled Databases.Tests build)

**Testing:**
- All unit tests pass (Core: 45, Blazor: 39, Databases: 16)
- E2E tests pass (8 passed, 1 skipped - the Aspire dashboard test that requires manual run)
- Build is clean with no warnings or errors

### 2026-03-11 - Fixed Aspire Database Seeder MySQL Compatibility Issue

**Problem:**
The DatabaseSeeder console app was failing with MySQL using MySql.EntityFrameworkCore version 9.0.9, which is incompatible with EF Core 10.0.1. Error: "Method not found: 'Microsoft.EntityFrameworkCore.Storage.IRelationalCommandBuilder Microsoft.EntityFrameworkCore.Storage.IRelationalCommandBuilder.Append(System.String)'."

**Root Cause:**
- `MySql.EntityFrameworkCore` v9.0.9 only supports EF Core 9.x
- `Pomelo.EntityFrameworkCore.MySql` (more popular MySQL provider) v9.0.0 also only supports EF Core 9.x
- Oracle's official `MySql.EntityFrameworkCore` v10.0.1 is the only MySQL provider supporting EF Core 10.x

**Solution:**
1. Updated `LinqStudio.Demo.csproj` to use `MySql.EntityFrameworkCore` v10.0.1 (removed incorrect Pomelo package)
2. Updated `LinqStudio.DatabaseSeeder.csproj` to use `MySql.EntityFrameworkCore` v10.0.1
3. Verified `Program.cs` uses `UseMySQL()` method (correct for Oracle provider)
4. Aspire `AppHost.cs` already correctly configured with `.AddDatabase()` for database-scoped connection strings

**Verification:**
- Both MSSQL and MySQL seeded successfully in Aspire
- All 84 tests pass (excluding E2E and Databases.Tests)
- Build clean with 0 errors

**Key Learning:**
- For EF Core 10 with MySQL, use Oracle's `MySql.EntityFrameworkCore` v10.0.1 (not Pomelo)
- Pomelo is more popular but lags behind on EF Core version support
- Always use `.AddDatabase()` in Aspire for database-scoped connection strings, not just `.AddSqlServer()` or `.AddMySql()`

### 2026-03-11 - Fixed Seeder Exit Code Issue (0xE0434352)

**Problem:**
The DatabaseSeeder console app was successfully seeding both MSSQL and MySQL databases (confirmed by logs showing "[MSSQL] Seeded successfully." and "[MySQL] Seeded successfully."), BUT the process was exiting with code **-532462766** (hex: 0xE0434352), which is the Windows SEH exception wrapper for unhandled .NET exceptions. This caused `WaitForCompletion(seeder)` in AppHost to never release, preventing `linqstudio-app-webserver` from starting.

**Root Cause:**
The `Program.cs` in DatabaseSeeder had no top-level exception handling. While the individual seeding tasks were succeeding, if one database failed all 10 retries while the other succeeded, the `throw new Exception()` on line 49 would propagate through `Task.WhenAll()`, causing an unhandled exception and the bad exit code.

Additionally, there was no explicit `Environment.Exit(0)` call on successful completion, which could lead to inconsistent exit codes.

**Solution:**
Wrapped the entire `Program.cs` main logic in a try-catch block that:
1. Catches any exceptions and logs them to `Console.Error` with full stack trace
2. Explicitly calls `Environment.Exit(1)` on failure
3. Explicitly calls `Environment.Exit(0)` on success after "Demo seeding complete." message

**Implementation:**
- Modified `src/LinqStudio.DatabaseSeeder/Program.cs` to add top-level try-catch
- Success path: prints "Demo seeding complete." then exits with code 0
- Failure path: prints "Fatal seeder error: {exception}" to stderr then exits with code 1

**Verification:**
1. Built seeder: `dotnet build src\LinqStudio.DatabaseSeeder\LinqStudio.DatabaseSeeder.csproj` — succeeded
2. Ran Aspire AppHost: `dotnet run --project src\LinqStudio.AppHost\LinqStudio.AppHost.csproj`
3. Confirmed seeder process completed and exited cleanly (no longer in process list)
4. Confirmed `linqstudio-app-webserver` started 4 seconds AFTER AppHost, proving `WaitForCompletion()` was released
5. Ran full test suite: `dotnet test` excluding E2E and DB tests — all 84 tests passed

**Key Learning:**
- Aspire's `WaitForCompletion()` requires the dependent process to exit with code 0
- Exit code 0xE0434352 is a Windows-specific indicator of unhandled .NET exceptions
- Always use explicit top-level exception handling in console apps used as Aspire dependencies
- Always use `Environment.Exit(0)` explicitly on success for clarity and consistency

**Files Modified:**
- src/LinqStudio.DatabaseSeeder/Program.cs (added try-catch wrapper with explicit exit codes)
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

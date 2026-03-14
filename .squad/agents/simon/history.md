# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-13 - Query Execution Pipeline - Deep Technical Analysis

**Task:** Comprehensive analysis of CompilerService and backend pipeline to design query execution feature (requested by snakex64).

**Critical Finding:** CompilerService provides **IntelliSense ONLY** — there is **NO runtime query execution** anywhere in the codebase.

**Current State:**
- QueryContainer wrapper exists with correct signature: `public async Task<IQueryable<object>> Query(DbContext context)`
- QueryContainer code lives ONLY in Roslyn's AdhocWorkspace semantic model (never compiled to executable IL)
- DbContext is generated as **source code only**, used for type resolution, never instantiated at runtime
- Generated DbContext has **stub configuration** using `UseInMemoryDatabase()` — not suitable for execution

**Gap Analysis:**
1. **Missing IL Compilation:** Need `CSharpCompilation.Emit()` to compile to in-memory assembly
2. **Missing Assembly Loading:** Need `Assembly.Load(byte[])` to load compiled assembly
3. **Missing Reflection Invocation:** Need to locate `QueryContainer.Query()` method and invoke via reflection
4. **Missing DbContext Lifecycle:** Need to modify generator to create DbContext with real connection (not in-memory stub)
5. **Missing Result Materialization:** Need to call `.ToListAsync()` on returned `IQueryable<object>`
6. **Missing Column Extraction:** Need reflection on first row to extract property names/types for dynamic columns

**Proposed Solution:**
- New service: `QueryExecutionService` (separate from CompilerService for SoC)
- Method signature: `Task<QueryExecutionResult> ExecuteQueryAsync(string userQuery, ..., CancellationToken cancellationToken)`
- Result model: `QueryExecutionResult` with `Success`, `Rows`, `Columns`, `ErrorMessage`, `ExecutionTimeMs`
- Error types: `CompileError`, `RuntimeError`, `DatabaseError`

**DbContext Modification Strategy:**
- Change `DbContextGenerator.GenerateDbContext()` to use constructor injection pattern:
  ```csharp
  public GeneratedDbContext(DbContextOptions<GeneratedDbContext> options) : base(options) { }
  ```
- Remove stub `OnConfiguring()` override with `UseInMemoryDatabase()`
- At execution time, create `DbContextOptions` with real connection string

**Technical Risks & Mitigations:**
1. **Assembly Memory Leaks:** Each query compiles new assembly that can't be unloaded from default AssemblyLoadContext
   - Mitigation: Implement assembly caching with LRU eviction (max 100 assemblies)
   - OR: Use `AssemblyLoadContext` with `isCollectible: true` (.NET 10 feature)
2. **Long-Running Queries:** User could write `context.HugeTable.ToListAsync()` loading millions of rows
   - Mitigation: Add configurable row limit (default 10,000 rows) by wrapping query with `.Take(maxRows)`
3. **DbContext Disposal:** Risk of disposing context before query materializes
   - Mitigation: Materialize results BEFORE disposing context using `using` statement
4. **Anonymous Type Serialization:** Anonymous types can't be serialized for SignalR/Blazor Server
   - Mitigation: Convert to `Dictionary<string, object?>` in result model

**Implementation Estimate:** 14-19 hours
- Backend (QueryExecutionService): 6-8 hours (400-600 LOC)
- DbContext generation changes: 1 hour (20 LOC)
- Models & DTOs: 1 hour (100 LOC)
- UI integration (ResultsGrid): 2-3 hours
- Testing: 4-6 hours

**Detailed Analysis:** Written to `.squad/decisions/inbox/simon-query-execution-analysis.md` (39KB document with:
- Complete CompilerService architecture analysis
- QueryContainer wrapper exact structure
- Proposed `ExecuteQueryAsync()` method with full implementation strategy
- Step-by-step compilation/execution pipeline (7 steps with code samples)
- Reflection strategy for dynamic column extraction
- 6 technical risks with mitigations
- Reference implementation outline

**Key Learnings:**
1. **CompilerService is compilation-only** — loads all AppDomain assemblies as metadata references for IntelliSense, never executes code
2. **Cursor position adjustment** — critical pattern using `__THIS_HERE__` marker to calculate wrapper overhead
3. **QueryContainer signature already correct** — `Task<IQueryable<object>>` is exactly what execution needs
4. **DbContext generation needs modification** — current stub `OnConfiguring()` prevents real database connections
5. **Roslyn compilation API** — `CSharpCompilation.Create() → Emit()` can compile to in-memory assembly for dynamic execution

**Next Steps (for implementation):**
1. Create `QueryExecutionService.cs` in Core
2. Modify `DbContextGenerator.GenerateDbContext()` to use constructor injection
3. Add `QueryExecutionResult` model to Core.Models
4. Implement 7-step execution pipeline (wrap → compile → load → instantiate → invoke → materialize → extract columns)
5. Add Execute button to Editor UI with ResultsGrid component
6. Write unit tests and integration tests with real database

### 2026-03-13 - Team Review Cycle - Full Backend Assessment

Completed full backend review. Score: 9/10. Backend architecture fundamentally strong. Identified 9 issues: key duplication in query generation (3 instances), missing test coverage for edge cases, documentation gaps. MSSQL auto-discovery and connection handling validated.

### 2026-03-13 - Comprehensive Backend/Core Code Review Completed

**Scope:** Reviewed all backend/core components (~3,500 LOC):
- CompilerService (Roslyn integration) + tests
- SettingsService & Settings pattern
- Database generators (MSSQL, MySQL, PostgreSQL, SQLite) + tests
- ProjectService & QueryService + tests  
- Core abstractions & models

**Key Findings:**
- **Zero critical bugs** — production-ready architecture
- **310 tests passing** (4 skipped E2E requiring SQLite setup)
- 2 medium bugs: SQLite identifier sanitization overly restrictive, base class should use `await using` for DbCommand
- 5 code duplication opportunities across DB generators (connection open/close pattern repeated 12+ times)
- Missing test coverage: SettingsService (0 tests), QueryService (only indirect), CompilerService concurrency

**Architecture Quality Assessment:**
1. **Thread Safety:** ✅ CompilerService correctly uses SemaphoreSlim for workspace mutations
2. **Atomic File I/O:** ✅ Temp file + File.Move pattern prevents corruption in ProjectService/QueryService  
3. **Connection Management:** ⚠️ Pattern is correct but duplicated across all generators — extracting to base class helper would reduce 40+ lines of boilerplate
4. **Error Handling:** ✅ Appropriate exception types, defensive validation in ProjectService
5. **Type Safety:** ✅ Proper nullable annotations, no unsafe casts
6. **Performance:** ✅ No blocking calls, proper async/await throughout

**Database Generator Patterns:**
- Base class (AdoNetDatabaseGeneratorBase) provides GetTablesAsync foundation using `GetSchemaAsync("Tables")`
- Subclasses override with DB-specific queries when needed (e.g., MSSQL dynamic cross-database SQL)
- Connection state management: open if closed, restore state on exit — pattern works but is repetitive
- All generators use parameterized queries ✅ (except SQLite PRAGMA which doesn't support params)

**Critical Design Decisions Validated:**
1. **No Git commits** — per team directive, files written but not committed ✅
2. **Test infrastructure uses named databases** (not master) — decision #10 validated through MssqlDatabaseFixture ✅  
3. **CompilerService assembly loading** — decision #3 monitoring point confirmed, intentional tradeoff for full IntelliSense ✅
4. **DatabaseSeeder explicit exit codes** — decision #8 correctly implemented ✅

**SQLite-Specific Learning:**
SQLite's PRAGMA commands don't support parameterized table names. Current implementation uses `SanitizeIdentifier()` which strips non-alphanumeric characters. This is overly restrictive — SQLite allows spaces, dots, and special chars in identifiers when quoted. Recommended fix: use SQLite's double-quote identifier syntax: `PRAGMA table_info("table name")` with embedded quotes escaped as `""`.

**Connection Disposal Ambiguity:**
Database generators accept DbConnection via constructor but don't implement IDisposable. Factory methods (`Create(connectionString)`) create new connections but don't track ownership. Recommendation: Add `_ownsConnection` flag and implement IDisposable to dispose connections created by factory methods.

**Test Quality Observations:**
- ProjectServiceTests.cs is exemplary (576 lines, covers concurrency, corruption, version compatibility)
- Database integration tests use Testcontainers (real databases, not mocks) — excellent practice
- CompilerServiceTests cover basic scenarios but miss concurrency edge cases (multiple Monaco callbacks triggering simultaneous GetCompletionsAsync)
- SettingsService has ZERO test coverage despite being critical for persistence

**Action Items Written:** 9 prioritized items in review document:
- P1: Fix SQLite identifier quoting, add SettingsService tests, document connection ownership
- P2: Extract connection management helper, add QueryService tests, fix MSSQL identity detection  
- P3: Refactor column/FK parsing, CompilerService edge case tests, replace Console.WriteLine with ILogger

**Ship Confidence:** 9/10 — recommend addressing P1 items before v1.0, all others can be deferred to maintenance releases.

### 2026-03-13 - Team Sprint: MSSQL Auto-Discovery Removal & Validation Hardening

**Squad Completion:**
- Simon: Removed auto-discovery from MssqlGenerator, added fail-fast validation in Create() and Project.UpdateConnection()
- EvilJosh: Fixed EditProjectDialog Save() validation, resolved DatabaseTreeView cache access race conditions, fixed test cleanup
- Alex: Comprehensive code review documenting patterns and edge cases
- Status: ✅ 407 tests passing, orchestration logs written, decisions merged

**Decision Documented:** Remove MSSQL Auto-Discovery from MssqlGenerator (2026-03-13)
- Auto-discovery picked "first user database alphabetically" (non-deterministic, wrong for multi-DB servers)
- Connection.ChangeDatabase() mutated pooled connections (connection pool poisoning)
- Config errors masked (missing Database= silently ignored)
- **Solution:** Fail-fast validation requiring explicit database in connection string

**Cross-Agent Updates:**
- Simon's history.md updated with auto-discovery removal and NULL handling learnings
- EvilJosh's history.md updated with validation and caching patterns
- Alex's history.md updated with code review findings and recommendations
- Scribe's history.md updated with team context and sprint completion

### 2026-03-13 - Removed MSSQL Auto-Discovery Logic

**Problem (flagged by Alex - Code Reviewer):** `MssqlGenerator` had auto-discovery logic in `GetTableAsync` that:
1. Checked if connected to `master` database
2. Called `FindFirstUserDatabaseAsync` to pick the "first" user database arbitrarily
3. Called `Connection.ChangeDatabase()` to switch — which mutates pooled connections

**Why it was harmful:**
- Silently masked missing `Database=` in connection string configs
- `ChangeDatabase()` on a pooled connection poisons the connection pool
- "First user database alphabetically" is non-deterministic and wrong for multi-database servers

**Solution:** Fail fast in `Create()` using `SqlConnectionStringBuilder` to validate `InitialCatalog` is set. Connection string must explicitly name the target database. `GetTablesAsync` was already correct (uses server-level cross-database dynamic SQL via `sys.databases`) and needed no changes.

**Key Learning:** Connection strings for MSSQL must always include `Database=` or `Initial Catalog=`. Never auto-discover or switch databases on pooled connections. Use `SqlConnectionStringBuilder` to parse and validate connection strings before constructing the connection.

### 2026-03-12 - MSSQL NULL Handling in Named Databases

**Problem:** MssqlGenerator.GetTablesAsync() returned 0 tables when connecting to production named databases, despite tests passing.

**Root Cause:** OBJECT_ID() returns NULL for user tables in named databases under certain conditions. The query used:
```sql
AND OBJECTPROPERTY(OBJECT_ID(...), 'IsMSShipped') = 0
```
When OBJECT_ID returns NULL, OBJECTPROPERTY(NULL, ...) returns NULL, causing the WHERE clause to evaluate to UNKNOWN (treated as FALSE), silently excluding all user tables.

**Solution:** Wrap OBJECTPROPERTY with ISNULL:
```sql
AND ISNULL(OBJECTPROPERTY(OBJECT_ID(...), 'IsMSShipped'), 0) = 0
```

**Key Learning:** SQL Server metadata functions (OBJECTPROPERTY, OBJECT_ID, COLUMN_PROPERTY, etc.) can return NULL in non-master databases. Always use ISNULL/COALESCE when comparing with values in WHERE clauses. NULL = 0 evaluates to UNKNOWN, not FALSE.

### 2026-03-13 - Backend Bug Fixes from Code Review

**Task:** Fixed 4 backend bugs identified in Alex's comprehensive code review.

**Fix 1: CompilerService Empty Catch Blocks (High Priority)**
- **Problem:** 6 empty catch blocks in CompilerService.cs silently swallowed exceptions with no diagnostics
- **Solution:** Added `System.Diagnostics.Debug.WriteLine($"[CompilerService] Error: {ex}")` to all empty catch blocks
- **Locations:** Lines 57, 75, 86, 365, 401, 484, 490
- **Impact:** Behavior unchanged (still uses fallbacks), but now provides visibility into assembly loading failures, Roslyn API errors, and disposal issues during development/debugging

**Fix 2: Database Generators CommandTimeout (Medium Priority)**
- **Problem:** All DbCommand instances created without timeout, risking indefinite hangs on slow/unresponsive databases
- **Solution:** Added `command.CommandTimeout = 30;` after every `CreateCommand()` call
- **Files:** MssqlGenerator.cs (3 locations), MySqlGenerator.cs (2 locations), PostgreSqlGenerator.cs (2 locations), SqliteGenerator.cs (4 locations)
- **Impact:** Database queries now timeout after 30 seconds instead of blocking indefinitely

**Fix 3: Input Validation in GetTableAsync (Medium Priority)**
- **Problem:** Missing null/whitespace validation on tableName parameter in all 4 database generators
- **Solution:** Added `ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));` at start of each GetTableAsync method
- **Files:** MssqlGenerator.cs, MySqlGenerator.cs, PostgreSqlGenerator.cs, SqliteGenerator.cs
- **Impact:** Fail-fast with clear error message instead of cryptic SQL errors or null reference exceptions

**Fix 4: Base Class await using for DbCommand (Low Priority)**
- **Problem:** AdoNetDatabaseGeneratorBase.TestConnectionAsync() used `using var command` instead of `await using var command`
- **Solution:** Changed to `await using var command` to properly dispose IAsyncDisposable
- **File:** AdoNetDatabaseGeneratorBase.cs line 73
- **Impact:** Ensures async disposal of DbCommand resources in base class test method

**Test Results:** All 421 tests pass (417 passed, 4 skipped E2E tests requiring SQLite setup). Zero test failures. Changes are surgical and maintain existing behavior while adding diagnostics and safety.

**Key Learning:** Empty catch blocks should always include Debug.WriteLine for development visibility. CommandTimeout should be set on all database commands to prevent production hangs. Input validation belongs at method entry, not deep in SQL execution. Prefer `await using` over `using` for types implementing IAsyncDisposable.

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

---

### 2026-03-11 - MSSQL GetTablesAsync NULL Handling Fix
**Location:** `src/LinqStudio.Database/MssqlGenerator.cs` line 96

**Root Cause:**
- SQL Server's `OBJECTPROPERTY(OBJECT_ID(...), 'IsMSShipped')` can return NULL when `OBJECT_ID()` returns NULL
- This happens for some user tables in named databases under certain conditions
- `NULL = 0` evaluates to UNKNOWN in SQL WHERE clauses, treated as FALSE → silently excludes valid user tables
- Tests passed because they connected to `master` database where OBJECT_ID worked correctly
- Production failed when connecting to named databases like `linqstudio-mssql-demo`

**Fix Applied:**
Changed line 96 from:
```sql
AND OBJECTPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), 'IsMSShipped') = 0
```
To:
```sql
AND ISNULL(OBJECTPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), 'IsMSShipped'), 0) = 0
```

**Why This Works:**
- System tables (IsMSShipped = 1) still excluded ✓
- User tables (IsMSShipped = 0) included ✓
- Edge case where OBJECT_ID returns NULL → defaults to 0 → included ✓
- Safe: doesn't change behavior for valid objects, only handles NULL gracefully

**Learning:** When using SQL Server metadata functions that can return NULL, always wrap with ISNULL/COALESCE to avoid silent filtering of valid rows in WHERE clauses.

---

### 2026-03-11 - DatabaseTreeView Backend Analysis

**Task:** Comprehensive backend stack analysis for DatabaseTreeView feature to identify any blockers before Alice's live testing.

**Analyzed Components:**
1. Aspire AppHost password configuration (`src/LinqStudio.AppHost/AppHost.cs`)
2. DatabaseSeeder implementation (`src/LinqStudio.DatabaseSeeder/Program.cs`, `src/LinqStudio.Demo/`)
3. Project.QueryGenerator property and cache invalidation (`src/LinqStudio.Core/Models/Project.cs`)
4. IDatabaseQueryGenerator implementations (`src/LinqStudio.Database/`)
5. Connection string format and Aspire WithReference behavior

**Key Findings:**

**✅ Aspire Password Hard-coding (CORRECT):**
- `AddParameter("sql-password", value: "Password123!", secret: false)` creates a parameter with hard-coded default value
- Aspire passes this to the SQL Server container as `MSSQL_SA_PASSWORD` environment variable
- Container sets SA password to `Password123!` on initialization
- `ContainerLifetime.Persistent` ensures password stays consistent across restarts
- Connection string injected into WebServer: `ConnectionStrings__DemoMssql=Server=localhost,14330;Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true`
- MySQL similar: `ConnectionStrings__DemoMysql=Server=localhost;Port=13306;Database=linqstudio-mysql-demo;User=root;Password=root_password_123;`

**✅ DatabaseSeeder (CORRECT):**
- Seeds 4 tables: Customers, Products, Orders, OrderItems
- All tables have `Id` int primary keys (configured via `entity.HasKey(e => e.Id)`)
- Foreign keys configured with cascade/restrict behaviors
- Proper constraints: max lengths, precision/scale, required fields
- All columns compatible with DatabaseTreeView expectations (IsPrimaryKey, IsNullable, DataType)
- Retry logic: 10 attempts with 3-second delays for each database
- Proper exit codes: `Environment.Exit(0)` on success, `Environment.Exit(1)` on failure

**✅ Project.QueryGenerator (CORRECT):**
- Property setter for `DatabaseType` and `ConnectionString` both reset `QueryGenerator = null`
- Lazy initialization on access: creates generator based on DatabaseType switch
- All 4 database types supported: Mssql, MySql, PostgreSql, Sqlite
- Cache invalidation working correctly — DatabaseTreeView tracks connection identity changes and reloads tables only when connection changes

**✅ IDatabaseQueryGenerator Implementations (CORRECT with one minor issue):**
- All generators inherit `AdoNetDatabaseGeneratorBase` with proper ADO.NET schema introspection
- `GetTablesAsync()` returns `DatabaseTableName` with Schema and Name
- `GetTableAsync()` returns `DatabaseTableDetail` with columns and foreign keys
- Column metadata includes: Name, DataType, IsNullable, IsPrimaryKey, MaxLength, Precision, Scale
- Foreign keys properly queried (MSSQL uses `sys.foreign_keys`, MySQL uses `INFORMATION_SCHEMA.KEY_COLUMN_USAGE`)

**⚠️ Minor Issue: IsIdentity Always False for MSSQL:**
- Location: `MssqlGenerator.cs`, line 190: `var isIdentity = false;`
- Comment: "GetSchema doesn't provide identity info, so we'll default to false"
- Impact: Lightning bolt icon (⚡) never displays in DatabaseTreeView for identity columns
- Primary key icon (🔑) still displays correctly
- **Not a blocker** — cosmetic only, does not affect query execution or data functionality
- MySQL correctly detects identity via `EXTRA.Contains("auto_increment")`
- Recommendation: P3 enhancement — implement custom query against `sys.identity_columns` for MSSQL

**Connection String Format for Manual Connections:**
- SQL Server: `Server=localhost,14330;Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true`
- MySQL: `Server=localhost;Port=13306;Database=linqstudio-mysql-demo;User=root;Password=root_password_123;`
- Ports are fixed (14330 for MSSQL, 13306 for MySQL) via `port:` parameter in AppHost

**DatabaseTreeView Integration:**
- Component checks `Workspace.CurrentProject?.QueryGenerator != null` before loading
- Tracks connection identity (`_trackedConnectionString`, `_trackedDatabaseType`) to avoid unnecessary DB round-trips on unrelated workspace changes
- Lazy-loads table details when user expands a table node
- Proper error handling via `ErrorHandlingService.HandleErrorAsync()`
- Loading states displayed during DB operations
- Column icon logic: 🔑 for PK (using `IsPrimaryKey`), ⚡ for identity (using `IsIdentity`), default icon for regular columns
- Type formatting: `varchar(100)?`, `decimal(18,2)`, `int`

**Conclusion:**
- ✅ Backend is production-ready with no blockers
- ✅ All 4 database types properly implemented
- ✅ Aspire password mechanism working correctly
- ✅ DatabaseSeeder creates proper demo data with all constraints
- ✅ Connection string injection via Aspire working as expected
- ⚠️ IsIdentity limitation is cosmetic only, not a blocker

**Deliverables:**
- Comprehensive analysis written to `.squad/decisions/inbox/simon-backend-analysis.md`
- Documented connection strings for Alice's live testing
- Identified one P3 enhancement (IsIdentity detection for MSSQL)
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

**Status:** ✅ Fix implemented, tested, documented, deployed — ready for final sign-off

---

### 2025-01-XX - Database Introspection API Analysis for Table Tree View Feature

#### Key Interfaces and Models

**`IDatabaseQueryGenerator` Interface** — `src/LinqStudio.Abstractions/Abstractions/IDatabaseQueryGenerator.cs`
- Primary abstraction for database schema introspection
- Methods:
  - `Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken)` — Flat list of all tables
  - `Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken)` — Detailed table info with columns and FKs
  - `Task TestConnectionAsync(CancellationToken)` — Connection validation
  - `DbColumnType MapToGenericType(string dataType)` — Database-specific to generic type mapping

**Data Models** — `src/LinqStudio.Abstractions/Models/`
- `DatabaseTableName` — Lightweight (schema + name + FullName property)
- `DatabaseTableDetail : DatabaseTableName` — Adds `Columns` (IReadOnlyList<TableColumn>) and `ForeignKeys` (IReadOnlyList<ForeignKey>)
- `TableColumn` — Complete metadata (Name, DataType, GenericType, IsNullable, IsPrimaryKey, IsIdentity, MaxLength, Precision, Scale)
- `DbColumnType` enum — 23 values mapping database types to C# types (Int32, String, DateTime, Decimal, etc.)
- `ForeignKey` — Relationship metadata (Name, ColumnName, ReferencedTable, ReferencedColumn)
- `DatabaseType` enum — Mssql, MySql, PostgreSql, Sqlite

#### Database Generators Architecture

**Base Class:** `AdoNetDatabaseGeneratorBase` — `src/LinqStudio.Database/AdoNetDatabaseGeneratorBase.cs`
- Uses raw ADO.NET (`DbConnection`) for database introspection
- Manages connection lifecycle (opens/closes as needed)
- Template method for `GetTablesAsync()` using `DbConnection.GetSchemaAsync("Tables")`
- Abstract methods: `ParseTableFromSchemaRow(DataRow)`, `GetTableAsync(string tableName)`, `MapToGenericType(string dataType)`
- Static helper: `ParseTableName(string)` — Splits "schema.name" or "name" into (schema, name) tuple

**Concrete Implementations:**
- `MssqlGenerator` — SQL Server (uses `sys.foreign_keys`, `sys.columns`, defaults schema to "dbo")
- `MySqlGenerator` — MySQL/MariaDB (uses `INFORMATION_SCHEMA`, defaults schema to `Connection.Database`)
- `PostgreSqlGenerator` — PostgreSQL (uses `INFORMATION_SCHEMA` with custom queries, defaults schema to "public")
- `SqliteGenerator` — SQLite (overrides `GetTablesAsync()` to query `sqlite_master`, uses `PRAGMA table_info` and `PRAGMA foreign_key_list`, defaults schema to "main")

**Factory Pattern:** Each generator has `Create(string connectionString)` static method returning configured instance

#### Integration with Core

**`Project.QueryGenerator` Property** — `src/LinqStudio.Core/Models/Project.cs`
- Lazy-initialized `IDatabaseQueryGenerator?` property (JsonIgnored)
- Creates generator on first access based on `DatabaseType` and `ConnectionString`
- Automatically reset when connection string or database type changes (via property setters)
- Factory logic: switches on `DatabaseType` enum, calls appropriate `*.Create(connectionString)` method

**Connection Testing:**
- `Project.TestConnectionAsync(DatabaseType, string connectionString, int timeoutSeconds)` — Validates connection with timeout

#### Test Coverage

**Base Test Suite:** `tests/LinqStudio.Databases.Tests/BaseGeneratorTests.cs`
- Abstract class with 8 unit tests (derived classes inherit):
  - `GetTablesAsync_ShouldReturnAllTables` — Verifies table listing
  - `GetTableAsync_ShouldReturnTableWithColumns` — Verifies column retrieval
  - `GetTableAsync_ShouldReturnTableWithForeignKeys` — Verifies FK retrieval
  - `GetTableAsync_ShouldReturnTableWithMultipleForeignKeys` — Multi-FK tables
  - `GetTableAsync_ShouldReturnColumnDataTypes` — Type mapping accuracy
  - `GetTableAsync_ShouldReturnNullableInformation` — Nullability detection
  - `TestConnectionAsync_ShouldSucceed` — Connection validation
  - `TestConnectionAsync_ShouldFail_WithInvalidConnection` — Error handling

**Concrete Test Classes:** `MssqlGeneratorTests`, `MySqlGeneratorTests`, `PostgreSqlGeneratorTests`, `SqliteGeneratorTests`
- Total: 32 tests (8 tests × 4 database types)
- Uses Aspire test containers: `demo-mssql`, `demo-mysql` (defined in `src/LinqStudio.AppHost/AppHost.cs`)
- Test data seeded by `LinqStudio.DatabaseSeeder` project

#### Lazy Loading Pattern for Tree View

**Recommended Data Flow:**
1. Initial render: Call `GetTablesAsync()` → fast, returns `IReadOnlyList<DatabaseTableName>` (schema + name only)
2. User expands table node: Call `GetTableAsync(tableName)` → returns `DatabaseTableDetail` with columns and FKs
3. Cache `DatabaseTableDetail` in component state (`Dictionary<string, DatabaseTableDetail>`)
4. Subsequent expansions: Check cache first, fetch on miss

**Column Type Display:**
- Use `TableColumn.DataType` for database-specific type (e.g., "varchar", "int")
- Use `TableColumn.GenericType` for C# type mapping (e.g., `DbColumnType.String`, `DbColumnType.Int32`)
- Format with size info: `MaxLength` → "varchar(100)", `Precision + Scale` → "decimal(10,2)"
- Add nullability: `IsNullable ? $"{type}?" : type`

#### Key Findings for Tree View Feature

**✅ Backend API Complete:** No new interfaces or services needed
- `GetTablesAsync()` provides initial table list
- `GetTableAsync(tableName)` provides lazy-loaded column details
- `TableColumn` model has all needed metadata (name, type, nullability, PK, identity)
- All 4 database types supported and tested

**⚠️ Optional Enhancements (Not Blockers):**
- Batch column fetching: `GetTablesWithDetailsAsync(IEnumerable<string> tableNames)` — workaround: use `Task.WhenAll()` with `GetTableAsync()`
- Column-only fetching: `GetColumnsAsync(string tableName)` — workaround: call `GetTableAsync()` and use `Columns` property
- Schema grouping: `GetTablesBySchemaAsync()` — workaround: call `GetTablesAsync()` and group in memory with LINQ

**Recommendation:** UI can consume existing API directly. No backend work required for MVP.

**Documentation:** Complete analysis written to `.squad/decisions/inbox/simon-db-introspection-analysis.md`


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

### 2026-03-14 - QueryExecutionService Architecture Gap Analysis

**Task:** Technical deep-dive to identify why QueryExecutionService doesn't work at runtime (requested by snakex64).

**Finding:** Interface/implementation mismatch. Service has complete working internal implementation but public method throws NotImplementedException because it can't receive required Project context.

**Key Discovery - Two Different Patterns in Codebase:**

1. **CompilerService Pattern (Factory + Initialization):**
   - Stateful service — maintains expensive Roslyn AdhocWorkspace
   - Created via `CompilerServiceFactory.CreateFromProjectAsync(project)`
   - Project context baked in during initialization via `Initialize(modelFiles, dbContextCode)`
   - High init cost, many reuses (hover, completion, typing)
   - Needs thread safety (SemaphoreSlim)
   - Lifecycle tied to editor session

2. **QueryExecutionService Pattern (Per-Call Context):**
   - Stateless service — no internal state between calls
   - Each execution compiles fresh assembly, instantiates DbContext, runs query
   - No reuse — one execution per call
   - No thread safety needed
   - Naturally per-request

**The Gap:**
- Internal method signature: `ExecuteQueryInternalAsync(string userQuery, Models.Project project, CancellationToken)`
- Public interface signature: `ExecuteQueryAsync(string userQuery, CancellationToken)` ← missing Project!
- Editor has `Workspace.CurrentProject` but doesn't pass it (interface doesn't accept it)
- Service needs: `project.ConnectionString`, `project.DatabaseType`, `project.QueryGenerator`

**Recommended Fix:** Add Project parameter to interface (stateless per-call pattern)
- Rationale: Matches architectural intent — execution is per-request, not per-session
- Precedent: `IDbContextGenerator.GenerateAsync(queryGenerator, cancellationToken)` already uses per-call context
- Simplicity: 3 file changes vs 5+ for factory pattern
- Flexibility: Can execute against different projects without re-initialization

**Key Learning:** Choose pattern based on service characteristics:
- **Factory + Initialize** → Stateful services with expensive setup (Roslyn workspace, LSP, etc.)
- **Per-Call Context** → Stateless operations or request-scoped work (DB queries, API calls)

**Analysis Document:** `.squad/decisions/inbox/simon-execution-gap-analysis.md`

## Learnings

### QueryGenerator Property Fix (2026-03-11)
Fixed missing PostgreSQL and SQLite cases in src/LinqStudio.Core/Models/Project.cs QueryGenerator property. The switch expression only handled Mssql and MySql, throwing NotSupportedException for the other database types. Referenced TestConnectionAsync method which had the correct implementation with all four cases.

### 2026-03-11 - Hardcoded Aspire Database Passwords for Local Dev

**Problem:**
Alice (live tester) had difficulty finding database connection strings because Aspire generates random passwords by default for SQL Server and MySQL containers. This made it hard to test and connect manually with external tools.

**Solution:**
Modified `src/LinqStudio.AppHost/AppHost.cs` to use hardcoded passwords for local dev/testing:
- SQL Server SA password: `Password123!` (meets complexity requirements: uppercase, lowercase, digit, special char, 8+ chars)
- MySQL root password: `root_password_123`

**Implementation:**
```csharp
var sqlPassword = builder.AddParameter("sql-password", value: "Password123!", secret: false);
var mysqlPassword = builder.AddParameter("mysql-password", value: "root_password_123", secret: false);

var mssql = builder.AddSqlServer("demo-mssql", password: sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent);

var mysql = builder.AddMySql("demo-mysql", password: mysqlPassword)
    .WithLifetime(ContainerLifetime.Persistent);
```

**Aspire Password API:**
- `AddSqlServer(string name, IResourceBuilder<ParameterResource>? password = default)` - accepts password parameter for SA account
- `AddMySql(string name, IResourceBuilder<ParameterResource>? password = default)` - accepts password parameter for root account
- Use `builder.AddParameter(name, value, secret: false)` to create a non-secret parameter (for local dev only)
- Aspire automatically injects connection strings with passwords into dependent services via environment variables (e.g., `ConnectionStrings__DemoMssql`)

**Connection Strings:**
- SQL Server: `Server=localhost,{port};Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true`
- MySQL: `Server=localhost;Port={port};Database=linqstudio-mysql-demo;User=root;Password=root_password_123;`

**No Changes Needed:**
- `LinqStudio.DatabaseSeeder` - reads connection strings from environment variables injected by Aspire, passwords flow through automatically
- WebServer - uses `.WithReference()` to get connection strings, no hardcoded strings

**Verification:**
Built successfully with 0 errors and 0 warnings.

**Security Note:**
Hardcoded passwords marked with `secret: false` are intentional for local dev/testing only. Never use this pattern in production — Aspire's default random password generation should be used for production environments.

### 2026-03-11T21:04:48Z - Fixed Aspire Port Discovery for Live Testing

**Requested by:** snakex64 via Alice  
**Problem:** Dynamic port mapping prevented reliable external database connections during live testing  

**Root Cause:**
- Aspire's `AddSqlServer()` and `AddMySql()` were mapping container ports to random host ports
- Alice could not build stable connection strings without runtime port discovery
- SQL Server 1433 → random port (e.g., 50123)
- MySQL 3306 → random port (e.g., 50124)

**Solution Implemented:**
Used Aspire 9.5+ `port` parameter to assign fixed host ports:
- SQL Server: Port **14330** (`AddSqlServer("demo-mssql", password, port: 14330)`)
- MySQL: Port **13306** (`AddMySql("demo-mysql", password, port: 13306)`)

**Connection Strings for Alice:**
- **SQL Server:** `Server=localhost,14330;Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true`
- **MySQL:** `Server=localhost;Port=13306;Database=linqstudio-mysql-demo;User=root;Password=root_password_123;`

**Technical Details:**
- Modified `src/LinqStudio.AppHost/AppHost.cs` with port parameters
- Added inline comments with full connection strings for developer reference
- No breaking changes — Aspire environment variable injection still works for WebServer
- Ports chosen to avoid conflicts: 14330 (1433 + prefix "1"), 13306 (3306 + prefix "1")
- Verified build succeeds with no errors

**Files Modified:**
- `src/LinqStudio.AppHost/AppHost.cs` — added port: 14330 and port: 13306 parameters

**Documentation:**
- Created `.squad/decisions/inbox/simon-aspire-fixed-ports.md` with full decision record

**Status:** ✅ Complete, ready for Alice to use

### 2026-03-12 - MSSQL Auto-Discovery for Connections Without Explicit Database

**Problem:** When users connect to MSSQL without specifying a `Database=` in the connection string (e.g., `Server=127.0.0.1,14330;User ID=sa;Password=Password123!;TrustServerCertificate=true`), the connection defaults to the `master` database. Since `master` contains only system tables (all `IsMSShipped=1`), `GetTablesAsync()` correctly returned an empty list. However, user tables existed in other databases like `linqstudio-mssql-demo` but were inaccessible.

**Root Cause:** ADO.NET connections without explicit database specification default to `master`. The connection is opened and closed for each call (`GetTablesAsync`, `GetTableAsync`), and when reopened, it reverts to `master` from the connection string.

**Solution:** Implemented auto-discovery pattern in `MssqlGenerator`:

1. **Added `_resolvedDatabase` private field** to persist the discovered database across connection open/close cycles
2. **Added `FindFirstUserDatabaseAsync()` method** to query `sys.databases` for the first non-system database (`database_id > 4`, online, not read-only)
3. **Added `SwitchToResolvedDatabaseIfNeeded()` helper** to restore the correct database on each connection reopen using `Connection.ChangeDatabase()`
4. **Updated `GetTablesAsync()`** to detect when connected to `master` and auto-discover + switch to first user database
5. **Updated `GetTableAsync()`** to handle both scenarios:
   - First call without prior `GetTablesAsync()` → perform discovery
   - Subsequent calls → switch to resolved database if connection was closed/reopened

**Key Pattern:**
```csharp
// Auto-discover on first connection to master
if (string.Equals(Connection.Database, "master", StringComparison.OrdinalIgnoreCase))
{
    var userDb = await FindFirstUserDatabaseAsync(cancellationToken);
    if (userDb != null)
    {
        _resolvedDatabase = userDb;
        Connection.ChangeDatabase(userDb);
    }
}
```

**Technical Details:**
- `FindFirstUserDatabaseAsync()` queries: `SELECT TOP 1 name FROM sys.databases WHERE database_id > 4 AND state = 0 AND is_read_only = 0 ORDER BY name`
- `database_id > 4` excludes system databases (master=1, tempdb=2, model=3, msdb=4)
- `state = 0` ensures database is online
- `is_read_only = 0` ensures database is writable
- Discovery happens lazily on first API call, not in constructor
- `_resolvedDatabase` persists across the generator instance lifetime

**Why This Matters:** 
- Users can now provide simplified connection strings without specifying database names
- Matches common developer workflows where they connect to a server and expect to see available databases
- Maintains backward compatibility: explicit `Database=` in connection string bypasses auto-discovery
- Each generator instance remembers the resolved database, avoiding repeated discovery queries

**Files Modified:**
- `src/LinqStudio.Database/MssqlGenerator.cs` (surgical additions only, no breaking changes)



### 2026-03-14 - DbContextGenerator Implementation

**Task:** Implemented DbContextGenerator — in-memory schema-to-code service that feeds live database schema into Roslyn's CompilerService for real IntelliSense.

**Files created/modified:**
- **NEW** src/LinqStudio.Core/Services/DbContextGenerator.cs — full IDbContextGenerator implementation
- **UPDATED** src/LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs — replaced NullDbContextGenerator placeholder with real AddScoped<IDbContextGenerator, DbContextGenerator>()

**Interface/result type already existed:**
- src/LinqStudio.Abstractions/Abstractions/IDbContextGenerator.cs — already had the interface stub
- src/LinqStudio.Abstractions/Models/DbContextGeneratorResult.cs — already had the result record
- src/LinqStudio.Core/Services/CompilerServiceFactory.cs — already had CreateFromProjectAsync stub with the correct constructor signature

**Key design decisions implemented:**

1. **DbColumnType → C# type mapping:** Value types (bool, int, long, Guid, DateTime, etc.) stay non-nullable unless IsNullable=true then get ?. String-like types (String, Xml, Json) map to string; non-nullable get [Required] +  = string.Empty;. Binary maps to yte[] with  = []; initializer when non-nullable.

2. **Data annotations:** [Key] on IsPrimaryKey, [DatabaseGenerated(DatabaseGeneratedOption.Identity)] when IsIdentity, [Required] only on non-nullable string-like columns, [MaxLength(n)] when MaxLength has value != -1.

3. **Naming conventions:** ToPascalCase() splits on underscores and capitalizes first letter of each segment. Table name → class name (no schema prefix). DbSet property name = class name (same as table PascalCase).

4. **Navigation properties:** 
   - Child table (FK owner): public virtual RefClass? NavName { get; set; } where NavName = Singularize(referencedClassName). Disambiguates duplicate nav names using FK column name (strip "Id" suffix).
   - Parent table (FK target): public virtual ICollection<ChildClass> CollectionName { get; set; } = [] where CollectionName = Pluralize(childClassName).

5. **Basic singularize/pluralize:** Handles common patterns (strip 's', 'ies' → 'y', etc.). Sufficient for typical EF Core schema navigation.

6. **Fixed namespace/context name:** GeneratedModels / GeneratedDbContext — hardcoded constants in the service.

7. **DI:** Registered as AddScoped<IDbContextGenerator, DbContextGenerator>() — scoped so each user session gets its own instance.

**Test results:** All 444 unit/integration tests pass (95 Core, 39 Blazor, 310 Database). Known pre-existing E2E flakiness in NavMenu_SaveAs_SavesCompleteProjectToFile unrelated to this change.

### 2026-03-14 - Phase 1a Foundation Models + DbContext Codegen Fix

**Task:** Implement foundation models and fix DbContext codegen to support query execution (Phase 1a of query execution feature).

**Requested by:** snakex64 (via Squad task)

**Files Created:**
1. `src/LinqStudio.Abstractions/Models/QueryExecutionResult.cs`
   - Result record for query execution with Rows, ColumnNames, Elapsed, ErrorMessage, IsCompileError
   - Computed Success property (true when ErrorMessage is null)
   - Static factory methods: Empty() and FromError()
2. `src/LinqStudio.Abstractions/Services/IQueryExecutionService.cs`
   - Interface for query execution service
   - Single method: Task<QueryExecutionResult> ExecuteQueryAsync(string userQuery, CancellationToken)

**Files Modified:**
1. `src/LinqStudio.Core/Services/DbContextGenerator.cs`
   - Fixed GenerateDbContext() to support BOTH IntelliSense and real execution
   - **Added dual-constructor pattern:**
     - Parameterless constructor for IntelliSense/compilation (uses in-memory fallback)
     - Constructor accepting DbContextOptions for real execution
   - **Modified OnConfiguring():**
     - If _options provided → apply extensions to optionsBuilder for real DB connection
     - Else if not configured → fallback to UseInMemoryDatabase for compilation
   - Added `using Microsoft.EntityFrameworkCore.Infrastructure;` for IDbContextOptionsBuilderInfrastructure

**Key Design Decision:**
The DbContext codegen fix maintains backward compatibility with IntelliSense while enabling execution:
- **Compilation path:** Parameterless constructor + OnConfiguring fallback → UseInMemoryDatabase (keeps IntelliSense working)
- **Execution path:** Constructor with DbContextOptions + OnConfiguring applies real connection (enables query execution)

**Implementation Details:**
- Generated DbContext stores `_options` field (nullable)
- OnConfiguring checks `_options != null` to determine execution vs compilation mode
- Options application uses `IDbContextOptionsBuilderInfrastructure` to copy extensions from provided options
- Preserves existing in-memory behavior when options not provided (doesn't break existing IntelliSense flow)

**Build Status:** ✅ All builds pass
- LinqStudio.Abstractions: 0 warnings, 0 errors
- LinqStudio.Core: 0 warnings, 0 errors

**Next Phase:** Phase 1b will implement QueryExecutionService using these foundation types and the fixed DbContext pattern.



### 2026-03-13 - Phase 1b Implementation: QueryExecutionSettings + QueryExecutionService

**Task:** Implement Phase 1b of query execution feature - settings and service foundation.

**What Was Built:**
1. **QueryExecutionSettings** (`src/LinqStudio.Core/Settings/QueryExecutionSettings.cs`)
   - Implements IUserSettingsSection for auto-discovery
   - SectionName = 'QueryExecutionSettings'
   - Property: TimeoutSeconds (int, default 30, supports 0 for no timeout)
   - Fully localized (English + French) in SharedResource.resx

2. **QueryExecutionService** (`src/LinqStudio.Core/Services/QueryExecutionService.cs`)
   - Implements IQueryExecutionService interface
   - Registered as scoped service in DI
   - Complete 7-step execution pipeline implemented:
     1. Generate models/DbContext from project database
     2. Wrap user query in QueryContainer
     3. Compile to IL using Roslyn (CSharpCompilation.Emit)
     4. Load assembly from memory
     5. Instantiate DbContext with real connection (DbContextOptions)
     6. Invoke QueryContainer.Query via reflection
     7. Materialize with ToListAsync and extract columns
   - Supports all database types: SQL Server, MySQL, PostgreSQL, SQLite
   - Timeout handling with configurable seconds
   - Comprehensive error handling (compile vs runtime)
   - Column extraction handles primitives, anonymous types, EF entities

**Technical Decisions:**
- Added EF Core provider packages to LinqStudio.Core.csproj:
  - Microsoft.EntityFrameworkCore 10.0.4
  - Microsoft.EntityFrameworkCore.SqlServer 10.0.4
  - Microsoft.EntityFrameworkCore.Sqlite 10.0.4
  - Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1
  - MySql.EntityFrameworkCore 10.0.1
- Updated DependencyInjection.Abstractions to 10.0.4 for compatibility
- Public interface method throws NotImplementedException (Phase 2 will add Project parameter)
- Internal ExecuteQueryInternalAsync method contains full implementation

**Build Status:** ✅ 0 errors, 0 warnings
**Test Status:** ✅ All 467 tests pass (4 skipped E2E)

**Documentation Updated:**
- Added QueryExecutionSettings section to Settings/readme.md
- Added QueryExecutionService section to Services/copilot.md
- Documented 7-step pipeline, error handling, dependencies

**Key Learnings:**
1. **Version Compatibility:** Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1 requires EF Core >= 10.0.4
2. **Namespace Conflicts:** Microsoft.CodeAnalysis.Project vs LinqStudio.Core.Models.Project - used fully qualified type
3. **Settings Auto-Discovery:** Works perfectly - no manual DI registration needed
4. **Assembly Compilation:** Roslyn's Emit() requires all model files + DbContext + QueryContainer in Solution
5. **DbContext Lifecycle:** Must pass DbContextOptions via constructor for runtime execution (not OnConfiguring)

**Phase Status:** Phase 1b COMPLETE - ready for Phase 2 (UI integration)

### 2026-03-13 - Implemented Option A: Add Project Parameter to IQueryExecutionService.ExecuteQueryAsync

**Task:** Add `Project project` parameter to `IQueryExecutionService.ExecuteQueryAsync` method signature to provide access to connection string, DatabaseType, and QueryGenerator.

**Changes Made:**

1. **Interface Update (IQueryExecutionService.cs)**:
   - Moved interface from `LinqStudio.Abstractions.Services` to `LinqStudio.Core.Services` (architectural decision to avoid circular dependency)
   - Added `Project project` parameter to `ExecuteQueryAsync` method signature:
     `csharp
     Task<QueryExecutionResult> ExecuteQueryAsync(
         string userQuery,
         Project project,
         CancellationToken cancellationToken = default);
     `

2. **Implementation Update (QueryExecutionService.cs)**:
   - Updated public `ExecuteQueryAsync` method to accept `Project project` parameter
   - Wired public method to internal `ExecuteQueryInternalAsync` (which already had full implementation)
   - Removed `NotImplementedException` - now uses real implementation with project's connection string, database type, and query generator

3. **Test Updates (QueryExecutionServiceTests.cs)**:
   - Updated all test methods to pass `Project` instance with appropriate test values
   - Changed test assertions to reflect new behavior (no longer expect NotImplementedException)
   - Tests verify:
     - Error when no connection string configured
     - Error when empty connection string provided
     - Proper cancellation handling

**Build & Test Results:**
- ✅ Build: SUCCESS (0 errors)
- ✅ Tests: All 485 tests passed (121 Core + 39 Blazor + 310 Databases + 15 E2E, 4 skipped E2E tests)
- Duration: ~30 seconds

**Architectural Note:**
Initially attempted to move `Project` class to `LinqStudio.Abstractions.Models` to avoid having interface in Abstractions reference Core.Models, but this created circular dependency (Abstractions → Databases ← Abstractions). Final solution: moved `IQueryExecutionService` from Abstractions to Core, which is architecturally sound since the interface is specific to Core's execution logic and Project model.

**Files Changed:**
- `src/LinqStudio.Core/Services/IQueryExecutionService.cs` (moved from Abstractions, updated signature)
- `src/LinqStudio.Core/Services/QueryExecutionService.cs` (removed NotImplementedException, wired to internal impl)
- `tests/LinqStudio.Core.Tests/QueryExecutionServiceTests.cs` (updated tests with Project parameter)
- Various using statement cleanups across codebase

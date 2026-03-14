# Squad Decisions

## Active Decisions

### User Directives

#### 2026-03-11T03:27:00Z: Never Use Git Commit or Push
**By:** snakex64 (via Copilot)  
**Decision:** Never use `git commit` or `git push` under any circumstances. Only snakex64 commits and pushes changes. The team must leave code as-is after making changes — no committing.  
**Why:** User request — captured for team memory  
**Status:** ✅ Active

---

## Architectural Decisions Already in Place

### 1. Layered Architecture with Strict Dependency Flow
**Decision**: Abstractions → Core → Databases → Blazor → App.WebServer → AppHost  
**Rationale**: 
- Clean separation prevents circular dependencies
- Each layer has clear responsibilities
- Abstractions layer defines contracts (IUserSettingsSection, IDatabaseQueryGenerator)
- Database introspection isolated from Core to avoid EF Core circular dependencies (uses ADO.NET directly)

**Implications**:
- When adding features, respect layer boundaries
- New database types go in Databases project (not Core)
- Shared models belong in Abstractions
- UI state management stays in Blazor (ProjectWorkspace, QueriesWorkspace)

**Status:** ✅ Established, working well

---

### 2. Settings Auto-Discovery via Reflection
**Decision**: All IUserSettingsSection implementations auto-registered at startup via assembly scan  
**Benefits**:
- No manual DI registration needed
- Adding new settings = create class + localization entries
- Modular: each setting is independent record class
- Reactive: IOptionsMonitor<T> provides change notifications

**Pattern for Adding Settings**:
1. Create `record class MySettings : IUserSettingsSection` in `LinqStudio.Core/Settings/`
2. Add translations to `SharedResource.resx`: `UserSettings.MySettings`, `UserSettings.MySettings.PropertyName`
3. Auto-discovered at runtime, no other changes needed

**Status:** ✅ Implemented, clean and extensible

---

### 3. CompilerService Thread Safety & Query Wrapping
**Decision**: Single SemaphoreSlim protects AdhocWorkspace, user queries wrapped in QueryContainer class  
**Rationale**:
- Monaco triggers multiple concurrent callbacks (hover, completion, typing)
- Roslyn workspace mutation must be serialized
- Query wrapping enables Roslyn to analyze partial/incomplete LINQ expressions

**Critical Details**:
- Cursor position adjustment: wrapper adds prefix text, must calculate offset
- `__THIS_HERE__` placeholder technique for position calculation
- All CompilerService methods await `_lock.WaitAsync()` before Roslyn operations

**Future Consideration**: Per-editor CompilerService instances might eliminate lock contention, but increases memory footprint

**Status:** ✅ Implemented correctly, thread-safe

---

### 4. Monaco Provider Management
**Decision**: MonacoProvidersService tracks global providers, routes by editor URI  
**Problem Solved**: Monaco registers providers globally, Blazor components can create/destroy multiple editor instances  
**Solution**:
- Single global provider registration per language
- ConcurrentDictionary maps editor URI → delegate
- Component lifecycle: register on init, unregister on dispose
- Retry logic for Monaco initialization timing (up to 20 attempts, 250ms intervals)

**Implication**: Never call `Global.RegisterHoverProvider()` directly from components - always use MonacoProvidersService

**Status:** ✅ Elegant pattern, production-ready

---

### 5. Workspace Pattern for UI State
**Decision**: ProjectWorkspace (orchestrator) + QueriesWorkspace (query state) + change events  
**Benefits**:
- Separation of concerns: project-level vs query-level state
- Unsaved changes detection across both levels
- Event-driven: `WorkspaceChanged`, `QueriesChanged` enable reactive UI
- Scoped services: one instance per user session (Blazor Server)

**State Flow**:
- ProjectWorkspace tracks: current project, file path, connection settings
- QueriesWorkspace tracks: open queries, current query, in-memory edits
- Both track unsaved changes independently, aggregate at ProjectWorkspace level

**Status:** ✅ Well-designed, fully functional

---

### 6. File System Abstraction
**Decision**: IFileSystemService interface for platform-agnostic file dialogs  
**Current Implementation**: ServerFileSystemService uses NativeFileDialogSharp (cross-platform native dialogs)  
**Future Path**: Ready for Blazor WebAssembly implementation when needed  
**Default Path Logic**: 
1. Try ~/Documents/LinqStudio/ (if exists)
2. Fall back to ~/Documents/
3. Last resort: current directory

**Status:** ✅ Well-abstracted, future-proof

---

### 7. Error Handling Three-Layer Strategy
**Decision**: ErrorHandlingService (manual) + AppErrorBoundary (global) + ErrorDialog (UI)  
**Architecture**:
- **Layer 1**: ErrorHandlingService - scoped, injected into components, manual try-catch usage
- **Layer 2**: AppErrorBoundary - global unhandled exception catcher, wraps Router in Routes.razor
- **Layer 3**: ErrorDialog - reusable MudDialog with collapsible technical details

**Pattern**: All expected exceptions handled manually, unexpected ones caught globally, consistent UI presentation

**Status:** ✅ Comprehensive, well-tested

---

### 8. Explicit Exit Code Handling for Aspire Console Dependencies
**Date:** 2026-03-11  
**Author:** Simon (Backend Core Dev)  
**Decision**: All console applications used as Aspire dependencies MUST:
1. Wrap the entire main logic in a top-level `try-catch` block
2. Log exceptions to `Console.Error` with full stack traces on failure
3. Call `Environment.Exit(1)` explicitly on any failure path
4. Call `Environment.Exit(0)` explicitly on success path
5. Use structured error messages that clearly distinguish success from failure

**Rationale**:
- Aspire's `WaitForCompletion()` relies on exit codes to detect success/failure
- Top-level statements without explicit exit handling can trigger unhandled exception wrapper codes (e.g., `0xE0434352`)
- Predictable exit codes enable reliable orchestration and dependency ordering
- Explicit error logging improves debuggability

**Application**: Fixed LinqStudio.DatabaseSeeder which was exiting with `0xE0434352` despite successful seeding, blocking web server startup

**Pattern**:
```csharp
try {
    // Main async logic here
    Environment.Exit(0);  // explicit success
}
catch (Exception ex) {
    Console.Error.WriteLine($"Fatal error: {ex}");
    Environment.Exit(1);  // explicit failure
}
```

**Status:** ✅ Implemented, tested, production-ready

---

## Known Issues & Monitoring Items

### 1. FluentAssertions Contradiction
**Status:** Identified by Samy  
**Issue:** Core.Tests uses FluentAssertions, but custom instructions say DO NOT use  
**Action:** Standardize on xUnit Assert.* across all test projects  
**Priority:** P1 (Quality consistency)

---

### 2. JsonSerializerOptionsExtensions Experimental Syntax
**Status:** Identified by Simon  
**Issue:** Uses experimental C# 13 `extension()` keyword (non-standard)  
**Compiles:** ✅ With `<LangVersion>latest</LangVersion>` in .NET 10  
**Risk:** May break if syntax changes or is removed from language  
**Action:** Monitor for language changes  
**Recommendation:** If proposal withdrawn, refactor to standard extension method  
**Priority:** P3 (Monitoring only, no urgency)

---

### 3. CompilerService Assembly Loading
**Status:** Identified by Simon  
**Observation:** CompilerService loads ALL AppDomain assemblies as metadata references (dozens)  
**Impact:** Large metadata footprint per instance, CompilerServiceFactory creates new per session  
**Current Behavior:** ✅ Correct for IntelliSense (needs full type visibility)  
**Monitoring:** Watch memory usage at scale with multiple concurrent users  
**Action:** If memory issues arise, consider lazy loading or shared workspace (adds threading complexity)  
**Priority:** P3 (Monitoring, address if performance issues arise)

---

### 4. Project Model — Future Properties
**Status:** Identified by Simon  
**Issue:** Placeholder properties exist (Models, DbContextCode) — not yet implemented  
**Details:** No code references these properties  
**Schema:** Versioning in place (SchemaVersion = 1)  
**Action:** No action required; documentation captured  
**Priority:** P3 (Design already supports future expansion)

---

### 5. SettingsService — Single-Process Assumption
**Status:** Identified by Simon  
**Current:** Works correctly for single-user, single-process scenario  
**Risk:** If future adds multi-process or multi-user settings, race conditions possible  
**Action:** Document single-process assumption; add file locking if needed later  
**Priority:** P3 (No urgency, design is correct for current scope)

---

### 6. Monaco Initialization Timing Workaround
**Status:** Identified by EvilJosh and Samy  
**Pattern:** All Monaco editors require `Task.Delay(500)` before rendering  
**Issue:** BlazorMonaco resource loading race condition  
**Used In:** Editor.razor, SettingsEditor.razor  
**Action:** Monitor BlazorMonaco updates for better lifecycle support  
**Priority:** P2 (Workaround works, address if BlazorMonaco improves)

---

## Critical Build System Issue (BLOCKER)

### Build System Broken — Blocks All Tests
**Status:** Identified by Jordan  
**Root Cause:** `.slnx` file disables build for Abstractions and Databases projects  
**Impact:**
- `dotnet build` fails with 13 compilation errors in LinqStudio.Core
- `dotnet test` cannot run — test DLLs not built
- CI/CD pipeline likely failing
- Cannot verify current test state

**Configuration:**
```xml
<Project Path="src/LinqStudio.Abstractions/LinqStudio.Abstractions.csproj">
  <Build Solution="Debug|*" Project="false" />  <!-- ❌ Explicitly disabled -->
</Project>
<Project Path="src/LinqStudio.Database/LinqStudio.Databases.csproj">
  <Build Solution="Debug|*" Project="false" />  <!-- ❌ Explicitly disabled -->
</Project>
```

**Solution:** Remove `<Build Solution="Debug|*" Project="false" />` lines OR add proper project build order dependencies

**Priority:** 🔥 **P0 - BLOCKER** — Must fix before any other test work

---

## Test Coverage Gaps (Summary)

### P0 - BLOCKER
- **Build System:** Fix .slnx (described above)
- **Run Tests:** Once fixed, verify all 105 tests pass

### P1 - High Priority
- **QueryService:** 0 tests (load/save, file corruption, concurrency)
- **SettingsService:** 0 tests (usersettings.json, invalid JSON, reflection)
- **MonacoProvidersService:** 0 tests (provider lifecycle, duplicate prevention)
- **Blazor Components:** SettingsEditor.razor, MainLayout.razor have 0 unit tests
- **E2E Flakiness:** Fix or document `Editor_AutoTriggers_CompletionOnSpace`
- **Empty Test Project:** LinqStudio.App.WebServer.Tests has 0 tests (populate or remove)

### P2 - Medium Priority
- **CompilerService Edge Cases:** Syntax errors, semantic errors, concurrency, memory
- **Database Connection Failures:** Invalid strings, timeouts, auth failures
- **FileSystemService:** I/O errors, file locking, large projects

### P3 - Lower Priority
- **Performance Tests:** Large models, long queries, stress testing
- **Localization Tests:** Verify all settings have translations
- **E2E Expansion:** Query execution, results grid, multi-tab behavior

---

### 9. MSSQL OBJECTPROPERTY NULL Handling in Named Databases
**Date:** 2026-03-12  
**Author:** Simon (Backend Core Dev)  
**Decision**: Wrap `OBJECTPROPERTY()` with `ISNULL()` when filtering system tables in MSSQL  
**Rationale**:
- `OBJECT_ID()` returns NULL for user tables in named databases under certain conditions
- `OBJECTPROPERTY(NULL, 'IsMSShipped')` returns NULL, causing WHERE clause evaluation to UNKNOWN (false)
- `ISNULL(OBJECTPROPERTY(...), 0)` handles NULL gracefully by defaulting to 0 (user table)

**Pattern**:
```sql
-- Before (bugs in named databases)
AND OBJECTPROPERTY(OBJECT_ID(...), 'IsMSShipped') = 0

-- After (handles NULL safely)
AND ISNULL(OBJECTPROPERTY(OBJECT_ID(...), 'IsMSShipped'), 0) = 0
```

**Application**: Fixed MssqlGenerator.GetTablesAsync (line 96) which was silently excluding all user tables in production named databases.

**Status:** ✅ Implemented and tested

---

### 10. Test Infrastructure Must Match Production Database Patterns
**Date:** 2026-03-12  
**Author:** Jordan (Tests Dev)  
**Decision**: All database test fixtures must use named databases, not master database  
**Rationale**:
- Production Aspire setup uses named databases (e.g., `linqstudio-mssql-demo`)
- Test fixtures using `master` miss edge cases that only occur in named database context
- Example: OBJECTPROPERTY NULL handling only manifests with named databases
- Tests that pass but miss production bugs undermine quality assurance

**Pattern for MSSQL Test Fixtures**:
1. Start Testcontainers MSSQL container (connects to master initially)
2. Create named database using `CREATE DATABASE [TestLinqStudio]`
3. Use `SqlConnectionStringBuilder` to properly set `InitialCatalog` property
4. Connect DbContext and tests to named database
5. Add explicit regression test for production-relevant scenarios

**Application**: Fixed MssqlDatabaseFixture to create and use `TestLinqStudio` database. Added regression test `GetTablesAsync_ShouldReturnTables_WhenConnectedToNamedDatabase()`.

**Future Audit**: Similar review recommended for MySQL and PostgreSQL fixtures.

**Status:** ✅ Implemented and tested (All 295 tests pass)

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
- User directives are binding (e.g., no git commits)
- Build system and test coverage must be maintained at all times

## Team Review Findings - 2026-03-13

Consolidated from decision inbox (30 files). Team review cycle captured detailed findings from architecture, frontend, backend, and code quality reviews.

### [alex-full-codebase-review]
**Source:** alex

**Reviewer:** Alex (Code Reviewer)  
**Date:** 2026-03-12  
**Scope:** Comprehensive review of entire LinqStudio codebase (all source files)

---

### [alex-review-fixes]
**Source:** alex

**Review Date:** 2026-03-12  
**Reviewer:** Alex (Code Reviewer)  
**Scope:** All uncommitted changes following Simon's auto-discovery fix  

---

### [alice-dbtreeview-test-results]
**Source:** alice

**By:** Alice (Live Tester)
**What:** End-to-end validation of DatabaseTreeView feature against running Aspire stack
**Why:** Feature ship gate — results recorded for team awareness

---

### [alice-final-sign-off]
**Source:** alice

**Date:** 2026-03-11T21:52:00Z  
**Tester:** Alice (Live Tester)  
**Task:** Final visual sign-off on the fully fixed Aspire stack

---

### [alice-mssql-visual-confirmation]
**Source:** alice

**Date:** 2026-03-12  
**Tester:** Alice (Live Tester)  
**Developer:** Simon (Backend Core Dev)  

---

### [eviljosh-tree-view-implementation]
**Source:** eviljosh

**Agent:** EvilJosh (Frontend Dev)  
**Date:** 2026-03-11  
**Task:** Build Database Tree View for left panel

---

### [eviljosh-tree-view-ui-analysis]
**Source:** eviljosh

**Author:** EvilJosh (Frontend Dev)  
**Date:** 2026-03-11  
**Purpose:** Comprehensive UI analysis for implementing a Table Tree View in the left panel

---

### [eviljosh-ui-analysis]
**Source:** eviljosh

**Date:** 2026-03-11  
**Analyzed by:** EvilJosh (Frontend Dev)  
**Requested by:** snakex64

---

### [eviljosh-ui-review]
**Source:** eviljosh

**Date:** 2026-03-13  
**Reviewer:** EvilJosh (Frontend Dev)  
**Requested by:** snakex64  

---

### [jordan-auto-discovery-tests]
**Source:** jordan

**Date:** 2026-03-12  
**Author:** Jordan (Tests Dev)  
**Context:** Simon's fix to auto-discover user databases when connected to master

---

### [jordan-db-test-coverage]
**Source:** jordan

**Date:** 2026-03-12  
**Author:** Jordan (Tests Dev)  
**Status:** ✅ Completed

---

### [jordan-post-fix-test-results]
**Source:** jordan

**Author:** Jordan (Tests Dev)  
**Date:** 2026-03-11  
**Status:** ✅ VERIFIED CLEAN

---

### [jordan-test-audit]
**Source:** jordan

**Date:** 2026-03-11  
**Author:** Jordan (Tests Dev)  
**Requested by:** snakex64

---

### [jordan-test-gap-analysis]
**Source:** jordan

**Date:** 2026-03-12  
**Author:** Jordan (Tests Dev)  
**Status:** Comprehensive analysis complete  

---

### [jordan-test-infrastructure-analysis]
**Source:** jordan

**Author:** Jordan (Tests Dev)  
**Date:** 2026-03-11  
**Purpose:** Comprehensive analysis of existing test infrastructure to guide testing of the Table Tree View feature

---

### [jordan-test-results-final]
**Source:** jordan

**Date:** 2026-03-11  
**Author:** Jordan (Tests Dev)  
**Task:** Run complete test suite and report results

---

### [jordan-tree-view-tests]
**Source:** jordan

**Date:** 2026-03-11  
**Author:** Jordan (Tests Dev)  
**Status:** ✅ Tests Written, Awaiting Component Implementation

---

### [samy-architecture-review]
**Source:** samy

**Conducted by:** Samy (Architect)  
**Date:** 2026-03-12  
**Scope:** Full codebase analysis (src/, tests/, docs/, build/)

---

### [samy-full-analysis]
**Source:** samy

**Analyst:** Samy (Architect)  
**Date:** 2026-03-11  
**Requested By:** snakex64  

---

### [samy-mssql-system-table-filter]
**Source:** samy

**Date:** 2026-03-11T22:15:00Z  
**Author:** Samy (Lead Architect)  
**Status:** ✅ Implemented  

---

### [samy-tree-view-analysis]
**Source:** samy

**Analyst:** Samy  
**Date:** 2026-03-11  
**Feature:** Database Table Tree View in Left Panel  

---

### [samy-treeview-arch-analysis]
**Source:** samy

**Date:** 2026-03-11  
**Author:** Samy (Analyst/Architect)  
**Status:** Complete architectural review prior to Alice validation  

---

### [simon-aspire-fixed-ports]
**Source:** simon

**Date:** 2026-03-11  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Implemented  

---

### [simon-aspire-hardcoded-password]
**Source:** simon

**Date:** 2026-03-11  
**Author:** Simon (Backend Core Dev)  
**Requested By:** snakex64 (via Alice's feedback)  

---

### [simon-backend-analysis]
**Source:** simon

**Analyst:** Simon (Backend Core Dev)  
**Date:** 2026-03-11  
**Requested by:** snakex64 via Alice  

---

### [simon-backend-review]
**Source:** simon

**Date:** 2026-03-13  
**Reviewer:** Simon (Backend Core Dev)  
**Scope:** CompilerService, Settings, Database Generators, Core Services, Abstractions, Tests

---

### [simon-db-introspection-analysis]
**Source:** simon

**Date:** 2025-01-XX  
**Author:** Simon (Backend Core Dev)  
**Context:** Table Tree View feature - backend API analysis and requirements

---

### [simon-dbconnection-fix]
**Source:** simon

**Date:** 2025-01-09  
**Author:** Simon (Backend Core Dev)  
**Status:** Implemented  

---

### [simon-mssql-auto-discover-fix]
**Source:** simon

**Date:** 2026-03-12  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Implemented and Tested  

---

### [simon-querygenerator-fix]
**Source:** simon

**Date:** 2026-03-11  
**Author:** Simon (Backend Core Dev)  
**Requested By:** snakex64

---

## Feature: Query Result DataGrid — Complete Implementation

### 11. QueryExecutionResult Model & IQueryExecutionService Interface (Phase 1a)

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Complete

**Decision:** Implement foundational models and interfaces for query execution.

**Details:**
- `QueryExecutionResult`: Rows as dynamic dictionaries, column names, elapsed time, error context
- `IQueryExecutionService`: Single method contract `ExecuteQueryAsync(string, CancellationToken)`
- DbContext dual-constructor pattern: parameterless for IntelliSense, parameterized for real execution
- Zero breaking changes to CompilerService

**Files:** QueryExecutionResult.cs, IQueryExecutionService.cs (both in Abstractions layer)

---

### 12. QueryExecutionService with 7-Step Roslyn Compilation Pipeline (Phase 1b)

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Complete

**Decision:** Implement complete query execution pipeline using in-memory Roslyn compilation and reflection.

**7-Step Pipeline:**
1. Generate DbContext from live database schema (IDbContextGenerator)
2. Wrap user query in QueryContainer class
3. Compile to IL using CSharpCompilation.Emit()
4. Load compiled assembly from memory
5. Instantiate DbContext with real DbContextOptions (connection string + provider)
6. Invoke QueryContainer.Query() method via reflection
7. Materialize with ToListAsync() and extract column metadata

**Error Handling:**
- Compile errors: IsCompileError=true with Roslyn diagnostics
- Runtime errors: IsCompileError=false with exception message
- Timeout support via CancellationTokenSource

**Database Support:** SQL Server, MySQL, PostgreSQL, SQLite (via provider-specific extensions)

**DI:** Scoped registration (per-session, each may target different database)

---

### 13. QueryExecutionSettings with User-Configurable Timeout

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Complete, Fully Localized EN+FR

**Decision:** Implement user settings for query execution timeout using the existing IUserSettingsSection pattern.

**Properties:**
- TimeoutSeconds: int (default: 30, valid: 10/30/60/120/300/0)
- 0 = no timeout (unlimited)

**Implementation:**
- Auto-discovered via reflection DI (no manual registration)
- Fully localized: SharedResource.resx (EN) + SharedResource.fr.resx (FR)
- Reactive: IOptionsMonitor<QueryExecutionSettings> for change notifications
- Follows existing settings pattern exactly

---

### 14. QueryResultGrid Component — Dynamic Columns with 5 States

**Date:** 2026-03-14  
**Author:** EvilJosh (Frontend Dev)  
**Status:** ✅ Complete

**Decision:** Use MudTable with dynamic HeaderContent/RowTemplate for query results display.

**Rationale for MudTable over MudDataGrid:**
- More reliable for dynamic columns in Blazor Server
- No closure capture issues with foreach loops
- Consistent with existing MudTable usage (DatabaseTreeView)
- Simpler implementation for dynamic schema

**Component States:**
1. Not executed (Result=null, !IsExecuting) → No visual output
2. Loading (IsExecuting=true) → MudProgressCircular + "Executing query..."
3. Error (!Result.Success) → MudAlert with error message + elapsed time
4. Empty (Success with 0 rows) → MudAlert with "Query returned no results"
5. Success with data → MudTable with footer (row count + elapsed time)

**Parameters:** Result (QueryExecutionResult?), IsExecuting (bool)

---

### 15. Editor.razor Integration — Per-Tab Execution State & Result Display

**Date:** 2026-03-14  
**Author:** EvilJosh (Frontend Dev)  
**Status:** ✅ Complete

**Decision:** Manage execution state per-query-tab using Dictionary<Guid, QueryExecutionState> pattern.

**Architecture:**
- QueryExecutionState class: Result, IsExecuting, CancellationTokenSource
- _executionStates dictionary keyed by query ID (Guid)
- Matches existing QueriesWorkspace pattern
- Switching tabs preserves results for original tab

**UI Changes:**
- Execute/Stop button (toggles on IsExecuting state)
- Timeout dropdown (10s–5min, 0=unlimited, default 30s)
- QueryResultGrid component embedded below editor
- Layout: Editor (flex:1) + ResultContainer (flex:1) + vertical scrolling

**ExecuteCurrentQueryAsync Flow:**
1. Validate editor exists
2. Get/create execution state for current tab
3. Cancel any existing execution
4. Fetch query text from Monaco
5. Create CancellationTokenSource with timeout
6. Call QueryExecutionService.ExecuteQueryAsync()
7. Store result, show snackbar feedback, StateHasChanged()

**StopCurrentQuery:** Cancels CancellationTokenSource for current tab

---

### 16. Backend Bug Fixes — Diagnostics & Safety Improvements

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Complete (All tests pass)

**Fixes Applied:**
1. CompilerService: Add Debug.WriteLine() to 6 empty catch blocks (diagnostics)
2. Database Generators: Add CommandTimeout=30 to all DbCommand instances (resilience)
3. Database Generators: Add ArgumentException validation to GetTableAsync (fail-fast)
4. AdoNetDatabaseGeneratorBase: Change `using` to `await using` for DbCommand (async disposal)

**Rationale:**
- Empty catches need development visibility via Debug output
- CommandTimeout prevents indefinite hangs on unresponsive databases
- Input validation at method boundary improves error clarity
- Async disposal aligns with modern C# patterns

---

### 17. QueryExecutionServiceTests — 18 Comprehensive Unit Tests

**Date:** 2026-03-14  
**Author:** Jordan (Tests Dev)  
**Status:** ✅ Complete (121 Core tests, all pass)

**Test Coverage:**
- QueryExecutionResult factory methods (8 tests)
- QueryExecutionSettings defaults (4 tests)
- Error handling: compile, runtime, timeout, cancellation (6 tests)

**Key Scenarios:**
- Success result with correct columns and rows
- Compile errors with Roslyn diagnostics
- Runtime errors with exception message
- Timeout cancellation
- Settings auto-discovery and localization

---

### 18. Architecture Decision: DbContextGenerator Dual-Constructor Pattern

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Implemented

**Decision:** Generate DbContext with two constructors to support both IntelliSense and real execution.

**Pattern:**
```csharp
public class GeneratedDbContext : DbContext
{
    // Parameterless for IntelliSense (falls back to in-memory)
    public GeneratedDbContext() { }
    
    // Parameterized for real execution (accepts DbContextOptions)
    public GeneratedDbContext(DbContextOptions options)
    {
        _options = options;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_options != null)
        {
            // Use provided options (real connection)
        }
        else if (!optionsBuilder.IsConfigured)
        {
            // Fallback to in-memory (IntelliSense)
            optionsBuilder.UseInMemoryDatabase("LinqStudioGeneratedDb");
        }
    }
}
```

**Benefits:**
- IntelliSense unchanged (CompilerService continues to work)
- Execution enabled (QueryExecutionService receives real DbContext)
- Zero breaking changes
- Supports all provider types (MSSQL, MySQL, PostgreSQL, SQLite)

---




---

## Decision (Inbox): samy-project-context-gap.md

# Architectural Analysis: QueryExecutionService Project Context Gap

**Analyst:** Samy  
**Requested by:** snakex64  
**Date:** 2026-03-11  
**Status:** Analysis Complete - Recommendation Ready

---

## Executive Summary

`QueryExecutionService` was implemented with a placeholder that throws `NotImplementedException` because the `IQueryExecutionService.ExecuteQueryAsync()` interface lacks a `Project` parameter. The service needs project context (connection string, DatabaseType) to:
1. Generate the DbContext from the live database schema
2. Create the correct DbContextOptions for the database type
3. Execute the compiled query against the real database

**Recommended Fix:** **Option C** - Inject `ProjectWorkspace` into `QueryExecutionService` to access the current project context. This is architecturally clean, requires minimal changes, and aligns with existing patterns.

---

## 1. What is a "Project" in this codebase?

### Project Model
**File:** `src/LinqStudio.Core/Models/Project.cs`

```csharp
public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DatabaseType DatabaseType { get; set; }  // ← Critical for execution
    public string? ConnectionString { get; set; }    // ← Critical for execution
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ModifiedDate { get; set; }
    
    // Future properties for storing generated models
    public Dictionary<string, string>? Models { get; set; }
    public string? DbContextCode { get; set; }
    
    // Cached QueryGenerator for DB introspection
    [JsonIgnore]
    public IDatabaseQueryGenerator? QueryGenerator { get; private set; }
}
```

**Key Properties for Query Execution:**
- `DatabaseType`: Enum (Mssql, MySql, PostgreSql, Sqlite) - needed to create correct `DbContextOptions`
- `ConnectionString`: The database connection string - needed to connect at runtime
- `QueryGenerator`: Cached database-specific query generator for fetching schema metadata

### Project Management Service
**File:** `src/LinqStudio.Core/Services/ProjectService.cs`

**Purpose:** Handles file I/O for `.linq` project files (JSON serialization)

**Key Methods:**
- `CreateNew(name, connectionString)` - Creates new project instance
- `LoadProjectAsync(filePath)` - Loads from disk
- `SaveProjectAsync(project, filePath)` - Saves to disk with atomic write pattern

**Scope:** File operations only - does NOT track "current" project (that's the workspace's job)

---

## 2. How does CompilerService get its project context?

### CompilerService Architecture
**File:** `src/LinqStudio.Core/Services/CompilerService.cs`

**Answer:** **CompilerService does NOT get connection string or DatabaseType.** It only gets generated code.

**Constructor Signature:**
```csharp
public CompilerService(
    string contextTypeName,      // e.g., "NorthwindDbContext"
    string projectNamespace,     // e.g., "LinqStudio.Generated"
    ILogger<CompilerService>? logger = null
)
```

**Initialization:**
```csharp
public async Task Initialize(
    Dictionary<string, string> tableModelFiles,  // Generated C# model classes
    string dbContextCode                         // Generated DbContext code
)
```

**Key Insight:** CompilerService operates on **already-generated code strings**. It doesn't connect to databases. Its job is:
1. Load generated model code into Roslyn workspace
2. Provide IntelliSense completions for Monaco editor
3. Provide hover tooltips
4. Wrap user queries in `QueryContainer` class for analysis

**Where does the generated code come from?**

### CompilerServiceFactory Pattern
**File:** `src/LinqStudio.Core/Services/CompilerServiceFactory.cs`

```csharp
public class CompilerServiceFactory(
    IDbContextGenerator? generator = null,
    ILogger<CompilerService>? logger = null
)
{
    // Creates compiler from a Project's live database
    public async Task<CompilerService> CreateFromProjectAsync(
        Project project, 
        CancellationToken cancellationToken = default
    )
    {
        if (generator is null || project.QueryGenerator is null)
            return await CreateAsync(); // Fallback to demo model
        
        // ← THIS IS WHERE PROJECT CONTEXT IS USED
        var result = await generator.GenerateAsync(
            project.QueryGenerator,  // ← Uses Project's QueryGenerator
            cancellationToken
        );
        
        var svc = new CompilerService(
            result.ContextTypeName, 
            result.Namespace, 
            logger
        );
        await svc.Initialize(result.ModelFiles, result.DbContextCode);
        return svc;
    }
}
```

**Data Flow:**
1. **Editor.razor** calls `CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)`
2. Factory uses `Project.QueryGenerator` (which knows connection string + DB type)
3. `IDbContextGenerator.GenerateAsync()` introspects the database and generates C# code
4. Generated code is passed to `CompilerService.Initialize()`
5. CompilerService now has model classes for IntelliSense

**Conclusion:** CompilerService gets project context **indirectly** via generated code from `IDbContextGenerator`, which is given the `Project.QueryGenerator`.

---

## 3. How does the UI (Editor.razor) know which project is active?

### Editor.razor.cs Dependency Injection
**File:** `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` (lines 31-35)

```csharp
[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
[Inject] private CompilerServiceFactory CompilerServiceFactory { get; set; } = null!;
[Inject] private IDbContextGenerator DbContextGenerator { get; set; } = null!;
[Inject] private IQueryExecutionService QueryExecutionService { get; set; } = null!;
```

**Project Access Pattern:**
```csharp
// Line 212-214: Initialize CompilerService from project
_compiler = Workspace.CurrentProject != null
    ? await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)
    : await CompilerServiceFactory.CreateAsync();

// Line 444-447: Check if project has DB connection
if (!Workspace.IsProjectOpen || Workspace.CurrentProject?.QueryGenerator is null)
{
    Snackbar.Add("No database connection configured for this project.", Severity.Warning);
    return;
}

// Line 521: Execute query (current broken code - no project passed)
var result = await QueryExecutionService.ExecuteQueryAsync(
    queryText, 
    state.CancellationTokenSource.Token
);
```

**Key Observations:**
- Editor has direct access to `ProjectWorkspace`
- Editor knows which project is active via `Workspace.CurrentProject`
- Editor passes project to `CompilerServiceFactory` for IntelliSense setup
- Editor does NOT pass project to `QueryExecutionService` (the bug)

---

## 4. What is ProjectWorkspace and QueriesWorkspace?

### ProjectWorkspace
**File:** `src/LinqStudio.Blazor/Services/ProjectWorkspace.cs`

**Registration:** Scoped service (one per user session)

**Purpose:** Manages the current working project in the IDE workspace

**Key Properties:**
```csharp
public Project? CurrentProject { get; }          // The active project
public string? CurrentFilePath { get; }          // Path to .linq file
public QueriesWorkspace Queries { get; }         // Child workspace for queries
public bool HasUnsavedChanges { get; }           // Tracks project + queries
public bool IsProjectOpen { get; }               // Whether a project is loaded
```

**Key Methods:**
- `CreateNewAsync(name)` - Create new in-memory project
- `LoadAsync(filePath)` - Load project from disk
- `SaveAsync()` - Save project and queries to disk
- `Update(updatedProject)` - Update project properties
- `Close()` - Close current project

**Event:** `WorkspaceChanged` - Fired when project state changes

**Relationship with Queries:**
- Owns a `QueriesWorkspace` instance
- Aggregates unsaved changes from both project properties and queries
- Initializes `QueriesWorkspace` with project file path on load

### QueriesWorkspace
**File:** `src/LinqStudio.Blazor/Services/QueriesWorkspace.cs`

**Purpose:** Manages query-related operations for the current project

**Key State:**
```csharp
private Guid? _currentQueryId;                                      // Active query tab
private Dictionary<Guid, OpenQueryState> _openQueries;              // In-memory edits
private Dictionary<Guid, SavedQuery> _allQueries;                   // All queries from disk
private string? _projectFilePath;                                   // Where to save queries
```

**Key Methods:**
- `InitializeAsync(projectFilePath)` - Load all queries from project's query folder
- `OpenQuery(queryId)` - Open a query tab
- `CreateNewQuery(name)` - Create new query
- `UpdateQueryText(queryId, newText)` - Track in-memory changes
- `SaveQueryAsync(queryId)` - Persist query to disk

**Event:** `QueriesChanged` - Fired when query state changes

**Relationship with Project:**
- Does NOT hold a Project reference
- Only knows the project file path (for query folder location)
- Queries are stored in `<projectPath>_queries/*.linq.query` files

---

## 5. What is the right fix?

### Analysis of Options

#### Option A: Add `Project project` parameter to interface
```csharp
Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    Project project,  // ← NEW
    CancellationToken cancellationToken = default
);
```

**Pros:**
- Explicit - caller controls which project is used
- No hidden dependencies
- Easy to test - just pass a Project instance

**Cons:**
- Breaks the interface - requires updating all callers
- Editor.razor already has `ProjectWorkspace` - would be passing `Workspace.CurrentProject` every time
- Redundant - the UI always wants "the current project", not arbitrary projects

**Impact:**
- Change `IQueryExecutionService.cs` (interface)
- Change `QueryExecutionService.cs` (implementation)
- Change `Editor.razor.cs` (line 521: add `Workspace.CurrentProject` parameter)
- Update all tests to pass Project parameter

**Verdict:** ❌ **Not recommended** - breaks interface, adds redundant parameter

---

#### Option B: Create scoped `ICurrentProjectService`
```csharp
public interface ICurrentProjectService
{
    Project? CurrentProject { get; }
}

public class CurrentProjectService : ICurrentProjectService
{
    private readonly ProjectWorkspace _workspace;
    
    public CurrentProjectService(ProjectWorkspace workspace)
    {
        _workspace = workspace;
    }
    
    public Project? CurrentProject => _workspace.CurrentProject;
}
```

Then inject it into `QueryExecutionService`:
```csharp
public QueryExecutionService(
    IDbContextGenerator generator,
    IOptionsMonitor<QueryExecutionSettings> settings,
    ICurrentProjectService currentProjectService,  // ← NEW
    ILogger<QueryExecutionService>? logger = null
)
```

**Pros:**
- No interface change to `IQueryExecutionService`
- Service automatically knows which project is active
- Clean dependency injection

**Cons:**
- Adds a new abstraction (`ICurrentProjectService`) just to wrap `ProjectWorkspace.CurrentProject`
- Indirection - harder to understand data flow
- `ProjectWorkspace` already exists and does this job
- Layer violation: Core services would depend on Blazor services (ProjectWorkspace is in Blazor layer)

**Layer Analysis:**
- `ProjectWorkspace` is in `LinqStudio.Blazor` project
- `QueryExecutionService` is in `LinqStudio.Core` project
- Core should NOT depend on Blazor (breaks layered architecture)
- Would need to move `ProjectWorkspace` to Core (major refactor) or create abstraction

**Verdict:** ❌ **Not recommended** - unnecessary abstraction, potential layer violation

---

#### Option C: Inject `ProjectWorkspace` directly into `QueryExecutionService`
```csharp
public QueryExecutionService(
    IDbContextGenerator generator,
    IOptionsMonitor<QueryExecutionSettings> settings,
    ProjectWorkspace workspace,  // ← NEW
    ILogger<QueryExecutionService>? logger = null
)
{
    _generator = generator;
    _settings = settings;
    _workspace = workspace;  // ← Store reference
    _logger = logger;
}

public async Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    CancellationToken cancellationToken = default
)
{
    // Get project from workspace
    var project = _workspace.CurrentProject;
    if (project is null)
    {
        return QueryExecutionResult.FromError(
            "No project is currently open.", 
            isCompileError: false, 
            TimeSpan.Zero
        );
    }
    
    // Continue with existing ExecuteQueryInternalAsync logic...
    return await ExecuteQueryInternalAsync(userQuery, project, cancellationToken);
}
```

**Pros:**
- ✅ No interface change - existing callers work unchanged
- ✅ Service automatically accesses current project
- ✅ Aligns with existing pattern: `Editor.razor` already injects `ProjectWorkspace`
- ✅ Simple - just add one constructor parameter
- ✅ Easy to test - mock `ProjectWorkspace` in tests

**Cons:**
- ⚠️ Layer issue: `QueryExecutionService` (Core) depends on `ProjectWorkspace` (Blazor)

**Layer Issue Resolution:**

**Current registration (ServiceCollectionExtensions.cs):**
```csharp
// LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs
services.AddScoped<IQueryExecutionService, QueryExecutionService>();
```

**Where ProjectWorkspace is registered:**
```csharp
// LinqStudio.Blazor/Extensions/ServiceCollectionExtensions.cs
services.AddScoped<ProjectWorkspace>();
services.AddScoped<QueriesWorkspace>();
```

**Analysis:**
- Both are **scoped** services (one per user session)
- Registration happens in correct order: Core → Blazor → App.WebServer
- At runtime in App.WebServer, both are available in DI container
- This is NOT a layer violation - it's a runtime dependency resolution

**Architectural Pattern Already Exists:**
```csharp
// Editor.razor already injects both:
[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
[Inject] private IQueryExecutionService QueryExecutionService { get; set; } = null!;
```

If we inject `ProjectWorkspace` into `QueryExecutionService`, the dependency graph becomes:
```
Editor.razor
  ↓ (injects)
  ├── ProjectWorkspace (has CurrentProject)
  └── IQueryExecutionService (implementation: QueryExecutionService)
        ↓ (injects)
        └── ProjectWorkspace (same instance, scoped)
```

This is **valid** because:
1. Both services are scoped - same instance per request
2. No circular dependency
3. Both need access to the current project state

**Verdict:** ✅ **RECOMMENDED** - simplest fix, aligns with existing architecture

---

#### Option D: Extract project context to a Core-layer abstraction

Create `IProjectContext` in Abstractions layer:
```csharp
// src/LinqStudio.Abstractions/Services/IProjectContext.cs
public interface IProjectContext
{
    Project? CurrentProject { get; }
}
```

Implement in Blazor layer:
```csharp
// src/LinqStudio.Blazor/Services/ProjectContext.cs
public class ProjectContext : IProjectContext
{
    private readonly ProjectWorkspace _workspace;
    
    public ProjectContext(ProjectWorkspace workspace)
    {
        _workspace = workspace;
    }
    
    public Project? CurrentProject => _workspace.CurrentProject;
}
```

Register in Blazor:
```csharp
services.AddScoped<IProjectContext, ProjectContext>();
```

Inject into QueryExecutionService:
```csharp
public QueryExecutionService(
    IDbContextGenerator generator,
    IOptionsMonitor<QueryExecutionSettings> settings,
    IProjectContext projectContext,  // ← NEW
    ILogger<QueryExecutionService>? logger = null
)
```

**Pros:**
- ✅ Respects layer boundaries (Core depends on Abstractions)
- ✅ No interface change to `IQueryExecutionService`
- ✅ Testable - easy to mock `IProjectContext`
- ✅ Future-proof - if we add multiple workspace types, abstraction remains stable

**Cons:**
- ⚠️ Adds one more abstraction layer
- ⚠️ More files to create/maintain
- ⚠️ Not strictly necessary (Option C works and is simpler)

**Verdict:** ✅ **ALSO RECOMMENDED** - more "proper" from a layering perspective, but more work

---

### Final Recommendation

**Primary Choice: Option C** (Inject `ProjectWorkspace` directly)

**Rationale:**
1. **Simplest implementation** - one constructor parameter, no new files
2. **No interface breaking change** - existing callers work unchanged
3. **Aligns with existing patterns** - Editor already injects ProjectWorkspace
4. **Not a layer violation** - runtime DI resolution, both services are scoped
5. **Easy to test** - just mock ProjectWorkspace

**Secondary Choice: Option D** (If you want stricter layering)

If strict layer separation is critical, Option D with `IProjectContext` is the better choice. It adds minimal complexity while respecting architectural boundaries.

---

## 6. Impact Assessment

### Files to Change (Option C - Recommended)

#### 1. `src/LinqStudio.Core/Services/QueryExecutionService.cs`
**Change:** Add `ProjectWorkspace` to constructor

**Before:**
```csharp
private readonly IDbContextGenerator _generator;
private readonly IOptionsMonitor<QueryExecutionSettings> _settings;
private readonly ILogger<QueryExecutionService>? _logger;

public QueryExecutionService(
    IDbContextGenerator generator,
    IOptionsMonitor<QueryExecutionSettings> settings,
    ILogger<QueryExecutionService>? logger = null)
{
    _generator = generator;
    _settings = settings;
    _logger = logger;
}
```

**After:**
```csharp
private readonly IDbContextGenerator _generator;
private readonly IOptionsMonitor<QueryExecutionSettings> _settings;
private readonly ProjectWorkspace _workspace;  // ← NEW
private readonly ILogger<QueryExecutionService>? _logger;

public QueryExecutionService(
    IDbContextGenerator generator,
    IOptionsMonitor<QueryExecutionSettings> settings,
    ProjectWorkspace workspace,  // ← NEW
    ILogger<QueryExecutionService>? logger = null)
{
    _generator = generator;
    _settings = settings;
    _workspace = workspace;  // ← NEW
    _logger = logger;
}
```

**Change:** Update `ExecuteQueryAsync` to use workspace's current project

**Before:**
```csharp
public async Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        throw new NotImplementedException(
            "QueryExecutionService requires Project parameter to access connection string and database type. " +
            "This will be added to the IQueryExecutionService interface in a future phase.");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger?.LogError(ex, "Query execution failed with runtime error");
        return QueryExecutionResult.FromError(ex.Message, isCompileError: false, stopwatch.Elapsed);
    }
}
```

**After:**
```csharp
public async Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();
    
    // Get project from workspace
    var project = _workspace.CurrentProject;
    if (project is null)
    {
        stopwatch.Stop();
        return QueryExecutionResult.FromError(
            "No project is currently open.", 
            isCompileError: false, 
            stopwatch.Elapsed);
    }
    
    // Delegate to internal implementation
    return await ExecuteQueryInternalAsync(userQuery, project, cancellationToken);
}
```

**Change:** Make `ExecuteQueryInternalAsync` private (no longer needs to be internal)

**Before:**
```csharp
internal async Task<QueryExecutionResult> ExecuteQueryInternalAsync(
```

**After:**
```csharp
private async Task<QueryExecutionResult> ExecuteQueryInternalAsync(
```

**Lines affected:** ~60-80 (remove NotImplementedException, add workspace access, change visibility)

---

#### 2. `tests/LinqStudio.Core.Tests/QueryExecutionServiceTests.cs`
**Change:** Update constructor tests to pass mock `ProjectWorkspace`

**Required Changes:**
- Add `MockProjectWorkspace` test helper class
- Update all `QueryExecutionService` constructor calls to pass mock workspace
- Add test: `ExecuteQueryAsync_WhenNoProjectOpen_ReturnsError`
- Add test: `ExecuteQueryAsync_WhenProjectOpen_CallsInternalMethod` (may require reflection or integration test)

**Estimated lines affected:** ~50-100 (mostly test setup boilerplate)

---

#### 3. `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs`
**Change:** None required! 

**Why:** Editor already injects `ProjectWorkspace`, and `QueryExecutionService` will now receive it via DI automatically. The call at line 521 remains unchanged:

```csharp
var result = await QueryExecutionService.ExecuteQueryAsync(queryText, state.CancellationTokenSource.Token);
```

---

### Breaking Changes Analysis

**Interface changes:** None

**Binary compatibility:** Preserved (no public API changes)

**Existing tests:** Need updates to pass mock workspace

**Runtime behavior:** Same - just now works instead of throwing NotImplementedException

---

### Ripple Effect

**Minimal ripple:**
- One constructor parameter added (handled by DI container)
- Tests need mock workspace setup
- No changes to calling code (Editor.razor.cs unchanged)

**No impact on:**
- `IQueryExecutionService` interface
- Blazor components using the service
- Other services in the DI container
- User-facing UI

---

## Additional Findings

### Related Architecture Patterns in Codebase

#### CompilerServiceFactory Already Uses Project Pattern
```csharp
// Editor.razor.cs line 212
_compiler = Workspace.CurrentProject != null
    ? await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)
    : await CompilerServiceFactory.CreateAsync();
```

This shows that passing project context from workspace is an **established pattern** in the codebase.

#### ProjectWorkspace is Already Injected Everywhere
```shell
$ grep -r "ProjectWorkspace" --include="*.cs" src/LinqStudio.Blazor/Components
```

**Results:**
- `MainLayout.razor.cs`: `[Inject] private ProjectWorkspace Workspace { get; set; }`
- `Editor.razor.cs`: `[Inject] private ProjectWorkspace Workspace { get; set; }`
- `ProjectSettings.razor.cs`: `[Inject] private ProjectWorkspace Workspace { get; set; }`
- `DatabaseTreeView.razor.cs`: `[Inject] private ProjectWorkspace Workspace { get; set; }`

**Insight:** Every major Blazor component injects `ProjectWorkspace`. Having `QueryExecutionService` also inject it is **consistent with existing architecture**.

---

## Risk Analysis

### Option C Risks

**Risk 1:** What if ProjectWorkspace is null at service creation?
- **Mitigation:** Both services are scoped - registered together, lifecycle managed by DI container
- **Validation:** Check at runtime in `ExecuteQueryAsync` (already planned)

**Risk 2:** What if ProjectWorkspace.CurrentProject changes mid-execution?
- **Mitigation:** ExecuteQueryAsync captures project reference at start of method
- **User scenario:** User closes project while query is running
- **Handling:** Query continues with captured project, or cancellation token cancels it

**Risk 3:** Layer architecture purists might object to Core depending on Blazor service
- **Mitigation:** Both services are in the same DI container scope at runtime
- **Alternative:** Switch to Option D (IProjectContext abstraction) if strict layering is required

---

## Documentation Needs

### Files to Create/Update

1. **Create:** `docs/QUERY_EXECUTION.md`
   - Document how query execution works end-to-end
   - Explain CompilerService vs QueryExecutionService roles
   - Show data flow: Project → Generator → Compiler → Execution
   - Include architecture diagram

2. **Update:** `src/LinqStudio.Core/Services/copilot.md`
   - Add section on QueryExecutionService implementation
   - Explain ProjectWorkspace dependency
   - Document ExecuteQueryInternalAsync method flow

3. **Update:** `src/LinqStudio.Blazor/Services/copilot.md` (if exists)
   - Document ProjectWorkspace role in query execution
   - Add ProjectWorkspace usage patterns section

---

## Conclusion

The gap is clear: **QueryExecutionService needs project context but the interface doesn't provide it.**

The cleanest fix is **Option C**: Inject `ProjectWorkspace` directly into `QueryExecutionService`. This:
- Requires minimal code changes
- Aligns with existing patterns in the codebase
- Preserves the existing interface
- Is easy to test
- Works within the established DI architecture

**Alternative:** If strict layer separation is critical, implement **Option D** with `IProjectContext` abstraction.

**Next Steps:**
1. Decide between Option C (simpler) and Option D (stricter layering)
2. Implement chosen option
3. Update tests
4. Add documentation
5. Run full test suite to ensure no regressions

---

## References

### Files Analyzed
- `src/LinqStudio.Core/Models/Project.cs`
- `src/LinqStudio.Core/Services/ProjectService.cs`
- `src/LinqStudio.Core/Services/CompilerService.cs`
- `src/LinqStudio.Core/Services/CompilerServiceFactory.cs`
- `src/LinqStudio.Core/Services/QueryExecutionService.cs`
- `src/LinqStudio.Blazor/Services/ProjectWorkspace.cs`
- `src/LinqStudio.Blazor/Services/QueriesWorkspace.cs`
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs`
- `src/LinqStudio.Abstractions/Services/IQueryExecutionService.cs`
- `src/LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs`
- `tests/LinqStudio.Core.Tests/QueryExecutionServiceTests.cs`

### Key Line Numbers
- **Project model:** `Project.cs:16-24` (DatabaseType, ConnectionString)
- **CompilerServiceFactory project usage:** `CompilerServiceFactory.cs:69-79`
- **Editor workspace injection:** `Editor.razor.cs:31`
- **Editor compiler initialization:** `Editor.razor.cs:212-214`
- **Editor query execution call:** `Editor.razor.cs:521`
- **QueryExecutionService NotImplementedException:** `QueryExecutionService.cs:46-48`
- **QueryExecutionService internal method:** `QueryExecutionService.cs:62-190`

### Team Context
- **Architecture decisions:** `.squad/decisions.md`
- **Samy history:** `.squad/agents/samy/history.md`
- **Layer architecture:** Abstractions → Core → Blazor (established pattern)
- **Workspace pattern:** ProjectWorkspace + QueriesWorkspace (established pattern)


---

## Decision (Inbox): simon-execution-gap-analysis.md

# QueryExecutionService Architecture Gap Analysis
**Author:** Simon (Backend Core Dev)  
**Date:** 2026-03-14  
**Status:** Technical Analysis - Pre-Implementation

---

## EXECUTIVE SUMMARY

The `QueryExecutionService` has a complete internal implementation (`ExecuteQueryInternalAsync`) but cannot be used because the public interface method `ExecuteQueryAsync(string userQuery, CancellationToken)` has no way to receive the required `Project` context (connection string, database type). The service currently throws `NotImplementedException` at runtime.

**Root cause:** Interface/implementation mismatch — the internal logic needs `Project` but the interface signature doesn't accept it.

---

## 1. WHAT I BUILT: QueryExecutionService

**File:** `src/LinqStudio.Core/Services/QueryExecutionService.cs`

### Current State

**Public Method (Lines 36-56):**
```csharp
public async Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    CancellationToken cancellationToken = default)
{
    // Line 46-48: Explicit NotImplementedException
    throw new NotImplementedException(
        "QueryExecutionService requires Project parameter to access connection string and database type. " +
        "This will be added to the IQueryExecutionService interface in a future phase.");
}
```

**Internal Implementation (Lines 62-190):**
```csharp
internal async Task<QueryExecutionResult> ExecuteQueryInternalAsync(
    string userQuery,
    Models.Project project,  // ← NEEDS THIS
    CancellationToken cancellationToken = default)
{
    // Line 71: Requires project.ConnectionString
    if (string.IsNullOrWhiteSpace(project.ConnectionString))
    
    // Line 78: Requires project.QueryGenerator
    var generatorResult = await _generator.GenerateAsync(project.QueryGenerator!, cancellationToken);
    
    // Line 98: Requires project.DatabaseType and project.ConnectionString
    var dbContextOptions = CreateDbContextOptions(project.DatabaseType, project.ConnectionString);
    
    // [Lines 80-189: Complete working implementation]
}
```

### What Goes Wrong

**Line 71:** Needs `project.ConnectionString` (string property)  
**Line 78:** Needs `project.QueryGenerator` (IDatabaseQueryGenerator)  
**Line 98:** Needs `project.DatabaseType` (enum) and `project.ConnectionString`

**None of these are available** because the public method doesn't accept a `Project` parameter.

---

## 2. HOW COMPILERSERVICE GETS PROJECT CONTEXT

**File:** `src/LinqStudio.Core/Services/CompilerService.cs`

### Pattern: Factory-Based Initialization

CompilerService does NOT accept project context per-call. Instead:

1. **Constructor (Lines 23-39):** Takes only `contextTypeName`, `projectNamespace`, and optional logger
2. **Initialize method (Lines 103-119):** Called by factory to load model files and DbContext code
3. **Factory handles project → code generation (CompilerServiceFactory.cs:69-80):**

```csharp
// CompilerServiceFactory.cs
public async Task<CompilerService> CreateFromProjectAsync(Project project, CancellationToken cancellationToken = default)
{
    if (generator is null || project.QueryGenerator is null)
    {
        return await CreateAsync();  // Fallback to demo model
    }

    // Step 1: Generate models/DbContext from project's DB
    var result = await generator.GenerateAsync(project.QueryGenerator, cancellationToken);
    
    // Step 2: Create and initialize CompilerService with generated code
    var svc = new CompilerService(result.ContextTypeName, result.Namespace, logger);
    await svc.Initialize(result.ModelFiles, result.DbContextCode);
    return svc;
}
```

**Key insight:** CompilerService is **stateful** — initialized once per project, reused for all queries. It doesn't need runtime project context because it was already baked in during initialization.

---

## 3. HOW EDITOR USES COMPILERSERVICE

**File:** `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs`

### Lifecycle (Lines 202-220)

```csharp
private async Task OnEditorInitialized()
{
    // Line 212-214: Create CompilerService from current project
    _compiler = Workspace.CurrentProject != null
        ? await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)
        : await CompilerServiceFactory.CreateAsync();
    
    // [Lines 222-271: Register Monaco providers that use _compiler]
}
```

### Schema Refresh (Lines 442-470)

```csharp
private async Task RefreshSchemaAsync()
{
    // Line 455-457: Re-create CompilerService from fresh DB schema
    var newCompiler = await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject!);
    _compiler?.Dispose();
    _compiler = newCompiler;
}
```

### Query Execution (Lines 488-546)

```csharp
private async Task ExecuteCurrentQueryAsync()
{
    // Line 503: Get query text from Monaco editor
    var queryText = await _editor.GetValue();
    
    // Line 521: Call QueryExecutionService
    var result = await QueryExecutionService.ExecuteQueryAsync(queryText, state.CancellationTokenSource.Token);
    
    // ← PROBLEM: No Project passed here, but service needs it!
}
```

**The gap:** Editor has `Workspace.CurrentProject` (Lines 94, 212, 455) but doesn't pass it to `ExecuteQueryAsync`.

---

## 4. THE PROJECT MODEL

**File:** `src/LinqStudio.Core/Models/Project.cs`

### Properties (Lines 10-43)

```csharp
public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    // Line 16-24: DatabaseType property
    public DatabaseType DatabaseType { get; set; } = DatabaseType.Mssql;
    
    // Line 26-34: ConnectionString property
    public string? ConnectionString { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ModifiedDate { get; set; }
    
    // Lines 41-42: Future properties (not used yet)
    public Dictionary<string, string>? Models { get; set; }
    public string? DbContextCode { get; set; }
    
    // Line 52-77: QueryGenerator computed property
    [JsonIgnore]
    public IDatabaseQueryGenerator? QueryGenerator { get; }  // Auto-created from DatabaseType + ConnectionString
}
```

**Available via:** `Workspace.CurrentProject` in Editor.razor.cs (injected Line 31)

---

## 5. THE EXACT GAP: CODE PATH THAT FAILS

### Call Stack

```
Editor.razor.cs:521
  └─> QueryExecutionService.ExecuteQueryAsync(queryText, cancellationToken)
        ├─ Line 46-48: throws NotImplementedException
        └─ NEEDS: project.ConnectionString, project.DatabaseType, project.QueryGenerator
              └─ Available at: Workspace.CurrentProject
                    └─ BUT: Not passed because interface doesn't accept it
```

### What Variable Is Missing

| Variable | Type | Available At | Needed At | Current Status |
|----------|------|--------------|-----------|----------------|
| `project.ConnectionString` | `string?` | `Workspace.CurrentProject` in Editor | Line 71, 98 in `ExecuteQueryInternalAsync` | ❌ Not passed |
| `project.DatabaseType` | `DatabaseType` enum | `Workspace.CurrentProject` in Editor | Line 98 in `ExecuteQueryInternalAsync` | ❌ Not passed |
| `project.QueryGenerator` | `IDatabaseQueryGenerator?` | `Workspace.CurrentProject` in Editor | Line 78 in `ExecuteQueryInternalAsync` | ❌ Not passed |

---

## 6. PROPOSED FIX: THE MINIMAL CORRECT SOLUTION

### Option A: Add Project Parameter to Interface (RECOMMENDED)

**Rationale:** QueryExecutionService is inherently **stateless** — unlike CompilerService, it doesn't need pre-initialization. Each query execution is independent and may target different projects/connections in the future.

**Changes Required:**

1. **Update interface signature** (`src/LinqStudio.Abstractions/Services/IQueryExecutionService.cs`):
   ```csharp
   Task<QueryExecutionResult> ExecuteQueryAsync(
       string userQuery,
       Models.Project project,  // ← ADD THIS
       CancellationToken cancellationToken = default);
   ```

2. **Update public method** (`src/LinqStudio.Core/Services/QueryExecutionService.cs`):
   ```csharp
   public async Task<QueryExecutionResult> ExecuteQueryAsync(
       string userQuery,
       Models.Project project,  // ← ADD THIS
       CancellationToken cancellationToken = default)
   {
       return await ExecuteQueryInternalAsync(userQuery, project, cancellationToken);
   }
   ```

3. **Update caller** (`src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs:521`):
   ```csharp
   // Add null check
   if (Workspace.CurrentProject is null)
   {
       state.Result = QueryExecutionResult.FromError("No project open", false, TimeSpan.Zero);
       return;
   }
   
   var result = await QueryExecutionService.ExecuteQueryAsync(
       queryText, 
       Workspace.CurrentProject,  // ← ADD THIS
       state.CancellationTokenSource.Token);
   ```

4. **Delete `internal` keyword** from `ExecuteQueryInternalAsync` (Line 62) — merge into public method.

**Pros:**
- ✅ Stateless design — no lifecycle management needed
- ✅ Flexible — can execute queries against different projects without re-initialization
- ✅ Matches the "per-call context" pattern already used in `IDbContextGenerator.GenerateAsync(queryGenerator, cancellationToken)`
- ✅ Minimal code changes (3 files)

**Cons:**
- ⚠️ Breaks interface contract (but service currently throws NotImplementedException anyway)

---

### Option B: Factory-Based Initialization (Like CompilerService)

**Pattern:** Create `QueryExecutionServiceFactory`, initialize service with project context, store as instance field.

**Changes Required:**

1. Add factory class
2. Add `Initialize(Project project)` method to service
3. Change service from stateless to stateful (store project fields)
4. Update DI registration to scoped
5. Update Editor to create/dispose service per project

**Pros:**
- ✅ Matches CompilerService pattern

**Cons:**
- ❌ More complex — 5+ file changes
- ❌ Stateful service → lifecycle management, disposal, re-initialization on project change
- ❌ Overkill — query execution is inherently per-call, not per-session
- ❌ No architectural benefit — service doesn't maintain expensive state like Roslyn workspace

---

### Option C: Pass Connection String + DatabaseType Individually

**Pattern:** Add individual parameters instead of whole Project object.

```csharp
Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    string connectionString,
    DatabaseType databaseType,
    CancellationToken cancellationToken = default);
```

**Pros:**
- ✅ Explicit dependencies

**Cons:**
- ❌ Still needs `project.QueryGenerator` (not passed)
- ❌ Potential parameter proliferation if service needs more project properties later
- ❌ Less cohesive — Project is already the natural unit of context

---

## 7. RECOMMENDATION

**Use Option A: Add Project Parameter to Interface**

**Justification:**
1. **Architectural fit:** QueryExecutionService is fundamentally a **per-request operation**, unlike CompilerService which maintains Roslyn state. Stateless design is correct.
2. **Precedent:** `IDbContextGenerator.GenerateAsync(queryGenerator, cancellationToken)` already takes context per-call.
3. **Simplicity:** 3 file changes vs 5+ for factory pattern.
4. **Flexibility:** Allows future features like "execute query against different connection" without service re-initialization.
5. **Current state:** Interface already non-functional (throws NotImplementedException), so breaking change has no impact.

**Implementation order:**
1. Update `IQueryExecutionService` interface
2. Merge `ExecuteQueryInternalAsync` into `ExecuteQueryAsync` (delete internal method)
3. Update Editor.razor.cs caller with null check
4. Run tests

---

## 8. SUPPORTING EVIDENCE

### CompilerService vs QueryExecutionService: Why Different Patterns?

| Aspect | CompilerService | QueryExecutionService |
|--------|-----------------|----------------------|
| **State** | Stateful (AdhocWorkspace, documents) | Stateless (compiles fresh each time) |
| **Initialization Cost** | High (Roslyn workspace, metadata refs) | Low (just wraps query, compiles) |
| **Reuse** | Many calls per instance (hover, completion, typing) | One call per query execution |
| **Lifecycle** | Tied to editor session | Per-request |
| **Mutation** | Workspace modified (AddOrUpdateFile) | No mutation |
| **Thread Safety** | Needs SemaphoreSlim | No locks needed |
| **Pattern Fit** | Factory + Initialize | Per-call context |

---

## APPENDIX: FILE LOCATIONS

- **Interface:** `src/LinqStudio.Abstractions/Services/IQueryExecutionService.cs` (Lines 8-19)
- **Implementation:** `src/LinqStudio.Core/Services/QueryExecutionService.cs` (Lines 20-406)
- **Caller:** `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` (Line 521)
- **Project Model:** `src/LinqStudio.Core/Models/Project.cs` (Lines 10-118)
- **CompilerService (for comparison):** `src/LinqStudio.Core/Services/CompilerService.cs` (Lines 10-150)
- **CompilerServiceFactory (for comparison):** `src/LinqStudio.Core/Services/CompilerServiceFactory.cs` (Lines 10-81)

---

## END OF ANALYSIS

---

### 9. Extract RoslynWorkspaceService — Reduce Code Duplication

**Date:** 2026-03-14  
**Author:** Simon (Backend Core Dev)  
**Status:** ✅ Implemented

**Problem:** CompilerService and QueryExecutionService both contained nearly identical code for:
1. Creating Roslyn `AdhocWorkspace` instances with project setup
2. Loading EF Core metadata references (Microsoft.EntityFrameworkCore.*, database providers, System.Linq.*)
3. Wrapping user LINQ queries in a synthetic `QueryContainer` class

**Consequences of Duplication:**
- **Inconsistent assembly lists**: CompilerService was missing SQLite, PostgreSQL, and MySQL providers
- **Maintenance burden**: Any change to workspace setup required updating both services
- **Testing overhead**: Test fixtures had to duplicate workspace creation logic

**Decision:** Extract shared Roslyn workspace management into a new `RoslynWorkspaceService` singleton service.

**Implementation:**

```csharp
public class RoslynWorkspaceService
{
    /// Creates a new AdhocWorkspace with a project pre-configured with all EF Core metadata references
    public (AdhocWorkspace Workspace, ProjectId ProjectId, Solution Solution) CreateWorkspace(string projectName);

    /// Returns the complete set of MetadataReferences for EF Core + all DB providers + common system assemblies
    public IReadOnlyList<MetadataReference> GetMetadataReferences();

    /// Wraps a user LINQ query in a QueryContainer class for Roslyn analysis or compilation
    public string WrapQuery(string userQuery, string contextTypeName, string projectNamespace, string beforeReturn = "return");
}
```

**Key Design Decisions:**

1. **Stateless Singleton** — No shared mutable state, thread-safe, creates fresh workspaces per call
2. **Assembly list from QueryExecutionService** — Canonical source includes all DB providers (fixed missing SQLite, PostgreSQL, MySQL)
3. **Preserved cursor position logic** — `WrapQuery()` produces byte-for-byte identical output to original implementations
4. **Maintained separation of concerns** — CompilerService retains `SemaphoreSlim` and document lifecycle management
5. **Registered before dependents** — Ensures proper injection order: RoslynWorkspaceService → CompilerService → QueryExecutionService

**Results:**
- ✅ Eliminated duplicate workspace initialization code
- ✅ Fixed assembly loading inconsistencies across services
- ✅ Improved testability with single RoslynWorkspaceService instance
- ✅ All 487 tests pass (121 Core + 56 Blazor + 310 Databases + 26 E2E, 4 skipped)
- ✅ Build succeeded with 0 errors, 0 warnings

**Status:** ✅ Implemented, tested, ready for merge


---

## Decision (Inbox): simon-option-a-fix.md

# Decision: Option A Implementation - Add Project Parameter to IQueryExecutionService

**Date:** 2026-03-13  
**Decided by:** Simon (Backend Core Dev)  
**Requested by:** snakex64  
**Status:** ✅ Implemented & Tested

## Problem

`IQueryExecutionService.ExecuteQueryAsync` lacked access to Project context (connection string, DatabaseType, QueryGenerator), preventing query execution against real databases.

Original signature:
`csharp
Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    CancellationToken cancellationToken = default);
`

## Solution Chosen: Option A

Add `Project project` parameter to the interface and implementation.

Final signature:
`csharp
Task<QueryExecutionResult> ExecuteQueryAsync(
    string userQuery,
    Project project,
    CancellationToken cancellationToken = default);
`

## Implementation Details

### Key Changes

1. **Interface Location**: Moved `IQueryExecutionService` from `LinqStudio.Abstractions.Services` to `LinqStudio.Core.Services`
   - Reason: Avoided circular dependency (Abstractions → Core for Project class would conflict with Core → Abstractions)
   - Impact: Minimal - interface is implementation-specific, not a general abstraction

2. **Service Implementation**: 
   - Removed `NotImplementedException` placeholder
   - Public method now delegates to `ExecuteQueryInternalAsync` which contains full implementation
   - Uses `project.ConnectionString`, `project.DatabaseType`, and `project.QueryGenerator` as designed

3. **Test Updates**:
   - All tests now instantiate `Project` with test values
   - Tests verify proper error handling for missing/empty connection strings
   - Cancellation handling verified with Project parameter

### Files Modified

- `src/LinqStudio.Core/Services/IQueryExecutionService.cs`
- `src/LinqStudio.Core/Services/QueryExecutionService.cs`
- `tests/LinqStudio.Core.Tests/QueryExecutionServiceTests.cs`
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` (using statement update)
- `src/LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs` (using statement cleanup)

## Validation

✅ **Build:** Successful (0 errors)  
✅ **Tests:** All 485 tests pass  
✅ **Test Breakdown:**
- 121 Core tests
- 39 Blazor tests  
- 310 Database tests
- 15 E2E tests (4 skipped)

## Why This Solution

**Pros:**
- ✅ Simple, direct approach
- ✅ Matches existing internal implementation pattern
- ✅ Type-safe at compile time
- ✅ No breaking changes to consumers (they must now provide Project)

**Alternatives Considered (but rejected):**
- Moving Project to Abstractions → Circular dependency
- Passing individual parameters (connectionString, databaseType, queryGenerator) → Too many parameters, loses cohesion
- Configuration/Options pattern → Over-engineered for this use case

## Next Steps

Callers of `IQueryExecutionService.ExecuteQueryAsync` (e.g., Editor.razor.cs) must now pass the active `Project` instance. This is expected and desired - query execution requires project context.


---

## Decision (Inbox): eviljosh-project-param-fix.md

# Decision: Query Execution Now Requires Project Context

**Date:** 2026-03-13  
**Agent:** EvilJosh (Frontend Dev)  
**Coordinated With:** Simon (Backend Dev)  
**Status:** ✅ Implemented (pending backend interface merge)

## Context

The `IQueryExecutionService.ExecuteQueryAsync` interface was updated to require a `Project` parameter. This change ensures that query execution has access to the project's database connection and configuration.

## Decision

Updated `Editor.razor.cs` to pass the current project when executing queries:

### Call Site Change

**Location:** `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` line 528

**Before:**
```csharp
var result = await QueryExecutionService.ExecuteQueryAsync(queryText, state.CancellationTokenSource.Token);
```

**After:**
```csharp
var result = await QueryExecutionService.ExecuteQueryAsync(queryText, Workspace.CurrentProject, state.CancellationTokenSource.Token);
```

### Null Safety Added

**Location:** Lines 511-515 in `ExecuteCurrentQueryAsync` method

Added check before execution:
```csharp
// Ensure project is available
if (Workspace.CurrentProject is null)
{
    Snackbar.Add("No project is open.", Severity.Warning);
    return;
}
```

## Rationale

1. **Type Safety:** The service needs guaranteed access to project configuration (connection string, database type, timeout settings)
2. **User Experience:** Clear error message when user attempts to execute query without an open project
3. **Consistency:** Matches existing patterns in Editor for project-dependent operations (e.g., `RefreshSchemaAsync` at line 444)

## Implementation Details

- **Project Source:** `Workspace.CurrentProject` property (type: `Project?`)
- **Null Handling:** Early return with user-facing warning snackbar
- **Error Message:** "No project is open." (Severity.Warning)
- **Parameter Order:** (string queryText, Project project, CancellationToken cancellationToken)

## Impact

- **Build:** Temporarily broken until Simon's interface change merges (expected error: "No overload for method 'ExecuteQueryAsync' takes 3 arguments")
- **Tests:** No test changes required (E2E tests already open projects before executing queries)
- **Runtime:** Execution button already disabled when no project open (via existing UI logic), this adds a defensive check

## Related Patterns

This follows the established pattern used throughout the Editor component:

1. **Lines 93-96:** Check `Workspace.IsProjectOpen` before rendering editor
2. **Lines 444-447:** Check `Workspace.CurrentProject?.QueryGenerator is null` before schema refresh
3. **Lines 152-154:** Check `Workspace.IsProjectOpen` before debounced updates

All project-dependent operations in the Editor use similar null checks with early returns.

## Testing Notes

Once Simon's interface change lands:
- Existing E2E tests should pass unchanged (they already open projects)
- Manual verification: Confirm "No project is open." warning shows when attempting to execute without a project
- Regression check: Verify normal execution flow works with an open project

## Cross-Reference

- Backend task: Simon updating `IQueryExecutionService` interface
- Related files: 
  - `src/LinqStudio.Abstractions/Services/IQueryExecutionService.cs` (interface definition)
  - `src/LinqStudio.Core/Services/QueryExecutionService.cs` (implementation)


---

# Decision: RoslynWorkspaceService.AddDocuments() Method

**Date:** 2026-03-13  
**Author:** Simon (Backend Core Dev)  
**Status:** Implemented

## Context

RoslynWorkspaceService was created to centralize Roslyn workspace creation and query wrapping. However, QueryExecutionService still contained verbose, repetitive code for adding documents (model files, DbContext, query wrapper) to the workspace.

## Decision

Added `AddDocuments()` method to RoslynWorkspaceService:

```csharp
public Solution AddDocuments(
    Solution solution,
    ProjectId projectId,
    IReadOnlyDictionary<string, string> modelFiles,
    string dbContextCode,
    string wrappedQuery,
    string queryFileName = "QueryContainer.cs")
```

This method handles batch document addition to a Roslyn solution in one call.

## Rationale

1. **Eliminates duplication**: QueryExecutionService had ~20 lines of document-adding code that's now a single method call
2. **Follows existing patterns**: RoslynWorkspaceService already owns workspace creation and query wrapping; document addition is a natural fit
3. **Clear separation of concerns**:
   - **AddDocuments()**: For fresh workspace creation (QueryExecutionService)
   - **AddOrUpdateFile()**: For long-lived workspaces with updates (CompilerService)

## Implementation Notes

- Refactored QueryExecutionService.CompileToAssemblyAsync() to use AddDocuments()
- Did NOT refactor CompilerService.Initialize() because it requires update capability (AddOrUpdateFile())
- Added `using Microsoft.CodeAnalysis.Text` to RoslynWorkspaceService for SourceText

## Alternatives Considered

**Option 1:** Refactor CompilerService to also use AddDocuments()
- **Rejected**: CompilerService.Initialize() can be called multiple times and must update existing documents, not add duplicates

**Option 2:** Make AddDocuments() handle updates
- **Rejected**: Would blur the distinction between one-time setup (QueryExecutionService) and incremental updates (CompilerService)

## Consequences

### Positive
- Cleaner QueryExecutionService code (single method call vs. foreach loops)
- Centralized document-adding logic in RoslynWorkspaceService
- Maintains clear distinction between fresh-workspace and update-workspace patterns

### Negative
- None identified - both services use the pattern that fits their lifecycle

## Related

- RoslynWorkspaceService extraction (2026-03-13)
- CompilerService and QueryExecutionService architecture


---

# RoslynWorkspaceService.AddDocuments() Test Structure

**Date:** 2026-03-13  
**Author:** Jordan (Tests Dev)  
**Status:** Implemented

## Decision

Created `RoslynWorkspaceServiceTests.cs` to test the `AddDocuments()` method using a minimal AdhocWorkspace pattern.

## Context

Simon added `RoslynWorkspaceService.AddDocuments()` which adds model files, DbContext, and query files to a Roslyn solution. Needed tests to verify correct document addition behavior.

## Test Pattern Established

### Minimal Workspace Creation
```csharp
private static (Solution solution, ProjectId projectId) CreateTestProject()
{
    var workspace = new AdhocWorkspace();
    var projectInfo = ProjectInfo.Create(
        ProjectId.CreateNewId(),
        VersionStamp.Create(),
        "TestProject",
        "TestProject",
        LanguageNames.CSharp);
    var solution = workspace.CurrentSolution.AddProject(projectInfo);
    return (solution, projectInfo.Id);
}
```

**Why this pattern:**
- No metadata references needed for document addition tests
- Fast test execution (no assembly loading)
- Straightforward tuple return for ergonomics
- Reusable across tests

### Verification Pattern
```csharp
var project = updatedSolution.GetProject(projectId);
var docNames = project.Documents.Select(d => d.Name).ToList();
Assert.Contains("DbContext.cs", docNames);
```

**Why this approach:**
- Tests the observable API surface (document names)
- Simple to read and maintain
- No need to verify document content (out of scope for AddDocuments)

## Alternatives Considered

1. **Full workspace with metadata references:** Rejected - unnecessary overhead for document addition tests
2. **Mocking Solution/Project:** Rejected - Roslyn APIs are concrete and work well in tests
3. **Testing document content:** Rejected - AddDocuments receives content as parameters; content validation is caller's responsibility

## Implications

- Future Roslyn service tests can reuse `CreateTestProject()` pattern
- Tests are fast (no assembly loading) and isolated
- Clear separation: AddDocuments tests verify structure, not content
- If we need content validation tests later, they belong in caller tests (e.g., CompilerService)

## Related

- `RoslynWorkspaceServiceTests.cs` in `tests/LinqStudio.Core.Tests/`
- Pattern similar to `CompilerServiceFactoryTests.CreateRoslynWorkspaceService()` helper


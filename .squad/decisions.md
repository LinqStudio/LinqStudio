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



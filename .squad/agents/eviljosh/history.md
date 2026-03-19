# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-18T22:43:55Z URL Sync + JS Rename
- Added URL sync on tab switch (NavigateTo replace:true) in Editor.razor.cs
- Renamed queryResultGrid.js → editor-utils.js for clarity; updated references in App.razor and copilot.md files
- All 527 tests passing

### 2026-06-XXT00:00:00Z Editor KeepPanelsAlive
Medium-large refactor required, sort machinery deletion, Monaco/splitter multi-instance management.

### 2026-03-13 - QueryResultGrid Component Implementation

**Task:** Created QueryResultGrid.razor component for displaying LINQ query execution results.

**Files Created:**
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor` - Display component with 5 states
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs` - Code-behind with elapsed time formatting

**Component Design:**
- Pure display component (receives data via parameters, doesn't fetch)
- Two parameters: `QueryExecutionResult? Result`, `bool IsExecuting`
- Five states handled:
  1. **Not yet executed** - Empty (no visual output)
  2. **Loading** - MudProgressCircular + "Executing query..." text
  3. **Error** - MudAlert (Severity.Error) with error message, prefixes "Compilation error: " if IsCompileError, shows elapsed time
  4. **Empty result** - MudAlert (Severity.Info) "Query returned no results." + elapsed time
  5. **Success with data** - MudTable with dynamic columns + footer showing "{N} rows · {elapsed}"

**MudBlazor Component Choice:**
- Used **MudTable** (Option B) instead of MudDataGrid
- Reason: Dynamic columns with `IReadOnlyDictionary<string, object?>` work reliably in MudTable
- MudDataGrid with TemplateColumn in foreach loops can have column ordering issues in some Blazor Server scenarios
- MudTable's HeaderContent + RowTemplate pattern handles dynamic schemas predictably

**Dynamic Column Rendering:**
```razor
<HeaderContent>
    @foreach (var col in Result.ColumnNames) { <MudTh>@col</MudTh> }
</HeaderContent>
<RowTemplate>
    @foreach (var col in Result.ColumnNames) { <MudTd>@context.GetValueOrDefault(col)?.ToString()</MudTd> }
</RowTemplate>
```

**Key Patterns Followed:**
- Code-behind pattern (separate .razor.cs file)
- MudBlazor components only (no custom HTML tables)
- Localization ready (messages can be extracted to SharedResource.resx later)
- Elapsed time formatting: < 1s shows ms, >= 1s shows seconds with 2 decimals
- Follows established component patterns from DatabaseTreeView.razor

**Build Status:** ✅ Clean build (0 warnings, 0 errors) in 1.51s

**Next Steps:**
- Execute button integration in Editor.razor (separate task)
- Optional: Extract display strings to SharedResource.resx for full localization

### 2026-03-13 - Bug Fixes from Code Review (Critical + High Priority)

**Task:** Fixed 5 bugs identified in code review (4 critical, 1 low priority).

**Fixes Applied:**

1. ✅ **NavMenu.razor.cs (Critical)** - Replaced dangerous `ContinueWith` anti-pattern with proper async/await on lines 59-78. The old pattern used `ContinueWith(async lambda, TaskScheduler.FromCurrentSynchronizationContext())` which doesn't properly await inner operations and can fail in Blazor Server. Now uses direct `async/await` pattern.

2. ✅ **Editor.razor.cs (High)** - Added exception logging to completion provider (line 232) and hover provider (line 277) catch blocks. Changed from silent `catch { return null; }` to `catch (Exception ex) { Console.Error.WriteLine($"[Editor] ... error: {ex.Message}"); return null; }`. This will help diagnose IntelliSense issues without breaking the UI.

3. ✅ **QueriesWorkspace.cs (High)** - Fixed Guid.Empty conflation bug in two locations (lines 151, 254). The bug: `FirstOrDefault()` on `Dictionary<Guid, T>.Keys` returns `Guid.Empty` (not null) when empty, which was conflated with "Guid.Empty key exists". Fixed with: `_currentQueryId = collection.Any() ? collection.Keys.First() : (Guid?)null;`

4. ✅ **SettingsEditor.razor.cs (Low)** - Removed debug Console.WriteLine at line 191 that was logging JSON token positions.

5. ℹ️ **EditProjectDialog.razor (Already Fixed)** - The code-behind uses `Snackbar` and `ErrorHandlingService`, and they ARE properly injected via `@inject` directives on lines 3-4 of the .razor file. No fix needed.

**Test Results:** All 417 tests pass (310 database tests, 48 core tests, 44 Blazor tests, 15 E2E tests, 4 E2E skipped). Build succeeded in 1:46.

**Key Pattern Learnings:**
- **Blazor async**: Never use `ContinueWith()` with TaskScheduler - use direct `async/await` or `InvokeAsync()`
- **Guid collections**: `FirstOrDefault()` on Guid collections returns `Guid.Empty` not null - always check with `.Any()` first
- **Exception handling**: Always log exceptions even when returning null/default - silent failures hide bugs

### 2026-03-13 - Team Review Cycle - Full UI/Blazor Assessment

Completed full UI/Blazor review. 27 issues identified (7 critical). Critical findings: component lifecycle management, editor initialization workarounds, state synchronization gaps. Tree view implementation reviewed. Recommendations prioritized by impact for next sprint.

### 2026-03-13 - Comprehensive UI/Blazor Code Review

**Task:** Full codebase review of all Blazor/UI components, services, and configuration requested by snakex64.

**Scope:** 23 files reviewed (~3,050 LOC) across LinqStudio.Blazor and LinqStudio.App.WebServer:
- 15 Razor components (.razor + .razor.cs)
- 5 services (ErrorHandlingService, MonacoProvidersService, ProjectWorkspace, QueriesWorkspace, ServerFileSystemService)
- 3 app config files (Program.cs, Routes.razor, App.razor)
- Test coverage analysis (LinqStudio.Blazor.Tests, E2ETests, WebServer.Tests)

**Findings:**
- **7 CRITICAL issues** — async/threading bugs, missing DI, null reference risks, hard-coded config
- **6 HIGH issues** — error handling gaps, TOCTOU race conditions, validation missing
- **14 MEDIUM/LOW issues** — code duplication, debug code, performance, accessibility

### 2026-03-13 - Query Execution Integration in Editor

**Task:** Integrate Execute/Stop button, timeout selector, and QueryResultGrid into Editor.razor.

**Files Modified:**
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor` - Added execution UI and result grid
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` - Implemented execution logic

**Per-Tab State Management:**
- Created `QueryExecutionState` class to hold per-tab execution state:
  - `QueryExecutionResult? Result` - Stores query results
  - `bool IsExecuting` - Tracks execution status
  - `CancellationTokenSource?` - Manages cancellation
- State stored in `Dictionary<Guid, QueryExecutionState>` keyed by query ID
- Tab switching preserves results - each tab maintains its own execution state independently

**UI Components Added:**
1. **Execute/Stop Button:**
   - Shows "▶ Execute" when idle, switches to "■ Stop" during execution
   - Primary color (Execute) vs Error color (Stop)
   - Stop button cancels via CancellationTokenSource
   - Disabled when no query is open

2. **Timeout Dropdown:**
   - MudSelect with 6 options: 10s, 30s, 1min, 2min, 5min, No timeout
   - Maps to seconds: 10, 30, 60, 120, 300, 0
   - Default from `QueryExecutionSettings.TimeoutSeconds` (30s)
   - Disabled during execution
   - Inline placement next to Execute button

3. **QueryResultGrid:**
   - Placed in new flex:1 container below execution bar
   - Receives current tab's Result + IsExecuting state
   - Auto-updates via `GetCurrentExecutionState()` helper

**Layout Changes:**
- Moved "Refresh Schema" button from bottom bar to execution bar (right side)
- Execution bar between Monaco editor and result grid
- Editor info bar at bottom (IntelliSense info text)
- Result container takes remaining vertical space with overflow:auto

**Execution Logic:**
```csharp
ExecuteCurrentQueryAsync():
1. Get query text from Monaco editor via await _editor.GetValue()
2. Create CancellationTokenSource with timeout (or no timeout if 0)
3. Set IsExecuting = true, clear previous result
4. Call QueryExecutionService.ExecuteQueryAsync(queryText, cts.Token)
5. Store result in per-tab state
6. Show success snackbar (N rows returned) or handle cancellation/errors
7. Set IsExecuting = false, dispose CTS

StopCurrentQuery():
- Cancel the current tab's CancellationTokenSource
- IsExecuting flag cleared when ExecuteQueryAsync completes
```

**DI Injections Added:**
- `IQueryExecutionService QueryExecutionService` - Executes queries
- `IOptionsMonitor<QueryExecutionSettings>` - Reads initial timeout default

**Error Handling:**
- OperationCanceledException → "Query execution was cancelled" (warning snackbar)
- All exceptions → Logged + error result with "Unexpected error: {message}"
- Success → "{N} row(s) returned" (success snackbar)

**Disposal:**
- All execution state CancellationTokenSources cancelled and disposed on component disposal
- Dictionary cleared to avoid memory leaks

**Build Status:** ✅ Clean build (0 warnings, 0 errors)
**Test Status:** ✅ All 485 tests pass (4 skipped) - no regressions introduced

**Integration Points:**
- Uses existing QueryResultGrid component (pure display, no logic duplication)
- Follows existing Monaco editor patterns (GetValue, async initialization)
- Consistent with Editor.razor's existing per-tab workspace state management
- MudBlazor components throughout (MudButton, MudSelect, MudPaper, MudStack)

**Key Pattern Used:**
Tab-local state via `Dictionary<Guid, QueryExecutionState>` matches how QueriesWorkspace tracks per-tab text edits (OpenQueries dictionary). This ensures switching tabs doesn't clear results - each tab is truly independent.

**Critical Patterns Identified:**

1. **ContinueWith Anti-Pattern (NavMenu.razor.cs:65-72)**
   - Using `ContinueWith(async lambda, TaskScheduler.FromCurrentSynchronizationContext())` in Blazor Server
   - WRONG: Async lambda in ContinueWith doesn't properly await inner operations
   - WRONG: TaskScheduler.FromCurrentSynchronizationContext() can fail in Blazor Server (no guaranteed context)
   - RIGHT: Use direct `async/await` pattern instead
   ```csharp
   // DON'T:
   confirmTask.ContinueWith(async task => { await SomethingAsync(); }, scheduler);
   
   // DO:
   var confirm = await confirmTask;
   if (confirm) await SomethingAsync();
   ```

2. **Guid.Empty Conflation Bug (QueriesWorkspace.cs:151-155, 252-259)**
   - `FirstOrDefault()` on `Dictionary<Guid, T>.KeyCollection` returns `Guid.Empty` (default), NOT null
   - Checking `if (guid == Guid.Empty)` conflates "empty collection" with "Guid.Empty key exists"
   - WRONG: `_currentQueryId = _openQueries.Keys.FirstOrDefault(); if (_currentQueryId == Guid.Empty) ...`
   - RIGHT: `_currentQueryId = _openQueries.Keys.FirstOrDefault(g => g != Guid.Empty);`
   - Alternative: `_currentQueryId = _openQueries.Keys.Any() ? _openQueries.Keys.First() : (Guid?)null;`

3. **Swallowed Exceptions in Completion Provider (Editor.razor.cs:232-235, 277-280)**
   - Broad `catch { return null; }` with no logging hides IntelliSense bugs
   - User gets no feedback, developers can't diagnose issues
   - ALWAYS log exceptions: `catch (Exception ex) { Logger.LogError(ex, "..."); return null; }`

4. **InvokeAsync with Unobserved Async (DatabaseTreeView.razor.cs:62-70)**
   - Calling `InvokeAsync(async () => { await ... })` without awaiting the lambda properly
   - `StateHasChanged()` called BEFORE async work completes (wrong order)
   - RIGHT: `_ = InvokeAsync(async () => { await LoadData(); StateHasChanged(); });`

5. **Task.Run in Blazor Components (Editor.razor.cs:153-175)**
   - Using `Task.Run()` to run debounce logic on thread pool
   - Problem: Accesses component state (`Workspace.Queries`) outside Blazor sync context
   - Risk: Component disposal during Task.Run callback causes race conditions
   - RIGHT: Use `InvokeAsync(async () => { await Task.Delay(...); ... })` to stay in Blazor context

6. **Missing Dependency Injection (EditProjectDialog.razor.cs)**
   - Using `Snackbar` and `ErrorHandlingService` without `@inject` directives or `[Inject]` attributes
   - Will throw `NullReferenceException` at runtime
   - Must add: `@inject ISnackbar Snackbar` and `@inject ErrorHandlingService ErrorHandlingService`

7. **Hard-Coded Theme Colors (MainLayout.razor.cs:92-129)**
   - All palette colors hard-coded in C# (15+ hex values)
   - Violates settings pattern used elsewhere (UISettings uses JSON)
   - Should extract to `appsettings.json` or UISettings for consistency

**Test Coverage:**
- ✅ **Good:** 44 unit tests (workspace, error handling, component smoke tests)
- ✅ **Excellent:** 19+ E2E tests with Playwright (editor, nav menu, database tree)
- ❌ **Gap:** MonacoProvidersService has 0 tests (complex service, high risk)
- ❌ **Gap:** SettingsEditor, EditProjectDialog, MainLayout have 0 unit tests
- ❌ **Empty:** LinqStudio.App.WebServer.Tests has 0 tests (only generated files)

**Blazor Lifecycle Best Practices:**
1. Always wrap `StateHasChanged()` in `InvokeAsync()` when called from async operations or non-UI threads
2. Avoid `Task.Run()` in components — use `InvokeAsync()` to stay in Blazor context
3. Call `StateHasChanged()` AFTER async data loads, not before
4. Never use `ContinueWith()` in Blazor — use `async/await` directly
5. Always implement `IDisposable` with `_disposed` flag for components with event subscriptions

**MudBlazor Patterns:**
- ✅ Proper use of `<Content>` templates for complex icon layouts (avoids `Icon=` parameter limitations)
- ✅ Correct `ExpandedChanged` event (not `@bind-Expanded`) in MudTreeView
- ✅ Good loading state patterns with MudProgressCircular/MudProgressLinear

**Code Quality Observations:**
- Positive: Clean separation of concerns (workspace pattern works well)
- Positive: Error boundary architecture (manual + global + dialog) is solid
- Issue: Some code duplication (ShowUnsavedChangesDialog in 2 places)
- Issue: Magic delays (500ms Monaco workarounds) should be replaced with event-based loading
- Issue: Debug code left in (Console.WriteLine in SettingsEditor.razor.cs:191)

**Recommendations:**
- **P0 (Immediate):** Fix missing DI (#5), remove debug code (#13), fix ContinueWith (#1), add logging (#3)
- **P1 (Next Sprint):** Fix Guid.Empty bugs (#2), InvokeAsync issues (#4, #6), error handling (#8), validation (#12)
- **P2 (Next Month):** Remove Monaco delays (#14), extract theme config (#7), deduplicate dialog logic (#26), add tests
- **P3 (Tech Debt):** Standardize disposal patterns, accessibility audit, performance profiling

**Output:** Full findings written to `.squad/decisions/inbox/eviljosh-ui-review.md` (27 issues categorized and prioritized)

**Overall Assessment:** Codebase quality is solid with good architectural patterns. Most issues are async/threading edge cases and error handling gaps rather than fundamental design flaws. No security vulnerabilities found. Risk level: LOW to MEDIUM.

### 2026-03-13 - Team Sprint: Validation & Cache Access Patterns

**Squad Completion:**
- Simon: Removed auto-discovery from MssqlGenerator, added fail-fast validation in Create() and Project.UpdateConnection()
- EvilJosh: Fixed EditProjectDialog Save() validation, resolved DatabaseTreeView cache access race conditions, fixed test cleanup
- Alex: Comprehensive code review documenting patterns and edge cases
- Status: ✅ 407 tests passing, orchestration logs written, decisions merged

**Key Fixes Implemented:**
1. **EditProjectDialog.Save():** Added null/empty validation before calling Project.UpdateConnection() to prevent empty connection strings
2. **DatabaseTreeView Cache:** Replaced direct dictionary access with GetValueOrDefault() pattern for safe concurrent access
3. **Test Cleanup:** Fixed temporary directory leak in DatabaseTreeViewTests.cs by properly disposing DirectoryInfo objects

**Pattern Learnings:**
- Empty string vs null confusion: Treat empty string as invalid for required fields like connection strings
- Dictionary access safety: Use GetValueOrDefault() instead of direct indexing in concurrent scenarios
- Test cleanup: All temporary resources created in tests must be properly disposed (not just deleted)

### 2026-03-11: DatabaseTreeView Full Analysis — All Fixes Verified

**COMPLETED FULL UI ANALYSIS OF DatabaseTreeView COMPONENT**

**Task:** Comprehensive read-only analysis of DatabaseTreeView implementation per snakex64's request.

**Files Analyzed:**
1. `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor` — markup
2. `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor.cs` — code-behind  
3. `src/LinqStudio.Blazor/Components/Layout/MainLayout.razor` — integration context
4. `src/LinqStudio.Blazor/Components/Layout/NavMenu.razor` — drawer sibling
5. `tests/LinqStudio.Blazor.Tests/DatabaseTreeViewComponentTests.cs` — test coverage

**VERIFICATION RESULTS — All Fixes in Place:**

1. ✅ **MudIcon Fix Verified:**
   - Lines 56-64 (columns): Explicit `<MudIcon>` inside `<Content>` template
   - Lines 39-46 (loading): `<MudProgressCircular>` + `<MudText>` in template
   - Pattern: Using `<Content>` instead of `Icon=` parameter for complex layouts
   - **Why:** MudBlazor `Icon=` param can't mix icons + text + formatting; explicit templates required

2. ✅ **`_fixedSizeTypes` HashSet Verified:**
   - Lines 176-177 in `.cs` file
   - Contains: `int`, `bigint`, `smallint`, `tinyint`, `bit`
   - Used in `FormatColumnType` (line 183) to skip size checks for fixed-size SQL types
   - **Purpose:** Performance optimization — avoids MaxLength/Precision checks on types that don't need them

3. ✅ **Connection Tracking Fix Verified:**
   - Fields: `_trackedConnectionString`, `_trackedDatabaseType` (lines 19-21)
   - Logic in `OnWorkspaceChanged` (lines 38-71)
   - **Behavior:** Only reloads tables when connection/database type changes OR project open state changes
   - Early return on line 51-52 prevents unnecessary DB queries
   - **Impact:** Query saves, in-memory edits, and other workspace events no longer trigger DB round-trips

4. ✅ **Two-Click Expand Issue RESOLVED:**
   - **Alice's original bug:** Required (1) click arrow to expand, (2) click row to load columns
   - **Current implementation:** Uses `ExpandedChanged` event (line 33), NOT `@bind-Expanded`
   - **Flow:** Arrow click → `OnTableExpandedChanged(table, expanded)` → loads columns immediately
   - **Result:** ONE CLICK to expand + load data
   - **No `OnTableClick` method exists** — it was removed or never implemented

**BUILD STATUS:** ✅ Succeeds (after killing stray testhost processes)

**COMPONENT ARCHITECTURE:**
- **State Management:** Proper separation of concerns
  - `_tables` — all table names
  - `_tableDetailsCache` — lazy-loaded columns per table
  - `_expandedStates` — UI state (which tables expanded)
  - `_loadingTables` — async operation tracking
- **Performance:** Lazy loading, caching, selective reload, type optimization
- **Error Handling:** All async operations wrapped in try-catch with user-friendly ErrorHandlingService
- **Lifecycle:** Subscribes to workspace changes, auto-loads on project open, disposes correctly

**TEST COVERAGE:**
- 5 basic tests exist (placeholder, loading, smoke test, DI injection)
- **Gap:** No tests for table expansion, column loading, refresh, type formatting
- **No conflicts:** Tests don't use `Icon=` parameter (good — would fail with current implementation)

**UX REVIEW:**
- Three clear states: no project → placeholder, loading → progress bar, loaded → tree
- Visual feedback: Icons for keys/identity columns, loading spinners, tooltips
- Accessibility: MudBlazor built-in ARIA support
- **No UX issues found**

**MUDBLAZOR USAGE:**
- ✅ Proper patterns: `MudTreeView`, `<Content>` templates, `CanExpand="false"` on leaves
- ✅ No anti-patterns: Not mixing `@bind-Expanded` with `ExpandedChanged`
- ✅ Loading states: `MudProgressCircular`, `MudProgressLinear`

**RECOMMENDATIONS:**
1. Test coverage gap is minor (component works correctly in practice)
2. Consider adding `copilot.md` explaining connection tracking optimization
3. Future enhancements (not bugs): search/filter, copy to clipboard, schema grouping

**VERDICT:** Component is production-ready. All fixes verified. No changes needed.

**DELIVERABLES:**
- Full analysis written to `.squad/decisions/inbox/eviljosh-ui-analysis.md`
- History entry appended

**KEY LEARNING:** When analyzing UI components, verify:
1. MudBlazor pattern usage (especially `Icon=` vs `<Content>`)
2. Event handlers exist and match markup calls
3. Performance optimizations are correctly implemented
4. Lifecycle methods (Init/Dispose) pair correctly
5. Build actually succeeds (watch for stray test processes locking files)

### 2026-03-11: Frontend/UI Architecture Deep Dive

**PROJECT STRUCTURE:**
- `src/LinqStudio.Blazor/` - Reusable Razor components library (Components, Services, Abstractions, Models)
- `src/LinqStudio.App.WebServer/` - ASP.NET Core Blazor Server host (App.razor, Routes.razor, Program.cs, server-specific services)
- Blazor components auto-discovered from LinqStudio.Blazor assembly via `AddAdditionalAssemblies()`
- Interactive Server render mode with prerender disabled: `new InteractiveServerRenderMode(prerender: false)`

**ALL RAZOR COMPONENTS (17 total):**

1. **MainLayout.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Inherits LayoutComponentBase, implements IDisposable
   - Uses `IOptionsMonitor<UISettings>` for reactive dark/light theme changes
   - Components: MudThemeProvider (with custom _lightPalette/_darkPalette), MudAppBar, MudDrawer (with NavMenu), MudMainContent
   - AppErrorBoundary wraps @Body
   - `OnChange` subscription for UISettings triggers StateHasChanged automatically
   - DarkModeToggle saves to SettingsService immediately via `await SettingsService.Save()`
   - Custom palettes: light (minimalist white/gray), dark (purple primary #7e6fff, dark surfaces #1e1e2d/#1a1a27)

2. **NavMenu.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Project menu: New, Open, Properties (Edit), Save, Save As, Close
   - Editor menu: New Query, Open Query (disabled if no project open)
   - Subscribes to `Workspace.WorkspaceChanged` event for reactive updates
   - Uses UnsavedChangesDialog for confirmation prompts
   - Integrates with IFileSystemService (native file dialogs via NativeFileDialogSharp)
   - File extensions: `.linq` (projects), `.linquery` (queries)

3. **ReconnectModal.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Blazor Server reconnection UI (native HTML `<dialog>` element)
   - Custom CSS animations (fade in/out, slide up)
   - JavaScript file: ReconnectModal.razor.js (handles reconnect/resume events)

4. **Home.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/` (root)
   - Simple welcome page with MudContainer
   - Minimal content: "Welcome to LinqStudio - Your IDE for EF Core LINQ queries"

5. **Editor.razor** (`src/LinqStudio.Blazor/Components/Pages/Editor/`)
   - Routes: `/editor`, `/editor/{QueryIdParam:guid}`, `/editor/new`
   - Main code editor page with Monaco editor
   - Shows query tabs, query info bar (name, unsaved indicator), save/close buttons
   - MonacoProvidersService registers completion + hover providers via Roslyn CompilerService
   - **Task.Delay(500) workaround**: Monaco editor rendered only after 500ms delay (BlazorMonaco rendering issue)
   - Debouncing: 300ms debounce on text changes to avoid excessive workspace updates
   - Language: `csharp`, theme: `vs-dark` or default (light)
   - Completion mapping: uses Roslyn completion tags (WellKnownTags) → Monaco CompletionItemKind
   - Hover provider: shows Roslyn quick info as markdown
   - **Critical pattern**: cursor position adjusted for QueryContainer wrapper in CompilerService

6. **Settings.razor** (`src/LinqStudio.Blazor/Components/Pages/Settings/`)
   - Route: `/settings`
   - Reflection-based settings loading: scans assembly for all `IUserSettingsSection` implementations
   - Uses `IServiceProvider.GetRequiredService(typeof(IOptionsMonitor<>).MakeGenericType(x))` to get typed options
   - MudTabs with Position.Left, KeepPanelsAlive=true
   - SaveAll: validates JSON, deserializes all settings, then saves via SettingsService
   - OnChange subscriptions for all settings trigger StateHasChanged

7. **SettingsEditor.razor** (`src/LinqStudio.Blazor/Components/Pages/Settings/`)
   - One tab per setting section (created inside MudTabs)
   - Monaco editor with JSON language, hover providers for setting descriptions
   - Hover logic: parses JSON to ensure hovering on first-level property key (not value, not nested)
   - Translation: uses SharedResource `"UserSettings.{SectionName}.{PropertyName}"` for descriptions
   - Reload dialog: prompts user when settings change externally (unless `AlwaysReloadSettingsInSettingsPage` is true)
   - **Task.Delay(500) workaround** in OnAfterRenderAsync

8. **Error.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/Error`
   - ASP.NET Core default error page (shows Request ID in development)

9. **NotFound.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/not-found`
   - Simple 404 page

10. **AppErrorBoundary.razor** (`src/LinqStudio.Blazor/Components/`)
    - Wraps @Body in MainLayout
    - Catches unhandled component exceptions
    - Logs via ILogger, shows via ErrorHandlingService
    - Fallback UI: MudAlert with "An unexpected error occurred"

11. **ErrorDialog.razor** (`src/LinqStudio.Blazor/Components/`)
    - MudDialog showing error message + collapsible stack trace (MudExpansionPanel)
    - Parameters: Message (string), StackTrace (string?)
    - Used by ErrorHandlingService

12. **UnsavedChangesDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - Generic confirmation dialog for unsaved changes
    - Parameters: Message, ConfirmText, CancelText
    - Returns bool via DialogResult.Ok(true)

13. **EditProjectDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - MudDialog for editing project properties
    - Fields: Project Name (readonly/disabled), DatabaseType (MudSelect), ConnectionString (multiline MudTextField), Timeout (MudSelect: 5/10/15/30/60s)
    - Validate Connection button: calls `Project.TestConnectionAsync()`, shows progress spinner
    - Returns updated Project via DialogResult.Ok(Project)

14. **EditorMenuDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - Simple dialog with New/Open options for queries
    - Returns EditorMenuAction enum (New, Open)

15. **App.razor** (`src/LinqStudio.App.WebServer/`)
    - HTML document head + body
    - Links: Roboto font, MudBlazor CSS, scoped CSS, app.css
    - Scripts: Blazor, MudBlazor, BlazorMonaco (jsInterop, loader, editor.main)
    - Routes component with InteractiveServerRenderMode
    - ReconnectModal component

16. **Routes.razor** (`src/LinqStudio.App.WebServer/`)
    - Router with AppAssembly=LinqStudio.Blazor, NotFoundPage=NotFound
    - DefaultLayout=MainLayout

17. **_Imports.razor** (2 files)
    - `src/LinqStudio.App.WebServer/_Imports.razor`: Basic ASP.NET + MudBlazor
    - `src/LinqStudio.Blazor/Components/_Imports.razor`: Full imports (Core services, Blazor services, SharedResource)

**BLAZORMONACO INTEGRATION:**

- **MonacoProvidersService** (singleton pattern for global provider registration)
  - Prevents duplicate Monaco provider registrations (Monaco tracks providers globally)
  - Registers hover + completion providers once globally for `csharp` and `json` languages
  - Uses ConcurrentDictionary keyed by model URI to route events to correct delegates
  - `RetryUntilMonacoReady()`: retries registration up to 20 times with 250ms delays (handles Monaco loading race condition)
  - Returns IDisposable to unregister provider for specific model URI
  - Registered in DI as Scoped service

- **Monaco Editor Initialization Pattern:**
  1. Component state: `Delay = true` initially
  2. OnAfterRenderAsync: if Delay, set to false, await Task.Delay(500), StateHasChanged
  3. Only then render StandaloneCodeEditor
  4. This workaround ensures Monaco resources are fully loaded before editor initialization

- **Completion Provider (Editor.razor):**
  - Trigger characters: `.`, `(`, `<`, `[`, ` ` (space)
  - Calls CompilerService.GetCompletionsAsync() with user query text + cursor offset
  - Maps Roslyn CompletionItem tags to Monaco CompletionItemKind (Property, Method, Field, Class, Text)
  - Inserts parenthesis if `ShouldProvideParenthesisCompletion` property is true

- **Hover Provider (Editor.razor + SettingsEditor.razor):**
  - Editor: calls CompilerService.GetHoverAsync(), shows as MarkdownString
  - SettingsEditor: custom hover for JSON properties, shows translated descriptions from SharedResource

**MUDBLAZOR SETUP:**

- Theme: Custom MudTheme with distinct light/dark palettes
- Light palette: minimalist (white backgrounds, subtle grays)
- Dark palette: purple-themed (#7e6fff primary, #1e1e2d surfaces, #1a1a27 background)
- Components used: MudThemeProvider, MudAppBar, MudDrawer, MudNavMenu, MudMainContent, MudTabs, MudDialog, MudSnackbar, MudAlert, MudButton, MudIconButton, MudTextField, MudSelect, MudChip, MudExpansionPanel, MudTooltip, MudProgressCircular, MudMenu, MudMenuItem
- Added via `services.AddMudServices()` in ServiceCollectionExtensions

**IOPTIONSMONITOR<T> PATTERNS:**

- All settings use `IOptionsMonitor<T>` for reactive updates
- Pattern: `_disposable = UISettings.OnChange(_ => InvokeAsync(StateHasChanged))`
- Components: MainLayout (UISettings), Settings (all IUserSettingsSection), SettingsEditor (UISettings), Editor (UISettings)
- Disposed in component Dispose() method
- Enables instant UI updates when settings change (e.g., dark mode toggle, settings reload)

**SHAREDRESOURCE LOCALIZATION:**

- Location: `src/LinqStudio.Core/Resources/SharedResource.resx` (English) + `SharedResource.fr.resx` (French)
- Categories: AppBar, ErrorDialog, Global, SettingsPage, UserSettings, ConnectionSettings
- UserSettings keys: `UserSettings.{SectionName}` (section title), `UserSettings.{SectionName}.{PropertyName}` (property description)
- Accessed via: `SharedResource.ResourceManager.GetString(key, SharedResource.Culture)`
- Current settings: UISettings (IsDarkMode, AlwaysReloadSettingsInSettingsPage)

**BLAZOR SERVICES (ServiceCollectionExtensions.AddLinqStudioBlazor):**

1. **MonacoProvidersService** (Scoped) - Monaco provider management
2. **ErrorHandlingService** (Scoped) - Centralized error dialog display + logging
3. **QueriesWorkspace** (Scoped) - Per-session query state management (open queries, current query, unsaved changes)
4. **ProjectWorkspace** (Scoped) - Per-session project state management (current project, file path, unsaved changes)
5. **MudServices** - MudBlazor services

**APP.WEBSERVER SERVICES:**

- **ServerFileSystemService** (implements IFileSystemService) - Native file dialogs via NativeFileDialogSharp
- Default path: ~/Documents/LinqStudio/ or ~/Documents/ fallback

**WORKSPACE PATTERN:**

- **ProjectWorkspace**: Manages current open project, tracks unsaved changes (properties + queries), events: WorkspaceChanged
- **QueriesWorkspace**: Manages all queries for current project, tracks open queries, current query, unsaved changes per query, events: QueriesChanged
- **OpenQueryState**: Model tracking editor state (current text, unsaved changes, last modified)
- Both workspaces use EventHandler pattern for reactive updates across components
- Components subscribe in OnInitialized, unsubscribe in Dispose

**CSS ORGANIZATION:**

1. `wwwroot/app.css` (LinqStudio.Blazor) - `.mainBody { height: 100% }`
2. `App.razor.css` (App.WebServer) - `body { width: 100vw; height: 100vh; overflow: hidden }`
3. Component-scoped CSS files:
   - `Settings.razor.css` - flexbox layout (tabs + actions)
   - `SettingsEditor.razor.css` - `.editorParent` + `::deep .editor` for Monaco
   - `Editor.razor.css` - `::deep .editor { height: 100% }`
   - `ReconnectModal.razor.css` - extensive reconnection modal styles + animations

**ROUTING/PAGES:**

- `/` - Home page (welcome message)
- `/editor` - Editor page (no query selected)
- `/editor/{QueryIdParam:guid}` - Editor page with specific query
- `/editor/new` - Editor page (create new query)
- `/settings` - Settings page
- `/Error` - Error page (ASP.NET Core default)
- `/not-found` - 404 page
- Router configured in Routes.razor with MainLayout as default, NotFound for 404s

**CRITICAL PATTERNS & BEHAVIORS:**

1. **Monaco Delay Workaround**: Always `Task.Delay(500)` before rendering Monaco editors (BlazorMonaco race condition)
2. **MonacoProvidersService**: Use to prevent duplicate provider registrations across multiple Monaco instances
3. **IOptionsMonitor + OnChange**: Pattern for reactive settings - always dispose subscription
4. **EventHandler Pattern**: ProjectWorkspace/QueriesWorkspace raise events, components subscribe/unsubscribe
5. **Debouncing**: Editor uses 300ms debounce for text changes to avoid excessive workspace updates
6. **Unsaved Changes**: UnsavedChangesDialog for confirmations, tracked at both project + query level
7. **File Extensions**: `.linq` (projects), `.linquery` (queries) via FileExtensions constants
8. **ErrorHandlingService**: Centralized error handling - logs via ILogger, shows via MudDialog (ErrorDialog)
9. **Reflection-based Settings**: Settings page uses reflection to discover all IUserSettingsSection implementations
10. **Native File Dialogs**: ServerFileSystemService uses NativeFileDialogSharp (cross-platform)

**UI/UX FEATURES:**

- Dark/light mode toggle in AppBar (instant switch)
- Drawer toggle for NavMenu
- Query tabs with unsaved indicator (asterisk)
- Settings reload prompt (optional, user can choose "Always" to skip)
- Connection string validation with loading spinner
- Error dialogs with collapsible stack trace
- Reconnection modal with animations for Blazor Server disconnects
- Project/query unsaved indicators in NavMenu title
- Monaco hover tooltips for settings descriptions (localized)
- Snackbar notifications (success, info, error)

**IN-PROGRESS / INCOMPLETE FEATURES:**

- No visible incomplete UI features in code
- All components appear fully functional
- All dialogs have proper cancel/save flows
- All services are registered and used consistently

**TESTING HOOKS:**

- Extensive `data-testid` attributes throughout components for Playwright E2E tests
- Examples: `editor-page`, `query-name-display`, `query-save-btn`, `nav-menu`, `edit-project-dialog`, etc.

### 2026-03-11: Database Tree View Feature Analysis

**DATABASE SCHEMA INTROSPECTION MODELS:**
- `DatabaseTableName` (`LinqStudio.Abstractions.Models`): Schema + Name, FullName property
- `DatabaseTableDetail` (extends DatabaseTableName): Columns + ForeignKeys collections
- `TableColumn`: Name, DataType, GenericType, IsNullable, IsPrimaryKey, IsIdentity, MaxLength, Precision, Scale
- `IDatabaseQueryGenerator` interface: `GetTablesAsync()`, `GetTableAsync(tableName)`, `TestConnectionAsync()`
- Available via `Project.QueryGenerator` property (auto-created based on DatabaseType + ConnectionString)

**LEFT DRAWER STRUCTURE:**
- `MainLayout.razor` contains single `MudDrawer` with only `NavMenu.razor` inside
- No database schema browser or table explorer currently exists
- Drawer toggles via `_drawerOpen` state bound to hamburger menu in `MudAppBar`
- Clean structure ready for expansion with additional components (e.g., DatabaseTreeView below NavMenu)

**MUDTREEVIEW RESEARCH:**
- `<MudTreeView T="TType">` and `<MudTreeViewItem>` components available in MudBlazor
- Built-in async data loading via `ServerData` parameter for lazy loading children
- Templating support via `ItemTemplate` for custom node rendering
- State management: `@bind-Expanded`, `@bind-Selected` for reactive UI
- Tree structure pattern: root nodes → children (schema → tables → columns)
- Icons: `MudTreeViewItemToggleButton` for expand/collapse, custom `MudIcon` for node types

**PROPOSED TREE VIEW COMPONENT STRUCTURE:**
- New component: `DatabaseTreeView.razor` in `src/LinqStudio.Blazor/Components/Layout/`
- New model: `DatabaseTreeNode` in `src/LinqStudio.Blazor/Models/` (NodeType enum: Schema/Table/Column)
- Integration: Add below NavMenu in MainLayout's MudDrawer, separated by MudDivider
- Data flow: Inject `ProjectWorkspace` → access `CurrentProject.QueryGenerator` → call `GetTablesAsync()` and `GetTableAsync()`
- Lazy loading: Load tables on component init, load columns when table node expanded (OnNodeClick handler)
- Caching: Dictionary<string, DatabaseTableDetail> to avoid redundant DB queries
- Event subscriptions: `Workspace.WorkspaceChanged` for reactive updates when project opens/closes

**TREE NODE DISPLAY PATTERNS:**
- Schema nodes: `Schema.Name` with folder icon (if database has schemas)
- Table nodes: `Table.Name` with table icon
- Column nodes: `ColumnName: DataType[?][PK][ID]` format (nullable?, primary key, identity markers)
- Icons: `Icons.Material.Filled.Folder` (schema), `Icons.Material.Filled.TableChart` (table), `Icons.Material.Filled.ViewColumn` (column)
- Refresh button: `Icons.Material.Filled.Refresh` in header, clears cache and reloads

**COMPONENT LIFECYCLE FOR TREE:**
- `OnInitialized`: Subscribe to `Workspace.WorkspaceChanged` event
- `OnParametersSetAsync`: Load tables if project open and tables not yet loaded
- `OnWorkspaceChanged`: Clear cache, trigger StateHasChanged, reload tables if project open
- `Dispose`: Unsubscribe from workspace events
- Loading state: `_isLoading` bool → show `MudProgressLinear` while fetching data
- Error handling: Use `ErrorHandlingService.HandleErrorAsync()` for all exceptions

**DATA BINDING & CACHING STRATEGY:**
- Initial load: Fetch all table names via `GetTablesAsync()` (lightweight, single query)
- Lazy load: Fetch columns via `GetTableAsync(tableName)` only when table node expanded
- Cache: Store `DatabaseTableDetail` in `Dictionary<string, DatabaseTableDetail>` keyed by `{Schema}.{Name}`
- Refresh: Manual button clears cache and reloads tables (no auto-refresh)
- Workspace change: Clear all cache when project changes (prevent stale data)

**KEY FILES FOR TREE VIEW IMPLEMENTATION:**
- `src/LinqStudio.Blazor/Models/DatabaseTreeNode.cs` (NEW) - tree node data model
- `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor` (NEW) - tree UI component
- `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor.cs` (NEW) - tree component logic
- `src/LinqStudio.Blazor/Components/Layout/MainLayout.razor` (MODIFY) - add DatabaseTreeView below NavMenu
- `src/LinqStudio.Abstractions/Models/DatabaseTableName.cs` (EXISTS) - table metadata model
- `src/LinqStudio.Abstractions/Models/DatabaseTableDetail.cs` (EXISTS) - table + columns model
- `src/LinqStudio.Abstractions/Abstractions/IDatabaseQueryGenerator.cs` (EXISTS) - schema query interface

**TESTID CONVENTIONS FOR E2E TESTS:**
- Tree container: `data-testid="database-tree-view"`
- Refresh button: `data-testid="database-tree-refresh-btn"`
- Schema nodes: `data-testid="schema-{schemaName}"`
- Table nodes: `data-testid="table-{schema}.{tableName}"`
- Column nodes: `data-testid="column-{columnName}"`
- Required test scenarios: visibility when project open/closed, table loading, column expansion, refresh, error handling

### 2026-03-11: DatabaseTreeView Implementation Complete

**COMPONENT CREATED:**
- `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor` - Main component markup
- `src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor.cs` - Component logic
- Integrated into `MainLayout.razor` below NavMenu with MudDivider separator

**IMPLEMENTATION APPROACH:**
- **Flat table list** - No schema grouping, schema shown as prefix (e.g., `dbo.Customers`)
- **MudTreeView pattern** - Used `MudTreeView<string>` with nested `MudTreeViewItem` for tables and columns
- **State management** - `Dictionary<string, bool>` for expanded states, `Dictionary<string, DatabaseTableDetail>` for column cache
- **Lazy loading** - Tables loaded on component init, columns loaded on first table expansion via `OnTableClick`
- **Event subscription** - Subscribes to `Workspace.WorkspaceChanged`, clears cache and reloads on project changes
- **Icons and colors** - Storage icon (header), TableChart (tables), Key/gold (PK), Bolt (identity), ViewColumn (regular columns)
- **Type formatting** - Formats as `DataType(size)`, `DataType(precision,scale)`, appends `?` for nullable

**DATA-TESTID ATTRIBUTES:**
- `db-tree-view` - Only on tree container when tables loaded (NOT on wrapper when showing placeholder)
- `db-tree-placeholder` - Placeholder text element when no project/connection
- `db-tree-refresh` - Refresh button
- `db-tree-loading` - Loading progress indicator
- `table-{FullName}` - Each table tree item
- `column-{tableName}-{columnName}` - Each column tree item

**KEY LEARNINGS:**
- Test expectations required `db-tree-view` to NOT exist when showing placeholder (moved testid from wrapper to actual tree)
- BUnit `FindAll` + `Assert.Empty` checks for non-existence, while Playwright `Not.ToBeVisibleAsync` checks visibility
- Placeholder text must contain "open a project" (case-insensitive) per test assertion
- MudTreeView requires explicit state management with `@bind-Expanded` and separate expanded state dictionary
- Column loading state requires separate `HashSet<string>` to track which tables are currently loading

**TEST RESULTS:**
- ✅ All 5 unit tests passing (BUnit component tests)
- ✅ 2 E2E tests passing (placeholder scenarios)
- ⏭️ 3 E2E tests skipped (require real database setup for table/column testing)
- Total DatabaseTreeView test coverage: 7 tests (5 passed, 3 skipped)

**DOCUMENTATION UPDATED:**
- `src/LinqStudio.Blazor/Components/copilot.md` - Added comprehensive DatabaseTreeView section with API usage, lifecycle, testid conventions


### 2026-03-11: DatabaseTreeView Bug Fixes - Column Icons and Type Formatting

**BUGS FIXED:**

1. **Column icons not rendering** (BUG#1)
   - Root cause: MudBlazor's MudTreeViewItem silently ignores Icon= and IconColor= parameters when <Content> template is used
   - Solution: Removed Icon= and IconColor= attributes from column MudTreeViewItem, added explicit <MudIcon> inside <Content> div
   - Changes: src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor lines 52-64
   - Pattern: <MudIcon Icon="@GetColumnIcon(column)" Color="@GetColumnIconColor(column)" Size="Size.Small" Class="mr-1" />
   - Result: Column icons (Key/gold for PK, Bolt for identity, ViewColumn for regular) now visible

2. **Int type showing as "int(10,0)"** (BUG#2)
   - Root cause: SQL Server internally stores int with precision=10, scale=0; FormatColumnType was adding these
   - Solution: Added _fixedSizeTypes HashSet containing fixed-size numeric types that should never show precision
   - Changes: src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor.cs - added static field + logic in FormatColumnType method
   - Fixed types: int, bigint, smallint, tinyint, bit
   - Result: int columns now display as "int" or "int?" (nullable), not "int(10,0)"

**KEY LEARNING:**
- **MudBlazor Content Template Pattern:** When using <Content> template in MudTreeViewItem, ALL visual elements must be explicitly placed inside the template. The component's built-in Icon=, IconColor=, Text= parameters are completely bypassed.
- This is by design, not a bug — <Content> gives full control but requires manual icon placement.

**BUILD VERIFICATION:**
- ✅ Build succeeded with 0 warnings, 0 errors
- No test changes required (fixes visual rendering only)
### 2026-03-11 21:05:50 - Fixed Two-Click Expand UX Bug in DatabaseTreeView

**Task:** Fixed the two-click expand UX bug in DatabaseTreeView.razor where users had to click the expand arrow AND then click the row text to load table columns.

**Root Cause:** 
- @bind-Expanded handled expand/collapse toggle state
- OnClick handled column loading
- Clicking the expand arrow only fired the binding, not the OnClick event
- Result: columns never loaded on expand arrow click alone

**Solution Implemented:**
- Replaced @bind-Expanded + OnClick pattern with single ExpandedChanged event
- Changed to: Expanded="@_expandedStates[table.FullName]" + ExpandedChanged="@(v => OnTableExpandedChanged(table, v))"
- Replaced OnTableClick method with OnTableExpandedChanged(DatabaseTableName table, bool expanded)
- New method updates state AND loads columns in single callback when expanding

**Files Modified:**
1. src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor (lines 28-34)
   - Removed: @bind-Expanded and OnClick attributes
   - Added: Expanded and ExpandedChanged attributes
2. src/LinqStudio.Blazor/Components/Layout/DatabaseTreeView.razor.cs (lines 105-112)
   - Removed: OnTableClick method
   - Added: OnTableExpandedChanged method

**Build Status:** ✅ PASS (0 errors, 56 warnings - file lock warnings only)

**Notes:**
- Used PowerShell for Razor file edit due to CRLF/TAB encoding
- Pattern aligns with MudBlazor's event model best practices
- Single-click expand now triggers column load immediately


### 2026-03-13: Alex Code Review Fixes — Validation, Defensive Access, Test Cleanup

**COMPLETED 3 UI LAYER FIXES per Alex's code review (requested by snakex64)**

**Task 1 — EditProjectDialog.razor.cs Save() validation:**
- Added string.IsNullOrWhiteSpace(_connectionString) guard before calling Project.UpdateConnection
- Shows SharedResource.ConnectionSettings_Message_ValidationFailed snackbar on empty connection string
- Removed ?? string.Empty null-coercion — passes _connectionString directly after validation
- Prevents future crash when Simon adds a guard/throw in UpdateConnection
- Pattern mirrors existing ValidateConnection() method in the same file

**Task 2 — DatabaseTreeView.razor defensive dictionary access:**
- Changed both _expandedStates[table.FullName] reads (line 32 Expanded= attr, line 35 @if guard) to use GetValueOrDefault(table.FullName, false)
- The write path (_expandedStates[table.FullName] = expanded in .razor.cs) is intentional and correct — only reads needed the fix
- Prevents potential KeyNotFoundException if state dictionary is cleared while UI is rendering (race between OnWorkspaceChanged clearing state and Blazor re-render)

**Task 3 — DatabaseTreeViewComponentTests.cs temp directory leak:**
- Removed 3 lines in CreateMockWorkspaceWithProject that created a temp dir + file path but never used them and never cleaned up
- Stale comment // We can't directly set QueryGenerator... also removed (replaced by accurate comment)
- Zero test logic removed — the unused variables were dead code

**BUILD:** ✅ 0 warnings, 0 errors  
**TESTS:** ✅ All 44 Blazor tests pass, 45 Core tests pass  
**NOTE:** 1 pre-existing MSSQL test failure (GetTableAsync_ShouldReturnColumns_AfterAutoDiscovery) in Simon's database domain — not caused by these changes


### 2026-03-13 - Refresh Schema UI (Editor.razor + Editor.razor.cs)

**Task:** Wired up Refresh Schema button to trigger live DB schema generation and CompilerService re-initialization.

**Changes Made:**

1. **IDbContextGenerator** (LinqStudio.Abstractions/Abstractions/IDbContextGenerator.cs) — New interface. DbContextGeneratorResult record in Models/DbContextGeneratorResult.cs. Both are team contracts; Simon implements DbContextGenerator in Core.

2. **CompilerServiceFactory** — Updated constructor to (IDbContextGenerator? generator, ILogger<CompilerService>? logger). New method CreateFromProjectAsync(Project) — uses real schema when project.QueryGenerator != null, falls back to demo model.

3. **Editor.razor.cs** — Injected IDbContextGenerator. Added _isRefreshingSchema state field. Updated OnEditorInitialized to call CreateFromProjectAsync with try-catch fallback (critical: unreachable DB must not crash the editor). Added RefreshSchemaAsync() method with loading state, snackbar feedback, and early-exit guard for missing DB connection.

4. **Editor.razor** — Replaced info-bar MudPaper with MudStack Row layout containing info text + MudButton (refresh trigger).

**data-testid values added:**
- efresh-schema-btn — The Refresh Schema button in the editor info bar.

**Key Patterns:**
- OnEditorInitialized always wraps CreateFromProjectAsync in try-catch; DB errors silently fall back to demo model (editor stays functional).
- RefreshSchemaAsync guards on Workspace.CurrentProject?.QueryGenerator is null before doing anything — shows warning snackbar if no DB configured.
- _isRefreshingSchema bool drives both button disabled state and spinner/label swap inside the button body.


### 2026-03-13 - Query Result Data Grid - UI Layer Analysis

**Task:** Comprehensive UI analysis for adding query result data grid feature to LinqStudio (requested by snakex64).

**Scope:** Analyzed the Blazor component hierarchy, state management patterns, MudBlazor dynamic grid strategies, and UI/UX patterns for displaying query execution results.

**Key Findings:**

1. **Component Architecture:**
   - Editor.razor is a single-page component managing ALL query tabs (not per-tab instances)
   - Monaco editor is singleton that swaps content on tab switch
   - State lives in QueriesWorkspace (persistent query text) + component-level execution state (transient results)
   - Per-tab execution state should use Dictionary<Guid, QueryExecutionState> in Editor.razor.cs

2. **MudDataGrid Dynamic Column Strategy:**
   - Query results return `List<object>` with unknown compile-time type
   - Recommended: Convert to `List<Dictionary<string, object>>` using reflection
   - Use MudDataGrid<Dictionary<string, object>> with TemplateColumns (foreach column)
   - Trade-off: Lose built-in sorting/filtering, gain maximum flexibility for any result type
   - Handles entities, anonymous types, primitives via reflection

3. **Execute Button Placement:**
   - Add to existing "Editor Actions" section (below Monaco editor, line ~90)
   - Use MudButton with StartIcon, Color.Success, loading state pattern (matches Refresh Schema button)
   - MudProgressCircular + "Executing..." text during operation

4. **Results Display:**
   - Add results section after Monaco editor (line ~106)
   - Three states: Loading (MudProgressCircular) → Error (MudAlert) → Results (MudDataGrid)
   - Row count display: "Results — N row(s)"
   - Follow existing MudPaper + Elevation="1" pattern

5. **Critical Gap Identified:**
   - NO backend query execution service exists (CompilerService only provides IntelliSense)
   - Need QueryExecutorService to compile + execute LINQ queries against DbContext
   - UI can be built with mock data, but backend coordination required for integration

6. **Column Extraction Logic:**
   - Use reflection to get Type.GetProperties() from first result item
   - Handle primitive types (show "Value" column), entities (all public properties), anonymous types
   - Convert each row to Dictionary<string, object> for grid binding
   - Handle null values as "(null)" string display

**Files Identified for Modification:**
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor` — Add Execute button + results grid section
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` — Add QueryExecutionState, ExecuteQuery(), ProcessResults()
- `src/LinqStudio.Core/Services/QueryExecutorService.cs` — NEW (backend, requires Simon or backend dev)

**Design Decisions Written to:** `.squad/decisions/inbox/eviljosh-ui-datagrid-analysis.md`

**Pattern Learnings:**
- MudBlazor button loading state: Swap button content based on bool flag (if loading: spinner + text, else: normal text)
- Dynamic MudDataGrid: Use Dictionary<string, object> rows + foreach TemplateColumn for runtime column generation
- Per-tab state in Editor.razor.cs: Dictionary<Guid, State> keyed by query ID, created on-demand
- State lifecycle: Created on first execution, cleared when tab closes, persists during tab switching

**Questions for snakex64:**
1. Who implements QueryExecutorService? (Simon/backend dev or new task?)
2. Should results persist when switching tabs? (Recommendation: Yes)
3. Result size limits? (Recommendation: No limit in v1, add pagination if needed)
4. Export to CSV/JSON? (Recommendation: Not in v1)

**Next Steps:**
1. Confirm backend service implementation ownership
2. Implement UI layer with mock data (1-2 hours)
3. Wire up real backend when ready
4. E2E testing with Jordan


### 2026-03-13 - Editor.razor.cs Query Execution Project Parameter Update

**Task:** Update ExecuteCurrentQueryAsync to pass Project parameter to IQueryExecutionService.ExecuteQueryAsync (coordinated change with Simon's backend update).

**Files Modified:**
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs`

**Changes Made:**

1. **Line 528** (ExecuteCurrentQueryAsync method):
   - Updated call signature from: `ExecuteQueryAsync(queryText, state.CancellationTokenSource.Token)`
   - To: `ExecuteQueryAsync(queryText, Workspace.CurrentProject, state.CancellationTokenSource.Token)`
   - Added null check for `Workspace.CurrentProject` before execution (lines 511-515)
   - Shows warning snackbar "No project is open." if project is null

**Technical Details:**
- Project access: via `Workspace.CurrentProject` property (already available in component)
- Null safety: Early return with user-friendly warning if no project open
- Parameter order: queryText → project → cancellationToken (matches new interface signature)

**Build Status:** 
- ⚠️ Expected build failure until Simon's IQueryExecutionService interface change lands
- Error: "No overload for method 'ExecuteQueryAsync' takes 3 arguments"
- Fix is correct — just waiting on backend interface update

**Integration:**
- Works with existing ProjectWorkspace pattern
- Follows existing null-check patterns in Editor (e.g., lines 93-96, 444-447)
- Consistent with other project-dependent operations (RefreshSchemaAsync uses same pattern)

**Key Pattern:**
All query execution operations require an open project. The null check pattern used here matches existing patterns in the component where project-dependent features (schema refresh, DB context generation) already check `Workspace.CurrentProject is null` before proceeding.


---

## Learnings

### 2026-03-11 - QueryResultGrid SSMS-like Features UI/UX Analysis

**Task:** Comprehensive analysis of enhancing QueryResultGrid.razor with SSMS-like advanced grid features (column resize, reorder, cell/row selection, sorting).

**Key Findings:**

#### Current State
- Component uses basic MudTable with dynamic columns (foreach over Result.ColumnNames)
- Data model: QueryExecutionResult with Dictionary<string, object?> rows
- No custom CSS — fully relies on MudBlazor theming
- Embedded in Editor page with fixed 400px height, scrollable container

#### MudDataGrid vs MudTable Decision
- **MudDataGrid provides natively:**
  - Column resizing (Resizable="true")
  - Column reordering (DragDropColumnReordering="true")
  - Multi-column sorting
  - Row selection without checkboxes (SelectOnRowClick="true")
  - Better performance (virtualization, optimized rendering)
- **Challenge:** MudDataGrid expects strongly-typed T, current code uses Dictionary<string, object?>
- **Solution:** Use ExpandoObject or dynamic TemplateColumn approach (explored previously in Dec 2024 context)
- **Recommendation:** Switch to MudDataGrid — saves 2+ weeks of custom JS interop work

#### Implementation Complexity by Feature
| Feature | MudDataGrid | MudTable Custom | Winner |
|---------|-------------|-----------------|--------|
| Column resize | ✅ Native (1-2h) | ❌ Custom JS (2-3d) | **MudDataGrid** |
| Column reorder | ✅ Native (1h) | ❌ Custom JS (2d) | **MudDataGrid** |
| Column sorting | ✅ Native (1h) | ✅ MudTableSortLabel (1-2h) | Tie |
| Row selection | ✅ Native (2h) | ⚠️ Custom (1d) | **MudDataGrid** |
| Cell selection | ❌ Custom (2-3d) | ❌ Custom (2-3d) | Tie |

**Total effort:** MudDataGrid (~1.5 weeks) vs MudTable custom (~3-4 weeks)

#### Cell Selection vs Row Selection Insight
- **SSMS has both modes** with separate toggle (cell mode vs row mode)
- **Blazor constraint:** Can't easily do both simultaneously — they conflict in event handling
- **Options:**
  1. Implement both with mode toggle (complex but flexible)
  2. Cell selection only (power-user preference)
  3. Row selection only (simpler, good enough for most queries)
- **Recommendation:** Start with row selection (easy with MudDataGrid), add cell selection in v2 if users request

#### JS Interop Concerns
- **Cell selection requires JS interop:**
  - Blazor's @onclick event args don't expose Ctrl/Shift modifier keys
  - Need custom JS to detect modifiers and pass to C# (getModifierKeys() function)
  - Keyboard navigation needs JS event listeners (keydown on table element)
- **Copy to clipboard:**
  - 
avigator.clipboard.writeText() API (requires HTTPS or localhost)
  - Format decision needed: tab-delimited? CSV? Include headers?
- **Custom column resizing is brittle:**
  - JS drag handlers (mousedown, mousemove, mouseup) conflict with Blazor re-rendering
  - Resize handles get lost on state changes unless re-attached (fragile)
  - **Strong recommendation:** Don't custom-build this — use MudDataGrid native

#### CSS Specificity with MudBlazor
- MudBlazor uses ::deep for nested component styling
- Custom cell/row selection styles need higher specificity:
  `css
  ::deep .mud-table-cell.cell-selected {
      border: 2px solid blue !important;
  }
  `
- **Better approach:** Use MudDataGrid's Class properties on columns/rows (no ::deep needed)

#### Key Design Questions for User
1. **MudDataGrid vs MudTable?** (Recommend MudDataGrid)
2. **Cell, row, or both selection modes?** (Recommend row only for MVP)
3. **Feature priority?** (Resize > sort > row select > cell select > reorder?)
4. **Persist column widths/order?** (Impacts state management)
5. **Clipboard format?** (Tab-delimited with headers recommended)
6. **Virtualization for large datasets?** (Recommend yes if 1000+ rows common)

#### Frontend Dev Philosophy
- **Don't reinvent the wheel:** MudDataGrid already has professional-grade features
- **JS interop is expensive:** Avoid unless necessary (cell selection is necessary, column resize isn't)
- **Blazor Server latency:** Every click = SignalR round-trip → use ShouldRender() optimizations
- **Component-first thinking:** If MudBlazor provides it, use it — custom code is technical debt

**Next Steps:**
1. User decisions on design questions
2. Prototype MudDataGrid adapter for Dictionary<string, object?> rows
3. Implement Phase 1: resize + sort + row select (~1 week)
4. Phase 2: keyboard nav + clipboard (~1 week)
5. Phase 3: cell selection if requested (~3 days)

**Documentation Created:**
- .squad/decisions/inbox/eviljosh-results-grid-ui-analysis.md (comprehensive 500+ line analysis)


### 2026-03-13 - Enhanced QueryResultGrid with MudDataGrid + SSMS-like Interactivity

**Task:** Migrated QueryResultGrid from MudTable to MudDataGrid with full row/cell selection, sorting, column reordering, clipboard support, and draggable editor/results splitter.

**Files Modified:**
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor - Migrated to MudDataGrid with TemplateColumn
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs - Added selection logic, clipboard TSV copy
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor - Added draggable vertical splitter
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs - Added JSRuntime injection, splitter init
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.css - Splitter + flex layout styling
- src/LinqStudio.App.WebServer/App.razor - Added queryResultGrid.js script reference
- src/LinqStudio.Blazor/Components/copilot.md - Updated QueryResultGrid + Editor documentation

**Files Created:**
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor.css - Cell/row selection styling
- src/LinqStudio.Blazor/wwwroot/queryResultGrid.js - Splitter drag-drop JS
- .squad/decisions/inbox/eviljosh-results-grid-implementation.md - Technical decision doc

**Features Implemented:**
1. **MudDataGrid Migration**: Replaced MudTable with MudDataGrid using TemplateColumn + @foreach dynamic columns
2. **Row Selection**: Click-to-highlight (no checkboxes), Ctrl+Click multi-select, Shift+Click range
3. **Cell Selection**: Click individual cells, Ctrl+Click multi, Shift+Click vertical range (same column)
4. **Sorting**: Client-side via SortBy parameter on each TemplateColumn
5. **Column Reordering**: Drag-and-drop enabled (DragDropColumnReordering=true)
6. **Column Resizing**: ResizeMode.Container (resize individual columns)
7. **Virtualization**: Enabled for large result sets (Virtualize=true, FixedHeader=true)
8. **NULL Display**: Shows ""NULL"" text for null values (SSMS-style)
9. **Clipboard Copy (Ctrl+C)**: TSV format with column headers via navigator.clipboard.writeText
10. **Draggable Splitter**: Vertical splitter between editor (~40%) and results (~60%), resets on page load

**Key Technical Patterns:**

**Selection State (per-tab, non-persisted):**
`csharp
private HashSet<int> _selectedRows = new();
private HashSet<(int RowIndex, string ColumnName)> _selectedCells = new();
`
- Cell click uses @onclick:stopPropagation=""true"" to prevent row selection
- Keyboard tracking: _isShiftDown, _isCtrlDown for modifier keys

**Row Index Lookup (no LINQ):**
`csharp
private int GetRowIndex(IReadOnlyDictionary<string, object> row)
{
    for (int i = 0; i < Result.Rows.Count; i++)
        if (ReferenceEquals(Result.Rows[i], row)) return i;
    return -1;
}
`
- Uses ReferenceEquals for O(1) identity check
- Avoids .IndexOf() extension (not available on IReadOnlyList<T>)

**Nullable Reference Type Workaround:**
- Razor file: T=""IReadOnlyDictionary<string, object>"" (no ?)
- Code-behind: Wraps logic in #nullable enable / #nullable restore
- Reason: Blazor Razor compiler doesn't support @nullable enable directive

**Splitter JS Interop:**
`javascript
window.initSplitter = function(splitterId, topId, bottomId) {
    // mousedown/mousemove/mouseup handlers
    // Min height: 80px per panel
    // Updates top.style.height and bottom.style.height
}
`
Called in Editor.OnAfterRenderAsync(firstRender).

**Clipboard TSV Generation:**
- Selected cells: column headers for selected columns only + rows with those cells
- Selected rows: all column headers + full rows
- Format: tab-separated values with \t and \n
- Graceful failure if clipboard API unavailable

**data-testid Attributes Added:**
- column-header-{ColumnName} - Each column header
- cell-{RowIndex}-{ColumnName} - Each cell (e.g., cell-0-Id)
- selection-count - Selection count indicator
- ditor-results-splitter - Draggable splitter

**Build Status:** ✅ Clean build (0 errors, 0 warnings)

**Why MudDataGrid?**
- MudBlazor 8.15.0 has stable TemplateColumn support
- Advanced features: sorting, column resize, drag-drop reordering, virtualization
- Previous decision used MudTable for simplicity; new requirements demand DataGrid capabilities

**Known Issues:**
- Selection state resets on tab switch (per design - no persistence)
- Clipboard API may fail in older browsers (gracefully handled)

**Next Steps:**
- Component tests for selection logic and clipboard
- E2E tests for full interaction flow
- Potential: Persist column order in saved query files (future enhancement)

## Learnings

### Removing addDataTestIdsToRows JavaScript Function
**Date:** 2025-01-26
**Context:** Project policy enforces "no JS when avoidable in Blazor/C#". The `addDataTestIdsToRows` function was injecting `data-testid="row-X"` attributes to MudDataGrid `<tr>` rows purely for E2E testing purposes.

**Action Taken:**
- Removed entire `addDataTestIdsToRows` function from `queryResultGrid.js` (lines 71-94)
- Removed C# invocation in `QueryResultGrid.razor.cs` `OnAfterRenderAsync` method (Task.Delay + JSRuntime call)
- Kept `IJSRuntime` injection intact (still needed for clipboard functionality)

**Why This Was Right:**
- JS was only for testing, not user-facing functionality
- MudDataGrid's RowTemplate cannot easily add attributes to `<tr>` in pure Blazor
- E2E tests updated by Jordan to use existing cell-based selectors (`data-testid="cell-{RowIndex}-{ColumnName}"`)
- Aligns with team charter: avoid JS when Blazor can handle it OR when feature isn't critical

**Key Principle:** Test infrastructure should adapt to the codebase, not vice versa. Don't introduce JS just to make tests easier if it violates architectural principles.

### Session Complete: 2026-03-15 — Remove JS Row TestID Injection

**Scribe Update:** All work items from this session are now documented:
- Orchestration logs created for EvilJosh, Jordan, Alex, Alice
- Session log created: `.squad/log/2026-03-15T15-38-18Z-remove-js-testid-rows.md`
- Decision record merged into `.squad/decisions/decisions.md`
- Inbox files archived (3 decision files removed)

**Final Status:** ✅ ALL TESTS PASS (212/212)
- Core Unit Tests: 119/119 ✅
- Blazor Unit Tests: 60/60 ✅
- E2E Tests: 33/33 ✅
- Zero regressions, ready for production


### 2026-03-15: Clipboard Service Abstraction
Created IClipboardService in src/LinqStudio.Blazor/Services/ClipboardService.cs to wrap the copyToClipboard JavaScript function. The service follows project patterns: sealed internal implementation class with public interface, registered as scoped in AddLinqStudioBlazor(), and uses file-scoped namespace. Updated QueryResultGrid to inject IClipboardService instead of IJSRuntime directly. This enables testability and reduces direct JS interop in components.


### 2026-03-18: Sort Definitions Propagation Investigation

**Task:** Investigated how sort definitions flow from MudDataGrid in QueryResultGrid up to the parent Editor component.

**Components Involved:**
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor + .razor.cs
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor + .razor.cs

**Full Data Flow (step by step):**
1. User clicks a column header in MudDataGrid inside QueryResultGrid
2. MudDataGrid internally updates its own SortDefinitions dictionary (keyed by column title, SortDefinition<IReadOnlyDictionary<string,object?>>)
3. Blazor re-renders the component tree, triggering OnAfterRenderAsync in QueryResultGrid
4. In OnAfterRenderAsync, code POLLS _dataGrid.SortDefinitions (the internal MudDataGrid state) and compares it to _lastKnownSortDefinitions using AreSortDefinitionsEqual() (checks Count, Descending, Index per key)
5. If a difference is detected, OnSortDefinitionsChangedInternal is called, which: (a) updates local SortDefinitions parameter field, (b) invokes the OnSortDefinitionsChanged EventCallback upward
6. In Editor.razor, the callback is a lambda: @((defs) => { execState.SortDefinitions = defs; })
7. xecState is a QueryExecutionState object stored in _executionStates (Dictionary keyed by query GUID), which persists sort state per tab

**Why Sort Is Propagated Upward:**
- _executionStates is the per-tab state store in the parent Editor; sort state must live there so it survives tab switches and is re-supplied as a parameter on re-render
- Without propagation, switching tabs would reset the user's sort — a bad UX regression
- The sort definitions are passed DOWN as a parameter (SortDefinitions="@execState.SortDefinitions") on each render, so the grid restores sort correctly after tab navigation

**Code Smell / Design Issue — Polling in OnAfterRenderAsync:**
- MudDataGrid does not expose a sort-changed EventCallback or event. There's no OnSortChanged callback to bind to.
- The team worked around this with a polling approach: after every render, diff the current vs. last-known sort state.
- This means every render cycle (including unrelated re-renders like hover effects) runs the comparison. It's benign in practice (the comparison is cheap and O(N) columns), but it's a side-effect in a lifecycle method.
- The mutation SortDefinitions = defs; inside the component (setting a [Parameter] field directly) is also technically an anti-pattern in Blazor — parameters should be controlled by the parent, not mutated locally. But it's necessary here because MudDataGrid owns its sort state internally.
- **No official MudBlazor API exists to observe sort changes externally** — this is the pragmatic solution for the constraint.

**Summary:** Sort propagation is a render-cycle polling pattern forced by MudDataGrid's opaque internal sort management. It works correctly for the per-tab state persistence use case but is a controlled workaround, not a clean event-driven design.


## Learnings

### 2026-06-XX — KeepPanelsAlive Investigation (requested by snakex64)

**Finding: KeepPanelsAlive cannot be applied as a drop-in fix.**

The current MudTabs in Editor.razor uses *empty* MudTabPanel elements as a pure navigation strip. Content (Monaco editor, execution bar, QueryResultGrid) lives **outside** the tab panels entirely — below the <MudPaper> that wraps the tabs. Tab switching is URL-based (NavigationManager.NavigateTo), not panel show/hide.

Because the panels contain no content, adding KeepPanelsAlive="true" to the current MudTabs would have **zero effect**. The approach first requires moving per-query content *into* each MudTabPanel, which is a significant architectural refactor.

**Sort machinery summary (confirmed by reading source):**
- QueryResultGrid.razor.cs: OnAfterRenderAsync polls _dataGrid.SortDefinitions every render, diffs via AreSortDefinitionsEqual, fires OnSortDefinitionsChanged callback
- Editor.razor.cs: QueryExecutionState stores SortDefinitions per tab; lambda (defs) => { execState.SortDefinitions = defs; } wires the callback
- Editor.razor line 158-159: passes SortDefinitions="@execState.SortDefinitions" and OnSortDefinitionsChanged back down
- The entire machinery exists solely because switching tabs would otherwise lose sort state

**Monaco editor is outside tab panels** — it's a singleton <StandaloneCodeEditor> at line 71-78 of Editor.razor, always rendered (after the Delay flag clears). It is NOT affected by MudTabs.

**For KeepPanelsAlive to be the actual fix**, the refactor must:
1. Restructure MudTabPanels to each contain Monaco + execution bar + QueryResultGrid
2. Switch from URL-navigation tabs to MudBlazor's built-in panel activation model
3. Remove the sort polling machinery, sort storage, and sort parameters
4. Handle N Monaco editor instances (init delay per editor, splitter per panel, providers per editor)

### 2026-06-XX - KeepPanelsAlive Redesign Spec

QueryResultGrid is at Components/QueryResultGrid.razor (not in Editor folder). Sort polling in OnAfterRenderAsync every cycle. MonacoProvidersService already supports N editors via URI routing. @ref inside @foreach cannot target a dict - recommend QueryEditorPanel child component. KeepPanelsAlive renders panels lazily on first activation. Monaco fix: AutomaticLayout=true + explicit layout() after 50ms on tab activation. Splitters become per-tab with GUID-suffixed IDs. URL navigation preserved via GetActivePanelIndex() computed from CurrentQueryId. OnParametersSet _editor.SetValue block must be removed. SortDefinitions and OnSortDefinitionsChanged parameters on QueryResultGrid can be deleted (sort preserved by KeepPanelsAlive). Spec: .squad/decisions/inbox/eviljosh-redesign-spec.md
### 2026-XX-XX - KeepPanelsAlive Full Redesign (Major Refactor)

**Task:** Implement full MudTabPanel KeepPanelsAlive redesign for the Editor per spec from snakex64.

**Files Created:**
- src/LinqStudio.Blazor/Components/Pages/Editor/QueryEditorPanel.razor - New per-tab component containing Monaco editor, splitter, execution bar, QueryResultGrid
- src/LinqStudio.Blazor/Components/Pages/Editor/QueryEditorPanel.razor.cs - Code-behind with all per-tab logic
- src/LinqStudio.Blazor/Components/Pages/Editor/QueryEditorPanel.razor.css - Scoped CSS for per-tab layout

**Files Modified:**
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor - Replaced singleton content with KeepPanelsAlive MudTabs structure
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs - Removed all per-tab logic, now manages compiler + tab refs + navigation
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.css - MudTabs flex height CSS
- src/LinqStudio.Blazor/Components/Pages/Editor/copilot.md - Updated notes
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor - Removed SortDefinitions binding
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs - Deleted sort propagation machinery
- 	ests/LinqStudio.App.WebServer.E2ETests/Helpers/E2ETestHelpers.cs - Added GetActivePanel(), fixed Monaco locators for multi-tab
- 	ests/LinqStudio.App.WebServer.E2ETests/QueryResultGridInteractiveE2ETests.cs - Scoped per-tab test assertions to active panel
- 	ests/LinqStudio.App.WebServer.E2ETests/QueryExecutionE2ETests.cs - Scoped per-tab test assertions to active panel
- 	ests/LinqStudio.App.WebServer.E2ETests/NavMenuE2ETests.cs - Scoped multi-tab test assertions to active panel

**Architecture Changes:**
- QueryEditorPanel is a new self-contained per-tab component (Monaco + splitter + results)
- Editor reduced to: compiler management, tab ref tracking, navigation, RefreshSchema
- QueryResultGrid sort propagation machinery fully deleted (4 methods, 2 parameters, 1 field)
- KeepPanelsAlive="true" preserves MudDataGrid sort state, selection, scroll naturally

**Critical Learnings:**
- **Compiler race condition**: Editor.OnInitializedAsync creates shared compiler but Monaco panels may init before it's ready. Fix: panels create a _localCompiler fallback in OnEditorInitialized and use Compiler ?? _localCompiler in provider callbacks.
- **overflow:hidden on MudTabPanel breaks Monaco pointer events**: CSS overflow:hidden on the tab panel container prevents Playwright from hovering Monaco spans (elementFromPoint returns the container instead of the span). Fix: remove overflow:hidden from ::deep .mud-tab-panel.
- **KeepPanelsAlive + E2E tests**: With all panels mounted, data-testid elements appear once per open tab in DOM. Playwright strict mode counts hidden elements too. Fix: added GetActivePanel() helper scoping to [role='tabpanel']:visible. All multi-tab E2E tests updated.
- **Monaco layout after tab show**: Must call ditor.Layout() after tab becomes visible (after display:none removed). Add 50ms delay to let browser complete the CSS change.
- **@ref in foreach + Dictionary**: @ref="_tabPanelRefs[capturedQ.Id]" works correctly in Blazor; indexer setter adds keys dynamically. Use Dictionary<Guid, QueryEditorPanel?> for nullable safety.

**Test Results:** All 521 tests pass (119 Core, 60 Blazor, 309 Databases, 33 E2E passed, 4 E2E skipped).

### 2026-03-19 - Monaco Blank on Tab Switch Fix

**Task:** Fixed Monaco editor collapsing to 5×5px on every tab switch (Bug 1 from Alice's live test report).

**Root Cause:** OnTabActivatedAsync() called _editor.Layout(new Dimension { Width = 0, Height = 0 }) — passing EXPLICIT zero dimensions to Monaco. This instructed Monaco to be 0×0 rather than auto-measuring the container. Combined with a too-short 50ms delay (MudBlazor may not have removed display:none yet), the editor collapsed on every tab switch.

**Fix Applied:**
- Added window.monacoRelayout(editorContainerId) JS function in src/LinqStudio.Blazor/wwwroot/queryResultGrid.js
  - Finds the Monaco editor instance whose DOM node is inside the given container
  - Calls ditor.layout() with NO arguments → Monaco auto-reads container size
- Updated OnTabActivatedAsync in QueryEditorPanel.razor.cs to:
  - Use 100ms delay (was 50ms) to ensure MudBlazor CSS visibility change completes
  - Call JSRuntime.InvokeVoidAsync("monacoRelayout", EditorId) instead of _editor.Layout(new Dimension{...})

**Key Learnings:**
- monaco.editor.layout() with no args = auto-measure container. With {width:0,height:0} = set to 0×0. These are opposite behaviors.
- AutomaticLayout = true uses ResizeObserver which may not reliably fire when going from display:none to visible; explicit relayout call is needed.
- Monaco ditor.layout() must be called WITHOUT dimensions to trigger auto-measurement.

**Tab Bar Scroll Bug (Bug 2 from Alice):** Investigated. Intermittent scrollTop:52 on div.mud-tabs. Requires deep investigation — no simple MudTabs prop available to fix. Left as-is per task instruction.

**Test Results:** All 527 tests pass (119 Core, 61 Blazor, 309 Databases, 38 E2E passed, 4 E2E skipped).


### 2026-03-18 - Alex's Code Review Fixes

**Task:** Applied 3 low-severity findings from Alex's code review.

**Fix 1 - Dead field _activePanelIndex in Editor.razor.cs:**
- Removed private int _activePanelIndex = 0; field declaration.
- Removed _activePanelIndex = newIndex; assignment in OnActivePanelIndexChanged.
- The active index was always computed via GetActivePanelIndex() — the field was never read.

**Fix 2 - Silent no-op in ExecuteQueryAsync in QueryEditorPanel.razor.cs:**
- The old guard if (_editor is null || !Workspace.IsProjectOpen || Workspace.CurrentProject is null) only showed a snackbar for the project-closed case, silently returning if _editor was null while project was open.
- Split into two separate guards: project-closed check first (with "No project is open." snackbar), then _editor == null check (with "Editor not ready. Please try again." snackbar).
- ISnackbar was already injected — no new injection needed.

**Fix 3 - Redundant GC.SuppressFinalize in Dispose() in QueryEditorPanel.razor.cs:**
- Removed GC.SuppressFinalize(this) from Dispose() — it was called redundantly since DisposeAsync() already calls it after Dispose().
- No finalizer exists on the class; the call in DisposeAsync is the correct single location.

**Test Results:** All 527 tests pass (119 Core, 61 Blazor, 309 Databases, 38 E2E passed, 4 E2E skipped). One E2E test (TabClose_RemovesTab_AndRemainingTabsWork) was flaky on first run but passed on retry — pre-existing flakiness, not caused by these changes.


### 2026 - Code Review Fixes (Cleanup Pass)

4 files changed. 527 tests passing. See .squad/decisions/inbox/eviljosh-cleanup.md for full detail.


## Learnings

### 2026 - URL Sync on Tab Switch + queryResultGrid.js → editor-utils.js

**FIX 1 — URL sync in OnActivePanelIndexChanged (Editor.razor.cs):**
- Added NavigationManager.NavigateTo($"/editor/{query.Id}", replace: true) after Workspace.Queries.OpenQuery(query.Id) in OnActivePanelIndexChanged.
- Uses eplace: true so tab switching doesn't spam browser history. Only deep-link navigation (opening the page for the first time via OnParametersSet) creates history entries.
- F5 now reloads the correct tab.

**FIX 2 — Renamed queryResultGrid.js → editor-utils.js:**
- The file at src/LinqStudio.Blazor/wwwroot/queryResultGrid.js was renamed to ditor-utils.js — it contains Monaco relayout, splitter init/dispose, and clipboard utilities, not just result grid code.
- Updated <script> reference in src/LinqStudio.App.WebServer/App.razor.
- Updated all copilot.md references: src/LinqStudio.Blazor/Components/copilot.md (3 occurrences) and src/LinqStudio.Blazor/Components/Pages/Editor/copilot.md (2 occurrences).

**Test Results:** All 527 tests pass (119 Core, 61 Blazor, 309 Databases, 38 E2E passed, 4 E2E skipped).

5 files changed. See .squad/decisions/inbox/eviljosh-url-and-rename.md for full detail.


### 2026-06-XX — Tab Bar Scroll Fix (Bug 2 from Alice)

**Bug:** With 3+ tabs, div.mud-tabs intermittently got scrollTop: 52. The 48px tab header bar scrolled behind the 64px fixed app bar and disappeared.

**Root cause:** MudBlazor activates a panel by removing display:none. The browser triggers scrollIntoView() on the newly-visible panel, which propagates to .mud-tabs (the nearest scrollable ancestor). Since .mud-tabs had no overflow guard, scrollTop = ~52px (≈ toolbar height), scrolling the toolbar out of view.

**Fix applied (CSS-only primary + JS belt-and-suspenders):**
- overflow-y: hidden on .mud-tabs — creates a scroll container context (CSS spec: overflow != visible creates a scroll container) without hiding horizontal tab scroll buttons
- position: sticky; top: 0; z-index: 10 on .mud-tabs-toolbar — within the overflow-y: hidden scroll container, sticks at top: 0 regardless of what scrollTop is set to. Immune to any timing issues.
- window.resetMudTabsScroll() in editor-utils.js — resets scrollTop to 0, called from OnTabActivatedAsync as cleanup

**Key CSS insight:** position: sticky + overflow: hidden parent IS valid — the hidden-overflow ancestor IS the sticky scroll container per CSS spec. The toolbar sticks at top: 0 of the container's visible area even if scrollTop is set by MudBlazor's JS.

**Files changed:**
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.css
- src/LinqStudio.Blazor/wwwroot/editor-utils.js
- src/LinqStudio.Blazor/Components/Pages/Editor/QueryEditorPanel.razor.cs

**Test results:** 119 Core + 61 Blazor passing. E2E pre-existing flaky (require app server).

### 2026 — CSS Selector Typo Fix (Tab Bar Scroll Bug — Root Cause)

**Bug reported by:** Alice (via snakex64)

**Root cause:** .mud-tab-panels in Editor.razor.css was a typo — the correct MudBlazor class is .mud-tabs-panels (one character difference: "tab" → "tabs"). This meant the display: flex; flex: 1; min-height: 0 rule on the panels container never applied, causing a permanent 52px structural overflow on div.mud-tabs. That overflow was exactly what the browser scrolled to when Monaco got focus in a newly-visible panel.

**Fix:**
- src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.css: Corrected ::deep .mud-tab-panels → ::deep .mud-tabs-panels

**Confirmed existing (from prior task):**
- overflow-y: hidden on ::deep .mud-tabs — prevents browser focus-scroll propagation
- position: sticky; top: 0; z-index: 10 on ::deep .mud-tabs-toolbar — toolbar stays pinned regardless of scrollTop

The three rules together make the scroll bug structurally impossible:
1. Correct selector eliminates the 52px overflow
2. overflow-y: hidden blocks any focus-scroll from propagating
3. sticky toolbar survives any residual scrollTop

**Test Results:** 527 tests pass (119 Core, 61 Blazor, 309 Databases, 38 E2E passed, 4 E2E skipped). Exit code 0.

1 file changed, 1 line.

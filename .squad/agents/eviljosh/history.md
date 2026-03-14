# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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

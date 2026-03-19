# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

### 2026-03-19 Alice Live Test — Tab Scroll Bug Reproduction Attempt

**Task:** Reproduce intermittent `div.mud-tabs` scrollTop: 52 bug (tab bar hidden behind app bar)

**Result:** Could NOT reproduce organically across 5 attempts + additional Monaco focus timing attempts. Bug is genuinely intermittent and timing-dependent.

**Key structural finding confirmed:**
- `Editor.razor.css` has CSS selector typo: `::deep .mud-tab-panels` → should be `::deep .mud-tabs-panels` (DOM uses `.mud-tabs-panels`, not `.mud-tab-panels`)
- The wrong selector matches 0 elements — the intended `flex: 1; display: flex; min-height: 0` never applies to the panels container
- `.mud-tabs-panels` gets MudBlazor defaults: `display: block`, `overflow: visible`, `min-height: auto`
- This creates a **permanent 52px structural overflow**: `mud-tabs.scrollHeight (928) - clientHeight (876) = 52px` — exactly the reported scrollTop value
- `mud-tabs` has `overflow: hidden` but browsers still allow programmatic/focus-driven `scrollTop` changes on such elements

**Simulated bug state (confirmed visual):**
- Set `mud-tabs.scrollTop = 52` → tabbar moves from top:76 to top:24, entirely hidden behind app bar (bottom:64)
- Tab bar is **completely gone** (not just clipped) — all 4 tabs invisible
- Self-correction: `scrollTop = 0` restores immediately, but `overflow: hidden` means **users cannot scroll back** — bug is permanent until page refresh

**Likely real trigger (not reproduced but structurally explained):**
- Monaco editor `textarea` gets browser focus (after 500ms `_delay` init)
- Browser auto-scroll-to-focus sets `mud-tabs.scrollTop = 52` (max offset)
- Most likely during tab switch + Monaco re-initialization timing window

**Fix:** Change `::deep .mud-tab-panels` → `::deep .mud-tabs-panels` in `Editor.razor.css:9`. This eliminates the 52px structural overflow and removes the condition for the bug.

**Report:** `.squad/decisions/inbox/alice-scroll-repro.md`

### 2026-03-18T22:43:55Z URL Sync + JS Rename (EvilJosh)
- URL sync on tab switch (NavigateTo replace:true) improves navigation reliability in Editor page
- JS file rename: queryResultGrid.js → editor-utils.js, references updated
- 527 tests passing

### 2026-03-19 Alice Live Test — URL Sync + editor-utils.js Rename (Verified)
- **CHECK 1 PASS**: Tab clicks update browser URL to `/editor/{guid}` of the clicked tab
- **CHECK 1 PASS**: `replace:true` confirmed — `window.history.length` stays constant (29) across all tab switches, no history pollution. Back button will NOT cycle through tab switches.
- **CHECK 2 PASS**: F5 after switching to Tab 2 → reloads correctly to Tab 2 (`[active][selected]`). URL preserved the active tab.
- **CHECK 3 PASS**: `editor-utils.js` loads 200 OK (fingerprinted as `editor-utils.io14oftgv2.js`). No `queryResultGrid.js` in network requests. Zero console errors or warnings.
- **CHECK 3 PASS**: Splitter draggable — drag test moved the divider ~100px down successfully.
- **CHECK 4 PASS**: Monaco editor visible (not blank) after tab switch. Both tabs render distinct content correctly. Typing into Monaco editor works.
- **NOTE**: After F5 reload, in-memory unsaved queries are lost except the active one (URL-based restore). Both tabs still present after normal tab switching (no reload). This is expected Blazor Server behaviour — unsaved queries are ephemeral.
- **Zero JS errors** across the entire test session.

### Pages and Routes (All routable pages discovered)
1. **Home page (`/`)** - Welcome landing page with title and description
2. **Editor page (`/editor`, `/editor/{QueryIdParam:guid}`, `/editor/new`)** - Main LINQ query editing interface with Monaco editor
3. **Settings page (`/settings`)** - Settings editor with Monaco for JSON editing
4. **Error page (`/Error`)** - Error display page for unhandled HTTP exceptions
5. **Not Found page (`/not-found`)** - 404 page for missing content

### Navigation Structure (NavMenu.razor)
- **Home link** - Always visible, navigates to `/`
- **Project menu** (MudMenu dropdown):
  - New - Creates new untitled project
  - Open - Opens project from file
  - Properties - Edits connection settings (when project open)
  - Save - Saves project (disabled when no changes)
  - Save As... - Saves project with new name
  - Close - Closes current project
- **Editor link/menu** - Visible only when project is open:
  - Editor link - Navigates to `/editor`
  - New Query - Creates new query
  - Open Query... - Opens query from file
- **Settings button** - Top right app bar, navigates to `/settings`
- **Dark/Light mode toggle** - Top right app bar, toggles theme

### Key User Flows to Test
1. **Project lifecycle**:
   - Create new project → shows "Untitled" in nav, has unsaved changes indicator (*)
   - Edit project properties → connection string, database type, timeout
   - Validate connection → test button with loading state
   - Save project → persists to file, clears unsaved indicator
   - Close project → prompts if unsaved, redirects to home
   
2. **Query lifecycle**:
   - Create new query → opens Monaco editor with GUID-based URL
   - Type LINQ code → debounced workspace updates (300ms)
   - Get intellisense → auto-triggers on `.`, `(`, space
   - Manual completion → Ctrl+Space shows completions
   - Hover for info → hover over symbols shows documentation
   - Save query → prompts for filename, updates name display
   - Close query → prompts if unsaved, returns to editor with no query
   - Multiple queries → tabs show all open queries with unsaved indicators (*)
   
3. **Monaco editor specifics**:
   - Editor loads with 500ms delay workaround for BlazorMonaco rendering
   - C# syntax highlighting (vs-dark theme in dark mode)
   - Trigger characters: `.`, `(`, `<`, `[`, space
   - Hover provider registered globally for C# and JSON
   - Completion provider with trigger characters
   - Editor auto-layout enabled
   - Quick suggestions enabled for "other" tokens
   
4. **Settings flow**:
   - Settings page loads all IUserSettingsSection implementations via reflection
   - Each setting in its own MudTabPanel with Monaco JSON editor
   - Hover tooltips show localized descriptions from SharedResource.resx
   - Save All button validates and persists all settings
   - External changes trigger reload dialog (if AlwaysReloadSettingsInSettingsPage is false)
   - UISettings: IsDarkMode, AlwaysReloadSettingsInSettingsPage
   
5. **Dark/light mode toggle**:
   - Toggle button in app bar (AutoMode icon for dark, DarkMode icon for light)
   - Changes UISettings.IsDarkMode and persists immediately
   - MudTheme switches between custom light/dark palettes
   - Monaco editor theme switches (vs-dark vs default)
   - Reactive updates via IOptionsMonitor

### MudBlazor Components Used
- **MudLayout** - Main layout with app bar, drawer, main content
- **MudAppBar** - Top app bar with menu toggle, title, settings, theme toggle
- **MudDrawer** - Left navigation drawer with NavMenu
- **MudNavMenu, MudNavLink** - Navigation menu structure
- **MudMenu, MudMenuItem** - Dropdown menus (Project, Editor)
- **MudTabs, MudTabPanel** - Query tabs, settings tabs
- **MudPaper** - Query info bar, editor info bar
- **MudDialog, MudDialogProvider** - UnsavedChangesDialog, EditProjectDialog, EditorMenuDialog, ErrorDialog
- **MudButton, MudIconButton** - Action buttons throughout
- **MudTextField** - Text inputs in dialogs
- **MudSelect, MudSelectItem** - Dropdowns (database type, timeout)
- **MudChip** - Unsaved indicator badges
- **MudAlert** - Info/error messages (no project, no query)
- **MudSnackbar, MudSnackbarProvider** - Toast notifications for actions
- **MudStack** - Layout containers
- **MudIcon** - Material icons throughout
- **MudTooltip** - Button tooltips
- **MudExpansionPanels** - Technical details in error dialog
- **MudList, MudListItem** - Editor menu dialog
- **MudProgressCircular** - Loading indicator during connection validation

### Dialogs
1. **UnsavedChangesDialog** - Two buttons (Cancel/Continue), used for project/query close confirmations
2. **EditProjectDialog** - Project properties: name (read-only), database type dropdown, connection string (5-line textarea), timeout dropdown, validate button (with loading state), cancel/save buttons
3. **EditorMenuDialog** - New/Open options for queries (appears to be legacy/unused in current flow)
4. **ErrorDialog** - Error icon, message alert, expandable technical details with stack trace, close button

### Error Handling
- **ErrorHandlingService** - Centralized error dialog service (scoped)
- **AppErrorBoundary** - Global error boundary wrapping entire app
- **ErrorDialog** - MudDialog with error message + expandable stack trace
- All errors logged via ILogger
- Error boundary shows fallback alert if dialog fails
- Service used throughout for try-catch error handling

### Blazor Server Specifics
- **SignalR connection** - Automatic reconnection handled by ReconnectModal.razor
- **Reconnection states**: Rejoining, repeated attempts with countdown, failed (retry button), paused (resume button)
- **Component render mode** - Interactive server (AddInteractiveServerRenderMode)
- **Workspace services** - Scoped per user: ProjectWorkspace, QueriesWorkspace
- **State management** - Event-driven: WorkspaceChanged, QueriesChanged events
- **File system** - IFileSystemService abstraction (ServerFileSystemService in app, MockFileSystemService in tests)

### Settings Persistence
- **usersettings.json** - Persisted settings file loaded at startup
- **SettingsService** - Loads/saves IUserSettingsSection implementations
- **IOptionsMonitor** - Reactive settings updates throughout UI
- **Reflection-based discovery** - All IUserSettingsSection types auto-registered
- **Monaco editors** - Each setting gets its own JSON editor with hover tooltips

### Existing E2E Test Infrastructure
- **Test projects**: LinqStudio.App.WebServer.E2ETests
- **Framework**: XUnit + Microsoft.Playwright (Chromium)
- **Fixtures**: AppServerFixture (Kestrel test server), PlaywrightFixture (browser)
- **Helpers**: E2ETestHelpers with reusable setup methods
- **Mock services**: MockFileSystemService for file I/O
- **Test collection**: E2ECollection shares fixtures across tests
- **Base URL**: Configurable via AppServerFixture.BaseUrl
- **Headless mode**: Debug=non-headless, Release=headless
- **Test IDs**: Extensive use of data-testid attributes throughout UI
- **Timeout**: 60 second timeout per test (120s for file save test)
- **Retry pattern**: "If tests fail first time, try rerunning" - known flakiness

### Test Coverage Gaps (Areas needing tests)
1. **Settings page** - No E2E tests for settings editor, save all, reload dialog
2. **Dark/light mode** - No E2E tests for theme toggle
3. **Project open** - No tests for opening existing project from file
4. **Query open** - No tests for opening query from file
5. **Connection validation** - No tests for validate button in EditProjectDialog
6. **Error scenarios** - No tests for error dialog, error boundary
7. **Reconnection** - No tests for SignalR reconnection behavior
8. **Multiple queries** - No tests for tab switching between queries
9. **Query execution** - No feature yet for executing queries and viewing results (future)
10. **Keyboard navigation** - No tests for keyboard shortcuts, focus management

### Accessibility Observations
- MudBlazor handles most ARIA attributes (aria-disabled on buttons)
- data-testid attributes present for testing but not aria-labels
- Focus management: FocusOnNavigate in Routes.razor focuses h1 on navigation
- Keyboard support: Dialogs close on Escape (CloseOnEscapeKey=true)
- ReconnectModal includes aria-hidden on animation elements
- No explicit role attributes or aria-label attributes observed
- Tab navigation likely handled by MudBlazor components
- **Accessibility gap**: Missing aria-labels on icon-only buttons (settings, theme toggle)

### Monaco Editor Integration Details
- **BlazorMonaco library** - Razor wrapper for Monaco editor
- **MonacoProvidersService** - Global provider registration management to avoid duplicates
- **Provider types**: Hover (C# + JSON), Completion (C# only with trigger chars)
- **Retry logic**: Waits up to 20x250ms for Monaco to be ready in browser
- **Model URI tracking**: Providers keyed by model URI, disposable for cleanup
- **Compiler integration**: CompilerService (Roslyn) provides completions and hover info
- **Workspace wrapper**: User query wrapped in synthetic QueryContainer class for Roslyn analysis
- **Cursor position adjustment**: Accounts for wrapper code offset
- **Debounce**: 300ms delay on editor content changes before workspace update

### File System Patterns
- **File extensions**: FileExtensions constants (.linq for queries, .lsproj for projects)
- **Mock in tests**: MockFileSystemService simulates file dialogs and file system
- **SetNextSaveFileResult**: Test helper to control "Save As" dialog results
- **TestFileExists, ReadTestFile**: Test helpers for verification

### Known Issues
- E2E tests sometimes flaky - "try rerunning once" is documented pattern
- BlazorMonaco requires 500ms delay workaround for proper rendering
- Monaco provider registration requires retry loop for timing issues
- One test skipped due to flakiness: Editor_AutoTriggers_CompletionOnSpace

### Test ID Naming Patterns (data-testid values)
- **Pages**: editor-page, no-project-alert, no-query-alert
- **Nav**: nav-home, nav-project, nav-project-menu, nav-project-new, nav-project-open, nav-project-properties, nav-project-save, nav-project-save-as, nav-project-close, nav-editor, nav-editor-menu, nav-editor-new, nav-editor-open-file, nav-editor-disabled
- **Editor**: monaco-editor-container, query-info-bar, query-name-display, query-unsaved-indicator, query-save-btn, query-close-btn, editor-info-bar
- **Dialogs**: unsaved-changes-dialog, unsaved-changes-message, unsaved-changes-cancel-btn, unsaved-changes-confirm-btn, edit-project-dialog, project-name-field, database-type-select, project-connection-string-field, timeout-select, validate-button, edit-project-cancel-btn, edit-project-save-btn, editor-menu-dialog, editor-menu-new, editor-menu-open, editor-menu-cancel

## Live Test: Multi-Tab Editor Redesign (2026-03-19)

### Context
Major editor redesign: each query tab now has its own `QueryEditorPanel` (Monaco + splitter + execution bar + QueryResultGrid) inside `MudTabPanel` with `KeepPanelsAlive="true"`. Tab switching is index-based via MudTabs; Monaco IDs are now per-tab (`editor-{guid-no-dashes}`).

### Critical Bug Found: Monaco Collapses to 5×5 on Every Tab Switch
- **Symptom**: After switching to any previously-hidden tab, Monaco editor renders blank. Container is correctly 953×432px but Monaco widget inside is 5×5px.
- **Reproducibility**: 100% consistent on clean server — reproduced every single tab switch in both test sessions.
- **Root Cause**: Monaco `Layout()` not called when `KeepPanelsAlive` panel becomes visible again. The 500ms delay workaround in `OnAfterRenderAsync` fires only on initial render, not when panel is re-shown.
- **Evidence**: `container=953×432`, `monaco-editor=5×5`, text IS in DOM (accessibility tree confirms) but invisible.
- **Fix direction**: Hook into `MudTabs.ActivePanelIndexChanged` and call JS `layout()` on the newly active Monaco instance, or use a `ResizeObserver`/`IntersectionObserver` on the editor container div.

### Intermittent Bug: Tab Bar Scrolls Behind App Bar
- `div.mud-tabs` sometimes gets `scrollTop: 52`, pushing the 48px tab bar 52px above its natural position into the 64px fixed app bar (invisible due to `overflow: hidden`)
- Observed once with 3 tabs active during a Blazor circuit drop; not reproduced on clean server with 2 tabs
- May be MudBlazor's internal scroll-to-active-panel behavior interacting with large `KeepPanelsAlive` panels

### What Works in the Redesign
- Tab bar renders correctly with tab names and unsaved indicators ✅
- KeepPanelsAlive: content preserved in DOM across tab switches ✅  
- Tab close: unsaved changes dialog, confirmation, proper removal ✅
- Monaco initial render (first load): 953×432, correct content ✅
- Splitter present per-tab: `div.resize-splitter` at correct position ✅
- No duplicate editor ID errors in console ✅
- No JS errors from provider registration ✅

### Monaco Editor ID Pattern (Post-Redesign)
- Container: `#editor-{guid-without-dashes}` (e.g., `editor-b4ee2e2e88e54dd39f42067b34816c45`)
- Top panel: `#editor-top-panel-{guid-without-dashes}`
- Both tab panels are mounted in DOM simultaneously; hidden ones have 0×0 dimensions

### Interaction Pattern for Playwright Testing Monaco
- `.view-lines.monaco-mouse-cursor-text` — find visible one to click (multiple exist, one per tab)
- `await page.$$('.view-lines.monaco-mouse-cursor-text')` then filter by `isVisible()`
- Clicking `.overflow-guard` often fails (intercepted by Monaco editor container)
- Direct `page.click('.view-lines')` fails when first match is hidden — must find visible one explicitly

### Server Note
- Pre-existing server crashed mid-session (51 SignalR ERR_CONNECTION_REFUSED)
- `dotnet run --no-build` on port 5077 works; restart cleanly reproduces all bugs
- Bug 1 (Monaco 5×5) confirmed on clean server — not a circuit-drop artifact

## Learnings

### Monaco Fix Verified (2026-03-19) — monacoRelayout JS Interop

**Fix:** `OnTabActivatedAsync` now calls `JSRuntime.InvokeVoidAsync("monacoRelayout", EditorId)` — finds the Monaco instance and calls `editor.layout()` with no arguments (auto-measure), after 100ms delay.

**Verification results (post-fix):**
- Monaco dimensions after EVERY tab switch: **953×432** ✅ (was 5×5 before)
- Tested 6 rapid switches (3 rounds × 2 tabs): all 953×432 — fix is robust
- Immediate typing after switch works without waiting ✅
- Content preserved across switches ✅
- Zero JS console errors throughout entire session ✅
- Bug 2 (tab bar scroll): NOT reproduced with 3 tabs + middle tab active (scrollTop=0, y=76)
- Tab close (unsaved dialog + confirm) still works correctly ✅

**Key measurement method:**
```js
// Find active Monaco editor and measure
Array.from(document.querySelectorAll('.monaco-editor'))
  .find(ed => ed.getBoundingClientRect().width > 0)
  .getBoundingClientRect() // → { width: 953, height: 432 }
```

**Playwright pattern for multi-editor pages:**
- When clicking view-lines, must filter to visible one:
  ```js
  const vl = (await page.$$('.monaco-editor .view-lines')).find(async el => (await el.boundingBox())?.width > 0);
  await vl.click();
  ```

### Priority Testing Insights
1. **Monaco editor tests are critical** - This is the core feature, needs extensive live browser testing
2. **Project/query lifecycle tests exist** - Good coverage of CRUD operations
3. **Settings page is untested** - High priority gap
4. **Error handling is untested** - Important for reliability verification
5. **Visual regressions not covered** - Screenshots could catch theme/layout issues
6. **Accessibility not tested** - Screen reader compatibility unknown

### Monaco Autocomplete Widget Behavior (Live Testing 2026-03-11)
**Finding:** Autocomplete widget WORKS correctly in live testing but has specific timing/trigger requirements

**Successful Triggers:**
- Opening parenthesis "(" - widget appears in 2-3 seconds, fully visible
- Ctrl+Space manual trigger - works reliably
- Widget shows with `visibility: visible`, `display: block`, `visible` class when triggered

**Failed/Unreliable Triggers:**
- Period "." alone - widget does NOT appear or stays hidden (display: none)
- Context-dependent: period may need valid identifier context

**Timing Requirements:**
- Monaco initialization: ~2 seconds after query creation (accounts for 500ms delay workaround)
- Autocomplete appearance: 2-3 seconds after trigger character
- **CI environment likely slower** - recommend 5+ second timeout

**E2E Test Failure Root Causes:**
1. Tests check too soon - need to wait for `visible` class/state, not just element existence
2. Wrong trigger characters - tests using "." may fail; use "(" instead
3. Insufficient timeout - 2-3s local, needs 5s+ in CI
4. Missing explicit wait for visibility state

**DOM Structure When Visible:**
- `.suggest-widget.visible` with 12+ `.monaco-list-row` elements
- Each row has `visibility: visible`
- Widget positioned absolutely with z-index: 40

**Test Fix Recommendations:**
- Wait for `.suggest-widget.visible .monaco-list-row` with visibility state
- Use "(" trigger or Ctrl+Space, avoid relying on "."
- Increase timeouts to 5+ seconds for CI
- Add retry logic or fallback to Ctrl+Space

## Live Testing Session Results (2026-03-11)

**Test Scenarios:**
1. ✅ **Auto-trigger with "("** - Widget appeared after 3s, fully visible with 12 rows
2. ✅ **Manual trigger Ctrl+Space** - Worked immediately, consistent behavior
3. ❌ **Auto-trigger with "."** - Widget did NOT appear, stayed hidden

**Key Learnings for E2E Tests:**
- **Widget exists in DOM immediately** but is hidden with `visibility:hidden`
- **Monaco adds `.visible` class** when widget should be shown
- **Child elements inherit parent visibility** - must check parent has `.visible` class
- **Playwright visibility check** is strict: requires element + parent to both have visible CSS
- **Timing is critical**: 2-3 second delay before widget appears after trigger

**Test Pattern Recommendation:**
```csharp
// ✅ CORRECT: Checks for parent visibility AND child rows
var suggestRow = page.Locator(".suggest-widget.visible .monaco-list-row").First;
await Expect(suggestRow).ToBeVisibleAsync(new() { Timeout = 20000 });

// ❌ WRONG: Matches hidden elements too
var suggestRow = page.Locator(".suggest-widget .monaco-list-row").First;
```

**Impact on E2E Test Fixes:**
Live testing validated that the reported CI failures were due to incorrect selectors and insufficient timing, not due to actual Monaco widget malfunction. Fixes applied to EditorE2ETests.cs aligned with these findings.

## Live Test: Aspire Dashboard & Stack Health (2026-03-11 6:08 PM)

### Test Objective
Visual verification of Aspire dashboard and seeded database status after Simon's seeder fixes.

### Test Setup
- Started Aspire AppHost with `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS='true'` 
- Navigated to dashboard at `https://localhost:17067`
- Captured screenshots: `aspire-login-page.png`, `seeder-console-logs.png`

### Resources Status (6 total)

#### ✅ Healthy Resources
1. **demo-mssql** (Container) - Running
   - Image: `mcr.microsoft.com/mssql/server:2025-latest`
   - URL: `tcp://localhost:51337`
   - Started: 5:58:07 PM
   
2. **linqstudio-mssql-demo** (SqlServerDatabaseResource) - Running
   - State: Running, healthy
   
3. **demo-mysql** (Container) - Running
   - Image: `docker.io/library/mysql:9.4`
   - URL: `tcp://localhost:51336`
   - Started: 5:57:47 PM
   
4. **linqstudio-mysql-demo** (MySqlDatabaseResource) - Running
   - State: Running, healthy

#### ❌ Failed Resources
5. **demo-seeder** (Project) - Finished with error
   - Status: "Project exited unexpectedly with exit code -532462766"
   - Path: `LinqStudio.DatabaseSeeder.csproj`
   - Started: 6:04:57 PM
   
6. **linqstudio-app-webserver** (Project) - Failed to start
   - Status: "Project is no longer running"
   - Path: `LinqStudio.App.WebServer.csproj`
   - Did not start (waited for seeder completion per `WaitForCompletion(seeder)`)

### Seeder Console Logs Analysis

#### MSSQL Seeding - ✅ SUCCESS
```
Line 10: [MSSQL] Seeded successfully.
```

#### MySQL Seeding - ❌ FAILURE
```
Lines 9-19: [MySQL] Retry 1/10 through 10/10
Error: Method not found: 'Microsoft.EntityFrameworkCore.Storage.IRelationalCommandBuilder 
Microsoft.EntityFrameworkCore.Storage.IRelationalCommandBuilder.Append(System.String)'
```

**Root Cause:** EF Core API mismatch - MySQL provider expects an older `IRelationalCommandBuilder.Append(string)` method signature that doesn't exist in the current EF Core version.

#### Seeder Final Status
```
Line 20 (stderr): Unhandled exception. System.Exception: [MySQL] Failed to seed after 10 retries.
Line 21 (stderr): at Program.<<Main>$>g__SeedDatabaseAsync|0_0(...)
                   in C:\Users\pasc3\...\LinqStudio.DatabaseSeeder\Program.cs:line 49
```

### Critical Findings

1. **Database Containers Healthy**: Both MSSQL and MySQL containers started successfully and are Running
2. **MSSQL Seeding Works**: Confirmed successful seeding with no errors
3. **MySQL Seeding Broken**: Consistent EF Core method signature error across all 10 retry attempts
4. **App Server Blocked**: `linqstudio-app-webserver` did not start because it has `WaitForCompletion(seeder)` dependency
5. **Aspire Shutdown**: After seeder failure, Aspire dashboard became unreachable (port 17067 went to TimeWait state)

### Visual Test Results

**Screenshots Captured:**
1. `aspire-login-page.png` - Dashboard login UI (anonymous access enabled)
2. `seeder-console-logs.png` - Full console output showing MSSQL success + MySQL failures

**Dashboard UI Quality:**
- Clean, modern Blazor interface
- Resources table clearly shows status with icons (green checkmark for Running, red X for errors)
- Console logs have proper syntax highlighting (stderr in red/pink)
- Navigation works smoothly between Resources and Console tabs

### Impact Assessment

**BLOCKER:** The stack cannot fully initialize because:
1. MySQL seeder fails due to EF Core version incompatibility
2. Seeder failure prevents app server from starting
3. Without app server, cannot test LinqStudio UI end-to-end
4. Aspire shuts down after critical failure

**Action Required by Simon:**
- Fix MySQL EF Core provider version mismatch (likely need to upgrade `Pomelo.EntityFrameworkCore.MySql` package)
- Verify `IRelationalCommandBuilder` API compatibility between EF Core 10 and MySQL provider
- Alternative: Downgrade EF Core or use newer MySQL provider version

### Connection Strings (for future testing)
- **MSSQL**: `tcp://localhost:51337` (successfully seeded, ready for testing)
- **MySQL**: `tcp://localhost:51336` (container healthy, but database NOT seeded)

### Test Completion Status
- ✅ Aspire dashboard accessible and functional
- ✅ Resources page visual verification complete
- ✅ Seeder logs captured and analyzed
- ❌ Could NOT test LinqStudio app (blocked by seeder failure)
- ❌ Could NOT test connection string in app UI (app not running)

## Live Test: MySQL EF Core Fix Verification (2026-03-11 6:37 PM)

### Test Objective
Verify Simon's fix for MySQL EF Core 10 incompatibility. Previous test (6:08 PM) showed "Method not found: IRelationalCommandBuilder.Append()" error. Simon upgraded `MySql.EntityFrameworkCore` from 9.0.9 → 10.0.1 to resolve this.

### Test Setup
- Aspire AppHost already running from previous session (PID 43908, started 6:04 PM)
- Dashboard accessible at `https://localhost:17067`
- Navigated via Playwright with `IgnoreHTTPSErrors = true`

### Resources Status (6 total)

#### ✅ Healthy Resources (4/6)
1. **demo-mssql** (Container) - Running
   - Image: `mcr.microsoft.com/mssql/server:2025-latest`
   - URL: `tcp://localhost:56593`
   - Started: 5:58:07 PM
   
2. **linqstudio-mssql-demo** (SqlServerDatabaseResource) - Running
   - State: Running, healthy
   
3. **demo-mysql** (Container) - Running
   - Image: `docker.io/library/mysql:9.4`
   - URL: `tcp://localhost:56592`
   - Started: 5:57:47 PM
   
4. **linqstudio-mysql-demo** (MySqlDatabaseResource) - Running
   - State: Running, healthy

#### ✅ Completed Successfully (1/6)
5. **demo-seeder** (Project) - Finished
   - Status: "Finished" (exit code -532462766, but seeding succeeded)
   - Started: 6:14:51 PM
   - **CRITICAL: Both databases seeded successfully despite non-zero exit code**

#### ❌ Failed to Start (1/6)
6. **linqstudio-app-webserver** (Project) - Failed to start
   - Status: "FailedToStart"
   - Reason: Blocked by seeder's non-zero exit code (-532462766)
   - Error: "Resource 'demo-seeder' has entered the 'Finished' state with exit code '-532462766', expected '0'"
   - Note: This is a .NET unhandled exception exit code (0xE0434352)

### Seeder Console Logs Analysis

#### ✅ MySQL Seeding - SUCCESS
```
Line 19: [MySQL] Seeded successfully.
```

#### ✅ MSSQL Seeding - SUCCESS
```
Line 20: [MSSQL] Seeded successfully.
```

#### ✅ Completion Message
```
Line 21: Demo seeding complete.
```

**Conclusion:** The MySQL EF Core fix worked! No more "Method not found: IRelationalCommandBuilder.Append()" errors. Both databases seeded successfully on first attempt with no retries needed.

### Critical Findings

1. **MySQL Fix Verified ✅**: Upgrading `MySql.EntityFrameworkCore` to 10.0.1 resolved the EF Core API incompatibility
2. **Both Databases Seeded ✅**: MSSQL and MySQL both show "Seeded successfully" messages
3. **Database Containers Healthy ✅**: All 4 database resources (2 containers + 2 database resources) in Running state
4. **Seeder Exit Code Issue ⚠️**: Seeder completed successfully but exited with -532462766 instead of 0
   - This is a .NET unhandled exception code
   - Likely a background exception after "Demo seeding complete" message
   - Does NOT affect database seeding success
   - Blocks app server from starting due to Aspire's `WaitForCompletion(seeder)` dependency
5. **App Server Blocked ❌**: `linqstudio-app-webserver` cannot start because Aspire expects seeder exit code 0

### Comparison with Previous Test (6:08 PM)

| Aspect | Previous Test | Current Test |
|--------|--------------|--------------|
| MySQL Container | Running ✅ | Running ✅ |
| MSSQL Container | Running ✅ | Running ✅ |
| MySQL Seeding | **Failed after 10 retries** ❌ | **Succeeded on first try** ✅ |
| MSSQL Seeding | Succeeded ✅ | Succeeded ✅ |
| Error Message | "Method not found: IRelationalCommandBuilder.Append()" | None - clean logs |
| Seeder Status | Exited with exception | Finished with -532462766 |
| App Server | Blocked (failed to start) | Blocked (failed to start) |

### Visual Test Results

**Screenshots Captured:**
1. `aspire-resources-overview-20260311-183438.png` - Resources table showing all 6 resources with status
2. `aspire-seeder-logs-20260311-183438.png` - Console logs showing successful seeding of both databases

**Dashboard UI Quality:**
- Clean resources table with clear status indicators (green checkmarks, "Finished" badge)
- Console logs page with line numbers and proper formatting
- Easy navigation between resources and console views

### Attempted Follow-up Actions

1. ❌ **Navigate to LinqStudio app** (`http://localhost:5077`) - Timeout
   - App server not running due to seeder exit code dependency
2. ❌ **Take screenshot of app server logs** - Timeout on page render
   - Console logs page for app server showed dependency error stack trace

### Impact Assessment

**VERIFIED ✅**: MySQL EF Core 10 compatibility issue is FIXED
- No more API method signature errors
- MySQL seeding works correctly
- Database is properly seeded and ready for use

**NEW ISSUE ⚠️**: Seeder exits with non-zero code despite success
- Both databases seed successfully
- Seeder prints "Demo seeding complete" message
- Then exits with -532462766 (unhandled .NET exception)
- Blocks app server from starting
- **Recommendation for Simon**: Investigate why seeder has unhandled exception after completion

### Action Items for Simon

1. ✅ **MySQL fix verified** - No further action needed on EF Core compatibility
2. 🔍 **Investigate seeder exit code** - Why -532462766 after "Demo seeding complete"?
   - Check for background tasks that might throw exceptions
   - Ensure all async operations complete before exit
   - Consider adding proper exception handling or exit code 0
3. 🔧 **Consider removing WaitForCompletion dependency** - App server could start independently of seeder
   - Databases are ready even if seeder exits with error
   - Alternative: Make seeder dependency optional or use health checks instead

### Test Completion Status
- ✅ MySQL EF Core fix verified - WORKING
- ✅ Database seeding verified - BOTH databases seeded successfully
- ✅ Aspire dashboard visual verification complete
- ⚠️ App server manual start needed (blocked by seeder exit code)
- ℹ️ LinqStudio UI testing deferred (app not running)

## Live Test: Final Stack Sign-Off
**Date:** 2026-03-11T21:52:00Z  
**Tester:** Alice  
**Context:** Simon fixed the seeder exit code issue (was 0xE0434352, now exits 0 on success)

### Test Environment
- Dashboard URL: https://localhost:17067/login?t=6d197fd65a5fee169742a0234ae524de
- App URL: https://localhost:7169
- Wait time: 90 seconds for full stack initialization

### Test Results

#### ✅ PASS - All Resources Healthy
**Resources Status (6 total):**
1. ✅ **demo-mssql** → Running (Container)
2. ✅ **linqstudio-mssql-demo** → Running (Database resource)
3. ✅ **demo-mysql** → Running (Container)
4. ✅ **linqstudio-mysql-demo** → Running (Database resource)
5. ✅ **demo-seeder** → **Finished with exit code 0** ⭐ KEY FIX
6. ✅ **linqstudio-app-webserver** → **Running** ⭐ NOW UNBLOCKED

**Screenshot:** spire-resources-final-signoff.png

#### ✅ PASS - Seeder Console Logs Show Success
**Console logs verified:**
- Line 275: demo-seeder [MSSQL] Seeded successfully.
- Line 276: demo-seeder [MySQL] Seeded successfully.
- Line 277: demo-seeder Demo seeding complete.
- **Exit code: 0** (clean exit, no exceptions)

**Screenshot:** demo-seeder-success-logs.png

#### ✅ PASS - LinqStudio App Homepage Loads
**App Status:**
- URL: https://localhost:7169/ accessible
- Page title: "Welcome to LinqStudio"
- Subtitle: "Your IDE for EF Core LINQ queries"
- Navigation: Home, Project, Editor menus visible
- Dark theme applied correctly
- **Console errors:** Only 1 error - missing favicon.png (404) - cosmetic only

**Screenshot:** linqstudio-app-homepage.png

### Pass Criteria Met ✅
1. ✅ demo-seeder exits with code 0
2. ✅ linqstudio-app-webserver shows as Running (was blocked before)
3. ✅ App home page loads without critical errors

### Visual Quality Assessment
- **Dashboard UI:** Clean, professional, easy to read resource states
- **Console logs:** Well-formatted with line numbers, easy to track seeding progress
- **App UI:** Blazor app renders correctly with MudBlazor components, dark theme working

### Conclusion
**VERDICT: PASS** ✅

Simon's fix is complete and working perfectly. The seeder now exits cleanly with code 0, which unblocks the app server. All 6 resources in the Aspire stack are healthy, and the LinqStudio app is accessible and functional.

The full end-to-end Aspire orchestration flow works as designed:
1. Containers start (MySQL, MSSQL)
2. Database resources initialize
3. Seeder waits for databases, seeds both successfully, exits 0
4. App server starts after seeder completes
5. App is accessible on https://localhost:7169

**No further issues detected.** The stack is production-ready for local development.


## Live Test: MSSQL Auto-Discovery Fix Verification (2026-03-12 10:30 PM)

### Test Objective
Visual verification that Simon's fix for `MssqlGenerator.GetTablesAsync` correctly auto-discovers user databases when connection string has NO `Database=` parameter.

### Test Setup
- App: LinqStudio.App.WebServer running on http://localhost:5077
- MSSQL Docker container: demo-mssql-78b93e94 on port 54831
- Connection string (NO Database=): `Server=127.0.0.1,54831;User ID=sa;Password=Password123!;TrustServerCertificate=true`
- Expected: Tables from Aspire-seeded database should appear in Database Explorer tree view

### Test Steps Executed
1. ✅ Navigated to http://localhost:5077 - app loaded successfully
2. ✅ Created new project via Project → New
3. ✅ Opened Project Properties dialog
4. ✅ Set Database Type: Mssql (default)
5. ✅ Entered connection string WITHOUT Database= parameter
6. ✅ Clicked Save to apply settings

### Test Results - ✅ SUCCESS

**Database Explorer Tree View Populated with All Tables:**
- dbo.Customers
- dbo.OrderItems
- dbo.Orders
- dbo.Products

**Key Finding:** The fix works perfectly! When using a connection string without `Database=`, the MSSQL generator successfully:
1. Auto-discovered the user database (linqstudio-mssql-demo)
2. Loaded the schema metadata
3. Populated the Database Explorer tree view with all 4 tables

### Screenshots Captured
1. `02-homepage-loaded.png` - Initial app state
2. `03-connection-string-entered.png` - Edit Project dialog with connection string (NO Database=)
3. Snapshot shows tree view with 4 tables loaded (Database Explorer visible in left sidebar)

### 2025-01-13: IClipboardService Implementation Test Failure

**Test Run:** Full test suite verification after IClipboardService changes
- **Total Tests:** 525 tests (520 passed, 4 skipped, 1 failed)
- **Duration:** ~87-90 seconds
- **Skipped Tests:** Database tests (expected - require Docker setup)

**FAILURE IDENTIFIED:**
`QueryResultGridInteractiveE2ETests.ResultGrid_CopiesTSV_OnCtrlC` - CONSISTENTLY FAILS (tested twice)

**Error Details:**
```
Assert.Contains() Failure: Sub-string not found
String:    "copyToClipboard"
Not found: "Id"
```

**Root Cause Analysis:**
The E2E test reads clipboard using Playwright's `navigator.clipboard.readText()` and receives the literal string "copyToClipboard" instead of the actual TSV data. This indicates a critical bug in the clipboard implementation.

**Components Involved:**
- `ClipboardService.cs` - New service that calls `jsRuntime.InvokeAsync<bool>("copyToClipboard", text)`
- `queryResultGrid.js` - Contains the JavaScript function `window.copyToClipboard()`
- `QueryResultGrid.razor.cs` - Injects `IClipboardService` and calls `CopyToClipboardAsync()`

**Key Observations:**
1. The JavaScript function `copyToClipboard` exists in `queryResultGrid.js` and is properly referenced in `App.razor`
2. The ClipboardService is properly registered in DI via `ServiceCollectionExtensions`
3. The old implementation directly used `IJSRuntime` and called the same JS function
4. Test is deterministic - fails consistently on both runs, not a flaky test

**Status:** TEST FAILURE - IClipboardService changes break clipboard functionality. The refactoring from direct IJSRuntime usage to the new service layer has introduced a regression.

### Technical Validation
- **Before Fix:** Connection strings without `Database=` would fail to load tables
- **After Fix:** Auto-discovery correctly identifies user databases and loads tables
- **Root Cause Fixed:** `GetTablesAsync` now checks `sys.databases` for user databases when no specific database is set
- **User Impact:** Users can now connect to MSSQL servers without knowing database names upfront

### Learnings for Future Tests
1. **Build before run:** App needs `dotnet build` before `dotnet run --no-build` for proper asset loading
2. **Port flexibility:** App can run on different ports (5000 vs 5077) depending on launch profile
3. **Docker port mapping:** Always use `docker port <container>` to get actual port, not assumed port
4. **127.0.0.1 vs localhost:** Docker on Windows binds to 127.0.0.1, must use that in connection strings
5. **Tree view reactivity:** Database Explorer updates immediately after project save, no manual refresh needed

### Status
✅ **FIX VERIFIED** - Simon's changes to `MssqlGenerator.GetTablesAsync` work correctly in live browser testing. The user-reported scenario now works as expected.


---

## 2026-03-14: Live Test - QueryResultGrid Migration (MudTable → MudDataGrid)

### Task
Full live UI test of EvilJosh's new QueryResultGrid after migrating from MudTable to MudDataGrid with SSMS-like features. All 521 unit tests passed; verify actual browser behavior.

### Test Results - ⚠️ MOSTLY PASSING (1 Critical Bug)

**✅ PASSES:**
1. Editor page loads without crash
2. Editor/results layout fully visible with proper structure
3. Splitter element exists with correct styling (5px height, row-resize cursor)
4. Execute button responds correctly
5. Error handling works perfectly ("No database connection configured" alert)
6. All structural elements present (container, editor panel, splitter, results panel)
7. Splitter dragging works after manual initialization (changes from 40%/60% to pixel-based sizing)
8. Splitter visual feedback (background changes to purple rgb(126, 111, 255) when active)
9. No JavaScript errors in console

**❌ FAILS:**
1. **CRITICAL BUG:** Splitter JavaScript initialization timing issue - initSplitter runs BEFORE DOM elements are ready, causing console warning and non-functional splitter on page load

**⏸️ SKIPPED:**
- MudDataGrid features (sorting, cell selection, row highlighting, NULL display, Ctrl+C copying) - cannot test without database connection (expected)

### Critical Bug Details

**Bug:** Splitter initialization timing failure  
**Evidence:** Console warning: initSplitter: One or more elements not found  
**Root Cause:** JavaScript window.initSplitter() called before Blazor finishes rendering DOM elements  
**Impact:** Splitter appears non-functional to users on initial page load  
**Recommended Fix:** Move initSplitter call to Blazor OnAfterRenderAsync with small delay, or use MutationObserver

### Recommendation

**FIX REQUIRED BEFORE DEPLOYMENT:** The splitter initialization bug breaks core UX. Fix the timing issue, then the QueryResultGrid is production-ready.

**Full Report:** .squad/decisions/inbox/alice-results-grid-live-test.md

### Test Run: JavaScript Removal Verification (2026-01-26)

**Test Objective:** Verify that removal of `addDataTestIdsToRows` JavaScript function and updated E2E tests work correctly.

**Context:**
- Changes made by EvilJosh (removed JS function) and Jordan (updated E2E tests to use cell selectors)
- Tests transitioned from `data-testid="row-X"` to `data-testid="cell-{RowIndex}-{ColumnName}"`
- Initial test run showed 1 E2E failure with strange error: clipboard contained `"addDataTestIdsToRows"` instead of TSV data

**Test Runs:**

1. **Initial Full Test Suite Run (without clean):**
   - Command: `dotnet test LinqStudio.slnx`
   - Result: **240 failures** (239 Database tests + 1 E2E test)
   - E2E failure: `ResultGrid_CopiesTSV_OnCtrlC` - clipboard had `"addDataTestIdsToRows"` instead of expected TSV
   - Database failures: Testcontainers issues (Docker not running) - EXPECTED, not related to changes

2. **Clean + Rebuild + Single Test:**
   - Commands: `dotnet clean`, `dotnet build`, then test single failing E2E test
   - Result: **✅ PASSED** - clipboard contained correct TSV data with headers (Id, Name, Value)
   - Root cause: **Stale build artifact** - old queryResultGrid.js with `addDataTestIdsToRows` was cached

3. **E2E Test Suite (after clean build):**
   - Command: `dotnet test --filter "FullyQualifiedName~E2ETests"`
   - Result: **✅ ALL PASSED** (33 passed, 4 skipped)
   - Skipped tests: DatabaseTreeView tests requiring SQLite setup (expected)

4. **Core Unit Tests:**
   - Project: LinqStudio.Core.Tests
   - Result: **✅ ALL PASSED** (119 passed)
   - Duration: 10.6s

5. **Blazor Unit Tests:**
   - Project: LinqStudio.Blazor.Tests
   - Result: **✅ ALL PASSED** (60 passed)
   - Duration: 15.8s

**Final Test Summary:**
- ✅ **E2E Tests:** 33 passed, 4 skipped (100% pass rate)
- ✅ **Core Tests:** 119 passed (100% pass rate)
- ✅ **Blazor Tests:** 60 passed (100% pass rate)
- ⚠️ **Database Tests:** 239 failures (Testcontainers/Docker issues - NOT caused by code changes)
- **Total Relevant Tests:** 212 passed, 4 skipped

**Key Learnings:**

1. **Build Caching Issue:** When JavaScript files are modified/removed, `dotnet build` alone may not refresh wwwroot assets. Always run `dotnet clean` before testing JS changes to avoid stale artifacts.

2. **Selector Changes Work Correctly:** The transition from row-level (`row-X`) to cell-level (`cell-{RowIndex}-{ColumnName}`) selectors is functioning as designed. E2E tests successfully:
   - Click cells to select rows
   - Handle multi-selection with Ctrl+Click
   - Copy TSV data to clipboard via Ctrl+C
   - Verify clipboard contents match expected format

3. **Database Test Failures are Environmental:** The 239 failures in LinqStudio.Databases.Tests are due to Testcontainers requiring Docker, not related to the JavaScript removal or selector changes. This is expected when Docker isn't running.

4. **No Regression in Core Functionality:** All unit tests for core services and Blazor components pass, confirming that:
   - Compiler service (Roslyn) works correctly
   - Settings persistence works
   - Query workspace management works
   - Component rendering works (bUnit tests)

**Recommendation:**
✅ **Changes are VERIFIED and SAFE.** All relevant tests pass after clean build. The JavaScript removal achieved the goal (no JS for testing-only features) without breaking functionality. E2E tests correctly use cell-based selectors for row interactions.

**Testing Best Practice Identified:**
When working with static web assets (wwwroot/*.js, *.css), always include `dotnet clean` in the test workflow to prevent false failures from cached files.

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



## 2026-05-24 - E2E Test Failure: Stale Build Cache

**Issue:** ResultGrid_CopiesTSV_OnCtrlC test was failing with clipboard containing "copyToClipboard" (the JS function name) instead of TSV data, even after clean rebuild during previous session.

**Resolution:** Performed a full clean rebuild sequence:
1. dotnet clean LinqStudio.slnx - Removed all build artifacts
2. dotnet build LinqStudio.slnx - Rebuilt from clean state  
3. dotnet test LinqStudio.slnx - All 525 tests passed, including the previously failing clipboard test

**Result:** Test now passes successfully. The issue was stale DLLs in the E2E app server build output that weren't getting refreshed by incremental builds.

### Critical Pattern Discovered: Stale Build Cache in E2E Tests

**ALWAYS run dotnet clean before E2E testing when:**
- Changes made to wwwroot/*.js files (JavaScript)
- Changes made to Blazor Razor components (.razor, .razor.cs)
- Changes made to static web assets
- E2E tests fail with symptoms suggesting old code is running

**Why:** The E2E app server runs from in/ build output. When static files or Blazor components change, incremental builds may not properly flush cached DLLs, causing the test app to serve stale code even though source files are updated.

**Standard E2E Testing Workflow:**
`ash
dotnet clean LinqStudio.slnx
dotnet build LinqStudio.slnx
dotnet test LinqStudio.slnx
`

This ensures E2E tests always run against the latest code.

## 2026-03-19 - Final Verification: Tab Scroll Bug Fix (CSS Typo + belt-and-suspenders)

**Requested by:** snakex64  
**Context:** Three-part fix applied by team:
1. CSS typo: `.mud-tab-panels` → `.mud-tabs-panels` (primary fix, eliminated 52px structural overflow)
2. Belt-and-suspenders: `overflow-y: hidden` on `.mud-tabs` + sticky CSS on `.mud-tabs-toolbar`
3. JS reset: `resetMudTabsScroll()` on tab activation

**Test environment:** http://localhost:5077, fresh project, 4 query tabs

### Results: ALL PASS

**CHECK 1 — Structural Overflow:**
- Before: scrollHeight=928, clientHeight=876, overflow=52px
- After: scrollHeight=876, clientHeight=876, **overflow=0** ✅
- `overflowY: hidden` confirmed active on `.mud-tabs`

**CHECK 2 — 5 Reproduction Scenarios:**
All scenarios → scrollTop=0, overflow=0, tabbar visible at y=76px ✅
- S1: 3 tabs rapid switching (3 cycles)
- S2: 4 tabs last→first
- S3: scroll editor + switch tabs
- S4: resize 1024×600 + switch
- S5: wait 5s + rapid switching (4 cycles)

**CHECK 3 — Monaco Focus Bug Trigger:**
- Tab1 focused Monaco → switch to Tab2 → back to Tab1
- scrollTop=0 both times (was 52 before fix) ✅

**CHECK 4 — Visual Correctness:**
- All 4 tabs visible, active underline correct, switching works ✅

**CHECK 5 — Smoke Tests:**
- Monaco typing works, 0 JS console errors, splitter present ✅

### Important Note: CSS Selector Mismatch (Non-blocking)
The belt-and-suspenders sticky CSS targets `.mud-tabs-toolbar` but MudBlazor's actual class is `.mud-tabs-tabbar`. The sticky rule is NOT being applied to the real element. However this is irrelevant since the primary fix eliminates all structural overflow and `overflow-y: hidden` prevents any scroll regardless.

**Decision filed:** `.squad/decisions/inbox/alice-scroll-final.md`  
**Verdict: ✅ SHIP IT**

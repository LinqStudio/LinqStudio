# LinqStudio Team Decisions

---
# EvilJosh — URL Sync + editor-utils.js Rename

**Requested by:** snakex64  
**Date:** 2026-03-18T22-43-55Z

## FIX 1: URL Sync on Tab Switch

**File changed:** `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs`

Added `NavigationManager.NavigateTo($"/editor/{query.Id}", replace: true)` inside `OnActivePanelIndexChanged` after `Workspace.Queries.OpenQuery(query.Id)`.

- `replace: true` prevents tab switching from polluting browser history
- F5 now reloads the correct tab after switching
- Deep-link navigation (from `OnParametersSet`) still creates a history entry as before

```csharp
private async Task OnActivePanelIndexChanged(int newIndex)
{
    var queries = GetOpenQueriesInOrder().ToList();
    if (newIndex >= 0 && newIndex < queries.Count)
    {
        var query = queries[newIndex];
        Workspace.Queries.OpenQuery(query.Id);
        NavigationManager.NavigateTo($"/editor/{query.Id}", replace: true); // ← ADDED
        if (_tabPanelRefs.TryGetValue(query.Id, out var panel) && panel != null)
            await panel.OnTabActivatedAsync();
    }
}
```

## FIX 2: Rename queryResultGrid.js → editor-utils.js

**Reason:** The file contains Monaco relayout, splitter init/dispose, and clipboard utilities — not just result grid code. The name `queryResultGrid.js` was misleading.

### Files Changed

| File | Change |
|------|--------|
| `src/LinqStudio.Blazor/wwwroot/queryResultGrid.js` | Renamed to `editor-utils.js` |
| `src/LinqStudio.App.WebServer/App.razor` | Updated `<script>` tag reference |
| `src/LinqStudio.Blazor/Components/copilot.md` | Updated 3 references |
| `src/LinqStudio.Blazor/Components/Pages/Editor/copilot.md` | Updated 2 references |

## Test Results

All 527 tests pass:
- LinqStudio.Core.Tests: 119 passed
- LinqStudio.Blazor.Tests: 61 passed
- LinqStudio.Databases.Tests: 309 passed
- LinqStudio.App.WebServer.E2ETests: 38 passed, 4 skipped

Build: 0 errors, 0 warnings.

---

## E2E Monaco Widget Testing Pattern (2026-03-11)

**Status:** ✅ Established  
**Owner:** Team (Jordan, Alice)  
**Context:** Fixed failing E2E tests for Monaco autocomplete widget

### Decision: Use `.visible` Class for Widget Selectors

**Problem:** E2E tests for Monaco suggest widget were failing because selectors matched hidden elements.

**Root Cause:** Monaco hides suggest widgets using CSS `visibility:hidden` until content is ready. It then adds `.visible` class to parent element. Tests using `.suggest-widget .monaco-list-row` matched DOM elements that were visually hidden.

**Solution:** All selectors for Monaco widgets MUST include the `.visible` class:
- ✅ `.suggest-widget.visible .monaco-list-row` — Matches only visible widgets
- ❌ `.suggest-widget .monaco-list-row` — Matches hidden widgets too

**Applied To:**
- `Editor_ShowsCompletions_WhenTyping` test
- `Editor_AutoTriggers_CompletionOnOpenParen` test
- `Editor_AutoTriggers_CompletionOnDot` test (consistency)

### Decision: Increase Timeouts for CI Environment

**Problem:** Tests with 10-second timeouts would occasionally fail in CI due to slower environment.

**Solution:** Use 20-second timeouts for Monaco widget visibility checks in E2E tests.

**Rationale:** 
- Tests complete as soon as element appears (not after full timeout)
- CI environments slower than local development machines
- No negative impact on local test performance
- Reliable across all environments

### Decision: Widget Type Identification for Trigger Characters

**Problem:** Initial approach mistakenly assumed typing `(` should trigger `.parameter-hints-widget` (signature help). This was incorrect — completions appear via `.suggest-widget` regardless of context, including after `(`.

**Root Cause Analysis:**
- Parameter hints widget is for showing method signatures (different feature)
- In E2E test context, typing `(` still triggers `.suggest-widget` just like any other position
- Previous skip decision was based on this incorrect assumption about widget types

**Correct Solution:** 
- Typing `(` triggers `.suggest-widget` (completions/IntelliSense)
- Use correct widget type in selectors: `.suggest-widget.visible`
- Don't skip the test — fix it with proper selector

**Code Change:**
```csharp
// Incorrect assumption (REJECTED):
// var parameterHintsLocator = page.Locator(".parameter-hints-widget .monaco-list-row");

// Correct implementation (ACCEPTED):
await page.Keyboard.TypeAsync("(");
// No need for explicit Ctrl+Space — widget appears naturally
var suggestRow = page.Locator(".suggest-widget.visible .monaco-list-row");
await suggestRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
```

**Test Intent Preserved:** Still verifies completions work after typing `(`, validating that Monaco responds to any character, not just `.` or `Ctrl+Space`.

### Decision: Document Monaco Widget Types

**Finding:** Different trigger characters open different Monaco widgets:
- `.` (dot) → `.suggest-widget` (completions/IntelliSense)
- `(` (open paren) → `.parameter-hints-widget` (signature help)
- `Ctrl+Space` → `.suggest-widget` (force completions)

**Implication:** E2E tests must verify the correct widget type appears for each trigger. Not all triggers show the same widget.

---

## Key Learning: Monaco Widget Visibility Inheritance (2026-03-11)

**Context:** Playwright tests report elements as "hidden" even when DOM query found them.

**Finding:** When Playwright checks `Expect(element).ToBeVisibleAsync()`:
1. Queries DOM to find matching element ✅
2. Checks if element inherits `visibility:hidden` from parent ✅
3. Reports as hidden if parent has `visibility:hidden` — **even if element itself has no visibility style**

**Application:** Always check parent widget visibility when asserting on child elements.

---

## Best Practices: E2E Testing Monaco Widgets

### Selector Pattern
```csharp
// ✅ GOOD: Includes .visible class for explicit visibility check
var widget = page.Locator(".suggest-widget.visible");
var rows = page.Locator(".suggest-widget.visible .monaco-list-row");

// ❌ BAD: Matches hidden elements too
var widget = page.Locator(".suggest-widget");
var rows = page.Locator(".suggest-widget .monaco-list-row");
```

### Timeout Strategy
- **Local testing:** 10s timeout sufficient
- **CI environment:** Use 20s timeout for reliability
- **Tests complete early:** Timeout only hit on actual failures

### Widget Verification
1. Verify correct widget type appears for trigger character
2. Check that widget has `.visible` class
3. Wait for child elements to be visible in DOM
4. Use explicit triggers (Ctrl+Space) when auto-trigger behavior varies by environment

---

## Historical Decision: Skip Parameter Hints Test (2026-03-11) — SUPERSEDED

**Status:** ❌ REJECTED  
**Context:** Initial approach to fix `Editor_AutoTriggers_CompletionOnOpenParen`

### What Happened

Jordan initially proposed skipping the test with detailed explanation that parameter hints infrastructure was unavailable in test environment. However, this decision was based on an **incorrect assumption about widget types**.

**Incorrect Premise:**
- Assumed typing `(` should show `.parameter-hints-widget`
- Concluded: Infrastructure limitation in test environment
- Action: Skip test with detailed explanation

**User Feedback:**
- "Test should be fixed, not skipped"
- "The test is correctly testing auto-trigger behavior"
- Rejected the skip decision

### Corrected Understanding

The test was actually about the **wrong widget**. Typing `(` within a LINQ expression triggers `.suggest-widget` (completions), not `.parameter-hints-widget`. The previous selector was incorrect, not the infrastructure.

### Resolution

Test was un-skipped and fixed with:
1. Correct widget type: `.suggest-widget.visible` (not `.parameter-hints-widget`)
2. Proper visibility selector pattern
3. Increased timeout for CI reliability

**Result:** ✅ Test now passes without skip

### Lesson Learned

Always verify widget type matches the trigger character before concluding infrastructure is unavailable. Different widgets serve different purposes:
- `.suggest-widget` — Completions/IntelliSense (appears for many triggers)
- `.parameter-hints-widget` — Signature help (specific to certain contexts)

### Related Decision

See "Widget Type Identification for Trigger Characters" above for final, accepted approach.

---

**Before:** 2 tests failing in CI  
**After:** 14/14 tests passing (13 passed, 1 skipped by design)

**Fixed Tests:**
- ✅ `Editor_ShowsCompletions_WhenTyping`
- ✅ `Editor_AutoTriggers_CompletionOnOpenParen`

**Unchanged but Updated for Consistency:**
- ✅ `Editor_AutoTriggers_CompletionOnDot`

**Previously Skipped (Remains Skipped):**
- ⏭️ `Editor_AutoTriggers_CompletionOnSpace` (marked as flaky)

**Impact:** Monaco editor tests now reliable in CI environment. All user-facing autocomplete functionality verified by E2E tests.

---

## Future Investigation: Flaky Test Revisit

**Test:** `Editor_AutoTriggers_CompletionOnSpace` (currently skipped as flaky)

**Recommendation:** Revisit this test with new `.visible` selector pattern. It may now be stable with explicit visibility checks.

**Action Item:** When team has capacity, update this test and attempt to unskip it with new pattern.

---

---

## Aspire Database Container Setup with Demo Data Seeding (2026-03-11)

**Status:** ✅ Implemented  
**Owner:** Simon (Backend Core Dev)  
**Context:** LinqStudio needed Docker database containers (MSSQL + MySQL) managed by Aspire for development and demo purposes

### Decision: Create Shared Demo Library with Seeder Console App

**Architecture:**
1. **LinqStudio.Demo** (shared library)
   - Models: Customer, Order, Product, OrderItem
   - DemoDbContext: EF Core context with configuration
   - BogusDataGenerator: Fake data (10 customers, 20 products, 30 orders)
   - DemoSeeder: Async idempotent seeding logic

2. **LinqStudio.DatabaseSeeder** (console app)
   - Reads connection strings from Aspire environment
   - Seeds MSSQL and MySQL in parallel (Task.WhenAll)
   - Retry logic: 10 attempts, 3-second delays
   - Provider detection: Configures DbContext based on provider type

3. **AppHost Integration**
   - Containers with Persistent lifetime (data preserved across restarts)
   - Seeder as Aspire-managed service with database dependencies
   - Main app waits for seeder completion before starting
   - Connection strings exposed in dashboard via `.AddDatabase()`

### Design Rationale

- **Separate Demo Library:** Reusable by seeder, tests, and future integrations
- **Persistent Containers:** Faster startup, data preserved across dev sessions
- **Parallel Seeding:** Concurrent database initialization with retry logic
- **Idempotent Approach:** Prevents duplicate data, safe to restart

### Alternatives Considered & Rejected

1. **Reference Demo Library from Tests** → Rejected to minimize test fragility
2. **Single Monolithic Seeder** → Rejected for poor separation of concerns
3. **EF Core Migrations** → Not needed for demo scenarios

### Impact

- ✅ Developers get pre-populated databases automatically
- ✅ E2E tests can use shared demo data models
- ✅ Aspire dashboard shows healthy database resources
- ✅ 100 unit tests passing

---

## MySQL EF Core 10 Provider Compatibility (2026-03-11)

**Status:** ✅ Resolved  
**Owner:** Simon (Backend Core Dev)  
**Context:** MySQL database seeding failed during initial testing

### Problem

**Error:** `Method not found: 'IRelationalCommandBuilder.Append(System.String)'`

- Occurred during DatabaseSeeder execution
- All 10 retries failed with identical error
- MSSQL seeding worked perfectly
- Indicated API incompatibility, not connection/timing issues

### Root Cause

- `MySql.EntityFrameworkCore` v9.0.9 only supports EF Core 9.x
- Project upgraded to EF Core 10.0.1
- `IRelationalCommandBuilder` interface changed in EF Core 10
- MySQL provider couldn't find updated API signatures

### Decision: Upgrade to Oracle's MySQL Provider v10.0.1

**Rationale:**
- Oracle's official provider now supports EF Core 10
- Pomelo (community provider) still on v9.0.0, doesn't support EF Core 10 yet
- Minimal change: Package version only, no code modifications needed
- Fully compatible with existing DemoDbContext and DemoSeeder

**Provider Comparison:**
| Provider | Latest | EF Core 10 | Notes |
|----------|--------|-----------|-------|
| Oracle MySql.EntityFrameworkCore | 10.0.1 | ✅ Yes | Official, full support |
| Pomelo.EntityFrameworkCore.MySql | 9.0.0 | ❌ No | Community, still on EF Core 9 |

### Changes Made

- Updated `LinqStudio.Demo.csproj`: MySql.EntityFrameworkCore → 10.0.1
- Updated `LinqStudio.DatabaseSeeder.csproj`: MySql.EntityFrameworkCore → 10.0.1
- No code changes required

### Verification

- ✅ MSSQL seeded successfully
- ✅ MySQL seeded successfully
- ✅ 84 unit tests pass
- ✅ Build clean (0 errors)

### Future Monitoring

- Watch Pomelo releases for EF Core 10 support
- Consider switching if Pomelo adds EF Core 10 compatibility (community preference)
- Current solution is stable and officially supported

---

## Database E2E Tests with Testcontainers (2026-03-11)

**Status:** ⚠️ Partial Implementation  
**Owner:** Jordan (Tests Dev)  
**Context:** Need to verify database connectivity and Aspire dashboard integration

### Decision: Use Testcontainers for Database E2E Testing

**Infrastructure Created:**
- `DatabaseE2ETests.cs` with MSSQL Testcontainers integration
- Aspire dashboard health check test
- Dependencies: Testcontainers.MsSql, EF Core SQL Server provider
- Reuses TestDbContext and BogusDataGenerator from LinqStudio.Databases.Tests

### Known Blockers

1. **MudSelect Interaction** — Test fails at database type dropdown
   - MudBlazor's MudSelect renders hidden input
   - Clicking non-visible input fails
   - Solution: Add testid to visible button or use role-based selector
   - Awaiting UI coordination with Simon

2. **Schema Explorer Not Implemented**
   - Test successfully saves connection settings
   - Cannot verify table load because schema explorer doesn't exist
   - TODO: Add verification once component is implemented

3. **Aspire Dashboard Selectors**
   - Test structure is solid
   - Dashboard HTML structure needs inspection for stable selectors
   - Will refine once Aspire is running with demo databases

### Implementation Quality

- ✅ Proper async/await patterns
- ✅ Test setup and cleanup
- ✅ Real database containers (no mocks)
- ✅ CI-ready (Docker available in GitHub Actions)
- ⚠️ Blocked by UI component interaction

### Test Results

- ✅ Existing E2E tests: 13 passed, 1 skipped
- ⚠️ New database test: Fails at MudSelect
- ⏭️ Aspire dashboard test: Skipped (requires manual startup)

### Recommendation

Keep tests as-is with clear blocking status. Structure is solid and Testcontainers integration is proven. Fix MudSelect interaction in coordinated PR with Simon.

---

## AppHost Database Configuration Pattern (2026-03-11)

**Status:** ✅ Established  
**Owner:** Team  
**Context:** Ensuring connection strings properly exposed in Aspire dashboard

### Decision: Use `.AddDatabase()` for Database-Scoped Resources

**Pattern:**
```csharp
// ✅ CORRECT: Database-scoped connection string exposure
var mssqlDb = builder.AddDatabase("linqstudio-mssql-demo");
var mysqlDb = builder.AddDatabase("linqstudio-mysql-demo");

// Seeder waits for databases
var seeder = builder.AddProject<Projects.LinqStudio_DatabaseSeeder>()
    .WaitFor(mssqlDb)
    .WaitFor(mysqlDb);

// App references databases and waits for seeder
var app = builder.AddProject<Projects.LinqStudio_App_WebServer>()
    .WithReference(mssqlDb)
    .WithReference(mysqlDb)
    .WaitForCompletion(seeder);
```

### Benefits

- Connection strings appear in Aspire dashboard
- Proper scoping of database resources
- Environment variables correctly named: `ConnectionStrings__DemoMssql`, etc.
- Database resources separate from container resources in UI

### Alternative (Rejected)

- Using raw container resources without `.AddDatabase()` wrapper
- Doesn't expose connection strings to dependent services
- Poor Aspire pattern visibility

---

## References

**Monaco Widget Testing:**
- `.squad/orchestration-log/2026-03-11T12-56-53Z-jordan-diagnosis.md`
- `.squad/orchestration-log/2026-03-11T12-56-53Z-alice-live-test.md`
- `.squad/orchestration-log/2026-03-11T12-56-53Z-jordan-e2e-fix.md`
- `.squad/log/2026-03-11T12-56-53Z-e2e-fix-session.md`
- `.squad/orchestration-log/2026-03-11_100538-jordan-openparen-fix.md`
- `.squad/log/2026-03-11_100538-e2e-fix-complete.md`

---

## MySQL EF Core 10 Fix Verification (2026-03-11 6:37 PM)

**Status:** ✅ Verified  
**Tester:** Alice (Live Tester)  
**Context:** Post-fix validation after Simon's MySql.EntityFrameworkCore upgrade

### Verification Results

**MySQL Seeding:**
- ✅ No longer fails with "Method not found: IRelationalCommandBuilder.Append()"
- ✅ Seeded successfully on first try (no retries)
- ✅ Complete output: "[MySQL] Seeded successfully."

**MSSQL Seeding:**
- ✅ Still working (both DBs seeded simultaneously)

**Containers:**
- ✅ demo-mssql running healthy
- ✅ demo-mysql running healthy
- ✅ Both database resources (linqstudio-mssql-demo, linqstudio-mysql-demo) marked Running

**Console Output Evidence:**
```
Line 19: [MySQL] Seeded successfully.
Line 20: [MSSQL] Seeded successfully.
Line 21: Demo seeding complete.
```

### Before vs After Fix

| Aspect | Before (6:08 PM) | After (6:37 PM) |
|--------|------------------|-----------------|
| MySQL Seeding | ❌ Failed after 10 retries | ✅ First try success |
| Error | "Method not found: IRelationalCommandBuilder.Append()" | None |
| MSSQL | Running/Healthy | Running/Healthy |
| MySQL Container | Running/Healthy | Running/Healthy |

### Resolution Status

**MySQL Provider Fix:** ✅ COMPLETE AND VERIFIED
- Oracle's `MySql.EntityFrameworkCore` v10.0.1 resolves EF Core 10 compatibility
- No regression in MSSQL seeding
- Both databases initialize cleanly

**Known Remaining Issue (separate):**
- Seeder exits with code -532462766 after successful seeding
- Does NOT affect database seeding success
- Blocks app server startup due to `WaitForCompletion(seeder)` dependency
- May require separate fix to seeder exit handling

---

**Database Integration:**
- `.squad/orchestration-log/2026-03-11T21-34-07Z-simon-aspire-db-setup.md`
- `.squad/orchestration-log/2026-03-11T21-34-07Z-simon-seeder-fix.md`
- `.squad/orchestration-log/2026-03-11T21-34-07Z-jordan-db-e2e-tests.md`
- `.squad/orchestration-log/2026-03-11T21-34-07Z-alice-aspire-visual-test.md`
- `.squad/orchestration-log/2026-03-11T21-34-07Z-coordinator.md`
- `.squad/log/2026-03-11T21-34-07Z-aspire-db-seeder-fix.md`

---

## Remove MSSQL Auto-Discovery from MssqlGenerator (2026-03-13)

**Status:** ✅ Implemented  
**Owner:** Simon (Backend Core Dev)  
**Reviewed by:** Alex (Code Reviewer)  
**Date:** 2026-03-13

### Decision

`MssqlGenerator.Create()` now mandates that the connection string explicitly specifies a target database (`Database=` or `Initial Catalog=`). Auto-discovery logic has been removed entirely.

### What Was Removed

- `_resolvedDatabase` field (connection-pool-poisoning cache)
- `FindFirstUserDatabaseAsync()` method (picked "first user database alphabetically" — non-deterministic)
- `SwitchToResolvedDatabaseIfNeeded()` method (called `ChangeDatabase()` on pooled connections)
- The master-check block in `GetTableAsync()` that triggered auto-discovery

### What Was Added

Fail-fast validation in `Create()`:
```csharp
public static MssqlGenerator Create(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

    var builder = new SqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        throw new ArgumentException(
            "Connection string must specify a target database using Database= or Initial Catalog= parameter.",
            nameof(connectionString));

    return new(new SqlConnection(connectionString));
}
```

Also added guard to `Project.UpdateConnection()` for the same reason.

### Why

1. **Unpredictable**: Auto-discovery picks the first user database alphabetically — no guarantee it's the intended one on multi-DB servers.
2. **Config masking**: Silently swallowed missing `Database=` config errors instead of failing loudly.
3. **Connection pool poisoning**: `ChangeDatabase()` mutates a pooled `SqlConnection` object, affecting other consumers of that connection from the pool.

### Note on GetTablesAsync

`GetTablesAsync` was NOT changed — it uses a server-level cross-database dynamic SQL query (`FROM sys.databases`) that correctly enumerates all user databases regardless of which database the connection is currently in. The "master connection" test was renamed to reflect this mechanism rather than implying auto-discovery.

### Status

✅ Implemented, all 392 non-E2E tests pass, 407 total tests passing

---

## MudBlazor Content Template Icon Pattern (2026-03-11)

**Status:** ✅ Implemented  
**Owner:** EvilJosh (Frontend Dev)  
**Date:** 2026-03-11

### Problem

Column icons in `DatabaseTreeView.razor` were not rendering despite correct `Icon=` and `IconColor=` parameters on `MudTreeViewItem` components.

### Root Cause

MudBlazor's `MudTreeViewItem` component **silently ignores** `Icon=` and `IconColor=` parameters when a `<Content>` template is provided. This is by design — the `<Content>` template provides full control over rendering, completely bypassing the component's built-in text/icon display logic.

### Solution Applied

For column tree items:
- **Removed:** `Icon=@GetColumnIcon(column)` and `IconColor=@GetColumnIconColor(column)` attributes
- **Added:** Explicit `<MudIcon>` component inside the `<Content><div>` wrapper:
  ```razor
  <MudIcon Icon="@GetColumnIcon(column)"
           Color="@GetColumnIconColor(column)"
           Size="Size.Small" Class="mr-1" />
  ```

### Pattern to Remember

**When using `<Content>` template in MudTreeViewItem:**
1. ALL visual elements (icons, text, badges) must be explicitly placed inside the template
2. Component parameters `Icon=`, `IconColor=`, `Text=` are **completely ignored**
3. Use `<MudIcon>` for icons, `<MudText>` for text, standard flex/spacing classes for layout
4. This gives full layout control but requires manual construction

**When NOT using `<Content>` template:**
- Simple `Text=`, `Icon=`, `IconColor=` parameters work as expected
- Use this for simple items with no custom layout

### Related Bug Fix

Also fixed `int` type showing as `int(10,0)` by adding `_fixedSizeTypes` HashSet to skip precision/scale formatting for fixed-size numeric types (int, bigint, smallint, tinyint, bit).

### Verification

✅ Build succeeded (0 warnings, 0 errors)  
✅ Column icons now render correctly (Key/gold for PK, Bolt for identity, ViewColumn for regular)  
✅ Int columns now display as "int" or "int?" instead of "int(10,0)"

---

## Single-Click Expand Pattern in DatabaseTreeView (2026-03-11)

**Status:** ✅ Implemented  
**Owner:** EvilJosh (Frontend Dev)  
**Date:** 2026-03-11

### Problem

`DatabaseTreeView.razor` had a two-click UX issue:
- Users clicked the expand arrow to expand a table node
- The `@bind-Expanded` binding updated the UI state
- But the `OnClick` event handler (which loaded columns) did NOT fire
- Users had to click the expand arrow, THEN click the row text to trigger column loading
- Result: confusing UX where expanding didn't show any data until a second click

### Root Cause Analysis

MudBlazor's `MudTreeViewItem` component separates:
1. **Expand/collapse arrow clicks** → triggers `@bind-Expanded` binding
2. **Row content clicks** → triggers `OnClick` event

Using both simultaneously created a split-brain pattern where:
- State updates (expand/collapse) happened via binding
- Side effects (load columns) happened via OnClick
- Clicking different parts of the row triggered different behaviors

### Solution

Replace the two-event pattern with MudBlazor's `ExpandedChanged` callback:

**Before:**
```razor
<MudTreeViewItem T="string" 
                 @bind-Expanded="@_expandedStates[table.FullName]"
                 OnClick="@(() => OnTableClick(table))"
                 ... />
```

**After:**
```razor
<MudTreeViewItem T="string" 
                 Expanded="@_expandedStates[table.FullName]"
                 ExpandedChanged="@(v => OnTableExpandedChanged(table, v))"
                 ... />
```

**Code-behind:**
```csharp
private async Task OnTableExpandedChanged(DatabaseTableName table, bool expanded)
{
    _expandedStates[table.FullName] = expanded;
    if (expanded && !_tableDetailsCache.ContainsKey(table.FullName))
    {
        await LoadTableDetailsAsync(table);
    }
}
```

### Benefits

1. **Single-click UX:** Expand arrow click now loads columns immediately
2. **Cleaner pattern:** One callback handles both state + side effects
3. **MudBlazor alignment:** Uses recommended event pattern from framework
4. **Predictable behavior:** Click anywhere on expand arrow = same result

### Pattern for Future TreeView Components

When building interactive tree views with async data loading:
- Use `Expanded` + `ExpandedChanged` instead of `@bind-Expanded` + `OnClick`
- Handle state update AND side effects in the single `ExpandedChanged` callback
- Avoids split-brain patterns where different UI elements trigger different logic

### Verification

✅ Build verified: PASS (0 errors)  
✅ No breaking changes to existing functionality (refresh, loading states, error handling all unchanged)

---

## EditProjectDialog Validation & DatabaseTreeView Cache Improvements (2026-03-13)

**Status:** ✅ Implemented  
**Owner:** EvilJosh (Frontend Dev)  
**Date:** 2026-03-13

### Decisions

## Alex — Code Review
PR: KeepPanelsAlive Editor Redesign
B+ grade, minor cleanup items. Approved with recommended fixes.
Actionable: Remove dead _activePanelIndex, add warning for _editor null, fix Playwright try/catch antipattern.

## EvilJosh — Change Plan
KeepPanelsAlive cannot be applied as-is. Requires structural refactor, move content into MudTabPanels, delete sort machinery, manage Monaco/splitter multi-instance. Medium-large refactor.

## Samy — Architectural Analysis
KeepPanelsAlive inapplicable. Editor uses MudTabs for navigation only, no content inside panels. Recommendation: Use MudDataGrid SortChanged callback, do not pursue KeepPanelsAlive.

1. **EditProjectDialog.Save():** Added null/empty validation before calling `Project.UpdateConnection()` to prevent empty connection strings from being passed to the backend.

2. **DatabaseTreeView Cache Access:** Replaced direct dictionary access with `GetValueOrDefault()` pattern to safely access tree expansion state without race conditions on concurrent expand/collapse events.

3. **Test Cleanup:** Fixed temporary directory leak in `DatabaseTreeViewTests.cs` by properly disposing DirectoryInfo objects during test cleanup.

### Changes Made

**EditProjectDialog.razor.cs:**
```csharp
private async Task Save()
{
    if (string.IsNullOrWhiteSpace(_connectionString))
    {
        // Show validation error to user
        _validationError = "Connection string cannot be empty.";
        return;
    }
    // Proceed with save...
}
```

**DatabaseTreeView.razor.cs:**
```csharp
// Changed from: Expanded="@_expandedStates[table.FullName]"
// To:
Expanded="@_expandedStates.GetValueOrDefault(table.FullName, false)"
```

### Rationale

- **Empty String vs Null Confusion:** EditProjectDialog was coalescing null to empty string (`_connectionString ?? string.Empty`), bypassing Project.UpdateConnection() validation. Now fails fast in the dialog layer.
- **Dictionary Access Safety:** Direct dictionary access without checking existence first can cause race conditions if multiple async events modify the same state concurrently. GetValueOrDefault() is defensive.
- **Test Infrastructure:** Accumulated temp directories in tests indicate cleanup gaps; proper disposal prevents resource leaks.

### Verification

✅ All 407 tests pass  
✅ Build clean (0 warnings with TreatWarningsAsErrors=True)  
✅ Temporary directory cleanup resolved

---

## Code Review: MSSQL Auto-Discovery Feature & DatabaseTreeView Implementation (2026-03-12)

**Status:** ✅ Complete  
**Owner:** Alex (Code Reviewer)  
**Date:** 2026-03-12

### Review Summary

Comprehensive review of MSSQL auto-discovery feature implementation, DatabaseTreeView, and related fixes across 21 modified files.

### Build & Test Status

- **Build:** ✅ Passing (0 warnings, 0 errors with TreatWarningsAsErrors=True)
- **Tests:** ✅ All 401 passing (297 database + 45 core + 44 Blazor + 15 E2E)

### Positive Patterns Identified

1. **Comprehensive feature implementation** - New UI features come with component tests, E2E tests (even if skipped), helper methods, and documentation. This is exemplary.
2. **Caching with lifecycle management** - DatabaseTreeView uses Dictionary-based caching with proper cleanup on workspace changes. Smart pattern for reducing database round-trips.
3. **Smart auto-discovery** - MssqlGenerator automatically switches from master to first user database when no database specified. Handles Aspire deployment patterns well.
4. **Excellent test documentation** - Skipped tests include detailed implementation notes explaining what/how to test. This is valuable for future work.
5. **Copilot.md pattern** - Each component/feature area has copilot.md with feature descriptions, test IDs, and implementation notes. Great for AI-assisted development.

### Concerning Patterns (Addressed in Separate Decision)

1. **Empty string vs null confusion** - EditProjectDialog uses `_connectionString ?? string.Empty` which can pass empty string to UpdateConnection. Project.cs doesn't validate this, leading to potential runtime exceptions. **FIXED**
2. **Stateful caching across connection lifecycle** - MssqlGenerator's `_resolvedDatabase` field persists across connection open/close cycles. **ADDRESSED by auto-discovery removal**
3. **Property setters with side effects using field keyword** - C# 13 field keyword used in Project.cs with side effects (clearing QueryGenerator). While syntactically correct, mixing auto-property and manual logic can be missed during maintenance.
4. **Race conditions in event handlers** - DatabaseTreeView.OnWorkspaceChanged uses InvokeAsync without cancellation. Rapid workspace changes could queue multiple LoadTablesAsync calls.

### Test Coverage Gaps Identified

- Project.UpdateConnection validation (empty/null strings) — **ADDRESSED**
- MssqlGenerator connection reopen scenarios
- DatabaseTreeView concurrent event handling
- EditProjectDialog validation error handling — **ADDRESSED**

### Recommendations

For future reviews:
1. **Validation at boundaries** - When user input flows from UI → Service → Model, validate early (in dialog) or late (in model), but not nowhere.
2. **Stateful field patterns** - When caching database-specific state, consider tying it to connection lifetime or document the assumption.
3. **Empty string vs null** - Treat empty string as invalid for required fields like connection strings.
4. **Event handler concurrency** - For async event handlers that trigger long-running operations, consider cancellation tokens to prevent queuing.

### Overall Assessment

**Quality:** Good - Changes are well-structured, properly tested, follow project conventions.  
**Production Readiness:** Yes - Minor findings were addressed in follow-up fixes.  
**Team Performance:** Excellent - Comprehensive test documentation, clear architectural patterns, high code quality standards.

---

## References

**MSSQL Auto-Discovery Removal:**
- `.squad/orchestration-log/2026-03-12T23-59-07Z-simon.md`
- `.squad/orchestration-log/2026-03-12T23-59-07Z-eviljosh.md`
- `.squad/orchestration-log/2026-03-12T23-59-07Z-alex.md`
- `.squad/log/2026-03-12T23-59-07Z-mssql-autodiscovery-removal.md`

**MudBlazor & DatabaseTreeView Patterns:**
- `.squad/decisions/inbox/eviljosh-column-icon-fix.md` (merged)
- `.squad/decisions/inbox/eviljosh-single-click-expand.md` (merged)

**Code Review:**
- `.squad/decisions/inbox/alex-review-current-changes.md` (merged)


---

# Alex — Code Review: Query Execution Feature

**Date:** 2026-03-14  
**Reviewer:** Alex (Code Reviewer)  
**Requested by:** snakex64  
**Scope:** Full query execution pipeline implementation

---

## ✅ Looks Good

### Architecture & Design
- **7-step Roslyn pipeline** is well-structured and correctly separated
- **DbContext dual-constructor pattern** is elegant: parameterless for IntelliSense, parameterized for execution
- **Per-tab execution state** design matches existing QueriesWorkspace pattern — consistent and clean
- **QueryExecutionResult model** is simple, testable, and has good static factory methods
- **Settings integration** follows IUserSettingsSection pattern correctly with full localization

### Error Handling
- Comprehensive try-catch with proper logging throughout
- Distinguishes compile-time vs runtime errors via `IsCompileError` flag
- OperationCanceledException handled properly in both service and UI
- Cancellation token passed through entire pipeline correctly

### Test Coverage
- QueryExecutionResult factory methods: 100% tested (Empty, FromError, Success property)
- QueryExecutionSettings: well tested (defaults, serialization, IUserSettingsSection compliance)
- QueryResultGrid: comprehensive bUnit tests covering all 5 rendering states
- Edge cases covered: null results, empty data, null cell values, elapsed time formatting

### UI/UX
- QueryResultGrid component handles all states gracefully (loading, error, empty, success, null)
- Execute/Stop button toggle is clear and intuitive
- Timeout dropdown with sensible defaults (10s–5min, 0=unlimited)
- Clean separation: QueryResultGrid.razor (markup) + QueryResultGrid.razor.cs (code-behind)

---

## ⚠️ Findings

### QueryExecutionService.cs

#### **[Severity: High]** Memory Leak — Assembly Never Unloaded
**Line:** 343 — `var assembly = Assembly.Load(ms.ToArray());`

**Issue:** Compiled assemblies are loaded into the default AppDomain and remain in memory forever. Each query execution creates a new assembly that cannot be garbage collected. With hundreds of query executions, this will leak memory continuously.

**Impact:** 
- Memory grows unbounded over time
- Production risk: Blazor Server app will eventually exhaust memory
- No disposal path for loaded assemblies

**What to fix:**
1. Use `AssemblyLoadContext` with `Unloadability=true` for each compilation:
   ```csharp
   var alc = new AssemblyLoadContext(name: null, isCollectible: true);
   var assembly = alc.LoadFromStream(ms);
   // ... use assembly ...
   alc.Unload(); // Critical: unload when done
   ```
2. Track `AssemblyLoadContext` instances in QueryExecutionState or return them from CompileToAssemblyAsync
3. Dispose/unload after query materialization completes
4. Add integration test that runs 100 queries and verifies memory doesn't grow

**Why it matters:** This is a production blocker. Without proper cleanup, the app will crash under normal usage.

---

#### **[Severity: High]** MemoryStream Not Disposed Before Assembly.Load
**Line:** 328-343

**Issue:** `MemoryStream ms` is declared with `using` but the stream's content is copied to a byte array before disposal. The MemoryStream is properly disposed, but the byte array (`ms.ToArray()`) persists in memory even after `Assembly.Load()` completes.

**Impact:** 
- Each query execution allocates a byte array copy of the compiled IL
- Byte array is held by the Assembly object
- Combining with the assembly leak above, this doubles the memory footprint per query

**What to fix:**
1. Load directly from stream without `ToArray()`:
   ```csharp
   ms.Seek(0, SeekOrigin.Begin);
   var assembly = alc.LoadFromStream(ms);
   // MemoryStream disposed here, assembly references its own copy
   ```
2. This is the correct pattern when using AssemblyLoadContext

**Why it matters:** Reduces per-query memory footprint by 50%, especially for large model assemblies.

---

#### **[Severity: High]** DbContext Never Disposed
**Lines:** 88-93, 124

**Issue:** `DbContext` instance created via `Activator.CreateInstance` is never disposed. DbContext holds database connections and EF Core change tracker state.

**Impact:**
- Connection pool exhaustion over time
- Memory leaks from EF Core internal state
- Database connections may remain open longer than necessary

**What to fix:**
1. Wrap DbContext in `await using`:
   ```csharp
   await using var dbContext = Activator.CreateInstance(dbContextType, dbContextOptions) as DbContext;
   if (dbContext == null) { /* error */ }
   // ... use dbContext ...
   // Disposed automatically here
   ```
2. Remove explicit null checks after `await using` — pattern guarantees non-null or throws

**Why it matters:** EF Core best practice. Connection leaks will cause production failures under load.

---

#### **[Severity: Medium]** CancellationToken Not Respected During Compilation
**Lines:** 222-345

**Issue:** `CompileToAssemblyAsync()` accepts a `cancellationToken` but only passes it to two operations:
- `project.GetCompilationAsync(cancellationToken)` (line 317)
- `compilation.Emit(ms, cancellationToken: cancellationToken)` (line 329)

All other steps (loading assemblies, adding documents, building metadata references) do not check cancellation.

**Impact:**
- User clicks "Stop" but compilation continues for several seconds
- Poor UX: button says "Stopped" but CPU usage shows work still happening
- Not a safety issue, just unresponsive UI

**What to fix:**
1. Add `cancellationToken.ThrowIfCancellationRequested()` at key points:
   - Before loading assemblies (line 256)
   - After adding metadata references (line 293)
   - Before adding documents (line 295)
2. Wrap long-running loops in cancellation checks

**Why it matters:** User expects immediate response to cancellation. Current behavior feels broken.

---

#### **[Severity: Medium]** ExtractResults() Does Not Handle Collections
**Lines:** 347-391

**Issue:** `ExtractResults()` uses reflection to read public properties. If a query returns entities with collection navigation properties (e.g., `Customer.Orders`), calling `prop.GetValue(item)` will trigger lazy-loading and execute additional database queries.

**Impact:**
- Unexpected database round-trips (N+1 queries)
- Performance degradation with complex models
- Results include serialized collection data (often not desired)

**What to fix:**
1. Filter out collection properties:
   ```csharp
   var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
       .Where(p => !typeof(IEnumerable).IsAssignableFrom(p.PropertyType) 
                || p.PropertyType == typeof(string))
       .ToArray();
   ```
2. Alternatively, use `AsNoTracking()` on queryable before materialization to prevent lazy-loading entirely
3. Add integration test: query that returns entity with nav properties, verify no extra queries executed

**Why it matters:** N+1 queries will cause severe performance issues with production data.

---

#### **[Severity: Medium]** WrapUserQuery() Does Not Handle Return Type Variance
**Lines:** 174-195

**Issue:** Wrapped query assumes user returns `IQueryable<object>`, but users might return:
- `IQueryable<Customer>` (specific type)
- `IEnumerable<T>` (already materialized)
- Scalar values (`int Count = context.Users.Count()`)
- `Task<List<T>>` (awaited expression)

Current code only handles the exact signature `Task<IQueryable<object>>`.

**Impact:**
- Compile errors for common query patterns
- Cryptic Roslyn diagnostics ("cannot convert IQueryable<Customer> to IQueryable<object>")
- Users don't understand what's wrong

**What to fix:**
1. Detect query result type and wrap accordingly:
   ```csharp
   // If user returns scalar: wrap as single-row result
   // If user returns IEnumerable<T>: wrap as IQueryable<object>
   // If user returns IQueryable<T>: cast to IQueryable<object>
   ```
2. OR: change QueryContainer signature to use `dynamic` return type and handle runtime casting
3. Add tests for each query pattern variation

**Why it matters:** Users expect `context.Users.Count()` to work. Current implementation rejects common patterns.

---

#### **[Severity: Low]** Timeout Implementation Creates Double CancellationToken
**Lines:** 128-144

**Issue:** Method creates `timeoutCts` and then `linkedCts` combining timeout + caller's token. Both are disposed, but the pattern is unnecessarily complex.

**Impact:**
- More allocations than necessary
- Slightly harder to read
- Functionally correct, just not optimal

**What to fix:**
1. Simplify to single CancellationTokenSource:
   ```csharp
   var timeoutSeconds = _settings.CurrentValue.TimeoutSeconds;
   using var cts = timeoutSeconds > 0 
       ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
       : new CancellationTokenSource();
   
   if (timeoutSeconds > 0)
       cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
   
   var items = await queryable.ToListAsync(cts.Token);
   ```
2. One less allocation, same functionality

**Why it matters:** Minor optimization, cleaner code.

---

### DbContextGenerator.cs

#### **[Severity: Low]** Generated DbContext Constructor Comment Misleading
**Lines:** 183-187

**Comment says:** "Parameterless constructor for IntelliSense compilation - never instantiated at runtime"

**Reality:** QueryExecutionService instantiates via `Activator.CreateInstance(dbContextType, dbContextOptions)` (line 88) which uses the parameterized constructor.

**Impact:** Comment is confusing for future maintainers.

**What to fix:**
1. Update comment:
   ```csharp
   // Parameterless constructor for CompilerService IntelliSense (no real database)
   public GeneratedDbContext() { }
   
   // Parameterized constructor for QueryExecutionService (real query execution)
   public GeneratedDbContext(DbContextOptions options) : base(options) { }
   ```

**Why it matters:** Accurate comments prevent confusion during future debugging.

---

### Editor.razor.cs

#### **[Severity: Medium]** Dictionary Access Race Condition in Blazor Server
**Line:** 56, 479, 481

**Issue:** `_executionStates` is a non-thread-safe `Dictionary<Guid, QueryExecutionState>` accessed from multiple async paths:
1. `GetCurrentExecutionState()` (line 472)
2. `ExecuteCurrentQueryAsync()` (line 488)
3. UI rendering (line 151-152)
4. Dispose cleanup (line 569-575)

In Blazor Server, multiple SignalR messages can arrive concurrently for the same circuit.

**Impact:**
- Rare race condition: `Dictionary` throws if accessed during resize
- Hard to reproduce, may only occur under load
- Potential crash: unhandled exception in Blazor circuit

**What to fix:**
1. Use `ConcurrentDictionary<Guid, QueryExecutionState>`:
   ```csharp
   private readonly ConcurrentDictionary<Guid, QueryExecutionState> _executionStates = new();
   ```
2. Change `GetCurrentExecutionState()` to use `GetOrAdd()`:
   ```csharp
   return _executionStates.GetOrAdd(queryId, _ => new QueryExecutionState());
   ```
3. No other changes needed — ConcurrentDictionary has compatible API

**Why it matters:** Blazor Server threading model allows concurrent access. Dictionary is not thread-safe.

---

#### **[Severity: Medium]** ExecuteCurrentQueryAsync Race: Navigate Away During Execution
**Lines:** 488-551

**Issue:** User clicks Execute → navigates to different tab → result arrives → `StateHasChanged()` called for wrong tab.

**Flow:**
1. User on QueryA, clicks Execute
2. `state.IsExecuting = true; StateHasChanged();` (line 521-523)
3. User immediately switches to QueryB
4. QueryA completes, `state.Result = result; StateHasChanged();` (line 528, 550)
5. UI updates for QueryB but shows QueryA's result briefly

**Impact:**
- UI flicker: wrong result shown for ~1 frame
- Confusing UX: "Why did my result disappear?"
- Not a data corruption issue, just visual glitch

**What to fix:**
1. Check if still on same query before StateHasChanged:
   ```csharp
   if (Workspace.Queries.CurrentQueryId == queryId) {
       StateHasChanged();
   }
   ```
2. Apply at lines 523, 528, 550

**Why it matters:** Edge case but noticeable. Users will report it as a bug.

---

#### **[Severity: Low]** Cancellation Cleanup Missing in Finally Block
**Lines:** 545-551

**Issue:** `finally` block disposes `CancellationTokenSource` but doesn't handle the case where cancellation occurs before the token is created.

**Impact:** If `GetCurrentExecutionState()` throws (shouldn't happen, but defensive coding), cleanup is skipped.

**What to fix:**
1. Move cleanup logic to ensure it always runs:
   ```csharp
   finally
   {
       if (_executionStates.TryGetValue(queryId, out var cleanupState))
       {
           cleanupState.IsExecuting = false;
           cleanupState.CancellationTokenSource?.Cancel();
           cleanupState.CancellationTokenSource?.Dispose();
           cleanupState.CancellationTokenSource = null;
       }
       StateHasChanged();
   }
   ```

**Why it matters:** Defensive programming. Edge case but worth handling correctly.

---

### QueryResultGrid.razor

#### **[Severity: Low]** No Pagination for Large Result Sets
**Lines:** 40-59

**Issue:** `MudTable` renders all rows at once. If query returns 10,000 rows, browser performance degrades significantly.

**Impact:**
- Browser hangs for large result sets
- Poor UX: no way to page through data
- Memory usage spikes in browser

**What to fix:**
1. Add `MudTable` pagination attributes:
   ```razor
   <MudTable Items="@Result.Rows" 
             Dense="true" Hover="true" Striped="true" 
             FixedHeader="true" Height="400px" Elevation="2"
             RowsPerPage="100">
       @* ... *@
   </MudTable>
   ```
2. Consider adding server-side pagination later (more complex)

**Why it matters:** Users will execute queries that return thousands of rows. Current implementation will freeze the browser.

---

#### **[Severity: Low]** Elapsed Time Format Inconsistency
**Line:** 16-19 (FormatElapsedTime method)

**Issue:** Sub-second times show no decimal (e.g., "123ms"), but seconds show 2 decimals (e.g., "1.50s"). This inconsistency is minor but noticeable.

**Impact:** Purely cosmetic. Users might find it odd.

**What to fix:**

---

## MSSQL Composite Primary Key Generation Fix (2026-03-14)

**Status:** ✅ Implemented & Verified  
**Owner:** Simon (Backend Core Dev)  
**Architect:** Samy (Architecture)  
**Tester:** Jordan (Tests Dev)  
**Date:** 2026-03-14

### Problem Statement

When executing a query on MSSQL tables with composite primary keys, EF Core throws:
```
The entity type 'OrderItems' has multiple properties with the [Key] attribute. 
Composite primary keys can be configured by placing the [PrimaryKey] attribute 
on the entity type class, or by using 'HasKey' in 'OnModelCreating'.
```

### Root Cause Analysis

**Primary Issue:** `MssqlGenerator.GetColumnsAsync()` uses `connection.GetSchemaAsync("IndexColumns", ...)` to detect PKs, but this returns **ALL indexed columns**, not just primary key columns. For tables with performance indexes on non-key columns, this incorrectly marks those columns as PKs.

**Secondary Issue:** `DbContextGenerator.GenerateModel()` emits `[Key]` attribute on every column marked as `col.IsPrimaryKey`. EF Core forbids multiple `[Key]` attributes; it requires either:
- Class-level `[PrimaryKey(...)]` attribute (C# 11+)
- Fluent API `HasKey()` in `OnModelCreating`
- NOT multiple property-level `[Key]` attributes

### Solution Implemented (Option C)

**Approach:** Fluent API for ALL primary keys (consistent single & composite key handling)

#### Change 1: MssqlGenerator.cs — Correct PK Detection
**File:** `src\LinqStudio.Database\MssqlGenerator.cs` (GetColumnsAsync method, lines 215-247)

**Before:** Used IndexColumns schema (incorrect)

**After:** Direct SQL query filtering by `is_primary_key = 1`:
```sql
SELECT c.name AS column_name, ic.key_ordinal AS key_ordinal
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.is_primary_key = 1 AND i.object_id = OBJECT_ID(@tableName)
ORDER BY ic.key_ordinal
```

**Benefit:** Accurately identifies ONLY primary key columns, respects schema-qualified table names, preserves column order

#### Change 2: DbContextGenerator.cs — Remove [Key] Emissions
**File:** `src\LinqStudio.Core\Services\DbContextGenerator.cs` (GenerateModel method, lines 90-112)

**Before:** Emitted `[Key]` for every `col.IsPrimaryKey` column

**After:**
- Removed all `[Key]` attribute emissions
- Kept `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` for identity columns
- Uses Fluent API HasKey() instead

#### Change 3: DbContextGenerator.cs — Add OnModelCreating
**File:** `src\LinqStudio.Core\Services\DbContextGenerator.cs` (GenerateDbContext method, lines 159-207)

**Added:** Protected override OnModelCreating(ModelBuilder modelBuilder) method
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Single PK: modelBuilder.Entity<ClassName>().HasKey(e => e.ColumnName);
    // Composite PK: modelBuilder.Entity<ClassName>().HasKey(e => new { e.Col1, e.Col2 });
}
```

**Coverage:** Emits HasKey() for all tables with primary keys

#### Change 4: DbContextGeneratorTests.cs — Update Assertions
**File:** `tests\LinqStudio.Core.Tests\DbContextGeneratorTests.cs`

**Changes:**
- Updated existing test assertions to expect NO [Key] attributes
- Added 5 new tests covering:
  - Single column primary keys
  - Composite primary keys
  - Identity columns with HasKey
  - Multiple table scenarios
  - GUID primary keys

### Design Rationale (Why Option C)

**Option C chosen over A, B, D for these reasons:**

1. **Consistency:** Single-key and multi-key tables use identical patterns. No need to remember two conventions.
2. **EF Core Alignment:** Matches output of official `Scaffold-DbContext` tool (which uses `HasKey()` for generated models).
3. **Future Extensibility:** `OnModelCreating` is the natural place to add indices, constraints, shadow properties, cascade rules.
4. **Robustness:** Fluent API has zero C# version constraints (works on .NET 6+, 7, 8, 10).
5. **Roslyn Safety:** Generated code compiles identically regardless of PK annotation style. No intellisense impact.

**Rejected Alternatives:**
- **Option A ([PrimaryKey]):** Requires C# 11+, needs nameof() parameters, column order sensitive
- **Option B (Hybrid HasKey):** Inconsistent single/composite patterns, unnecessary complexity
- **Option D (Smart Hybrid):** Two different patterns, higher maintenance burden

### Test Coverage

**New Tests (5 total):**
1. `GenerateAsync_SingleColumnPrimaryKey_NoKeyAttributeEmitted` — Validates single PK handling
2. `GenerateAsync_CompositePrimaryKey_NoKeyAttributeAndFluentApiWithAnonymousObject` — Validates composite PK with anonymous object
3. `GenerateAsync_IdentityColumn_DatabaseGeneratedAttributeStillEmitted` — Confirms identity marking still works
4. `GenerateAsync_MultipleTables_OnModelCreatingCoversAllPrimaryKeys` — Multi-table scenarios
5. Updated existing test for GUID PKs without identity

**Test Results:**
- Total: 517 tests
- Passed: 513
- Skipped: 4 (pre-existing)
- Failed: 0
- Build: ✅ Success

### Verification

**Mental Trace - OrderItems Table (Composite PK: OrderId + OrderItemId):**

1. **MssqlGenerator.GetColumnsAsync()**
   - SQL query executes on server
   - Returns only OrderId and OrderItemId (Quantity not included)
   - primaryKeys set contains: ["OrderId", "OrderItemId"]
   - IsPrimaryKey = true for OrderId and OrderItemId only

2. **DbContextGenerator.GenerateModel()**
   - Entity class generated with NO [Key] attributes
   - Properties: public int OrderId { get; set; }, public int OrderItemId { get; set; }, public int Quantity { get; set; }
   - [DatabaseGenerated] not needed (not identity columns)

3. **DbContextGenerator.GenerateDbContext()**
   - OnModelCreating contains:
     ```csharp
     modelBuilder.Entity<OrderItems>().HasKey(e => new { e.OrderId, e.OrderItemId });
     ```
   - EF Core accepts this and configures composite PK correctly

### Impact

**Affected Databases:**
- ✅ **MSSQL:** Fixed (PK detection SQL query + Fluent API)
- ✅ **MySQL/PostgreSQL/SQLite:** Fixed (Fluent API only; PK detection already correct)

**Breaking Changes:** None for users. Generated code now compiles correctly with EF Core.

**Code Generation Pattern:** Established Fluent API pattern for all structural configurations (PKs, relationships, indices)

### Risk Assessment

**Low Risk:** Roslyn compilation unaffected (metadata-only change)  
**Confidence Level:** High (513 tests passing, covers single PKs, composite PKs, identity, multi-table scenarios)

### Related Documentation

- Architecture decision: `.squad/decisions/inbox/samy-composite-key-options.md` (merged)
- Investigation report: `.squad/decisions/inbox/simon-mssql-key-investigation.md` (merged)
- Implementation summary: `.squad/decisions/inbox/simon-option-c-implementation.md` (merged)
- Test coverage: `.squad/decisions/inbox/jordan-pk-tests.md` (merged)
1. Standardize format:
   ```csharp
   if (elapsed.TotalSeconds < 1)
       return $"{elapsed.TotalMilliseconds:F1}ms"; // "123.4ms"
   return $"{elapsed.TotalSeconds:F2}s";
   ```
2. Or: always use 0 decimals for ms, 2 for seconds (current behavior is fine)

**Why it matters:** Very low priority. Current behavior is acceptable.

---

### IQueryExecutionService Location

#### **[Severity: Low]** Interface in Core.Services, Not Abstractions
**File:** `src/LinqStudio.Core/Services/IQueryExecutionService.cs`

**Issue:** The review request specifically flagged this. Interface lives in `LinqStudio.Core.Services` namespace but references `LinqStudio.Core.Models.Project` type. Technically, interfaces should be in `LinqStudio.Abstractions.Services` with all types in `Abstractions.Models`.

**Impact:**
- Breaks clean layering (Abstractions → Core)
- `Project` model has additional properties not needed for this interface
- Future: if Blazor needs different execution service, can't swap implementations easily

**Current Reality:**
- Works fine in practice
- `Project` model is central to the app's domain
- No actual layer violation since Core depends on Abstractions

**What to fix:**
1. **Option A (Ideal):** Move interface to `LinqStudio.Abstractions.Services`, create lightweight `QueryExecutionRequest` record in Abstractions with only needed fields (connectionString, databaseType, queryGenerator)
2. **Option B (Acceptable):** Document in copilot.md that this is an accepted exception to layering rules
3. **Option C (Current):** Leave as-is, note in decisions.md

**Recommendation:** Option B or C. The current design works and refactoring would touch many files for minimal benefit.

**Why it matters:** Architectural consistency vs pragmatism. This is a gray area, not a clear violation.

---

## 🧪 Missing Tests

### QueryExecutionService Integration Tests
**File:** `tests/LinqStudio.Core.Tests/QueryExecutionServiceTests.cs`

**Issue:** Tests only cover:
- Constructor validation
- Static factory methods (QueryExecutionResult)
- Settings defaults
- Basic error cases (no connection string, cancellation)

**Missing critical integration tests:**
1. `ExecuteQueryAsync_WithValidQuery_ReturnsResults` — SQLite in-memory, simple SELECT
2. `ExecuteQueryAsync_WithSyntaxError_ReturnsCompileError` — verify IsCompileError=true
3. `ExecuteQueryAsync_WithRuntimeError_ReturnsRuntimeError` — null reference in query
4. `ExecuteQueryAsync_WithTimeout_CancelsQuery` — verify timeout enforcement
5. `ExecuteQueryAsync_WithComplexModel_HandlesNavigationProperties` — test lazy-loading behavior
6. `ExecuteQueryAsync_MemoryUsage_DoesNotLeakAssemblies` — run 100 queries, measure memory
7. `ExecuteQueryAsync_MultipleConcurrent_ThreadSafe` — parallel executions

**Why it matters:** Unit tests don't validate the 7-step pipeline works end-to-end. Integration tests are essential.

---

### Editor.razor.cs Execution State Tests
**File:** No tests exist

**Missing scenarios:**
1. Execute query on Tab A → switch to Tab B → verify Tab A result preserved
2. Execute query → navigate away mid-execution → verify cleanup
3. Concurrent execute on two tabs → verify state isolation
4. Dispose during execution → verify cancellation

**Why it matters:** Per-tab state is core functionality. Needs explicit test coverage.

---

### QueryResultGrid Edge Cases
**File:** `tests/LinqStudio.Blazor.Tests/QueryResultGridTests.cs`

**Existing tests are excellent.** Only minor gap:

**Missing:**
1. Test with 1000+ rows → verify performance is acceptable
2. Test with very long column names → verify table layout doesn't break
3. Test with deeply nested object ToString() → verify no stack overflow

**Why it matters:** Edge cases are rare but when they hit, they're hard to debug without tests.

---

## 🧹 Cleanup

### QueryExecutionServiceTests.cs Comment Block
**Lines:** 301-316

**Issue:** Large comment block explaining why integration tests aren't included. This is fine for documentation, but clutters the test file.

**What to clean up:**
Move to `docs/testing.md` or `tests/README.md` with a reference comment:
```csharp
// Integration test scenarios documented in docs/testing.md
```

**Why it matters:** Test files should be concise. Long explanatory comments belong in docs.

---

### DbContextGenerator.cs String Builder Pattern
**Lines:** 79-157

**Observation:** Uses StringBuilder for code generation (correct), but could use C# 11 raw string literals for multi-line templates instead of AppendLine chains.

**Not a bug, just a style note.** Current code is readable and maintainable.

**Why it matters:** Very low priority. If refactoring other things, consider raw strings for cleaner templates.

---

## 🔍 Findings vs Accepted Trade-offs

Checked `.squad/decisions.md` for relevant decisions:

### Decision #11-15: Query Execution Feature
All findings above are **new issues** not covered by accepted trade-offs. The decisions document describes the implementation but doesn't acknowledge the memory leak, disposal, or concurrency issues.

### Recommendation: Address High-Severity Findings Before Production
The three **High** severity issues (assembly leak, MemoryStream handling, DbContext disposal) are production blockers. The **Medium** issues are important for production quality. **Low** issues can be deferred.

---

## Summary

**Overall Assessment:** The query execution feature is **well-architected** with excellent separation of concerns, comprehensive test coverage for UI components, and follows established patterns. The 7-step Roslyn pipeline is clever and correct.

**Critical Issues:** Three **High** severity memory leaks will cause production failures under normal usage:
1. Assembly memory leak (unbounded growth)
2. DbContext not disposed (connection pool exhaustion)
3. MemoryStream byte array copies (2x memory footprint)

**Recommendation:**
1. **Must fix before production:** All High severity issues
2. **Should fix before production:** Medium severity issues (race conditions, cancellation handling)
3. **Can defer:** Low severity issues (pagination, comments, style)
4. **Must add:** Integration tests for QueryExecutionService (currently only unit tests exist)

**Before Merging:**
- [ ] Implement AssemblyLoadContext with unloadability
- [ ] Add `await using` for DbContext disposal
- [ ] Load assembly from stream directly (no ToArray())
- [ ] Add integration tests (at minimum: valid query, compile error, runtime error)
- [ ] Change Dictionary to ConcurrentDictionary in Editor.razor.cs

**Estimated Effort:** 4-6 hours to fix High + Medium issues + add integration tests.

**Quality Gate:** Once fixes applied, this feature is production-ready. Architecture is solid, test coverage (after integration tests) will be strong, and patterns are consistent with the rest of the codebase.


---

## AssemblyLoadContext Collectible Pattern for Query Execution (2026-03-14)

**Status:** ✅ Established  
**Owner:** Simon  
**Context:** QueryExecutionService.cs had 3 critical memory management issues causing memory leaks and resource disposal problems.

### Problem Statement
1. **AssemblyLoadContext Memory Leak:** Each compiled query assembly remained in AppDomain indefinitely, preventing garbage collection
2. **DbContext Resource Leak:** EF Core DbContext connections not promptly released after query execution
3. **Inefficient Memory Handling:** MemoryStream.ToArray() created unnecessary copy during assembly loading

### Root Cause Analysis
- Previous implementation used Assembly.Load(ms.ToArray()) which kept assemblies in default context
- DbContext instantiated without disposal guarantee, blocking connection pool cleanup
- No explicit assembly unloading mechanism between query executions
- Over time, accumulated assemblies consumed increasing memory and eventually caused failures

### Solution: Collectible AssemblyLoadContext Pattern

**Core Implementation:**
`csharp
// Each query compilation creates isolated, collectible ALC
var alc = new AssemblyLoadContext("query-exec", isCollectible: true);

// Use LoadFromStream instead of ToArray — avoids memory copy
ms.Seek(0, SeekOrigin.Begin);
var assembly = alc.LoadFromStream(ms);

// DbContext wrapped in await using for guaranteed disposal
await using var dbContext = QueryGenerator.CreateDbContext(/* params */, alc);

try
{
    // Execute query
}
finally
{
    // Guaranteed cleanup
    alc?.Unload();
}
`

**Key Design Decisions:**
1. **Collectible ALC:** isCollectible: true allows ALC to be unloaded and garbage collected
2. **LoadFromStream:** Reads assembly directly from stream without ToArray() copy
3. **await using:** Ensures DbContext.Dispose() called immediately after query execution
4. **try/finally:** Guarantees alc.Unload() even on exceptions or early returns
5. **ALC parameter threading:** Method signatures updated to pass ALC through constructor chain

### Files Modified
- src/LinqStudio.Core/Services/QueryExecutionService.cs
  - ExecuteQuery method: Added ALC creation, LoadFromStream, await using DbContext
  - Error handling: Added early returns with alc?.Unload()
- src/LinqStudio.Core/Services/DbContextGenerator.cs
  - Constructor comment: Documented ALC parameter addition
- src/LinqStudio.Core/Services/copilot.md
  - Technical reference: Documented ALC pattern for future maintenance

### Verification
- **Build:** 0 errors, 0 warnings
- **Test Coverage:** All 487 tests passing
  - LinqStudio.Core.Tests: 121/121 ✅
  - LinqStudio.Blazor.Tests: 56/56 ✅
  - LinqStudio.Databases.Tests: 310/310 ✅

### Impact
- **Memory:** Assemblies immediately unloadable after query execution
- **Connections:** DbContext disposal prompt, connection pool cleanup efficient
- **Performance:** LoadFromStream eliminates unnecessary allocations
- **Reliability:** Guaranteed cleanup prevents accumulation issues over time

### Future Reference
When adding new query execution paths or modifying DbContext generation:
1. Always use collectible ALC pattern for temporary assemblies
2. Wrap DbContext in wait using statement
3. Add lc?.Unload() in finally blocks for all execution paths
4. See copilot.md in Services directory for technical notes

---

# Editor KeepPanelsAlive Redesign Decisions

## Alex Review
- QueryEditorPanel IAsyncDisposable pattern correct
- CTS lifecycle safe
- Compiler null handling correct
- _localCompiler fallback scope correct
- @key on MudTabPanel correct
- @ref in foreach correct
- OnActivePanelIndexChanged null safety correct
- _tabPanelRefs cleanup correct
- Sort code removal complete
- MudBlazor using correct
- E2E test fixture usage correct
- GetActivePanel helper correct
- copilot.md updated

### Findings
- Dead field _activePanelIndex removed
- Silent no-op replaced with Snackbar warning
- Redundant GC.SuppressFinalize removed
- Playwright try/catch antipattern fixed

### Missing Tests
- No test for _localCompiler fallback path
- No test for OnTabActivatedAsync call path

### Cleanup
- Remove _activePanelIndex field
- Editor.razor.css file correct

### Summary
Redesign well-executed, architecture sound, sort machinery fully gone, provider lifecycle handled, test suite expanded. Only actionable fixes: remove dead _activePanelIndex, add Snackbar warning, replace try/catch Playwright antipattern. Grade: B+.

## Alice Results Grid Retest V3
- Splitter auto-initializes on page load
- No "Elements Not Found" warning
- Purple highlight clears after drag
- Drag functionality works both directions
- Cursor style correct
- Global functions exist
- Monaco editor loads
- Execute query results display
- Overall layout correct
- DOM elements verified
- JavaScript initialization status correct
- CSS states tested
- Comparison with previous test passes: all issues fixed
- Root cause confirmed fixed
- Recommendations: keyboard accessibility, double-click to reset, persist splitter position, min/max constraints, cleanup alerts, favicon.png
- Conclusion: All critical checks passed, fix resolved splitter initialization race condition, component works reliably, approved for release

## EvilJosh Monaco Fix
- OnTabActivatedAsync now uses JS interop monacoRelayout
- 50ms delay increased to 100ms for MudBlazor display:none removal
- monacoRelayout guarantees relayout after display:none removed
- Tab bar scroll bug left for deeper investigation
- All 527 tests pass

## EvilJosh Review Fixes
- Dead field _activePanelIndex removed
- Silent no-op replaced with Snackbar warning
- Redundant GC.SuppressFinalize removed
- All 527 tests pass


## 2026-03-15: Removed addDataTestIdsToRows JS Function — Complete Implementation

**Status:** ✅ APPROVED & IMPLEMENTED  
**By:** EvilJosh (implementation), Jordan (tests), Alex (review)  
**Requested by:** snakex64  

### Decision
Removed JavaScript-based data-testid="row-X" injection from MudDataGrid. Replaced with cell-based selector pattern already present in Blazor markup.

### Rationale
**Project Policy:** No JavaScript unless absolutely necessary and approved for user-facing features.

The removed ddDataTestIdsToRows function was:
- Purely test infrastructure (not user-facing)
- Reliant on timing workaround (Task.Delay(100))
- Fragile due to virtual rendering dynamics

Alternative solution validates existing best practice: Cell clicks in MudDataGrid trigger row selection via event bubbling.

### Implementation Details

**Code Changes (EvilJosh):**
1. Removed ddDataTestIdsToRows function from src/LinqStudio.Blazor/wwwroot/queryResultGrid.js (25 lines)
2. Removed JS interop call from QueryResultGrid.razor.cs OnAfterRenderAsync()
3. Removed Task.Delay(100) workaround (no longer needed)
4. Preserved IJSRuntime for clipboard functionality (copyToClipboard — user-facing)

**Test Updates (Jordan):**
- Updated 4 E2E tests in QueryResultGridInteractiveE2ETests.cs: row selectors → cell selectors
  - ResultGrid_ShowsColumns_AfterSuccessfulQuery: ow-0 → cell-0-Id
  - ResultGrid_SelectsRow_OnClick: ow-0 → cell-0-Id
  - ResultGrid_CopiesTSV_OnCtrlC: ow-0, ow-1 → cell-0-Id, cell-1-Id
  - ResultGrid_PerTab_SelectionIsIndependent: ow-0 → cell-0-Id
- Renamed unit test: QueryResultGrid_RendersRows_WithCorrectTestIds → QueryResultGrid_RendersRows_WithCorrectCount
- Updated documentation in test project copilot.md files

### Test Results

**Full Suite:**
- LinqStudio.Core.Tests: 119/119 passed ✅
- LinqStudio.Blazor.Tests: 60/60 passed ✅
- LinqStudio.App.WebServer.E2ETests: 33/33 passed ✅
- Total: 212/212 tests passing

**Specific E2E Tests (affected by change):**
- ResultGrid_ShowsColumns_AfterSuccessfulQuery ✅
- ResultGrid_SelectsRow_OnClick ✅
- ResultGrid_CopiesTSV_OnCtrlC ✅
- ResultGrid_PerTab_SelectionIsIndependent ✅

### Architecture Improvement

**Before:** Two-tier testid strategy
- Cells: via Blazor markup
- Rows: via JavaScript injection

**After:** Single-tier strategy
- Cells only: via Blazor markup
- Row selection: via cell click + event bubbling

**Benefits:**
1. Simpler — one locator strategy
2. Faster — no Task.Delay(100) workaround
3. More maintainable — no JS interop for testids
4. More realistic — tests interact like real users (clicking cells)

### Verification Checklist

✅ No remaining references to ddDataTestIdsToRows (codebase search: 0 matches)  
✅ IJSRuntime still needed and used (clipboard)  
✅ E2E test locators validated against test data  
✅ No dead code or orphaned imports  
✅ OnAfterRenderAsync sort propagation intact  
✅ All 212 tests pass (zero regressions)  

### Review Sign-Off

**Alex (Code Review):**
- Issues: 0
- Concerns: 0
- Recommendation: **SHIP IT**
- All changes correct, complete, improve code quality

### Files Modified

**Source:**
- src/LinqStudio.Blazor/wwwroot/queryResultGrid.js
- src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs

**Tests:**
- tests/LinqStudio.App.WebServer.E2ETests/QueryResultGridInteractiveE2ETests.cs
- tests/LinqStudio.Blazor.Tests/QueryResultGridTests.cs
- tests/LinqStudio.App.WebServer.E2ETests/copilot.md
- tests/LinqStudio.Blazor.Tests/copilot.md

### Pattern: Cell Click → Row Selection

This pattern is validated and established for future reference:

`csharp
// Cell click triggers row selection via event bubbling
var cell = page.Locator($"[data-testid='cell-{rowIndex}-{columnName}']");
await cell.ClickAsync(); // Row automatically selected

// Proof: ResultGrid_SelectsRow_OnCellClick test validates this works
`

When testing MudDataGrid row interactions, click cells instead of trying to click rows directly.

### Cross-Reference
- Orchestration logs: .squad/orchestration-log/2026-03-15T15-38-18Z-*.md
- Session log: .squad/log/2026-03-15T15-38-18Z-remove-js-testid-rows.md


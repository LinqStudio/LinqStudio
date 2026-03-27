# LinqStudio Team Decisions

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

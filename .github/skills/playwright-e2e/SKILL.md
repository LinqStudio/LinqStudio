---
name: playwright-e2e
description: Patterns for writing and running Playwright E2E tests in LinqStudio — SPA navigation, Monaco editor interaction, MudBlazor tab scoping, clipboard testing, and result grid locators. Use this when working on tests/LinqStudio.App.WebServer.E2ETests.
---

# Playwright E2E — LinqStudio Test Patterns

## When to Use This Skill

Read this skill before:
- Writing any new E2E test file in `tests/LinqStudio.App.WebServer.E2ETests/`
- Debugging a flaky or failing E2E test
- Adding assertions against Monaco editor content, result grids, or tab state
- Touching any test that involves navigation between Editor tabs

---

## Project Layout

```
tests/LinqStudio.App.WebServer.E2ETests/
├── Fixtures/
│   ├── AppServerFixture.cs        # Web server lifecycle (shared per collection)
│   └── PlaywrightFixture.cs       # Browser lifecycle (Chromium, shared per collection)
├── Helpers/
│   └── E2ETestHelpers.cs          # All shared helpers — read this first
├── EditorE2ETests.cs              # Monaco editor: completions, hover, dot-trigger
├── QueryExecutionE2ETests.cs      # Execute button, stop, timeout, results
├── QueryResultGridInteractiveE2ETests.cs  # Grid: row selection, clipboard, splitter
├── TabBehaviorE2ETests.cs         # KeepPanelsAlive: tab switching, independent state
├── NavMenuE2ETests.cs             # Nav menu: project create/open/save/close
└── DatabaseTreeViewE2ETests.cs    # DB tree view: expand, refresh
```

---

## App URL

The app runs at a dynamic base URL provided by `AppServerFixture.BaseUrl`. Do NOT hardcode
`localhost:5077` in tests — always use `_app.BaseUrl`.

---

## Test Class Boilerplate

Every test class follows this pattern exactly:

```csharp
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class MyFeatureE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
    private readonly AppServerFixture _app = app;
    private readonly PlaywrightFixture _pw = pw;

    [Fact(Timeout = 60_000)]
    public async Task Feature_DoesX_WhenY()
    {
        Assert.NotNull(_pw.Browser);

        await using var context = await _pw.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // ... test body
    }
}
```

Key rules:
- Always `[Collection("E2E")]` — shares `AppServerFixture` and `PlaywrightFixture`
- Always `await using var context = ...` (fresh context per test)
- Always `Assert.NotNull(_pw.Browser)` as first line
- Timeout on `[Fact]`: 60_000ms minimum, 90_000ms for execution tests, 120_000ms for multi-tab

---

## SPA Navigation — Critical Rule

**NEVER use `page.GotoAsync` for in-app navigation after initial load.**

LinqStudio is a Blazor Server SPA. `GotoAsync` forces a full page reload, which:
- Destroys the Blazor SignalR circuit
- Loses all in-memory workspace state (open queries, project context)
- Causes a redirect to home because the server-side state no longer exists

### ✅ Correct: SPA Navigation via Nav Buttons

```csharp
// Navigate to editor (opens menu, then clicks item)
await page.GetByTestId("nav-editor").ClickAsync();
await Task.Delay(100);  // Wait for MudMenu to open
await page.GetByTestId("nav-editor-new").ClickAsync();
await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");
```

### ✅ Correct: Initial Load Only

```csharp
// GotoAsync is ONLY valid for the very first navigation
await page.GotoAsync(_app.BaseUrl.ToString());
```

### ✅ Correct: Tab Switching via URL history push (special cases only)

```csharp
// Only for testing URL deep-linking specifically — not normal tab clicks (use ClickTabAtIndexAsync)
await page.EvaluateAsync($"window.history.pushState(null, '', '{savedTabUrl}')");
await page.EvaluateAsync("window.dispatchEvent(new PopStateEvent('popstate'))");
await page.WaitForURLAsync(savedTabUrl);
await Task.Delay(300); // Allow Blazor to process popstate
```

---

## Standard Setup Helpers

### SetupEditorAsync — Start Here for Most Tests

Creates a new project AND a new query tab, waits for Monaco to be ready:

```csharp
await E2ETestHelpers.SetupEditorAsync(page, _app);
// After this: project is created, editor page is loaded, Monaco is focused
```

### CreateNewProjectAsync — Project Only (No Query)

```csharp
await E2ETestHelpers.CreateNewProjectAsync(page, _app);
// After this: project exists, still on home page
```

### CreateAdditionalTabAsync — Add a Second (or Third) Tab

```csharp
await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
// Opens nav-editor menu, clicks nav-editor-new, waits for URL + Monaco focus
```

Call this after `SetupEditorAsync` to add more tabs.

---

## KeepPanelsAlive — The Most Important Concept

MudBlazor `MudTabs` is configured with `KeepPanelsAlive="true"`. This means:

- **ALL open tab panels are mounted in the DOM simultaneously**
- Multiple `[role='tabpanel']` elements exist at all times
- Only ONE panel is visible at a time
- **Direct use of `page.GetByTestId(...)` can match elements in hidden tabs** → strict mode violations

### GetActivePanel — Always Use This When Multiple Tabs Exist

```csharp
// Returns the single visible tabpanel — filters by Visible = true
var panel = E2ETestHelpers.GetActivePanel(page);

// Scope ALL element queries to the active panel
var executeBtn = panel.GetByTestId("execute-query-btn");
var resultContainer = panel.GetByTestId("query-result-container");
```

**Rule:** Any time more than one query tab is open, scope locators to `GetActivePanel(page)`.
Single-tab tests may use `page.GetByTestId(...)` directly (only one panel exists).

### Switching Tabs

```csharp
// Click tab by 0-based index
await E2ETestHelpers.ClickTabAtIndexAsync(page, 0); // Switch to first tab
await E2ETestHelpers.ClickTabAtIndexAsync(page, 1); // Switch to second tab
// Handles: panel visibility wait, Monaco relayout delay, and active inputarea focus
```

After `ClickTabAtIndexAsync`, always use `GetActivePanel(page)` for subsequent locators.

---

## Monaco Editor Interaction

### Typing Into the Editor

```csharp
// Clear and write new content (most common pattern)
await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People.Take(5)");
// Selects all existing text, types the new query, then waits for debounce

// Wait for debounce separately when needed
await E2ETestHelpers.WaitForDebounceAsync(); // 500ms (300ms debounce + 200ms buffer)
```

### Focusing the Editor

```csharp
await E2ETestHelpers.WaitEditorAndFocusAsync(page);
// Force-clicks the active Monaco inputarea to move real browser keyboard focus
// Critical with multiple tabs: all inputareas exist in DOM simultaneously
```

### Triggering Completions

```csharp
// Ctrl+Space triggers suggestion widget
await page.Keyboard.PressAsync("Control+Space");

// Wait for suggest widget (with .visible CSS class)
var suggestRow = page.Locator(".suggest-widget.visible .monaco-list-row").First;
await Expect(suggestRow).ToBeVisibleAsync(new() { Timeout = 20000 });
```

### Hovering Over Tokens

```csharp
// Find token by text and hover
var token = page.Locator("span").Filter(new() { HasText = "Where", HasNotText = "context" });
await token.First.HoverAsync();

// Wait for hover widget
var hoverContent = page.Locator(".monaco-hover .hover-contents");
await Expect(hoverContent).ToBeVisibleAsync();
```

### Reading Editor Content

```csharp
// Check text visible in editor view lines
await Expect(
    E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
).ToContainTextAsync("TABTEST_ALPHA", new() { Timeout = 5_000 });
```

---

## Query Execution Pattern

### MockQueryExecutionService — Configure Results Before Clicking Execute

The test fixture replaces the real execution service with a mock (600ms simulated delay).
Configure the mock result IMMEDIATELY before clicking Execute to avoid race conditions:

```csharp
// Configure mock result
_app.MockQueryExecutionService.SetNextResult(
    E2ETestHelpers.CreateMultiColumnResult(rows: 3));

// Then immediately execute
var executeBtn = page.GetByTestId("execute-query-btn");
await executeBtn.ClickAsync();

// Wait for result grid
var resultContainer = page.GetByTestId("query-result-container");
var resultTable = resultContainer.Locator(".mud-table-root");
await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 });
```

### CreateMultiColumnResult Helper

```csharp
// Returns { ColumnNames: ["Id", "Name", "Value"], rows with some nulls }
var result = E2ETestHelpers.CreateMultiColumnResult(rows: 5);

// Empty result (shows "Query returned no results." alert)
_app.MockQueryExecutionService.SetNextResult(
    QueryExecutionResult.Empty(TimeSpan.FromMilliseconds(10)));
```

### Executing State Assertions

```csharp
// During execution (mock has 600ms delay — loading state IS visible)
var stopBtn = page.GetByTestId("stop-query-btn");
await Expect(stopBtn).ToBeVisibleAsync(new() { Timeout = 5000 });

// After execution
await Expect(executeBtn).ToBeVisibleAsync();
await Expect(stopBtn).Not.ToBeVisibleAsync();
```

---

## Result Grid Locators

Data-testid conventions for QueryResultGrid:

```csharp
// Column headers: data-testid="column-header-{ColumnName}"
var headerId = page.Locator("[data-testid='column-header-Id']");

// Cells: data-testid="cell-{rowIndex}-{ColumnName}" (0-based row index)
var firstCell = page.Locator("[data-testid='cell-0-Id']");
var nullCell  = page.Locator("[data-testid='cell-0-Value']");
await Expect(nullCell).ToContainTextAsync("NULL");

// Selection count (appears when rows are selected)
var selectionCount = page.GetByTestId("selection-count");
await Expect(selectionCount).ToContainTextAsync("1", new() { UseInnerText = true });
```

Click a cell to select the row; Ctrl+Click for multi-select:

```csharp
var cell = page.Locator("[data-testid='cell-0-Name']");
await cell.ClickAsync();
await secondCell.ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Control] });
```

---

## Clipboard Tests

Clipboard tests require explicit browser permissions. Create context with permissions:

```csharp
await using var context = await _pw.Browser.NewContextAsync(new()
{
    Permissions = ["clipboard-read", "clipboard-write"]
});
var page = await context.NewPageAsync();
```

### Clipboard Copy Pattern (Ctrl+C on grid)

```csharp
// Clear stale clipboard content first
await page.EvaluateAsync("navigator.clipboard.writeText('')");

// Press on the container element (guarantees focus, sends trusted event)
var gridContainer = page.Locator(".query-result-grid-container");
await gridContainer.PressAsync("Control+c");

// Poll for non-empty content (Blazor Server round-trip is variable)
var clipboardContent = "";
for (var attempt = 0; attempt < 20; attempt++)
{
    await Task.Delay(150);
    clipboardContent = await page.EvaluateAsync<string>("navigator.clipboard.readText()");
    if (!string.IsNullOrEmpty(clipboardContent)) break;
}
Assert.NotEmpty(clipboardContent);
Assert.Contains("\t", clipboardContent); // TSV format uses tabs
```

Poll instead of fixed delay: keydown → SignalR → C# handler → JS interop → clipboard API. Round-trip time is variable.

---

## Screenshot on Failure / Retry

Capture a screenshot on failure, then retry once before treating it as real:

```csharp
try { /* ... test assertions */ }
catch
{
    await page.ScreenshotAsync(new() { Path = $"failure-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png", FullPage = true });
    throw;
}
```

If a test fails, re-run: `dotnet test --filter "FullyQualifiedName~MyTestName"`.  
If it fails twice, investigate. Do NOT add `Thread.Sleep` or inflate timeouts — find the root cause.

---

## Common Timing Patterns

| Situation | Pattern |
|-----------|---------|
| After typing in Monaco | `await E2ETestHelpers.WaitForDebounceAsync()` (500ms) |
| After opening a MudMenu | `await Task.Delay(100)` |
| After `ClickTabAtIndexAsync` | Built-in 400ms delay + Monaco focus — no extra wait needed |
| After URL pushState navigation | `await Task.Delay(300)` — Blazor popstate processing |
| After drag/resize | `await Task.Delay(300)` |
| After cancelling execution | Wait for stop button to disappear: `await Expect(stopBtn).Not.ToBeVisibleAsync(...)` |
| Waiting for results | `await Expect(resultTable).ToBeVisibleAsync(new() { Timeout = 10000 })` |
| Clipboard content | Poll loop (20 × 150ms) — never a fixed wait |

---

## Running Tests

```bash
# Run all E2E tests
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj

# Run a specific test
dotnet test --filter "FullyQualifiedName~Execute_ShowsResults_WhenQuerySucceeds"

# Run with verbose output (shows stdout/stderr)
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj -v detailed
```

Install Playwright browsers if not present (required before first run):
```bash
pwsh tests/LinqStudio.App.WebServer.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

In DEBUG mode, the browser runs **headed** (visible window). In Release, headless.

---

## Anti-Patterns

### ❌ Using GotoAsync for In-App Navigation
Destroys the Blazor SignalR circuit and loses all workspace state. Use `GetByTestId("nav-editor").ClickAsync()` SPA navigation instead (see [SPA Navigation](#spa-navigation--critical-rule)).

### ❌ Querying Without GetActivePanel When Multiple Tabs Are Open
`page.GetByTestId("execute-query-btn")` matches buttons in ALL panels → strict mode violation. Always scope to `E2ETestHelpers.GetActivePanel(page).GetByTestId(...)` when more than one tab is open.

### ❌ Fixed Wait for Clipboard Content
Use the poll loop shown in the [Clipboard Tests](#clipboard-tests) section — not `await Task.Delay(2000)`.

### ❌ Using page.Keyboard Before Focusing Monaco
Keyboard events go to whichever element has focus. Always call `WaitEditorAndFocusAsync(page)` before typing or pressing shortcuts.

### ❌ Setting MockQueryExecutionService Too Early

```csharp
// WRONG — an in-flight execution could consume the configured result
_app.MockQueryExecutionService.SetNextResult(result);
// ... lots of setup code ...
await executeBtn.ClickAsync();

// RIGHT — configure immediately before clicking execute
await E2ETestHelpers.ClearAndWriteQueryAsync(page, "context.People");
_app.MockQueryExecutionService.SetNextResult(result);  // ← right here
await executeBtn.ClickAsync();
```

### ❌ Hardcoding the App URL
```csharp
// WRONG
await page.GotoAsync("http://localhost:5077");
// RIGHT
await page.GotoAsync(_app.BaseUrl.ToString());
```

### ❌ Skipping the inputarea Force-Click After Tab Switch
Use `ClickTabAtIndexAsync` (not a raw `.ClickAsync()` on the tab element) — it handles the Monaco focus transfer automatically.

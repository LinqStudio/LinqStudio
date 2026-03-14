# LinqStudio E2E Tests

## Test Structure

- **EditorE2ETests.cs** — Monaco editor functionality (completions, hover, unsaved indicators)
- **NavMenuE2ETests.cs** — Navigation menu, project lifecycle, unsaved changes prompts
- **DatabaseE2ETests.cs** — Database connectivity with Testcontainers, Aspire dashboard health checks
- **QueryExecutionE2ETests.cs** — Query execution feature: execute button, timeout, results grid, stop

## Database E2E Tests

Uses **Testcontainers** to spin up real MSSQL instances for authentic E2E testing:
- Starts MSSQL container in `InitializeAsync()`
- Seeds with demo data (Customers, Orders, Products, OrderItems) using `BogusDataGenerator` from `LinqStudio.Databases.Tests`
- Tests connection settings UI flow and schema loading

**Note:** Requires Docker to be running. Tests may fail in environments without Docker.

## Aspire Dashboard Test

The `AspireDashboard_ShowsBothDatabases_AsHealthy` test is **skipped** for CI because it requires:
1. Running `dotnet run --project src/LinqStudio.AppHost` first
2. Aspire dashboard accessible at `http://localhost:15888`

To run manually:
```bash
# Terminal 1: Start Aspire AppHost
dotnet run --project src/LinqStudio.AppHost

# Terminal 2: Run the specific test
dotnet test --filter "FullyQualifiedName~AspireDashboard_ShowsBothDatabases_AsHealthy"
```

## MudBlazor Interaction Patterns

MudBlazor components (MudSelect, MudMenu) require specific interaction strategies:
- Hidden inputs: Need to interact with visible parent containers or buttons
- Popovers: Wait for popover list items to appear before clicking
- Use `data-testid` attributes where available
- Increase timeouts for complex component rendering (500ms delays common)
- **MudSelect data-testid**: Wrap MudSelect in `<div data-testid="...">` to get a visible, clickable element. The MudSelect itself forwards attributes to a hidden input. See `timeout-select` in Editor.razor.

## DatabaseTreeView Tests (NEW)

Added comprehensive tests for the DatabaseTreeView component:

### Active Tests (Run Now):
- `DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen`
- `DatabaseTreeView_StillShowsPlaceholder_WhenProjectOpenWithoutConnection`

### Skipped Tests (Require DB Setup):
- `DatabaseTreeView_ShowsTables_WhenProjectWithSQLiteConnectionOpen`
- `DatabaseTreeView_ShowsColumns_WhenTableExpanded`
- `DatabaseTreeView_RefreshButton_ReloadsTableList`

Each skipped test includes detailed implementation notes for setting up SQLite database testing.

### Helper Methods Added to E2ETestHelpers:
```csharp
// Wait for tree view to be visible
await E2ETestHelpers.WaitForDatabaseTreeViewAsync(page);

// Expand a table node
await E2ETestHelpers.ExpandDatabaseTableAsync(page, "dbo.Customers");

// Click refresh button
await E2ETestHelpers.RefreshDatabaseTreeViewAsync(page);
```

### Required data-testid Attributes:
- `db-tree-view` - Root MudTreeView
- `db-tree-placeholder` - No project/connection message
- `db-tree-loading` - Loading indicator
- `db-tree-refresh` - Refresh button
- `table-{FullName}` - Table items (e.g., "table-dbo.Customers")
- `column-{tableName}-{columnName}` - Column items

See full documentation in `.squad/decisions/inbox/jordan-tree-view-tests.md`

## Query Execution E2E Tests (NEW)

Added `QueryExecutionE2ETests.cs` with 10 tests for the query execution feature.

### MockQueryExecutionService
- Located: `tests/LinqStudio.App.WebServer.E2ETests/Services/MockQueryExecutionService.cs`
- Registered as a **singleton** replacing the real `IQueryExecutionService` in `BlazorWebAppFactory`
- Default behavior: 600ms simulated delay, returns error "No database configured (test environment)"
- Key reason for delay: Without a real async yield, Blazor batches `IsExecuting=true` and `IsExecuting=false` state changes, so the loading state is never rendered to the browser
- Exposed via `AppServerFixture.MockQueryExecutionService`

### Configuring test-specific results
```csharp
// Return an empty result set for a specific test
_app.MockQueryExecutionService.SetNextResult(QueryExecutionResult.Empty(TimeSpan.FromMilliseconds(10)));
// SetNextResult is consumed once then resets to the default error result
```

### Tab Navigation in Tests
MudBlazor tabs may reorder in the DOM after selection changes. Use saved URLs for reliable tab switching:
```csharp
var tab1Url = page.Url;  // Save before creating second tab
// ... navigate to second tab ...
var tab2Url = page.Url;  // Save second tab's URL
// Switch using history API (SPA navigation, preserves Blazor circuit state):
await page.EvaluateAsync($"window.history.pushState(null, '', '{tab1Url}')");
await page.EvaluateAsync("window.dispatchEvent(new PopStateEvent('popstate'))");
await page.WaitForURLAsync(tab1Url);
```

### Tests Covered
| Test | What it verifies |
|------|-----------------|
| `Execute_Button_IsVisible_WhenQueryTabIsOpen` | Execute button and timeout select visible; stop button hidden |
| `Execute_ShowsResults_WhenQuerySucceeds` | After execution, result or error appears; execute button restored |
| `Execute_ShowsError_WhenQueryHasCompileError` | Error alert shown after failed execution |
| `Execute_StopButton_CancelsExecution` | Stop button appears during execution; cancels and restores execute button |
| `Execute_Button_IsDisabled_WhenNoQueryOpen` | Execute button hidden when no query tab is open (uses SPA nav, not GotoAsync) |
| `Execute_ShowsExecutingState_DuringExecution` | Loading spinner and "Executing query..." text visible during 600ms mock delay |
| `Execute_TimeoutSelect_IsDisabled_DuringExecution` | Timeout select disabled during execution |
| `Execute_ResultContainer_Exists` | Result container rendered when query tab open, empty before any execution |
| `Execute_TimeoutSelect_HasAllExpectedOptions` | All 6 timeout options (10s/30s/1min/2min/5min/No timeout) present in dropdown |
| `Execute_PerTabState_SwitchingTabsPreservesIndependentResults` | Tab 2 execution state preserved when switching to Tab 1 and back |
| `Execute_ShowsEmptyResultSet_WhenQueryReturnsNoRows` | "Query returned no results." alert when mock returns empty result |

### Important Notes
- **`Execute_Button_IsDisabled_WhenNoQueryOpen`**: Must use SPA navigation (`nav-editor` click), NOT `page.GotoAsync()`. Full page reload resets the Blazor circuit and loses workspace state, causing redirect to home page instead of showing the no-query alert.
- **Race condition prevention**: `Execute_ShowsEmptyResultSet_WhenQueryReturnsNoRows` calls `SetNextResult()` immediately before `executeBtn.ClickAsync()` (not at the start of the test) to minimize the window for other in-flight executions consuming the configured result.
- **MudBlazor timeout-select**: The MudSelect is wrapped in `<div data-testid="timeout-select">` in Editor.razor because MudSelect's UserAttributes go on a hidden input, making `GetByTestId` find a non-visible element.


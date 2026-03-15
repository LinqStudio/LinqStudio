# LinqStudio Blazor Components

## Splitter Lifecycle (Editor Page)

The editor/results draggable splitter (`initSplitter` in `queryResultGrid.js`) is initialized on the **second render** of the Editor component, not `firstRender`. This is because the splitter DOM elements are guarded by `@if (Workspace.IsProjectOpen)` and `@if (Workspace.Queries.CurrentQueryId is not null)` — the first render schedules a 500ms delay and re-render (Monaco timing workaround), the second render is when the elements actually exist.

Guard: `_splitterInitialized` bool prevents double-initialization across subsequent renders.

## IAsyncDisposable Pattern for JS Cleanup

`Editor` implements both `IDisposable` and `IAsyncDisposable`. When navigating away, Blazor calls `DisposeAsync()` which invokes `disposeSplitter` (removes `mousemove`/`mouseup` from `document`) before delegating to synchronous `Dispose()`. This prevents accumulating ghost event listeners on `document` with each Editor page visit.

The JS cleanup registry: `window._splitterCleanups[splitterId]` stores a named closure that removes both listeners and resets cursor state.

## Per-Tab Sort State (QueryResultGrid)

Sort state is stored per query-tab in `QueryExecutionState.SortDefinitions` (a `Dictionary<string, SortDefinition<...>>`). When switching tabs, the stored definitions are passed as `SortDefinitions` parameter to `QueryResultGrid`, which passes them to `MudDataGrid`. After each render, `QueryResultGrid.OnAfterRenderAsync` snapshots `_dataGrid.SortDefinitions` and propagates changes back to the parent via `OnSortDefinitionsChanged` EventCallback.

**Known limitation:** Column drag-drop ORDER is NOT persisted per tab. MudBlazor 8.15.0 `DragDropColumnReordering` on dynamic `TemplateColumn` doesn't expose a column-order-changed event, so there is no mechanism to capture or restore the user's column order when switching tabs.



### Overview
Dialog component for managing database connection settings with real-time connection testing.

### Features
- Database type selection (SqlServer, MySql, PostgreSql, Sqlite)
- Multi-line connection string input
- Connection validation with timeout control (5s, 10s, 15s, 30s, 60s)
- Loading animation during connection testing
- Unsaved changes detection
- Error handling via ErrorHandlingService
- Full localization (English/French)

### UI Location
Connection button is located in the top application bar, to the left of the settings button.

### Implementation Details
- Uses `ConnectionService` for connection testing
- All inputs disabled during connection testing
- MudProgressCircular shows loading state
- Timeout enforced via CancellationTokenSource
- Integration with MudBlazor DialogService

### Testing
- Component tests in `tests/LinqStudio.Blazor.Tests/ConnectionSettingsDialogTests.cs`
- E2E tests in `tests/LinqStudio.App.WebServer.E2ETests/ConnectionE2ETests.cs`

## Database Tree View

### Overview
Left panel component that displays database schema information with lazy-loaded columns. Shows all tables from connected database in a flat list (no schema grouping).

### Features
- Flat table list with schema prefix in name (e.g., `dbo.Customers`, `public.Orders`)
- Lazy loading: columns load only when table is expanded
- Column details: name + formatted type (`varchar(100)`, `decimal(10,2)?`, `int`)
- Primary key and identity column indicators (icons and colors)
- Refresh button to reload table list
- Reactive to workspace changes (auto-refresh on project open/close)
- Dark/light theme support via MudBlazor
- Graceful placeholder when no project/connection

### UI Location
Left drawer below NavMenu, separated by MudDivider. Integrated in MainLayout.razor.

### Implementation Details
- Uses `ProjectWorkspace.CurrentProject.QueryGenerator` (`IDatabaseQueryGenerator`)
- API calls:
  - `GetTablesAsync()` → loads table names on component init
  - `GetTableAsync(table)` → loads columns on first table expansion
- Cache pattern: `Dictionary<string, DatabaseTableDetail>` prevents redundant queries
- Event subscription: `Workspace.WorkspaceChanged` triggers reload and cache clear
- Icons:
  - Storage icon for header
  - TableChart icon for tables
  - Key icon (gold) for primary key columns
  - Bolt icon for identity columns
  - ViewColumn icon for regular columns
- Type formatting:
  - MaxLength: `DataType(MaxLength)` e.g., `nvarchar(50)`
  - Precision+Scale: `DataType(Precision,Scale)` e.g., `decimal(10,2)`
  - Nullable: append `?` e.g., `nvarchar(50)?`

### data-testid Attributes (E2E Testing)
- `db-tree-view` - Tree view container (only present when tables loaded)
- `db-tree-placeholder` - Placeholder text (when no project/connection)
- `db-tree-refresh` - Refresh button
- `db-tree-loading` - Loading progress indicator
- `table-{FullName}` - Each table item (e.g., `table-dbo.Customers`)
- `column-{tableName}-{columnName}` - Each column item (e.g., `column-dbo.Customers-Id`)

### Lifecycle
1. `OnInitialized`: Subscribe to `Workspace.WorkspaceChanged`
2. `OnParametersSetAsync`: Load tables if project open and not yet loaded
3. `OnWorkspaceChanged`: Clear cache, reload tables if project open
4. `Dispose`: Unsubscribe from workspace events

### Testing
- Component tests: `tests/LinqStudio.Blazor.Tests/DatabaseTreeViewComponentTests.cs`
- E2E tests: `tests/LinqStudio.App.WebServer.E2ETests/DatabaseTreeViewE2ETests.cs`
- All tests passing (3 E2E tests skipped, require real database setup)

## Query Result Grid

### Overview
Interactive data grid for displaying LINQ query execution results with SSMS-like features. Shows loading state, errors, empty results, or tabular data with row/cell selection, sorting, and clipboard support.

### Features (Enhanced - 2026-03-13)
- Five distinct states: not executed, loading, error, empty result, success with data
- **MudDataGrid** with dynamic TemplateColumns for modern grid features
- **Row selection**: Click to highlight, Ctrl+Click multi-select, Shift+Click range
- **Cell selection**: Click individual cells, Ctrl+Click multi-select, Shift+Click range (same column)
- **Sorting**: Client-side per-column (SortBy on TemplateColumn)
- **Column reordering**: Drag-and-drop enabled
- **Column resizing**: ResizeMode.Container (resize individual columns)
- **Clipboard copy (Ctrl+C)**: TSV format with column headers
  - Selected cells: only those cells + headers
  - Selected rows: all columns for those rows + headers
- **NULL display**: Shows "NULL" text for null values (SSMS-style)
- **Virtualization**: Enabled for large result sets (FixedHeader + Virtualize)
- **Selection count**: Shows "N rows selected" or "N cells selected"
- Elapsed time formatting (milliseconds or seconds)
- Compilation error vs runtime error distinction

### UI Location
Bottom panel of Editor page, below draggable splitter. ~60% of editor/results container height.

### Parameters
- `QueryExecutionResult? Result` — null when not yet executed, otherwise contains rows/columns/errors
- `bool IsExecuting` — true during query execution (shows spinner)
- `EventCallback<List<string>> OnColumnOrderChanged` — (future) callback for persisting column order

### Implementation Details
- **State 1 - Not Executed** (`Result is null && !IsExecuting`): No visual output
- **State 2 - Loading** (`IsExecuting`): MudProgressCircular + "Executing query..." text
- **State 3 - Error** (`!Result.Success`): 
  - MudAlert (Severity.Error, Variant.Filled)
  - Prefixes "Compilation error: " if `Result.IsCompileError`
  - Shows error message + elapsed time
- **State 4 - Empty Result** (`Result.Success && Rows.Count == 0`):
  - MudAlert (Severity.Info)
  - "Query returned no results." + elapsed time
- **State 5 - Success** (`Result.Success && Rows.Count > 0`):
  - MudDataGrid with TemplateColumn per Result.ColumnName
  - Each row is `IReadOnlyDictionary<string, object>` (nullable removed from generic type for Razor compatibility)
  - Footer: "{N} rows · {elapsed}" + selection count
  - Grid properties: Dense, Hover, Striped, FixedHeader, Virtualize, Height=100%, ColumnResizeMode=Container, DragDropColumnReordering, MultiSelection=false, SelectOnRowClick=false
  
### Selection Architecture
**Per-tab state** (no persistence between page loads):
- `HashSet<int> _selectedRows` — row indices
- `HashSet<(int RowIndex, string ColumnName)> _selectedCells` — cell coordinates
- `_lastClickedRowIndex`, `_lastClickedColumn` — for Shift+Click range selection
- `_isShiftDown`, `_isCtrlDown` — keyboard state tracking

**Row selection:**
- Click row (outside cells): single select
- Ctrl+Click row: toggle multi-select
- Shift+Click row: range select from last clicked row

**Cell selection:**
- Click cell: single select (clears row selection)
- Ctrl+Click cell: toggle multi-select
- Shift+Click cell: range select in same column (vertical range)
- Cell click uses `@onclick:stopPropagation="true"` to prevent row selection

**Styling:**
- `.cell-selected` — primary color background with outline
- `.row-selected td` — action-selected background
- CSS in `QueryResultGrid.razor.css` with scoped styles

### Clipboard Copy (Ctrl+C)
**TSV (Tab-Separated Values) with headers:**
```
ColumnA\tColumnB\tColumnC
Value1\tValue2\tValue3
Value4\tValue5\tValue6
```

**Implementation:**
- Container div has `tabindex="0"` to receive keyboard events
- `@onkeydown` handler detects Ctrl+C
- JS interop: `navigator.clipboard.writeText(tsvString)`
- Handles cell selection (sparse) and row selection (full rows)
- Graceful failure if clipboard API unavailable

### Dynamic Column Pattern (MudDataGrid)
```razor
@foreach (var colName in Result.ColumnNames)
{
    <TemplateColumn T="IReadOnlyDictionary<string, object>"
                    Title="@colName"
                    Sortable="true"
                    SortBy="@(row => row.GetValueOrDefault(colName))">
        <HeaderTemplate>
            <div data-testid="@($"column-header-{colName}")">@colName</div>
        </HeaderTemplate>
        <CellTemplate>
            @{
                var cellValue = context.Item.GetValueOrDefault(colName);
                var cellStr = cellValue is null ? "NULL" : cellValue.ToString();
                var rowIndex = GetRowIndex(context.Item);
                var isSelected = _selectedCells.Contains((rowIndex, colName));
            }
            <div class="@(isSelected ? "cell-selected" : "")"
                 style="width:100%; height:100%; padding:4px"
                 @onclick="@((MouseEventArgs e) => OnCellClick(e, context.Item, colName))"
                 @onclick:stopPropagation="true"
                 data-testid="@($"cell-{rowIndex}-{colName}")">
                @cellStr
            </div>
        </CellTemplate>
    </TemplateColumn>
}
```

**Why MudDataGrid now?**
- MudBlazor 8.15.0 has stable TemplateColumn support
- Advanced features needed: sorting, column resizing, drag-drop reordering
- Virtualization for performance with large result sets
- Modern SSMS-like UX requirements

### Row Index Lookup
```csharp
private int GetRowIndex(IReadOnlyDictionary<string, object> row)
{
    if (Result is null) return -1;
    for (int i = 0; i < Result.Rows.Count; i++)
    {
        if (ReferenceEquals(Result.Rows[i], row)) return i;
    }
    return -1;
}
```
- Uses `ReferenceEquals` for O(1) dictionary identity check
- Avoids LINQ `.IndexOf()` extension (not available on IReadOnlyList<T>)

### Elapsed Time Formatting
- < 1 second: shows milliseconds (e.g., "125ms")
- >= 1 second: shows seconds with 2 decimals (e.g., "1.25s")

### Files (Enhanced)
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor` - Razor markup with MudDataGrid
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs` - Code-behind with selection logic
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor.css` - Scoped CSS for selection styling
- `src/LinqStudio.Blazor/wwwroot/queryResultGrid.js` - (not used by QueryResultGrid directly, but contains splitter JS)

### data-testid Attributes (for E2E tests)
- `query-result-grid` - MudDataGrid component
- `column-header-{ColumnName}` - Each column header
- `cell-{RowIndex}-{ColumnName}` - Each cell (e.g., `cell-0-Id`, `cell-1-Name`)
- `selection-count` - Selection count indicator div

### Integration
Pure display component. Caller (Editor.razor) manages:
- Query execution trigger (Execute button)
- State transitions (IsExecuting flag)
- Passing QueryExecutionResult after execution completes
- Per-tab state persistence (selection resets on tab switch)

### Localization
Messages currently hard-coded in English. Can be extracted to SharedResource.resx if internationalization is needed.

### Testing
- Component tests: Pending (test selection logic, clipboard copy)
- E2E tests: Pending (test full grid interaction flow)

### Known Issues
- Clipboard API fails gracefully if navigator.clipboard unavailable (older browsers)
- Selection state not persisted (resets on tab switch, page reload) — by design per requirements

## Editor (Query Execution Integration)

### Overview
Main query editing page with Monaco code editor, IntelliSense, and query execution capabilities. Supports multiple tabs with per-tab execution state.

### Features
- Monaco code editor with C# syntax highlighting and dark/light theme
- Roslyn-powered IntelliSense (completion + hover) with EF Core schema
- Query execution with configurable timeout
- Stop/cancel running queries
- Per-tab result persistence (switching tabs doesn't clear results)
- Refresh schema from database
- Auto-save query changes with debouncing
- Unsaved changes detection

### UI Components

**Query Tabs**
- Shows all open queries with unsaved indicator (*)
- Click to switch between tabs

**Query Info Bar**
- Query name display
- Unsaved changes chip
- Save and Close buttons

**Monaco Editor**
- Full-featured code editor with IntelliSense
- Auto-complete on typing and Ctrl+Space
- Hover tooltips for type information
- Syntax highlighting for C#

**Query Execution Bar** (2026-03-13 update)
- Execute/Stop button (toggles based on execution state)
  - Execute (▶, Primary color) when idle
  - Stop (■, Error color) during execution
- Timeout dropdown with 6 options:
  - 10s, 30s, 1 min, 2 min, 5 min, No timeout
  - Default: 30s (from QueryExecutionSettings)
  - Disabled during execution
- Refresh Schema button (right side)

**Query Result Container** (2026-03-13 update)
- QueryResultGrid component shows results
- Scrollable for large result sets
- Persisted per-tab (switching tabs preserves results)

**Editor Info Bar**
- IntelliSense information text

### Per-Tab Execution State (2026-03-13)

**Pattern:**
```csharp
private class QueryExecutionState
{
    public QueryExecutionResult? Result { get; set; }
    public bool IsExecuting { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
private readonly Dictionary<Guid, QueryExecutionState> _executionStates = new();
```

**State Management:**
- Each query tab (identified by Guid) has independent execution state
- Switching tabs preserves:
  - Query results
  - Execution status (if running)
  - Cancellation token
- Helper method `GetCurrentExecutionState()` retrieves/creates state for current tab

**Why Dictionary<Guid, T>?**
- Matches existing QueriesWorkspace pattern (OpenQueries dictionary)
- Query ID is the stable identifier throughout Editor
- No coupling to tab order or component lifecycle
- Clean separation of concerns

### Query Execution Flow (2026-03-13)

**Execute:**
1. Validate editor and query ID exist
2. Get current tab's execution state
3. Cancel any existing execution for this tab
4. Get query text: `await _editor.GetValue()`
5. Create CancellationTokenSource with selected timeout:
   - Timeout > 0: `new CancellationTokenSource(TimeSpan.FromSeconds(timeout))`
   - Timeout = 0: `new CancellationTokenSource()` (no timeout)
6. Set `IsExecuting = true`, clear previous result
7. Call `await QueryExecutionService.ExecuteQueryAsync(queryText, cts.Token)`
8. Store result in tab's state
9. Show snackbar: "{N} row(s) returned" (success) or "Query execution cancelled" (cancelled)
10. Log and handle exceptions: "Unexpected error: {message}"
11. Set `IsExecuting = false`, dispose CancellationTokenSource
12. `StateHasChanged()` to update UI

**Stop:**
- Cancel the current tab's CancellationTokenSource
- Execution method handles cleanup when cancellation detected
- Result shows: "Query execution was cancelled"

### Monaco Editor Integration

**Initialization:**
- 500ms delay workaround for BlazorMonaco resource loading (known issue)
- Creates CompilerService from project schema (or demo model on failure)
- Registers completion and hover providers via MonacoProvidersService

**Content Changes:**
- Debounced updates (300ms) to avoid excessive workspace updates
- Uses CancellationTokenSource pattern (no exception throwing)
- Updates QueriesWorkspace with new text after debounce

**IntelliSense:**
- Completion provider: Roslyn completions → Monaco CompletionItems
- Hover provider: Roslyn quick info → Monaco Hover markdown
- Completion item kind mapping: Property, Method, Field, Class

### DI Injections
- `ILogger<Editor>` - Logging
- `ISnackbar` - User notifications
- `ErrorHandlingService` - Error dialogs
- `MonacoProvidersService` - Monaco provider lifecycle
- `CompilerServiceFactory` - Create Roslyn compiler instances
- `IDbContextGenerator` - Generate DbContext code
- `IOptionsMonitor<UISettings>` - Theme settings
- `IOptionsMonitor<QueryExecutionSettings>` - Timeout default (2026-03-13)
- `ProjectWorkspace` - Project + query state
- `NavigationManager` - Routing
- `IDialogService` - Unsaved changes dialogs
- `IFileSystemService` - Save file dialogs
- `IQueryExecutionService` - Execute queries (2026-03-13)
- `IJSRuntime` - JS interop for splitter (2026-03-13)

### Lifecycle
1. `OnInitialized`: Subscribe to WorkspaceChanged, load timeout default
2. `OnParametersSet`: Load query by ID from route, redirect if no project
3. `OnAfterRenderAsync`: 
   - 500ms delay then render Monaco (timing workaround)
   - Initialize splitter via JSRuntime on first render (2026-03-13)
4. `OnEditorInitialized`: Create CompilerService, register providers
5. `OnEditorContentChanged`: Debounce updates to workspace
6. `Dispose`: Unsubscribe events, dispose providers, dispose compiler, cancel all executions (2026-03-13)

### Error Handling
- CompilerService initialization failure → fallback to demo model
- Completion/hover provider errors → logged, return null (non-breaking)
- Query execution errors → error result with message
- Cancellation → "Query execution was cancelled"
- Unexpected exceptions → logged + error result

### Routes
- `/editor` - No query selected (shows placeholder)
- `/editor/new` - Create new query (not yet implemented)
- `/editor/{QueryIdParam:guid}` - Open specific query

### data-testid Attributes
- `editor-page` - Page container
- `query-info-bar` - Query info bar
- `query-name-display` - Query name text
- `query-unsaved-indicator` - Unsaved changes chip
- `query-save-btn` - Save button
- `query-close-btn` - Close button
- `monaco-editor-container` - Monaco editor container
- `editor-results-splitter` - Draggable splitter (2026-03-13)
- `query-execution-bar` - Execution bar (2026-03-13)
- `execute-query-btn` - Execute button (2026-03-13)
- `stop-query-btn` - Stop button (2026-03-13)
- `timeout-select` - Timeout dropdown (2026-03-13)
- `refresh-schema-btn` - Refresh schema button
- `query-result-container` - Result grid container (2026-03-13)
- `editor-info-bar` - Editor info bar
- `no-query-alert` - No query placeholder
- `no-project-alert` - No project placeholder

### Files
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor` - Razor markup
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.cs` - Code-behind logic
- `src/LinqStudio.Blazor/Components/Pages/Editor/Editor.razor.css` - Scoped CSS (splitter + layout - 2026-03-13)
- `src/LinqStudio.Blazor/wwwroot/queryResultGrid.js` - JS interop for splitter (2026-03-13)

### Testing
- Component tests: `tests/LinqStudio.Blazor.Tests/EditorComponentTests.cs`
- E2E tests: `tests/LinqStudio.App.WebServer.E2ETests/EditorE2ETests.cs`
- All tests passing as of 2026-03-13 (485 tests, 4 skipped, 0 failed)

### Known Issues
- Monaco 500ms initialization delay workaround (BlazorMonaco resource loading timing)
- CompilerService uses ALL AppDomain assemblies as metadata references (large memory footprint)

### Future Considerations
- Keyboard shortcut: Ctrl+Enter to execute query
- Show elapsed time indicator during execution
- Execute selection (partial query)
- Persist timeout selection in QueryExecutionSettings
- Query history panel

## QueryResultGrid Selection Model (Row-Only, SSMS-Style)

Cell selection was removed. The grid now uses **row-only selection**:
- Click a row to select it (clears previous selection)
- Ctrl+Click or Meta+Click to toggle a row in/out of selection
- Shift+Click to range-select rows from _lastClickedRowIndex to clicked row
- Ctrl+C copies selected rows as TSV (header + rows)

Row click is handled via MudDataGrid.RowClick event. DataGridRowClickEventArgs.MouseEventArgs provides modifier key state (CtrlKey, MetaKey, ShiftKey) directly — no need for field-based _isCtrlDown/_isShiftDown tracking.

For future cell editing: re-introduce @onclick handlers on cell <div>s and restore cell selection state.

# LinqStudio Blazor Components

## Connection Settings Dialog

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
Display component for LINQ query execution results. Shows loading state, errors, empty results, or tabular data with dynamic columns.

### Features
- Five distinct states: not executed, loading, error, empty result, success with data
- Dynamic column rendering from dictionary-based result sets
- Elapsed time formatting (milliseconds or seconds)
- Compilation error vs runtime error distinction
- MudTable for reliable dynamic column rendering
- Fixed height (400px) with scrolling for large result sets
- Row count footer

### UI Location
Bottom panel of Editor page. Integrated below Monaco editor component (future Editor.razor work).

### Parameters
- `QueryExecutionResult? Result` — null when not yet executed, otherwise contains rows/columns/errors
- `bool IsExecuting` — true during query execution (shows spinner)

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
  - MudTable with dynamic columns from `Result.ColumnNames`
  - Each row is `IReadOnlyDictionary<string, object?>`
  - Footer: "{N} rows · {elapsed}"
  - Table properties: Dense, Hover, Striped, FixedHeader, Height=400px, Elevation=2

### Dynamic Column Pattern
Uses MudTable (not MudDataGrid) for reliability:
```razor
<HeaderContent>
    @foreach (var col in Result.ColumnNames) { <MudTh>@col</MudTh> }
</HeaderContent>
<RowTemplate>
    @foreach (var col in Result.ColumnNames) { <MudTd>@context.GetValueOrDefault(col)?.ToString()</MudTd> }
</RowTemplate>
```

**Why MudTable?**
- MudDataGrid with TemplateColumn in foreach loops has column ordering issues in Blazor Server (MudBlazor 8.x)
- MudTable's HeaderContent/RowTemplate pattern is stable and predictable
- No advanced DataGrid features needed (sorting/filtering happens in LINQ query)

### Elapsed Time Formatting
- < 1 second: shows milliseconds (e.g., "125ms")
- >= 1 second: shows seconds with 2 decimals (e.g., "1.25s")

### Files
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor` - Razor markup
- `src/LinqStudio.Blazor/Components/QueryResultGrid.razor.cs` - Code-behind with FormatElapsedTime helper

### Integration
Pure display component. Caller (Editor.razor) manages:
- Query execution trigger (Execute button)
- State transitions (IsExecuting flag)
- Passing QueryExecutionResult after execution completes

### Localization
Messages currently hard-coded in English. Can be extracted to SharedResource.resx if internationalization is needed.

### Testing
- Component tests: Pending (Jordan's task - mock various QueryExecutionResult states)
- E2E tests: Pending (Alice's task - full query execution flow)

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

### Lifecycle
1. `OnInitialized`: Subscribe to WorkspaceChanged, load timeout default
2. `OnParametersSet`: Load query by ID from route, redirect if no project
3. `OnAfterRenderAsync`: 500ms delay then render Monaco (timing workaround)
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

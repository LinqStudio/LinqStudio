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

# LinqStudio Connection Settings

## Overview
Dialog component for managing database connection settings with real-time connection testing.

### Features
- Database type selection (SqlServer, MySql, PostgreSql, Sqlite)
- Multi-line connection string input
- Connection validation with timeout control (5s, 10s, 15s, 30s, 60s)
- Loading animation during connection testing
- Unsaved changes detection
- Error handling via ErrorHandlingService
- Full localization (English/French)
- **Connection naming**: Each connection has a user-provided name

### UI Location
~~Connection button is located in the top application bar, to the left of the settings button.~~
Connection button is now located in the Object Explorer panel on the left side of the application.

### Implementation Details
- Uses `ConnectionService` for connection testing
- Integrates with `ObjectExplorerService` to add connections
- All inputs disabled during connection testing
- MudProgressCircular shows loading state
- Timeout enforced via CancellationTokenSource
- Integration with MudBlazor DialogService

### Testing
- Component tests in `tests/LinqStudio.Blazor.Tests/ConnectionSettingsDialogTests.cs`
- E2E tests in `tests/LinqStudio.App.WebServer.E2ETests/ConnectionE2ETests.cs`

---

# Object Explorer Panel Implementation

## Overview
Left-side panel tree view that displays database connections and their schema information (tables, columns, foreign keys).

## Key Components

### ObjectExplorerService
- **Location**: `src/LinqStudio.Core/Services/ObjectExplorerService.cs`
- **Type**: Singleton service
- **Purpose**: Manages multiple database connections and caches their metadata
- **Features**:
  - Add/remove connections
  - Cache table lists and table details per connection
  - Refresh individual connections or all connections
  - Event-driven updates via `ConnectionsChanged` event

### UI Components
- **ObjectExplorerPanel**: Main panel component with "Add Connection" and "Refresh All" buttons
- **ConnectionNodeComponent**: Displays individual database connection with expand/collapse
- **TableNodeComponent**: Shows table details including columns and foreign keys on expansion

### Integration Points
- **MainLayout**: Object explorer replaces NavMenu in the left drawer
- **ConnectionSettingsDialog**: Enhanced to include connection name field and integrate with ObjectExplorerService
- **AppBar**: Connection button removed (now in object explorer)

## Features
- ✅ Lazy loading - table details only loaded when expanded
- ✅ Caching - table lists and details cached per connection
- ✅ Multiple connections supported
- ✅ Hierarchical view: Connection → Tables → Columns/Foreign Keys
- ✅ Refresh individual connection or all connections
- ✅ Localized in English and French

## Testing
- **Unit Tests**: 8 new tests for ObjectExplorerService (all passing)
- **E2E Tests**: Updated existing connection tests + 2 new object explorer tests
- **Coverage**: Add connection, remove connection, caching, refresh operations

## Database Support
- Added SQL Server to Aspire AppHost for development
- DatabaseSeederService seeds test data on startup (10 customers, 20 products, orders, order items)
- Uses Bogus library for generating fake data

## Technical Notes
- MudBlazor MudList/MudListItem components used for tree-like structure
- Resource strings manually added to SharedResource.Designer.cs due to .resx generation issues
- E2E tests require Playwright browser installation before running


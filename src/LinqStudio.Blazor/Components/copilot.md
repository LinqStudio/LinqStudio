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

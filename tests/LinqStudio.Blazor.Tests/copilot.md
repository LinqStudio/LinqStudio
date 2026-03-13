# DatabaseTreeView Tests - Quick Reference

## Component Tests (bUnit)
**File:** `tests/LinqStudio.Blazor.Tests/DatabaseTreeViewComponentTests.cs`

```bash
# Run all component tests
dotnet test tests/LinqStudio.Blazor.Tests/LinqStudio.Blazor.Tests.csproj --filter "FullyQualifiedName~DatabaseTreeView"

# Run specific test
dotnet test tests/LinqStudio.Blazor.Tests/LinqStudio.Blazor.Tests.csproj --filter "FullyQualifiedName~DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen"
```

### Test List:
- ✅ `DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen`
- ✅ `DatabaseTreeView_ShowsLoadingIndicator_WhenProjectOpenButNoConnectionString`
- ✅ `DatabaseTreeView_ShowsPlaceholder_WhenProjectOpenButNoConnection`
- ✅ `DatabaseTreeView_ComponentRenders_WithoutErrors`
- ✅ `DatabaseTreeView_InjectsRequiredServices`

## E2E Tests (Playwright)
**File:** `tests/LinqStudio.App.WebServer.E2ETests/DatabaseTreeViewE2ETests.cs`

```bash
# Run all E2E tests (requires Playwright browsers installed)
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj --filter "FullyQualifiedName~DatabaseTreeView"

# Run only active tests (skip the ones requiring DB setup)
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj --filter "FullyQualifiedName~DatabaseTreeView_ShowsPlaceholder"
```

### Test List:
- ✅ `DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen` (Active)
- ✅ `DatabaseTreeView_StillShowsPlaceholder_WhenProjectOpenWithoutConnection` (Active)
- ⏭️ `DatabaseTreeView_ShowsTables_WhenProjectWithSQLiteConnectionOpen` (Skipped - requires DB)
- ⏭️ `DatabaseTreeView_ShowsColumns_WhenTableExpanded` (Skipped - requires DB)
- ⏭️ `DatabaseTreeView_RefreshButton_ReloadsTableList` (Skipped - requires DB)

## Helper Methods
**File:** `tests/LinqStudio.App.WebServer.E2ETests/Helpers/E2ETestHelpers.cs`

```csharp
// Wait for tree view to load
await E2ETestHelpers.WaitForDatabaseTreeViewAsync(page);

// Expand a specific table
await E2ETestHelpers.ExpandDatabaseTableAsync(page, "dbo.Customers");

// Click refresh button
await E2ETestHelpers.RefreshDatabaseTreeViewAsync(page);
```

## Component Requirements

### Required `data-testid` Attributes:
```razor
<!-- Root tree view -->
<MudTreeView data-testid="db-tree-view">
  
  <!-- Table items -->
  <MudTreeViewItem data-testid="table-@table.FullName">
    
    <!-- Column items -->
    <MudTreeViewItem data-testid="column-@table.FullName-@column.Name">
```

### Special Elements:
- `data-testid="db-tree-placeholder"` - No project/connection message
- `data-testid="db-tree-loading"` - Loading spinner
- `data-testid="db-tree-refresh"` - Refresh button

## Expected Behavior

1. **No Project Open** → Show placeholder
2. **Project Open, No Connection** → Show placeholder
3. **Project Open, Has Connection** → Load and show tables
4. **Table Expanded** → Load and show columns
5. **Refresh Clicked** → Reload table list

## Notes for EvilJosh

When you implement the component:
1. All testid attributes are documented above
2. Tests expect `ProjectWorkspace` and `ErrorHandlingService` injection
3. Tests expect `QueryGenerator.GetTablesAsync()` for table list
4. Tests expect `QueryGenerator.GetTableAsync()` for column details
5. Column display format expected: `"Name: type(length)?"` (e.g., "Name: nvarchar(50)?")
6. Table testid uses `FullName` property (handles schema.table format)

## Running Tests During Development

**Option 1: Watch mode (recommended)**
```bash
# Component tests
dotnet watch test --project tests/LinqStudio.Blazor.Tests/LinqStudio.Blazor.Tests.csproj --filter "DatabaseTreeView"

# E2E tests
dotnet watch test --project tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj --filter "DatabaseTreeView"
```

**Option 2: Single run**
```bash
# All tests
./build.ps1 Test

# Just your feature
dotnet test --filter "DatabaseTreeView"
```

## Current Status

- ✅ Tests compile successfully
- ✅ Tests are discoverable by xUnit
- ⏳ Waiting for DatabaseTreeView.razor component implementation
- ⏳ Some tests will fail until component exists (expected)
- ✅ Basic infrastructure tests should pass now (placeholder, DI, etc.)

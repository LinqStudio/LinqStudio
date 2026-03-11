# LinqStudio E2E Tests

## Test Structure

- **EditorE2ETests.cs** — Monaco editor functionality (completions, hover, unsaved indicators)
- **NavMenuE2ETests.cs** — Navigation menu, project lifecycle, unsaved changes prompts
- **DatabaseE2ETests.cs** — Database connectivity with Testcontainers, Aspire dashboard health checks

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

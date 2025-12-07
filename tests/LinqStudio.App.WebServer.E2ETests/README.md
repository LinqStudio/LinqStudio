# LinqStudio E2E Tests

This directory contains end-to-end (E2E) tests for the LinqStudio web application using Playwright for browser automation.

## Prerequisites

### Playwright Browser Installation

**IMPORTANT**: E2E tests require Playwright browsers to be installed. Tests will fail if browsers are not installed.

To install Playwright browsers:

```bash
# From the test project directory
pwsh bin/Debug/net10.0/playwright.ps1 install --with-deps chromium

# Or from solution root
pwsh tests/LinqStudio.App.WebServer.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

The `--with-deps` flag installs required system dependencies (fonts, libraries, etc.) needed for Chromium to run in CI/headless environments.

## Test Structure

### Directory Layout

```
LinqStudio.App.WebServer.E2ETests/
├── Fixtures/                    # xUnit test fixtures
│   ├── AppServerFixture.cs     # Manages web server lifecycle
│   └── PlaywrightFixture.cs    # Manages browser lifecycle
├── EditorE2ETests.cs           # E2E tests for Monaco editor
├── README.md                   # This file
└── LinqStudio.App.WebServer.E2ETests.csproj
```

### Fixtures

#### AppServerFixture
- **Purpose**: Starts and manages the LinqStudio web server for E2E tests
- **Lifecycle**: Shared across all tests in a collection (one server instance per collection)
- **Port**: Fixed at `http://127.0.0.1:5020`
- **Initialization**: 
  - Starts the web server using `dotnet run`
  - Polls the server for up to 60 seconds until it responds
  - Captures stdout/stderr for diagnostics if startup fails
- **Cleanup**: Kills the server process tree on disposal

#### PlaywrightFixture
- **Purpose**: Creates and manages Playwright browser instances
- **Lifecycle**: Shared across all tests in a collection (one browser instance per collection)
- **Browser**: Chromium in headless mode
- **Initialization**: 
  - Creates Playwright instance
  - Launches Chromium browser
  - **FAILS if browsers not installed** (intentional - tests require proper environment)
- **Cleanup**: Closes browser and disposes Playwright on disposal

### Test Collections

Tests are organized into xUnit collections using the `[Collection]` attribute:

```csharp
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AppServerFixture>, ICollectionFixture<PlaywrightFixture>
{
}

[Collection("E2E")]
public class EditorE2ETests
{
    // Tests share the same AppServerFixture and PlaywrightFixture instances
}
```

**Key Points:**
- All tests in the same collection share fixture instances
- Tests within a collection run sequentially
- Different collections can run in parallel (xUnit default behavior)

## Test Implementation Patterns

### Fresh Page Load Per Test

Each test ensures a clean state by:
1. Creating a new browser context (`await _pw.Browser.NewContextAsync()`)
2. Creating a new page (`await context.NewPageAsync()`)
3. Using cache-busting URL parameters to force fresh page loads:

```csharp
await page.GotoAsync(_app.BaseUrl + $"/editor?_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
```

This approach:
- Prevents test interference from cached Blazor state
- Ensures each test starts with a fresh Blazor SignalR connection
- Avoids issues with stale DOM elements between tests

### Waiting for Elements

Tests use Playwright's built-in wait mechanisms:

```csharp
// Wait for selector to be present in DOM
await page.WaitForSelectorAsync("#editor-top .monaco-editor");

// Use Locator API for more complex queries
var element = page.Locator(".view-lines .view-line span")
    .Filter(new() { HasText = "Where" })
    .First;
```

**Best Practices:**
- Use `WaitForSelectorAsync` for critical elements before interaction
- Specify timeouts for operations that might be slow (`new() { Timeout = 10000 }`)
- Use Playwright Locators for type-safe element interactions

## Current Tests

### Editor_ShowsCompletions_WhenTyping
- **Purpose**: Verifies Monaco editor autocomplete functionality
- **Steps**:
  1. Navigate to editor page
  2. Wait for Monaco editor to load
  3. Focus the editor
  4. Trigger completions with `Ctrl+Space`
  5. Verify completion suggestions appear
- **Assertion**: At least one non-empty completion suggestion is shown

### Editor_Hover_ShowsSymbolInfo
- **Purpose**: Verifies Monaco editor hover tooltip functionality
- **Steps**:
  1. Navigate to editor page
  2. Wait for Monaco editor to load
  3. Wait for editor content to render
  4. Find and hover over "Where" keyword token
  5. Verify hover tooltip appears with content
- **Assertion**: Hover content is non-empty and contains "Where"

## Running Tests

### Command Line

```bash
# Run all E2E tests
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj

# Run with verbose output
dotnet test tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj -v detailed

# Run specific test
dotnet test --filter "FullyQualifiedName~Editor_ShowsCompletions_WhenTyping"
```

### Nuke Build System

```bash
# Run all tests (unit + E2E)
./build.sh Test

# Windows
./build.cmd Test

# PowerShell
./build.ps1 Test
```

## Troubleshooting

### Browser Not Installed Error

**Symptom**: Tests fail with Playwright exception about missing browsers

**Solution**: Install Playwright browsers:
```bash
pwsh tests/LinqStudio.App.WebServer.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

### Tests Timeout

**Symptom**: Tests timeout waiting for Monaco editor

**Possible Causes:**
1. Web server failed to start - check AppServerFixture logs
2. Monaco initialization issue - verify sample code loads in browser manually
3. Network/timing issue - may need to increase timeout values

**Debug Steps:**
1. Check server is running: `curl http://127.0.0.1:5020/editor`
2. Run test with screenshots enabled (modify test to call `await page.ScreenshotAsync()`)
3. Check test output for detailed Playwright error messages

### Hover Test Fails

**Symptom**: Hover test fails to find hover content

**Known Issue**: Hover provider may not trigger consistently in headless mode

**Workaround**: 
- Tests use cache-busting URLs to ensure fresh page loads
- Each test gets its own browser context to avoid state leakage

## CI/CD Considerations

### GitHub Actions

The Nuke build system includes a `PlaywrightInstall` target that automatically installs browsers in CI:

```yaml
# .github/workflows/ci.yml example
- name: Run Tests
  run: ./build.sh Test
```

The Nuke build ensures Playwright browsers are installed before running E2E tests.

### Test Isolation

- Each test creates a fresh browser context and page
- Cache-busting URLs ensure no Blazor state carryover
- Server fixture is shared but each page load creates a new SignalR connection

## Adding New Tests

To add a new E2E test:

1. Add a new test method to `EditorE2ETests` or create a new test class
2. Follow the established pattern:
   ```csharp
   [Fact(Timeout = 120_000)]
   public async Task MyNewTest()
   {
       await using var context = await _pw.Browser!.NewContextAsync();
       var page = await context.NewPageAsync();
       
       // Use cache-busting URL
       await page.GotoAsync(_app.BaseUrl + $"/mypage?_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
       
       // Wait for critical elements
       await page.WaitForSelectorAsync("#my-element");
       
       // Test interactions and assertions
       // ...
   }
   ```
3. Use xUnit `[Collection("E2E")]` attribute to share fixtures
4. Include clear comments explaining test purpose and steps

## Best Practices

1. **Always use cache-busting URLs** to ensure fresh page loads
2. **Wait for critical elements** before interacting with them
3. **Use Playwright Locators** for type-safe element queries
4. **Set appropriate timeouts** for slow operations (Monaco initialization, etc.)
5. **Keep tests independent** - each test should work in isolation
6. **Use descriptive test names** - follow pattern `ComponentName_Action_ExpectedResult`
7. **Add comments** for complex test logic or workarounds
8. **Dispose contexts properly** - use `await using` for browser contexts

## Resources

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/)
- [xUnit Documentation](https://xunit.net/)
- [Monaco Editor API](https://microsoft.github.io/monaco-editor/api/index.html)

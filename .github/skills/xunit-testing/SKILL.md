---
name: xunit-testing
description: Patterns for writing and maintaining tests in LinqStudio — XUnit conventions, embedded resources, in-memory fakes, bUnit Blazor tests, and E2E test structure. Use this when adding or modifying tests in any tests/ project.
---

# XUnit Testing — LinqStudio Skill

## When to Use This Skill

Read this before:
- Adding tests for a new service, repository, or Blazor component
- Modifying an existing test to account for a code change
- Adding E2E coverage for a new UI flow
- Setting up a new test project or adding test data files

---

## Test Project Structure

```
tests/
  LinqStudio.Core.Tests/          # Unit tests for core services and logic
  LinqStudio.Databases.Tests/     # Database-specific integration tests
  LinqStudio.Blazor.Tests/        # Blazor component unit tests (bUnit)
  LinqStudio.App.WebServer.Tests/ # Web server unit tests
  LinqStudio.App.WebServer.E2ETests/ # Playwright end-to-end tests
```

Each project references only its immediate production project:
- `Core.Tests` → `LinqStudio.Core.csproj`
- `Blazor.Tests` → `LinqStudio.Blazor.csproj`
- `E2ETests` → `LinqStudio.App.WebServer.csproj`

---

## Running Tests

**Always run all tests — never run a single project or filter:**

```powershell
# Via Nuke build system (preferred)
./build.ps1 Test

# Via dotnet CLI (acceptable)
dotnet test

# Install Playwright browsers before first E2E run
./build.ps1 PlaywrightInstall
# or manually:
pwsh playwright.ps1 install --with-deps chromium
```

> **Rule:** Run ALL tests after any change. Never scope to a single test file or test name. A passing test suite means ALL tests pass.

---

## XUnit Conventions

### Basic Test Shape

```csharp
namespace LinqStudio.Core.Tests;

public class MyServiceTests
{
    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var svc = CreateService();
        // Act
        var result = await svc.DoSomethingAsync();
        // Assert
        Assert.NotNull(result);
    }
}
```

### Naming Convention

`MethodName_Scenario_ExpectedBehavior`

### Assert Methods to Use

| What to test | Use |
|---|---|
| Object is non-null | `Assert.NotNull(obj)` |
| Collection is non-empty | `Assert.NotEmpty(collection)` |
| Collection is empty | `Assert.Empty(collection)` |
| String contains substring | `Assert.Contains("text", str)` |
| Predicate holds for at least one | `Assert.Contains(collection, x => predicate)` |
| Predicate holds for all | `Assert.All(collection, x => Assert.NotNull(x))` |
| Values are equal | `Assert.Equal(expected, actual)` |
| Boolean is true/false | `Assert.True(condition, "message")` / `Assert.False(...)` |
| String is not null or whitespace | `Assert.False(string.IsNullOrWhiteSpace(val))` |

**Never use FluentAssertions.** Standard `Assert.*` only.

### Global Using

`xunit` is in `<Using Include="Xunit" />` in every test `.csproj`, so no `using Xunit;` import is needed in test files. Other namespaces must still be imported explicitly.

### IDisposable / IAsyncDisposable Services

Wrap disposable services in `using var svc = ...` or `await using var ctx = ...`.

### Skipping Tests (Exceptional Only)

Use `[Fact(Skip = "reason")]` only when behavior is genuinely undetermined (e.g., known flaky external dependency). Document why. Never remove tests — skip with explanation.

```csharp
[Fact(Skip = "Flaky test due to Monaco Editor behavior, will need to investigate", Timeout = 60_000)]
public async Task Editor_AutoTriggers_CompletionOnSpace() { ... }
```

---

## Embedded Resource Pattern (Core.Tests)

Test model files (EF entities, DbContext stubs) live in `TestFiles/` and are compiled as embedded resources — NOT as real C# files.

### `.csproj` Configuration

```xml
<ItemGroup>
  <!-- Exclude from normal compilation -->
  <Compile Remove="TestFiles\*.cs" />
  <!-- Include as embedded binary resources -->
  <EmbeddedResource Include="TestFiles\*.cs" />
</ItemGroup>
```

### TestFiles Structure

```
tests/LinqStudio.Core.Tests/TestFiles/
  Person.cs         # Simple entity model for Roslyn to analyze
  TestDbContext.cs  # DbContext stub referencing the entity
```

`Person.cs` example — minimal, uses `namespace Test`:
```csharp
namespace Test;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

### Loading Embedded Resources in Tests

```csharp
private string ReadEmbeddedFile(string path)
{
    using var stream = Assembly
        .GetExecutingAssembly()
        .GetManifestResourceStream($"LinqStudio.Core.Tests.{path}")
        ?? throw new FileNotFoundException($"Resource not found: {path}");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}
```

**Resource name format:** `{AssemblyName}.{FolderName}.{FileName}`  
Example: `"LinqStudio.Core.Tests.TestFiles.Person.cs"`

### Using Embedded Files in Tests

```csharp
[Fact]
public async Task Completion_SuggestsMembers_AfterDot()
{
    var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
    var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

    var models = new Dictionary<string, string> { { "Person", modelCode } };
    using var service = new CompilerService("TestDbContext", "Test", CreateRoslynWorkspaceService());
    await service.Initialize(models, dbContextCode);

    var query = "context.People.";
    var completions = await service.GetCompletionsAsync(query, query.Length);

    Assert.NotNull(completions);
    Assert.NotEmpty(completions);
}
```

---

## In-Memory Fakes (Blazor.Tests)

Fakes live in `tests/LinqStudio.Blazor.Tests/Fakes/`. They implement repository interfaces backed by a `Dictionary<>` — no file I/O, no Moq for repository interactions.

### Pattern

```csharp
namespace LinqStudio.Blazor.Tests.Fakes;

public sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly Dictionary<string, Project> _store = new();

    public Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken ct = default)
    {
        var result = _store.Values.Select(p => new ProjectSummary(p.Id.ToString(), p.Name, p.CreatedDate, p.ModifiedDate))
                          .OrderByDescending(p => p.ModifiedDate).ToList();
        return Task.FromResult<IReadOnlyList<ProjectSummary>>(result);
    }

    public Task<Project> LoadProjectAsync(string id, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var project))
            throw new KeyNotFoundException($"Project '{id}' not found.");
        return Task.FromResult(project);
    }

    public Task<string> SaveProjectAsync(Project project, string? id = null, CancellationToken ct = default)
    {
        var key = id ?? project.Id.ToString();
        _store[key] = project;
        return Task.FromResult(key);
    }

    public Task DeleteProjectAsync(string id, CancellationToken ct = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }
}
```

**Rules for fakes:**
- `sealed` class, implements the interface directly
- Backed by `Dictionary<>` — no external dependencies
- Throws realistic exceptions (e.g., `KeyNotFoundException`) to match real behavior
- Use `Task.FromResult<T>()` for synchronous returns shaped as async
- One fake per interface — keep them focused

**When to use Moq vs Fakes:**
- **Fakes:** Repository interfaces (stateful, multi-method interactions)
- **Moq:** Single-method service interfaces, or when you need to verify call counts/arguments

---

## Blazor Component Tests (bUnit)

### Project Setup

Use `Microsoft.NET.Sdk.Razor` as the SDK. Required packages: `bunit`, `Moq`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. See `LinqStudio.Blazor.Tests.csproj` for current versions.

### Test Class Shape

```csharp
public class DatabaseTreeViewComponentTests : BunitContext
{
    public DatabaseTreeViewComponentTests()
    {
        // Use a unique temp directory per test run — see Temp Directory Isolation below
        var dir = Path.Combine(Path.GetTempPath(), $"Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        Services.AddLinqStudio().AddFileSystemRepositories(dir).AddLinqStudioBlazor();
        Services.AddLogging();
    }

    [Fact]
    public void DatabaseTreeView_ShowsPlaceholder_WhenNoProjectOpen()
    {
        var cut = Render<DatabaseTreeView>();
        var placeholder = cut.Find("[data-testid='db-tree-placeholder']");
        Assert.NotNull(placeholder);
        Assert.Contains("open a project", placeholder.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("[data-testid='db-tree-view']"));
    }
}
```

### bUnit Key Points

- Inherit from `BunitContext` (bUnit v2 API)
- Register services via `Services.Add*()` in a `SetupServices()` helper, called at the start of each test
- Use `Render<TComponent>()` to render; returns `IRenderedComponent<T>`
- Find elements with `cut.Find("[data-testid='...']")` — prefer `data-testid` attributes over CSS selectors
- Use `cut.FindAll(...)` when testing for absence (`Assert.Empty(...)`)
- Async component tests: `await cut.InvokeAsync(() => ...)` for state changes

---

## E2E Test Structure (Playwright)

### Architecture

```
LinqStudio.App.WebServer.E2ETests/
  Fixtures/
    AppServerFixture.cs      # Starts real Blazor server via WebApplicationFactory
    PlaywrightFixture.cs     # Launches Chromium browser
    E2ECollection.cs         # XUnit collection definition (shared fixtures)
  Helpers/
    E2ETestHelpers.cs        # Static helper methods (navigation, editor setup)
  Services/
    BlazorWebAppFactory.cs   # WebApplicationFactory with service overrides
    MockFileSystemService.cs # Temp directory management for test isolation
    MockQueryExecutionService.cs # Configurable mock for query execution
  *E2ETests.cs               # Test classes tagged [Collection("E2E")]
```

### Fixtures and Collection

```csharp
// E2ECollection.cs
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AppServerFixture>, ICollectionFixture<PlaywrightFixture> { }

// Test class
[Collection("E2E")]
public class EditorE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
    private readonly AppServerFixture _app = app;
    private readonly PlaywrightFixture _pw = pw;
    ...
}
```

- `AppServerFixture` implements `IAsyncLifetime` — starts a real Kestrel server on a random port
- `PlaywrightFixture` implements `IAsyncLifetime` — launches Chromium (headless in Release, headed in Debug)
- Sharing fixtures via `[CollectionDefinition]` means the server and browser start once per test run, not per test

### Writing an E2E Test

```csharp
[Fact(Timeout = 60_000)]
public async Task Editor_ShowsCompletions_WhenTyping()
{
    Assert.NotNull(_pw.Browser);

    await using var context = await _pw.Browser.NewContextAsync();
    var page = await context.NewPageAsync();

    // Always set up a project + editor before interacting
    await E2ETestHelpers.SetupEditorAsync(page, _app);

    // Playwright interaction
    await page.Keyboard.PressAsync("Control+Space");

    // Playwright assertions (static import)
    var suggestRow = page.Locator(".suggest-widget.visible .monaco-list-row").First;
    await Expect(suggestRow).ToBeVisibleAsync(new() { Timeout = 20000 });
    await Expect(suggestRow).Not.ToBeEmptyAsync();
}
```

**Always use `[Fact(Timeout = 60_000)]`** for E2E tests — prevents hangs from blocking CI.

**Import Playwright assertions via static import:**
```csharp
using static Microsoft.Playwright.Assertions;
```

### E2ETestHelpers Usage

Shared static helpers in `E2ETestHelpers` reduce duplication across test files. Always call setup helpers before UI interactions:

| Helper | What it does |
|---|---|
| `SetupEditorAsync(page, app)` | Creates project, opens editor, focuses Monaco |
| `CreateNewProjectAsync(page, app)` | Navigates home, creates Untitled project |
| `CreateQueryAsync(page, app, text)` | Opens editor menu, creates new query, types text |
| `ClearAndWriteQueryAsync(page, query)` | Ctrl+A then types text |
| `WaitForDebounceAsync()` | Delays 500ms (300ms debounce + 200ms buffer) |

### Service Substitution in E2E Tests

`BlazorWebAppFactory` overrides DI registrations for test isolation:

```csharp
// Replace file storage path with temp directory
services.AddSingleton(new FileSystemStorageOptions
    { BasePath = MockFileSystemService.GetTestFilesDirectory() });

// Replace IQueryExecutionService with configurable mock
services.AddSingleton<IQueryExecutionService>(MockQueryExecutionService);
```

**`MockQueryExecutionService`** has a configurable delay (default 600ms) and `SetNextResult()` to pre-configure what the service returns for the next execution call.

### Locators — Prefer `data-testid`

```csharp
// Good — stable, intent-revealing
page.GetByTestId("query-unsaved-indicator")
page.GetByTestId("nav-project-new")

// Acceptable for third-party component structure (Monaco, MudBlazor)
page.Locator(".suggest-widget.visible .monaco-list-row")
page.Locator(".mud-tab").Nth(index)
```

---

## Test `.csproj` Reference

See each test project's `.csproj` for current package versions. Key packages by project type:

| Package | Used in |
|---|---|
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | All test projects |
| `coverlet.collector` | Core.Tests, Blazor.Tests |
| `bunit`, `Moq` | Blazor.Tests |
| `Microsoft.Playwright`, `Microsoft.AspNetCore.Mvc.Testing` | E2ETests |

---

## Common Pitfalls to Avoid

### No New Tests for New Features
Every new public method or component behavior needs at least one `[Fact]`. Tests must be in place before a PR closes.

### Temp Directory Isolation in bUnit Tests
Use `Path.Combine(Path.GetTempPath(), $"TestName_{Guid.NewGuid()}")` to isolate test runs. Without isolation, file-system tests bleed state across tests.

### E2E Debounce Timing
Always call `WaitForDebounceAsync()` after typing into Monaco. The workspace update has a 300ms debounce — skipping this causes flaky timing failures.

### Multiple Monaco Instances (KeepPanelsAlive)
When multiple query tabs are open, multiple Monaco editor instances are mounted but only one is visible. Always scope interactions to `GetActivePanel(page)`. Clicking the outer editor container is not enough — also force-click `textarea.inputarea` to move keyboard focus to the correct instance.

---

## Anti-Patterns

| ❌ Never Do This | ✅ Do This Instead |
|---|---|
| Use FluentAssertions | Use `Assert.*` from xunit |
| `[Fact(Skip = ...)]` without a written explanation | Write the explanation as the skip message, file a follow-up issue |
| Run a subset of tests (`dotnet test --filter ...`) to verify your change | Run `./build.ps1 Test` — all tests |
| Delete or comment out a failing test | Fix the test or the production code |
| Write test files to `TestFiles/` without updating `.csproj` | Add `<Compile Remove="..." />` and `<EmbeddedResource Include="..." />` |
| Use `Thread.Sleep()` for timing in E2E | Use `await Task.Delay(...)` or Playwright's `ToBeVisibleAsync` timeouts |
| Assert on internal implementation details | Assert on observable outputs (return values, DOM state, exception type) |
| Share mutable state across `[Fact]` methods via fields | Construct test state fresh in each test or constructor |
| Use `Moq` for repository interfaces | Use the `InMemory*` fakes in `Fakes/` |

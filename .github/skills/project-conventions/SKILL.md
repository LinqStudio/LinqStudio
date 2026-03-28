---
name: project-conventions
description: C# code style, DI registration patterns, settings, localization, file structure, test conventions, and copilot.md documentation practice for LinqStudio. Use this as a baseline reference for any work in this repository.
---

## Context

LinqStudio is a .NET 10 Blazor Server application that provides a LINQ query IDE with Monaco editor, Roslyn IntelliSense, and multi-database support. The solution contains these key projects:

| Project | Purpose |
|---------|---------|
| `LinqStudio.Abstractions` | Shared interfaces & contracts (IUserSettingsSection, IDatabaseQueryGenerator) |
| `LinqStudio.Core` | Business logic, services, repositories, settings |
| `LinqStudio.Database` | Database introspection (ADO.NET, not EF Core) |
| `LinqStudio.Blazor` | Reusable Blazor component library (MudBlazor) |
| `LinqStudio.App.WebServer` | ASP.NET Core host |
| `LinqStudio.App.Maui` | MAUI host |
| `LinqStudio.AppHost` | .NET Aspire orchestration |

**Dependency flow (strict, no reversals):** Abstractions → Core → Database → Blazor → App.WebServer → AppHost

## When to Use This Skill

Read this skill before:
- Adding any new C# class, service, or Blazor component
- Registering DI services
- Adding user-configurable settings
- Adding or editing localized strings
- Writing tests
- Creating new source directories

---

## Patterns

### C# Code Style

All projects inherit from `Directory.Build.props`. These settings apply everywhere — no project opts out:

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<ImplicitUsings>enable</ImplicitUsings>
```

**Consequences:**
- Every nullable warning is a build error — annotate return types and parameters carefully (`string?` vs `string`)
- Implicit usings are on — do not add `using System;`, `using System.Collections.Generic;`, etc. unless needed for ambiguous types
- Use `latest` C# features freely (primary constructors, collection expressions, pattern matching)

**Indentation:** Tabs (not spaces) — set by `.editorconfig` (`indent_style = tab`).

**CA1822 suppressed:** Members that could be `static` are left as instance methods — the `.editorconfig` sets `dotnet_diagnostic.CA1822.severity = none`.

### File-Scoped Namespaces

Always use file-scoped namespaces. Never use block-scoped namespaces.

```csharp
// ✅ Correct
namespace LinqStudio.Core.Services;

public class MyService { }

// ❌ Wrong
namespace LinqStudio.Core.Services
{
    public class MyService { }
}
```

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Private fields | `_camelCase` with underscore prefix | `_lock`, `_workspace` |
| Private constants | `_camelCase` with underscore prefix | `_beforeUserQuery` |
| Public properties | `PascalCase` | `SectionName`, `IsDarkMode` |
| Methods | `PascalCase` | `GetCompletionsAsync`, `AddLinqStudio` |
| Interfaces | `IPascalCase` | `IUserSettingsSection`, `ICompilerServiceFactory` |
| Records | `PascalCase` | `HoverInfo`, `ProjectSummary` |
| Extension method classes | `{Target}Extensions` | `ServiceCollectionExtensions` |

### Settings Pattern (IUserSettingsSection)

User-configurable settings are `record class` types implementing `IUserSettingsSection`. They are **auto-discovered at startup via assembly scanning** — no manual DI registration.

**Steps to add a new setting:**

1. Create a `record class` in `LinqStudio.Core/Settings/`:

```csharp
using LinqStudio.Abstractions;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Settings;

public record class MyFeatureSettings : IUserSettingsSection
{
    [JsonIgnore]
    public string SectionName => nameof(MyFeatureSettings);

    public bool EnableFeature { get; set; } = true;
    public int MaxItems { get; set; } = 100;
}
```

2. Add localization keys to `SharedResource.resx` and `SharedResource.fr.resx`:
   - `UserSettings.MyFeatureSettings` — section display name
   - `UserSettings.MyFeatureSettings.EnableFeature` — property display name
   - `UserSettings.MyFeatureSettings.MaxItems` — property display name

3. That's it. The assembly scanner in `AddLinqStudioOptions` picks it up automatically. No changes to `ServiceCollectionExtensions.cs`.

**Consuming settings in Blazor:**

```csharp
@inject IOptionsMonitor<MyFeatureSettings> MyFeatureSettings

// React to changes:
_disposable = MyFeatureSettings.OnChange(_ => InvokeAsync(StateHasChanged));
```

### Dependency Injection Registration

DI registration is organized into extension methods on `IServiceCollection`. Each project exposes its own extension:

| Extension Method | Project | Registers |
|-----------------|---------|-----------|
| `AddLinqStudio()` | `LinqStudio.Core` | Core services, settings auto-discovery, Roslyn, repositories |
| `AddFileSystemRepositories(basePath)` | `LinqStudio.Core` | File-system project/query repositories |
| `AddLinqStudioBlazor()` | `LinqStudio.Blazor` | MudBlazor, workspace services, Monaco providers |

**Lifetimes used:**
- `Singleton` — stateless services, shared Roslyn workspace (`RoslynWorkspaceService`, `ProjectService`, `QueryService`)
- `Scoped` — per-user state in Blazor Server (`ProjectWorkspace`, `QueriesWorkspace`, `ICompilerServiceFactory`, `IQueryExecutionService`)

**Never register services directly in host projects** — all registrations belong in the appropriate project's `ServiceCollectionExtensions`.

### Blazor Component Pattern

Components use MudBlazor. Key conventions:

- `@inherits` and `@implements` on separate lines at the top
- `@inject` statements after `@inherits`/`@implements`
- Code-behind in `@code { }` block (not separate `.cs` files for most components)
- Implement `IDisposable` (or `IAsyncDisposable` when JS cleanup is needed) for event subscription cleanup
- Use `IOptionsMonitor<T>` for reactive settings — subscribe in `OnInitialized`, dispose in `Dispose()`

```razor
@inherits LayoutComponentBase
@implements IDisposable
@inject IOptionsMonitor<UISettings> UISettings

@code {
    private IDisposable? _settingsDisposable;

    protected override void OnInitialized()
    {
        _settingsDisposable = UISettings.OnChange(_ => InvokeAsync(StateHasChanged));
    }

    public void Dispose()
    {
        _settingsDisposable?.Dispose();
    }
}
```

**All user-facing strings in Blazor components must go through `SharedResource`** — access via `@SharedResource.MyKey` (the resource class is globally available).

### Localization

Localization uses standard .NET resource files. Supported languages: **English** (default) and **French**.

- `src/LinqStudio.Core/Resources/SharedResource.resx` — English (default)
- `src/LinqStudio.Core/Resources/SharedResource.fr.resx` — French

**Naming convention for resource keys:**

| Key Pattern | Usage |
|-------------|-------|
| `AppBar.Button.Settings` | UI element keys: `{Area}.{ElementType}.{Name}` |
| `UserSettings.UISettings` | Settings section name: `UserSettings.{ClassName}` |
| `UserSettings.UISettings.IsDarkMode` | Settings property: `UserSettings.{ClassName}.{PropertyName}` |
| `SettingsPage.Error.ErrorSavingSetting` | Page-scoped error: `{PageName}.Error.{ErrorName}` |
| `Global.MessageBox.Yes` | Shared across pages: `Global.{Category}.{Name}` |

**When adding any new UI text:** add the English key to `SharedResource.resx` AND the French translation to `SharedResource.fr.resx`. Both files must stay in sync.

### Thread Safety in CompilerService

`CompilerService` serializes all Roslyn workspace mutations via a `SemaphoreSlim(1,1)` named `_lock`. All public async methods must acquire `_lock` before mutating `_solution` or `_workspace` — Monaco fires concurrent completion, hover, and typing callbacks. Follow the existing `WaitAsync`/`Release` pattern in the file.

### Monaco Provider Pattern

Register Monaco language providers through `MonacoProvidersService` — never call `Global.RegisterHoverProvider()` directly from components. The service tracks global providers and routes by editor URI.

Components must unregister on `Dispose()` to avoid ghost providers accumulating across navigation.

### copilot.md Documentation Convention

**Every source directory that contains non-trivial logic must have a `copilot.md` file.** This file is the living documentation for that directory — it explains what lives there, key patterns, gotchas, and recent notable changes.

When adding a new feature to an existing directory, append notes to the existing `copilot.md`. When creating a new directory, create a new `copilot.md`. Keep entries brief (1-3 sentences per item).

### Error Handling

- Use `ILogger<T>` for logging. Inject via constructor parameter (nullable: `ILogger<T>? logger = null` for optional).
- Log with `_logger?.LogWarning(ex, "[ClassName] Description")` — include class name in message prefix.
- Use `ErrorHandlingService` in Blazor components to surface errors to the user via MudBlazor snackbar.
- Do not swallow exceptions silently — log before ignoring.

### File Structure

```
LinqStudio/
├── src/
│   ├── LinqStudio.Abstractions/       # Interfaces only (IUserSettingsSection, IDatabaseQueryGenerator)
│   ├── LinqStudio.Core/               # Business logic
│   │   ├── Extensions/                # ServiceCollectionExtensions
│   │   ├── Models/                    # Shared models (ProjectSummary)
│   │   ├── Repositories/              # IProjectRepository, IQueryRepository interfaces + implementations
│   │   ├── Resources/                 # SharedResource.resx, SharedResource.fr.resx
│   │   ├── Services/                  # CompilerService, QueryService, ProjectService, etc.
│   │   └── Settings/                  # IUserSettingsSection implementations (record classes)
│   ├── LinqStudio.Database/           # DB schema introspection (ADO.NET)
│   ├── LinqStudio.Blazor/             # Blazor component library
│   │   ├── Components/
│   │   │   ├── Dialogs/               # MudDialog components
│   │   │   ├── Layout/                # MainLayout, NavMenu
│   │   │   └── Pages/Editor/          # Monaco editor page
│   │   ├── Extensions/                # AddLinqStudioBlazor
│   │   └── Services/                  # MonacoProvidersService, ErrorHandlingService, workspace services
│   ├── LinqStudio.App.WebServer/      # ASP.NET Core host
│   └── LinqStudio.AppHost/            # .NET Aspire AppHost
├── tests/
│   ├── LinqStudio.Core.Tests/         # XUnit unit tests for Core
│   ├── LinqStudio.Blazor.Tests/       # Blazor component tests
│   └── LinqStudio.App.WebServer.E2ETests/ # Playwright E2E tests
└── Directory.Build.props              # Global build settings
```

---

## Examples

### Adding a New Setting (Complete Example)

```csharp
// src/LinqStudio.Core/Settings/EditorSettings.cs
using LinqStudio.Abstractions;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Settings;

public record class EditorSettings : IUserSettingsSection
{
    [JsonIgnore]
    public string SectionName => nameof(EditorSettings);

    public int FontSize { get; set; } = 14;
    public bool WordWrap { get; set; } = false;
}
```

```xml
<!-- SharedResource.resx (English) -->
<data name="UserSettings.EditorSettings" xml:space="preserve">
  <value>Editor Settings</value>
</data>
<data name="UserSettings.EditorSettings.FontSize" xml:space="preserve">
  <value>Font Size</value>
</data>
<data name="UserSettings.EditorSettings.WordWrap" xml:space="preserve">
  <value>Word Wrap</value>
</data>

<!-- SharedResource.fr.resx (French) -->
<data name="UserSettings.EditorSettings" xml:space="preserve">
  <value>Paramètres de l'éditeur</value>
</data>
```

### New Core Service (DI Registration Pattern)

```csharp
// src/LinqStudio.Core/Services/MyService.cs
namespace LinqStudio.Core.Services;

public class MyService
{
    private readonly ILogger<MyService>? _logger;

    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string> DoWorkAsync()
    {
        try
        {
            // work here
            return "result";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[MyService] DoWorkAsync failed");
            throw;
        }
    }
}
```

```csharp
// Add to src/LinqStudio.Core/Extensions/ServiceCollectionExtensions.cs AddLinqStudio()
services.AddScoped<MyService>(); // or AddSingleton for stateless services
```

---

## Testing

**Framework:** XUnit. Use standard xUnit assertions (`Assert.Equal`, `Assert.NotNull`, `Assert.True`, etc.). **Do NOT use FluentAssertions** — it is not an approved dependency.

**Test location:** `tests/LinqStudio.Core.Tests/` for Core unit tests, `tests/LinqStudio.Blazor.Tests/` for component tests.

**Global usings:** `using Xunit;` is a global using — no need to add it per file.

**Test naming pattern:** `{MethodOrScenario}_{Condition}_{ExpectedResult}`

```csharp
namespace LinqStudio.Core.Tests;

public class MyServiceTests
{
    [Fact]
    public async Task DoWorkAsync_WithValidInput_ReturnsResult()
    {
        var svc = new MyService();

        var result = await svc.DoWorkAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
```

**Test files used as embedded resources:** Source files in `tests/LinqStudio.Core.Tests/TestFiles/*.cs` are embedded resources (not compiled), used for Roslyn test inputs.

**Build and test commands:**

```bash
dotnet build          # build entire solution
dotnet test           # run all tests
dotnet test --filter "FullyQualifiedName~CompilerService"  # filter by name
```

> ⚠️ Do NOT use `build.ps1` or `build.sh` to build. Always use `dotnet build` / `dotnet test` directly.

---

## Anti-Patterns

- **Block-scoped namespaces** — always use file-scoped (`namespace Foo.Bar;`).
- **Manually registering settings types** — `IUserSettingsSection` implementations are auto-discovered. Adding them to DI manually creates duplicate registrations.
- **Writing user-facing strings inline in Razor** — all strings must go through `SharedResource`. Hard-coded English strings break French localization.
- **Direct Roslyn workspace mutation without the lock** — `CompilerService._lock` must be held for all `_solution` mutations. Skipping it causes race conditions with Monaco callbacks.
- **Registering services in host projects** — put all DI registrations in the library's own `ServiceCollectionExtensions`, not in `Program.cs` of host projects.
- **Calling `Global.RegisterHoverProvider()` from components** — always go through `MonacoProvidersService`.
- **Crossing layer boundaries** — `LinqStudio.Core` must not reference `LinqStudio.Database` or `LinqStudio.Blazor`. `LinqStudio.Database` uses ADO.NET, not EF Core, specifically to avoid circular dependencies.
- **Using `build.ps1` or `build.sh` for builds** — use `dotnet build` and `dotnet test` directly.
- **Omitting `copilot.md`** — new directories with non-trivial logic need a `copilot.md` explaining what lives there.

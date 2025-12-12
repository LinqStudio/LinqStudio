# LinqStudio AI Coding Agent Instructions

# Major instructions
1. Never remove, skip or deactive any tests.
2. When asked for new features or changes, always ensure that relevant tests are added or updated. DO NOT stop working until all tests pass, at all time (unless explicitely told otherwise)
3. During testing, run all the tests not just specific ones. NEVER leave before you ran the tests. If you make any change at all (such as code review changes) you MUST rerun the tests again, ALL THE TESTS.
4. If you encounter anything worth nothing, or add new features or functionnalities then create a "copilot.md" file in that directory and add the information to it (or to any existing "copilot.md" file). This is important to keep track of all the changes and information for future reference. For example, if creating a new service for a specific use, add a simple 1-2 lines in copilot.md in the directory of that service.

## Project Overview
LinqStudio is a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries, replacing the use of software such as SQL Server Management Studio. It uses Roslyn compiler APIs for intellisense/autocomplete. The architecture follows a layered approach with a core service layer, Blazor UI components, and an Aspire-based app host for orchestration.

## Architecture & Key Components

### Core Layers (reading order)
1. **LinqStudio.Core** - Domain logic, compiler service, settings management
2. **LinqStudio.Blazor** - Reusable Razor components and services
3. **LinqStudio.App.WebServer** - ASP.NET Core Blazor Server host combining Core + Blazor
4. **LinqStudio.AppHost** - Aspire orchestration layer (rarely modified)
5. **LinqStudio.ServiceDefaults** - Shared Aspire configuration (OpenTelemetry, health checks)
6. **LinqStudio.Databases** - Contain DB specific code to generate query to fetch list of tables, schemas, cols, etc.
7. **LinqStudio.Databases.Tests** - Tests different database types
8. **LinqStudio.Abstrations** - Interfaces, models, shared types

### Critical Service: CompilerService (`src/LinqStudio.Core/Services/CompilerService.cs`)
Manages Roslyn workspace for LINQ query compilation and autocomplete. Key responsibilities:
- Initializes `AdhocWorkspace` with EF Core assemblies and metadata references
- **Method `Initialize()`** - loads EF DbContext and model types 
- **Method `GetCompletionsAsync()`** - returns intellisense suggestions at cursor position
- Wraps user queries in a synthetic `QueryContainer` class to support Roslyn analysis
- Important: User query cursor position requires adjustment accounting for the wrapper code

### Settings Pattern (`src/LinqStudio.Core/Settings/`)
Modular user preferences system. Each setting:
- Implements `IUserSettingsSection` interface with `SectionName` property
- Auto-registered via reflection in `ServiceCollectionExtensions.AddLinqStudio()`
- Persisted to `usersettings.json` via `SettingsService`
- Example: `UISettings` controls dark mode, auto-reload behavior
- All settings have localized descriptions in `SharedResource.resx` (English + French)

### Blazor UI Patterns
**SettingsEditor.razor** - Monaco editor with custom hover providers:
- Uses reflection to load `IUserSettingsSection` implementations from DI
- `MonacoProvidersService` manages global Monaco provider registrations to avoid duplication
- Hover tooltips show translated setting descriptions by parsing JSON property names
- Watches for external settings changes and prompts reload

**MainLayout.razor** - Dark/light theme toggle:
- Injects `UISettings` via `IOptionsMonitor<UISettings>` for reactive updates
- Changes trigger immediate persistence via `SettingsService.Save()`
- MudBlazor theme customization with distinct light/dark palettes

## Documentation
Always read the documentation in "docs" before working on any task

## Critical Workflows

### Building & Running
```bash
# Using Nuke build system (build/_build.csproj)
./build.sh Test      # Linux/Mac: compile and run all tests
./build.ps1 Test     # Windows PowerShell
./build.cmd Test     # Windows CMD

# Direct dotnet commands
dotnet build
dotnet run --project src/LinqStudio.App.WebServer
```
- Solution file: `LinqStudio.slnx` (modern format) or `LinqStudio.sln` (legacy)
- App runs on http://localhost:5077 (HTTP) or https://localhost:7169 (HTTPS)
- Configuration loaded from `appsettings.Development.json` and `usersettings.json`

### Testing
- XUnit across all test projects
- `LinqStudio.Core.Tests/CompilerServiceTests.cs` - loads embedded test files for validation
- Test models embedded as resources: `TestFiles/*.cs` → `.EmbeddedResource` in .csproj
- Run tests: `./build.sh Test` or `dotnet test`

### Adding New Settings
1. Create record class inheriting `IUserSettingsSection` in `src/LinqStudio.Core/Settings/`
2. Add localization keys to `SharedResource.resx` (e.g., `UserSettings.MySettings`, `UserSettings.MySettings.PropertyName`)
3. Auto-registered via reflection - no manual DI registration needed
4. Expose via `IOptionsMonitor<MySettings>` in components

## Code Conventions & Patterns

### C# Code Style
- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`)
- **Implicit usings** enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Expression-bodied members** preferred for methods, properties, accessors
- **File-scoped namespaces** (C# 11 style): `namespace X;`
- **Warnings as errors** in main projects (`<TreatWarningsAsErrors>True</TreatWarningsAsErrors>`)

### Dependency Injection Pattern
- Services added via extension methods in `ServiceCollectionExtensions` classes:
  - `AddLinqStudio()` - Core services (SettingsService, compiler)
  - `AddLinqStudioBlazor()` - Blazor services (MudBlazor, MonacoProvidersService)
- Use `services.AddScoped<T>()` for component-scoped services
- Use `services.AddSingleton<T>()` for application-wide services

### Roslyn Integration Notes
- Dynamic assembly loading from `AppDomain.CurrentDomain.GetAssemblies()` 
- Falls back to `Assembly.Load()` for EF Core namespaces

## Project Structure Rules

- **Source code**: `src/LinqStudio.*/*.cs` - one namespace per project
- **Tests**: `tests/LinqStudio.*.Tests/` - one test project per feature project, XUnit format
- **Build scripts**: `build/` - Nuke-based, C# DSL (`Build.cs`)
- **Configuration**: `appsettings.json` + `appsettings.Development.json` per project
- **Embedded resources**: `Resources/*.resx` - localization, auto-generated `.Designer.cs`
- **Build outputs**: `bin/`, `obj/` (git-ignored)

## Integration Points

### ASP.NET Core Pipeline (`App.WebServer/Program.cs`)
- `AddServiceDefaults()` - Aspire defaults (health checks, OpenTelemetry, service discovery)
- `ConfigureHttpClientDefaults()` - automatic resilience via `AddStandardResilienceHandler()`
- Settings file path: `SettingsService.FILE_NAME` = `"usersettings.json"`
- Razor components auto-discovered from `LinqStudio.Blazor` assembly

### Aspire Orchestration (`AppHost/AppHost.cs`)
- Single project orchestration: `AddProject<Projects.LinqStudio_App_WebServer>()`
- Dashboard URLs in `launchSettings.json` for OTLP/resource visualization

## Testing Patterns
- **Embedded resources for test data**: Test models compiled as `.cs` files, embedded via `.csproj` ItemGroup
- **Reflection in tests**: `Assembly.GetExecutingAssembly().GetManifestResourceStream()`
- **xUnit conventions**: `[Fact]` for unit tests, `Assert.*` for assertions
- **Do NOT use FluentAssertions**: Use standard xUnit assertions (`Assert.Equal`, `Assert.NotNull`, etc.)
- Example: `CompilerServiceTests.GetCompletionsAsync_ReturnsCompletions_ForUserQuery()`

## Known Issues & Workarounds
- **BlazorMonaco rendering delay**: `OnAfterRenderAsync()` includes `Task.Delay(500)` workaround before editor initialization
- **Extension method syntax in JsonSerializerOptionsExtensions.cs**: Uses C# extension syntax (non-standard keyword `extension`) - may need refactoring
- **Settings UI reload prompt**: Dialog only shown if `UISettings.AlwaysReloadSettingsInSettingsPage` is false; respects user's persistent choice

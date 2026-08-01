# LinqStudio AI Coding Agent Instructions

# Major instructions
1. Never remove, skip or deactive any tests.
2. When asked for new features or changes, always ensure that relevant tests are added or updated. DO NOT stop working until all tests pass, at all time (unless explicitely told otherwise)
3. During testing, run all the tests not just specific ones. NEVER leave before you ran the tests. If you make any change at all (such as code review changes) you MUST rerun the tests again, ALL THE TESTS.

## Project Overview
LinqStudio is a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries, replacing the use of software such as SQL Server Management Studio. It uses Roslyn compiler APIs for intellisense/autocomplete. The architecture follows a layered approach with a core service layer, Blazor UI components, and an Aspire-based app host for orchestration.

## Documentation
Always read the documentation in "docs" before working on any task

## Building & Running
```bash
dotnet build
dotnet test
dotnet run --project src/LinqStudio.App.WebServer
```
- Solution file: `LinqStudio.slnx`
- App runs on http://localhost:5077 (HTTP) or https://localhost:7169 (HTTPS)
- Do NOT use `build.ps1`, `build.sh`, or `build.cmd` — always use `dotnet` CLI directly

## Known Issues & Workarounds
- **BlazorMonaco rendering delay**: `OnAfterRenderAsync()` includes `Task.Delay(500)` workaround before editor initialization — do not remove it
- **Extension method syntax in JsonSerializerOptionsExtensions.cs**: Uses C# extension syntax (non-standard keyword `extension`) - may need refactoring
- **Settings UI reload prompt**: Dialog only shown if `UISettings.AlwaysReloadSettingsInSettingsPage` is false; respects user's persistent choice

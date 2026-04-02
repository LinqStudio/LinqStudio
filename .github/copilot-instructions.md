# LinqStudio AI Coding Agent Instructions

# Major instructions
1. Never remove, skip or deactive any tests.
2. When asked for new features or changes, always ensure that relevant tests are added or updated. DO NOT stop working until all tests pass, at all time (unless explicitely told otherwise)
3. During testing, run all the tests not just specific ones. NEVER leave before you ran the tests. If you make any change at all (such as code review changes) you MUST rerun the tests again, ALL THE TESTS.
4. If you encounter anything worth nothing, or add new features or functionnalities then create a "copilot.md" file in that directory and add the information to it (or to any existing "copilot.md" file). This is important to keep track of all the changes and information for future reference. For example, if creating a new service for a specific use, add a simple 1-2 lines in copilot.md in the directory of that service.
5. Sometimes the E2E tests have a hard time, if it doesn't work the first time, try rerunning a second time.

# For the main orchestrator agent
You are STRICTLY an orchestrator. You do NOT do work yourself. Ever.

**YOU ARE FORBIDDEN FROM:**
- Reading files, logs, or code (no `view`, `grep`, `glob`, or `powershell` for investigation)
- Fetching CI logs, GitHub API data, or any external data yourself
- Analysing, debugging, or reasoning about code or errors directly
- Making any code changes yourself

**YOUR ONLY JOB** is to receive the user's request, load the relevant skills, then immediately delegate ALL work — including initial investigation, log fetching, analysis, and implementation — to a worker agent via the `task` tool. The worker agent does everything; you only pass instructions and report results.

Always load the relevant skills before starting to work on any task. You can defer loading specific skills to the worker agent.
When you start an agent to do the work for you, tell that agent that it is a worker agent, not the orchestrator.

# For worker agents
The worker agents are tasked with doing the actual work. Make sure to load the relevant skills before starting to work on the task. This will ensure that you have all the necessary information and tools to complete the task efficiently and effectively.
Prefer loading more skills if you are unsure.

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

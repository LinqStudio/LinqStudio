# Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — a .NET 10 Blazor web application providing an IDE-like interface for writing and executing EF Core LINQ queries. Replaces tools like SSMS. Supports multiple DB types. Uses Roslyn for intellisense/autocomplete via BlazorMonaco editor.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn (C# compiler APIs), EF Core, Aspire orchestration, XUnit, Playwright
- **Created:** 2026-03-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-11: Frontend/UI Architecture Deep Dive

**PROJECT STRUCTURE:**
- `src/LinqStudio.Blazor/` - Reusable Razor components library (Components, Services, Abstractions, Models)
- `src/LinqStudio.App.WebServer/` - ASP.NET Core Blazor Server host (App.razor, Routes.razor, Program.cs, server-specific services)
- Blazor components auto-discovered from LinqStudio.Blazor assembly via `AddAdditionalAssemblies()`
- Interactive Server render mode with prerender disabled: `new InteractiveServerRenderMode(prerender: false)`

**ALL RAZOR COMPONENTS (17 total):**

1. **MainLayout.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Inherits LayoutComponentBase, implements IDisposable
   - Uses `IOptionsMonitor<UISettings>` for reactive dark/light theme changes
   - Components: MudThemeProvider (with custom _lightPalette/_darkPalette), MudAppBar, MudDrawer (with NavMenu), MudMainContent
   - AppErrorBoundary wraps @Body
   - `OnChange` subscription for UISettings triggers StateHasChanged automatically
   - DarkModeToggle saves to SettingsService immediately via `await SettingsService.Save()`
   - Custom palettes: light (minimalist white/gray), dark (purple primary #7e6fff, dark surfaces #1e1e2d/#1a1a27)

2. **NavMenu.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Project menu: New, Open, Properties (Edit), Save, Save As, Close
   - Editor menu: New Query, Open Query (disabled if no project open)
   - Subscribes to `Workspace.WorkspaceChanged` event for reactive updates
   - Uses UnsavedChangesDialog for confirmation prompts
   - Integrates with IFileSystemService (native file dialogs via NativeFileDialogSharp)
   - File extensions: `.linq` (projects), `.linquery` (queries)

3. **ReconnectModal.razor** (`src/LinqStudio.Blazor/Components/Layout/`)
   - Blazor Server reconnection UI (native HTML `<dialog>` element)
   - Custom CSS animations (fade in/out, slide up)
   - JavaScript file: ReconnectModal.razor.js (handles reconnect/resume events)

4. **Home.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/` (root)
   - Simple welcome page with MudContainer
   - Minimal content: "Welcome to LinqStudio - Your IDE for EF Core LINQ queries"

5. **Editor.razor** (`src/LinqStudio.Blazor/Components/Pages/Editor/`)
   - Routes: `/editor`, `/editor/{QueryIdParam:guid}`, `/editor/new`
   - Main code editor page with Monaco editor
   - Shows query tabs, query info bar (name, unsaved indicator), save/close buttons
   - MonacoProvidersService registers completion + hover providers via Roslyn CompilerService
   - **Task.Delay(500) workaround**: Monaco editor rendered only after 500ms delay (BlazorMonaco rendering issue)
   - Debouncing: 300ms debounce on text changes to avoid excessive workspace updates
   - Language: `csharp`, theme: `vs-dark` or default (light)
   - Completion mapping: uses Roslyn completion tags (WellKnownTags) → Monaco CompletionItemKind
   - Hover provider: shows Roslyn quick info as markdown
   - **Critical pattern**: cursor position adjusted for QueryContainer wrapper in CompilerService

6. **Settings.razor** (`src/LinqStudio.Blazor/Components/Pages/Settings/`)
   - Route: `/settings`
   - Reflection-based settings loading: scans assembly for all `IUserSettingsSection` implementations
   - Uses `IServiceProvider.GetRequiredService(typeof(IOptionsMonitor<>).MakeGenericType(x))` to get typed options
   - MudTabs with Position.Left, KeepPanelsAlive=true
   - SaveAll: validates JSON, deserializes all settings, then saves via SettingsService
   - OnChange subscriptions for all settings trigger StateHasChanged

7. **SettingsEditor.razor** (`src/LinqStudio.Blazor/Components/Pages/Settings/`)
   - One tab per setting section (created inside MudTabs)
   - Monaco editor with JSON language, hover providers for setting descriptions
   - Hover logic: parses JSON to ensure hovering on first-level property key (not value, not nested)
   - Translation: uses SharedResource `"UserSettings.{SectionName}.{PropertyName}"` for descriptions
   - Reload dialog: prompts user when settings change externally (unless `AlwaysReloadSettingsInSettingsPage` is true)
   - **Task.Delay(500) workaround** in OnAfterRenderAsync

8. **Error.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/Error`
   - ASP.NET Core default error page (shows Request ID in development)

9. **NotFound.razor** (`src/LinqStudio.Blazor/Components/Pages/`)
   - Route: `/not-found`
   - Simple 404 page

10. **AppErrorBoundary.razor** (`src/LinqStudio.Blazor/Components/`)
    - Wraps @Body in MainLayout
    - Catches unhandled component exceptions
    - Logs via ILogger, shows via ErrorHandlingService
    - Fallback UI: MudAlert with "An unexpected error occurred"

11. **ErrorDialog.razor** (`src/LinqStudio.Blazor/Components/`)
    - MudDialog showing error message + collapsible stack trace (MudExpansionPanel)
    - Parameters: Message (string), StackTrace (string?)
    - Used by ErrorHandlingService

12. **UnsavedChangesDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - Generic confirmation dialog for unsaved changes
    - Parameters: Message, ConfirmText, CancelText
    - Returns bool via DialogResult.Ok(true)

13. **EditProjectDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - MudDialog for editing project properties
    - Fields: Project Name (readonly/disabled), DatabaseType (MudSelect), ConnectionString (multiline MudTextField), Timeout (MudSelect: 5/10/15/30/60s)
    - Validate Connection button: calls `Project.TestConnectionAsync()`, shows progress spinner
    - Returns updated Project via DialogResult.Ok(Project)

14. **EditorMenuDialog.razor** (`src/LinqStudio.Blazor/Components/Dialogs/`)
    - Simple dialog with New/Open options for queries
    - Returns EditorMenuAction enum (New, Open)

15. **App.razor** (`src/LinqStudio.App.WebServer/`)
    - HTML document head + body
    - Links: Roboto font, MudBlazor CSS, scoped CSS, app.css
    - Scripts: Blazor, MudBlazor, BlazorMonaco (jsInterop, loader, editor.main)
    - Routes component with InteractiveServerRenderMode
    - ReconnectModal component

16. **Routes.razor** (`src/LinqStudio.App.WebServer/`)
    - Router with AppAssembly=LinqStudio.Blazor, NotFoundPage=NotFound
    - DefaultLayout=MainLayout

17. **_Imports.razor** (2 files)
    - `src/LinqStudio.App.WebServer/_Imports.razor`: Basic ASP.NET + MudBlazor
    - `src/LinqStudio.Blazor/Components/_Imports.razor`: Full imports (Core services, Blazor services, SharedResource)

**BLAZORMONACO INTEGRATION:**

- **MonacoProvidersService** (singleton pattern for global provider registration)
  - Prevents duplicate Monaco provider registrations (Monaco tracks providers globally)
  - Registers hover + completion providers once globally for `csharp` and `json` languages
  - Uses ConcurrentDictionary keyed by model URI to route events to correct delegates
  - `RetryUntilMonacoReady()`: retries registration up to 20 times with 250ms delays (handles Monaco loading race condition)
  - Returns IDisposable to unregister provider for specific model URI
  - Registered in DI as Scoped service

- **Monaco Editor Initialization Pattern:**
  1. Component state: `Delay = true` initially
  2. OnAfterRenderAsync: if Delay, set to false, await Task.Delay(500), StateHasChanged
  3. Only then render StandaloneCodeEditor
  4. This workaround ensures Monaco resources are fully loaded before editor initialization

- **Completion Provider (Editor.razor):**
  - Trigger characters: `.`, `(`, `<`, `[`, ` ` (space)
  - Calls CompilerService.GetCompletionsAsync() with user query text + cursor offset
  - Maps Roslyn CompletionItem tags to Monaco CompletionItemKind (Property, Method, Field, Class, Text)
  - Inserts parenthesis if `ShouldProvideParenthesisCompletion` property is true

- **Hover Provider (Editor.razor + SettingsEditor.razor):**
  - Editor: calls CompilerService.GetHoverAsync(), shows as MarkdownString
  - SettingsEditor: custom hover for JSON properties, shows translated descriptions from SharedResource

**MUDBLAZOR SETUP:**

- Theme: Custom MudTheme with distinct light/dark palettes
- Light palette: minimalist (white backgrounds, subtle grays)
- Dark palette: purple-themed (#7e6fff primary, #1e1e2d surfaces, #1a1a27 background)
- Components used: MudThemeProvider, MudAppBar, MudDrawer, MudNavMenu, MudMainContent, MudTabs, MudDialog, MudSnackbar, MudAlert, MudButton, MudIconButton, MudTextField, MudSelect, MudChip, MudExpansionPanel, MudTooltip, MudProgressCircular, MudMenu, MudMenuItem
- Added via `services.AddMudServices()` in ServiceCollectionExtensions

**IOPTIONSMONITOR<T> PATTERNS:**

- All settings use `IOptionsMonitor<T>` for reactive updates
- Pattern: `_disposable = UISettings.OnChange(_ => InvokeAsync(StateHasChanged))`
- Components: MainLayout (UISettings), Settings (all IUserSettingsSection), SettingsEditor (UISettings), Editor (UISettings)
- Disposed in component Dispose() method
- Enables instant UI updates when settings change (e.g., dark mode toggle, settings reload)

**SHAREDRESOURCE LOCALIZATION:**

- Location: `src/LinqStudio.Core/Resources/SharedResource.resx` (English) + `SharedResource.fr.resx` (French)
- Categories: AppBar, ErrorDialog, Global, SettingsPage, UserSettings, ConnectionSettings
- UserSettings keys: `UserSettings.{SectionName}` (section title), `UserSettings.{SectionName}.{PropertyName}` (property description)
- Accessed via: `SharedResource.ResourceManager.GetString(key, SharedResource.Culture)`
- Current settings: UISettings (IsDarkMode, AlwaysReloadSettingsInSettingsPage)

**BLAZOR SERVICES (ServiceCollectionExtensions.AddLinqStudioBlazor):**

1. **MonacoProvidersService** (Scoped) - Monaco provider management
2. **ErrorHandlingService** (Scoped) - Centralized error dialog display + logging
3. **QueriesWorkspace** (Scoped) - Per-session query state management (open queries, current query, unsaved changes)
4. **ProjectWorkspace** (Scoped) - Per-session project state management (current project, file path, unsaved changes)
5. **MudServices** - MudBlazor services

**APP.WEBSERVER SERVICES:**

- **ServerFileSystemService** (implements IFileSystemService) - Native file dialogs via NativeFileDialogSharp
- Default path: ~/Documents/LinqStudio/ or ~/Documents/ fallback

**WORKSPACE PATTERN:**

- **ProjectWorkspace**: Manages current open project, tracks unsaved changes (properties + queries), events: WorkspaceChanged
- **QueriesWorkspace**: Manages all queries for current project, tracks open queries, current query, unsaved changes per query, events: QueriesChanged
- **OpenQueryState**: Model tracking editor state (current text, unsaved changes, last modified)
- Both workspaces use EventHandler pattern for reactive updates across components
- Components subscribe in OnInitialized, unsubscribe in Dispose

**CSS ORGANIZATION:**

1. `wwwroot/app.css` (LinqStudio.Blazor) - `.mainBody { height: 100% }`
2. `App.razor.css` (App.WebServer) - `body { width: 100vw; height: 100vh; overflow: hidden }`
3. Component-scoped CSS files:
   - `Settings.razor.css` - flexbox layout (tabs + actions)
   - `SettingsEditor.razor.css` - `.editorParent` + `::deep .editor` for Monaco
   - `Editor.razor.css` - `::deep .editor { height: 100% }`
   - `ReconnectModal.razor.css` - extensive reconnection modal styles + animations

**ROUTING/PAGES:**

- `/` - Home page (welcome message)
- `/editor` - Editor page (no query selected)
- `/editor/{QueryIdParam:guid}` - Editor page with specific query
- `/editor/new` - Editor page (create new query)
- `/settings` - Settings page
- `/Error` - Error page (ASP.NET Core default)
- `/not-found` - 404 page
- Router configured in Routes.razor with MainLayout as default, NotFound for 404s

**CRITICAL PATTERNS & BEHAVIORS:**

1. **Monaco Delay Workaround**: Always `Task.Delay(500)` before rendering Monaco editors (BlazorMonaco race condition)
2. **MonacoProvidersService**: Use to prevent duplicate provider registrations across multiple Monaco instances
3. **IOptionsMonitor + OnChange**: Pattern for reactive settings - always dispose subscription
4. **EventHandler Pattern**: ProjectWorkspace/QueriesWorkspace raise events, components subscribe/unsubscribe
5. **Debouncing**: Editor uses 300ms debounce for text changes to avoid excessive workspace updates
6. **Unsaved Changes**: UnsavedChangesDialog for confirmations, tracked at both project + query level
7. **File Extensions**: `.linq` (projects), `.linquery` (queries) via FileExtensions constants
8. **ErrorHandlingService**: Centralized error handling - logs via ILogger, shows via MudDialog (ErrorDialog)
9. **Reflection-based Settings**: Settings page uses reflection to discover all IUserSettingsSection implementations
10. **Native File Dialogs**: ServerFileSystemService uses NativeFileDialogSharp (cross-platform)

**UI/UX FEATURES:**

- Dark/light mode toggle in AppBar (instant switch)
- Drawer toggle for NavMenu
- Query tabs with unsaved indicator (asterisk)
- Settings reload prompt (optional, user can choose "Always" to skip)
- Connection string validation with loading spinner
- Error dialogs with collapsible stack trace
- Reconnection modal with animations for Blazor Server disconnects
- Project/query unsaved indicators in NavMenu title
- Monaco hover tooltips for settings descriptions (localized)
- Snackbar notifications (success, info, error)

**IN-PROGRESS / INCOMPLETE FEATURES:**

- No visible incomplete UI features in code
- All components appear fully functional
- All dialogs have proper cancel/save flows
- All services are registered and used consistently

**TESTING HOOKS:**

- Extensive `data-testid` attributes throughout components for Playwright E2E tests
- Examples: `editor-page`, `query-name-display`, `query-save-btn`, `nav-menu`, `edit-project-dialog`, etc.

---
name: blazor-frontend
description: Patterns for Blazor Server UI in LinqStudio — component structure, MudBlazor conventions, Monaco editor integration, reactive settings with IOptionsMonitor, CSS scoping, and localization. Use this when working on src/LinqStudio.Blazor or src/LinqStudio.App.WebServer UI code, when creating or modifying .razor/.razor.cs files, or when adding any user-visible string or styling.
---

---

The Frontend of LinqStudio is built with Blazor using the MudBlazor component library. 
NEVER inspect the nuget package source code for MudBlazor to understand how to use it. Always git clone the a temporary folder and explore the source code locally: https://github.com/MudBlazor/MudBlazor.git

## Project Layout

```
src/
  LinqStudio.Blazor/                     ← reusable Blazor library (components, services)
    Components/
      Layout/                            ← MainLayout, NavMenu, DatabaseTreeView, ReconnectModal
      Pages/
        Editor/                          ← Editor.razor (tab host), QueryEditorPanel.razor (per-tab)
        Settings/                        ← Settings.razor, SettingsEditor.razor (per-section Monaco JSON editor)
      Dialogs/                           ← modal dialogs (MudDialog-based)
      QueryResultGrid.razor              ← pure display component for query execution output
    Services/
      MonacoProvidersService.cs          ← scoped; prevents duplicate Monaco provider registration
      ErrorHandlingService.cs            ← centralised error surface (snackbar + dialog)
      ProjectWorkspace.cs                ← scoped; owns project open/close/save state
      QueriesWorkspace.cs                ← scoped; owns open query tabs, text, dirty state

  LinqStudio.App.WebServer/              ← host app (thin wrappers over LinqStudio.Blazor)
```

---

## Component Structure Conventions

### Code-Behind Split (Required)

Every component that has logic beyond trivial template rendering uses the **code-behind pattern**:

```
MyComponent.razor          ← markup only (@using, @inject, HTML/Razor)
MyComponent.razor.cs       ← partial class, lifecycle methods, fields, event handlers
MyComponent.razor.css      ← scoped CSS (only for this component's DOM)
```

The `.razor.cs` class must be `partial` and match the component name:

```csharp
namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class QueryEditorPanel : ComponentBase, IDisposable, IAsyncDisposable
{
    // [Inject] on private properties — not constructor injection
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<QueryEditorPanel> Logger { get; set; } = null!;
}
```

**Key rules:**
- `[Inject]` goes on **private properties** in the code-behind, never in the `.razor` file's `@code {}` block.
- Required parameters use `[Parameter, EditorRequired]`.
- `null!` suppresses nullable warnings for injected services — intentional.

### Implementing IDisposable / IAsyncDisposable

Components that subscribe to events or hold JS references implement **both** interfaces:

```csharp
public partial class MyComponent : ComponentBase, IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;       // ← _disposed set FIRST, before anything else
        _disposed = true;
        _someDisposable?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _someAsyncDisposable.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
```

Guard `_disposed` in any `await` continuation that could run after disposal (e.g., `await Task.Delay(500); if (_disposed) return;`).

---

## MudBlazor Patterns

### Providers in MainLayout (Required)

`MainLayout.razor` must include all four MudBlazor providers — no other layout should add them:

```razor
<MudThemeProvider Theme="@_theme" IsDarkMode="UISettings.CurrentValue.IsDarkMode" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />
```

### Dark/Light Theme

The canonical palette is defined in `MainLayout.razor.cs` (`PaletteLight` / `PaletteDark`). Never hardcode color hex values in components — use MudBlazor CSS variables instead:

```css
background: var(--mud-palette-surface);
color: var(--mud-palette-text-primary);
border-color: var(--mud-palette-divider);
```

### Component Choices

| Use case | Component | Notes |
|---|---|---|
| Tabbed panels | `MudTabs` + `MudTabPanel` | Always `KeepPanelsAlive="true"` for editor tabs so Monaco instances survive tab switches |
| Data results grid | `MudDataGrid` | Supports `TemplateColumn` with dynamic columns; use `Virtualize="true"` for large result sets |
| Notifications | `ISnackbar` (inject) | Transient; prefer over alert banners for action feedback |
| Confirmation dialogs | `IDialogService.ShowMessageBox` or custom `MudDialog` | Extract to `DialogServiceExtensions` if the same dialog is shown from >1 component |
| Navigation sidebar | `MudNavMenu` + `MudNavLink` | Menu items with sub-options use `MudMenu` with `ActivatorContent` + `MudNavLink` inside |
| Icons | `Icons.Material.Filled.*` | Filled variant is the project default; Outlined only for specific cases (e.g., dark mode toggle) |
| Progress during async | `MudProgressCircular Indeterminate="true"` | Always pair with conditional render: `@if (_isLoading)` |
| Toolbar rows | `MudPaper` + `MudStack Row="true" AlignItems="AlignItems.Center"` | Standard toolbar shell |
| Form inputs | `MudSelect`, `MudTextField`, etc. | Use `Variant.Outlined`, `Dense="true"` for compact toolbars |

### StateHasChanged Marshalling

Event callbacks from non-Blazor threads (e.g., `IOptionsMonitor.OnChange`, workspace events) must marshal back to the render thread:

```csharp
// In OnInitialized:
Workspace.WorkspaceChanged += OnWorkspaceChanged;

// Handler:
private void OnWorkspaceChanged(object? sender, EventArgs e)
    => InvokeAsync(StateHasChanged);
```

### List Rendering and Click Propagation

Always add `@key` in `@foreach` loops that render stateful components (`MudTabPanel`, `MudListItem`, etc.) — omitting it causes Blazor to reuse instances incorrectly when the list mutates.

Some MudBlazor components don't support `@onclick:stopPropagation` directly. Wrap them in a `<span @onclick:stopPropagation="true">` instead.

---

## Monaco Editor Integration

### The Task.Delay(500) Workaround (Non-Negotiable)

BlazorMonaco requires the host `<div>` to have **non-zero dimensions** before the JS editor instance is created. In Blazor Server, the element is in the DOM but may not yet be laid out on `firstRender`. The established workaround:

```csharp
private bool _delay = true;   // or _loaded = false — same intent

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _delay = false;                 // unblock the render flag
        await Task.Delay(500);          // let the browser lay out the container
        if (_disposed) return;          // guard against disposal during wait
        StateHasChanged();              // trigger re-render that shows the editor
        return;
    }
    // ... splitter init on subsequent renders
}
```

In the `.razor` file, guard the `<StandaloneCodeEditor>` behind the flag:

```razor
@if (!_delay)
{
    <StandaloneCodeEditor @ref="_editor"
                          Id="@EditorId"
                          ConstructionOptions="EditorConstructionOptions"
                          OnDidInit="OnEditorInitialized"
                          OnDidChangeModelContent="OnEditorContentChanged" />
}
```

### Editor Construction Options

Always return a `StandaloneEditorConstructionOptions` from a delegate — not a property — because the delegate receives the editor instance reference:

```csharp
private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor ed) => new()
{
    AutomaticLayout = true,      // required for resize
    Language = "csharp",         // or "json" for settings editors
    Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : null,
    Value = GetInitialContent(),
};
```

### Unique IDs (Required for Multi-Instance)

Every Monaco editor instance needs a globally unique DOM `id`. Derive it from a stable identifier (e.g., query GUID):

```csharp
private string EditorId => $"editor-{QueryId:N}";
```

Use the same discriminator for any related splitter or panel DOM IDs.

### Tab Activation Relayout

When `MudTabs` uses `KeepPanelsAlive="true"`, hidden panels have `display:none`. Monaco measures its container at init time. On tab switch, force a relayout via JS:

```csharp
public async Task OnTabActivatedAsync()
{
    if (_editor is not null)
    {
        await Task.Delay(100);          // wait for MudBlazor to remove display:none
        if (_disposed) return;
        await JSRuntime.InvokeVoidAsync("monacoRelayout", EditorId);
    }
    await JSRuntime.InvokeVoidAsync("resetMudTabsScroll");
}
```

For E2E tests: ensure the correct Monaco instance receives focus before sending keyboard input.

---

## MonacoProvidersService

Monaco registers providers **globally per language** in JS. With multiple editor instances, calling `RegisterHoverProviderAsync` / `RegisterCompletionItemProvider` per instance accumulates duplicate callbacks.

`MonacoProvidersService` (scoped per circuit) registers exactly **one** global handler per language and routes each invocation to the correct per-editor delegate.

**Usage in `OnEditorInitialized`:**

```csharp
_completionDisposable = await MonacoProvidersService.RegisterCompletionProviderAsync(_editor,
    async (modelUri, position, context) => { /* return CompletionList? */ });

_hoverDisposable = await MonacoProvidersService.RegisterHoverProviderAsync(_editor,
    async (uri, position, context) => { /* return Hover? */ });
```

Dispose the returned `IDisposable` in `Dispose()` — otherwise closed-tab delegates continue receiving callbacks and may throw.

**Do not** call `BlazorMonaco.Languages.Global.RegisterHoverProviderAsync` directly. Always use `MonacoProvidersService`.

The service retries registration automatically if Monaco is not yet loaded.

---

## IOptionsMonitor Pattern (Reactive Settings)

Never poll settings or read a settings value once in `OnInitialized`. Use `IOptionsMonitor<T>` for **live-reactive** settings:

```csharp
[Inject] private IOptionsMonitor<UISettings> UISettings { get; set; } = null!;

private IDisposable? _settingsDisposable;

protected override void OnInitialized()
{
    _settingsDisposable = UISettings.OnChange(_ => InvokeAsync(StateHasChanged));
}
```

Dispose `_settingsDisposable` in `Dispose()`. Read the current value via `UISettings.CurrentValue.SomeProperty`. To save settings, inject `ISettingsService` and call `await SettingsService.Save(newSettings)` — wrap in try/catch, surface via `ErrorHandlingService`.

---

## CSS Scoping

### Scoped CSS (`.razor.css`)

Each component gets its own `.razor.css` file. Styles are **automatically scoped** to the component's own DOM.

### Piercing MudBlazor's DOM with `::deep`

To style MudBlazor internals from a scoped CSS file, use `::deep`:

```css
/* Makes the MudTabs panel fill available height */
::deep .mud-tab-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
}

/* Styles the Monaco editor canvas inside MudTabPanel */
::deep .editor {
    height: 100%;
}
```

Without `::deep`, the scoped attribute selector will not match MudBlazor's child elements.

### Full-Height Flex Layout Pattern

Use a cascading `flex: 1; display: flex; flex-direction: column; min-height: 0` chain on the page root, `::deep` MudTabs containers, and each tab panel. Apply `overflow-y: hidden` on the tab root — this prevents MudBlazor's `scrollIntoView()` from shifting the tab header behind the app bar and creates the scroll container for `position: sticky`.

---

## Localization (SharedResource.resx)

All user-visible strings go in `SharedResource.resx` (English) and `SharedResource.fr.resx` (French). Do not hardcode display strings directly in components.

In Razor: `<MudTooltip Text="@SharedResource.AppBar_Button_Settings">` (add `@using LinqStudio.Core.Resources`).

Programmatic lookup for dynamic keys:

```csharp
var label = SharedResource.ResourceManager
    .GetString($"UserSettings.{Setting.SectionName}", SharedResource.Culture)
    ?? Setting.SectionName;   // fallback to the raw key if translation missing
```

### Naming conventions for resource keys:

| Context | Key pattern | Example |
|---|---|---|
| AppBar buttons | `AppBar_Button_{Name}` | `AppBar_Button_Settings` |
| Settings page UI | `SettingsPage_{Element}_{Action}` | `SettingsPage_MessageBox_ReloadTitle` |
| Settings section labels | `UserSettings.{SectionName}` | `UserSettings.UISettings` |
| Settings property tooltips | `UserSettings.{SectionName}.{PropertyName}` | `UserSettings.UISettings.IsDarkMode` |
| Global reusable | `Global_MessageBox_{Label}` | `Global_MessageBox_Yes` |

Add both English and French entries for every new key. Never ship a key with only one locale.

---

## Error Handling in Components

Use `ErrorHandlingService` for unexpected exceptions (surfaces as snackbar/dialog). Use `ISnackbar` directly for expected validation feedback:

```csharp
// Unexpected exceptions:
try { await SettingsService.Save(newSettings); }
catch (Exception ex) { await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save."); }

// Expected feedback:
Snackbar.Add("Saved.", Severity.Success);
```

---

## Service Registration

Add new scoped services to `AddLinqStudioBlazor()` in `ServiceCollectionExtensions.cs` — never in `Program.cs` directly.

**Scoped** is the correct lifetime for all Blazor Server services (one instance per SignalR circuit). Never use Singleton for state that touches component rendering.

---

## Debounce Pattern (Editor Content Changes)

The editor fires `OnDidChangeModelContent` on every keystroke. Debounce before writing to workspace state:

```csharp
private CancellationTokenSource? _debounceTokenSource;

private void DebounceUpdate(string newText)
{
    _debounceTokenSource?.Cancel();
    _debounceTokenSource?.Dispose();
    _debounceTokenSource = new CancellationTokenSource();
    var token = _debounceTokenSource.Token;

    _ = Task.Run(async () =>
    {
        try { await Task.Delay(300, token); }
        catch (TaskCanceledException) { return; }
        if (!token.IsCancellationRequested)
            await InvokeAsync(() => { /* apply update */ });
    });
}
```

---

## data-testid Attributes

Add `data-testid` (kebab-case) to all interactive and structural elements for Playwright targeting. For dynamic lists, append a discriminator: `data-testid="@($"cell-{rowIndex}-{colName}")"`.

---

## Anti-Patterns

### ❌ Don't hand-roll what MudBlazor provides

Before writing custom HTML for a UI element, check the [MudBlazor docs](https://mudblazor.com/components). The Component Choices table above lists the canonical components for common use cases.

### ❌ Don't copy-paste components — extract them

If the same UI pattern appears in two places, extract to a `DialogServiceExtensions` helper or a shared `.razor` component in `Components/`.

### ❌ Don't register Monaco providers directly from components

Never call `BlazorMonaco.Languages.Global.RegisterHoverProviderAsync` directly. Always use `MonacoProvidersService`. Direct registration accumulates duplicate callbacks across tab switches.

### ❌ Don't call `StateHasChanged` from a background thread without `InvokeAsync`

```csharp
// WRONG — fires from event handler on non-render thread
private void OnSettingsChanged(UISettings _) => StateHasChanged();

// CORRECT
private void OnSettingsChanged(UISettings _) => InvokeAsync(StateHasChanged);
```

### ❌ Don't use `ContinueWith` in Blazor components

`Task.ContinueWith` with `TaskScheduler.FromCurrentSynchronizationContext()` does not correctly await async lambdas in Blazor Server. Use `async/await` or `InvokeAsync` directly.

### ❌ Don't forget `@key` on list items

Omitting `@key` in `@foreach` loops that render stateful components (tabs, list items with click handlers) causes Blazor to reuse component instances incorrectly when the list changes.

### ❌ Don't skip the `Task.Delay(500)` workaround for Monaco

Removing the delay will cause Monaco to initialize inside a zero-size container, producing a collapsed or invisible editor. The delay is not a hack to remove later — it reflects a genuine timing constraint in BlazorMonaco's JS initialization sequence.

### ❌ Don't poll `IOptionsMonitor.CurrentValue` on a timer

Use `IOptionsMonitor<T>.OnChange()` to react to settings changes. Manual polling creates unnecessary work and introduces latency. The monitor fires immediately when the underlying configuration source changes.

### ❌ Don't add CSS to `.razor.css` expecting it to reach MudBlazor children

Scoped CSS does not pierce component boundaries by default. Use `::deep` for any selector that must reach into MudBlazor's rendered DOM.

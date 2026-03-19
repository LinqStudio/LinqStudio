# Editor Page Notes

## Refresh Schema Button (`refresh-schema-btn`)

The editor info bar contains a "Refresh Schema" button that re-initializes the `CompilerService` using the live DB schema via `IDbContextGenerator`.

- Disabled when no DB connection is configured (`Workspace.CurrentProject?.QueryGenerator is null`)
- Shows loading spinner (`_isRefreshingSchema = true`) while refreshing
- Falls back to demo model on failure (both during initial load and on explicit refresh)
- `OnEditorInitialized` in `QueryEditorPanel` uses `CompilerServiceFactory` to create a fallback local compiler if the shared `Compiler` param is not yet set (timing fallback)
- `Editor.RefreshSchemaAsync` creates a new shared compiler and passes it to all panels via `Compiler` parameter

## QueryEditorPanel Component

`QueryEditorPanel.razor` is a self-contained per-tab component that owns everything that was previously singleton in `Editor.razor`:
- Monaco editor with unique `Id="editor-{QueryId:N}"`
- Draggable splitter with unique IDs: `editor-top-panel-{QueryId:N}`, `editor-results-splitter-{QueryId:N}`, `results-bottom-panel-{QueryId:N}`  
- Execution bar (Execute/Stop, timeout selector, Refresh Schema callback)
- `QueryResultGrid` (no sort parameters — state preserved naturally via KeepPanelsAlive)
- Per-tab execution state: `_result`, `_isExecuting`, `_executionCts`, `_selectedTimeout`
- All Monaco logic: `_lastQueryText`, `_debounceTokenSource`, `_delay`, `_splitterInitialized`
- Compiler fallback: creates a `_localCompiler` in `OnEditorInitialized` if the shared `Compiler` param is null

### Key method: `OnTabActivatedAsync()`
Called by `Editor.OnActivePanelIndexChanged` when a tab becomes active:
```csharp
public async Task OnTabActivatedAsync()
{
    if (_editor is not null)
    {
        // Wait for MudBlazor to remove display:none before Monaco measures.
        await Task.Delay(100);
        await JSRuntime.InvokeVoidAsync("monacoRelayout", EditorId);
    }
}
```
This forces Monaco to recalculate its layout after being in a `display:none` panel.

`monacoRelayout` is defined in `editor-utils.js`. It calls `editor.layout()` with no arguments so Monaco auto-reads the actual container size. Calling `_editor.Layout(new Dimension { Width = 0, Height = 0 })` would set 0×0 explicitly — that is wrong and must not be used. The 100ms delay is needed because MudBlazor asynchronously removes `display:none` from the panel; Monaco must not measure before that change is applied.

## KeepPanelsAlive — Why and What It Solves

`MudTabs` uses `KeepPanelsAlive="true"`. This keeps ALL `QueryEditorPanel` instances mounted in the DOM (hidden via `display:none` for inactive tabs) instead of destroying and recreating them on tab switch. Benefits:
- MudDataGrid sort state is preserved naturally within each panel's `QueryResultGrid` instance
- Editor content is preserved without manually calling `_editor.SetValue()`  
- Execution results remain visible when switching back to a tab
- Row selection, scroll position all preserved

**Consequence for E2E tests**: With KeepPanelsAlive, elements with shared `data-testid` (like `execute-query-btn`) appear once per open tab in the DOM. Tests with multiple tabs must scope locators to the active panel using `E2ETestHelpers.GetActivePanel(page)`.

## Monaco Layout Fix on Tab Activation

Monaco reads container dimensions at initialization time. When a tab becomes active (was previously `display:none`), Monaco needs to re-measure its container. `OnTabActivatedAsync()` calls `monacoRelayout` (defined in `editor-utils.js`) via JS interop, which calls `editor.layout()` with no arguments so Monaco auto-reads the actual container size. A 100ms delay is needed to let MudBlazor complete the `display:none` removal before Monaco reads dimensions.

## Compiler Initialization Timing

`Editor.OnInitializedAsync` creates the shared `_compiler` and passes it to all `QueryEditorPanel` instances via the `Compiler` parameter. However, Roslyn workspace initialization can take 2-5 seconds. `QueryEditorPanel.OnEditorInitialized` creates a `_localCompiler` as a fallback if `Compiler` is still null at that point. Provider callbacks use `Compiler ?? _localCompiler` so completions always work.

## Splitter Initialization Timing

The splitter (`initSplitter` JS) MUST be called on the **second render** of `QueryEditorPanel`, not the first:

1. `firstRender=true` fires — returns early after scheduling a 500ms delay + `StateHasChanged()` (Monaco workaround)
2. Second render fires — `_splitterInitialized` flag prevents duplicate calls — `initSplitter` is called here
3. DOM elements exist by this point because Monaco has rendered

**Do NOT call `initSplitter` during `firstRender=true`** — those elements may not yet be in the DOM.

## IAsyncDisposable for JS Cleanup

`QueryEditorPanel` implements both `IDisposable` and `IAsyncDisposable`. The async path calls `disposeSplitter` with the unique splitter ID to remove event listeners. This prevents memory leaks from `document` event listeners accumulating. Editor itself only disposes the shared `_compiler` and unsubscribes from WorkspaceChanged.

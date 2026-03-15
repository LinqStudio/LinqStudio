# Editor Page Notes

## Refresh Schema Button (`refresh-schema-btn`)

The editor info bar contains a "Refresh Schema" button that re-initializes the `CompilerService` using the live DB schema via `IDbContextGenerator`.

- Disabled when no DB connection is configured (`Workspace.CurrentProject?.QueryGenerator is null`)
- Shows loading spinner (`_isRefreshingSchema = true`) while refreshing
- Falls back to demo model on failure (both during initial load and on explicit refresh)
- `OnEditorInitialized` uses `CompilerServiceFactory.CreateFromProjectAsync(project)` when a project is open, with try-catch fallback to `CreateAsync()` for unreachable DBs

## Splitter Initialization Timing

The splitter (`initSplitter` JS) MUST be called on the **second render**, not the first:

1. `firstRender=true` fires — returns early after scheduling a 500ms delay + `StateHasChanged()` (Monaco workaround)
2. Second render fires — `_splitterInitialized` flag prevents duplicate calls — `initSplitter` is called here
3. DOM elements (`editor-top-panel`, `results-bottom-panel`, `editor-results-splitter`) exist by this point because they are inside `@if (Workspace.IsProjectOpen)` + `@if (Workspace.Queries.CurrentQueryId is not null)` blocks

**Do NOT call `initSplitter` during `firstRender=true`** — those elements may not yet be in the DOM.

## IAsyncDisposable for JS Cleanup

`Editor` implements both `IDisposable` and `IAsyncDisposable`. The async path calls `disposeSplitter` to remove event listeners before delegating to synchronous `Dispose()`. This prevents memory leaks from `document` event listeners accumulating on each Editor navigation.

Pattern:
```csharp
public async ValueTask DisposeAsync()
{
    if (_splitterInitialized)
        await JSRuntime.InvokeVoidAsync("disposeSplitter", "editor-results-splitter");
    Dispose();
    GC.SuppressFinalize(this);
}
```

# Editor Page Notes

## Refresh Schema Button (`refresh-schema-btn`)

The editor info bar contains a "Refresh Schema" button that re-initializes the `CompilerService` using the live DB schema via `IDbContextGenerator`.

- Disabled when no DB connection is configured (`Workspace.CurrentProject?.QueryGenerator is null`)
- Shows loading spinner (`_isRefreshingSchema = true`) while refreshing
- Falls back to demo model on failure (both during initial load and on explicit refresh)
- `OnEditorInitialized` uses `CompilerServiceFactory.CreateFromProjectAsync(project)` when a project is open, with try-catch fallback to `CreateAsync()` for unreachable DBs

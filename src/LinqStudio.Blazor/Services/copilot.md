# Services

Blazor-scoped services for project and query workspace management.

## ProjectWorkspace

Manages the currently open project. Uses `IProjectRepository` (from `LinqStudio.Core.Repositories`) for persistence.

- `CurrentProjectId` — the project's repository ID (= project name for the file-system implementation)
- `HasUnsavedChanges` — uses a `_isDirty` bool flag (not JSON comparison) to avoid serialization on every keystroke
- `CreateNewAsync(name)` — creates an in-memory project (no ID until saved); `_isDirty = true`
- `LoadAsync(projectId)` — loads project and initializes `QueriesWorkspace`; `_isDirty = false`
- `SaveAsync()` — saves to current project ID; throws if no ID set; `_isDirty = false`
- `SaveAsAsync(name, existingProjectId?)` — saves under a new name; calls `SaveAllToProjectAsync` on queries workspace BEFORE `InitializeAsync` to prevent data loss

## QueriesWorkspace

Manages queries for the current project. Uses `IQueryRepository` (from `LinqStudio.Core.Repositories`) for persistence.

- `InitializeAsync(projectId?)` — loads queries from repository; clears state when called
- `SaveQueryAsync(queryId)` — throws if no project ID set (project must be saved first)
- `SaveAllDirtyQueriesAsync()` — saves only dirty queries to the current project ID
- `SaveAllToProjectAsync(projectId)` — applies all dirty in-memory edits and saves ALL queries to the given project ID; used by `ProjectWorkspace.SaveAsAsync` to migrate queries to the new location before reinitializing
- `DeleteQueryAsync(queryId)` — removes from memory and repository

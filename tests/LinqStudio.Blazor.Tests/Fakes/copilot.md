# Fakes

In-memory test repository implementations for `LinqStudio.Blazor.Tests`.

## InMemoryProjectRepository

Implements `IProjectRepository` using an in-memory dictionary. Assigns `project.Id.ToString()` as the ID for new projects and updates `ModifiedDate` on save.

## InMemoryQueryRepository

Implements `IQueryRepository` using a nested dictionary keyed by `projectId → queryId → SavedQuery`.

Use these in tests that need `QueriesWorkspace` or `ProjectWorkspace` without a real file system.

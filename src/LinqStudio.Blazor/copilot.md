# LinqStudio.Blazor

Reusable Blazor component library for LinqStudio.

## Abstraction Pattern
Project and query storage is accessed through `IProjectRepository` and `IQueryRepository` (defined in `LinqStudio.Abstractions`).
Blazor components depend on these interfaces via `ProjectWorkspace` service — no direct file system access or native dialogs.
The old `IFileSystemService` (native OS file dialogs) has been removed; all project open/save flows use `ProjectBrowserDialog` instead.

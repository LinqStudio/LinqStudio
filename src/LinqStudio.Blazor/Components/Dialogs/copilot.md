# Dialogs

MudBlazor dialog components for user interactions.

## EditProjectDialog

Allows editing a project's database connection settings (type + connection string).

**Key methods:**
- `OnInitialized()`: Reads both `Project.ConnectionString` AND `Project.DatabaseType` into local state
- `Save()`: Calls `Project.UpdateConnection(_databaseType, _connectionString)` — always updates BOTH type and connection string atomically
- `ValidateConnection()`: Tests the connection using `Project.TestConnectionAsync` before saving

**Important**: Always call `Project.UpdateConnection(databaseType, connectionString)` rather than setting `Project.ConnectionString` directly. The `UpdateConnection` method correctly updates both `DatabaseType` and `ConnectionString`, which resets the `QueryGenerator` cache and triggers the `DatabaseTreeView` to reload.

## ProjectBrowserDialog

Replaces native OS file dialogs for browsing, opening, and saving projects. Supports two modes via `ProjectBrowserMode` enum.

- **Open mode**: Shows sorted project list; selecting and confirming returns `ProjectBrowserResult(id, name)`.
- **SaveAs mode**: Text input for project name + list of existing projects (clicking one pre-fills the name). Returns `ProjectBrowserResult(existingId_or_empty, typedName)` — empty ID means new project.
- Inline delete (trash icon per item) reloads the list automatically via `IProjectRepository.DeleteProjectAsync`.

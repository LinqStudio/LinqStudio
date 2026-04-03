# Dialogs

MudBlazor dialog components for user interactions.

## EditProjectDialog

Allows editing a single `ServerConnection`'s database settings (type + connection string).

**Key parameter:** `[Parameter] ServerConnection Connection` — replaces the old `Project` parameter.

**Key methods:**
- `OnInitialized()`: Reads `Connection.ConnectionString` AND `Connection.DatabaseType` into local state.
- `Save()`: Calls `Connection.UpdateConnection(_databaseType, _connectionString)` and returns the updated `ServerConnection` via `DialogResult.Ok(Connection)`.
- `ValidateConnection()`: Tests the connection using `Connection.TestConnectionAsync` before saving.

**Usage patterns:**
- **New connection**: Caller creates `new ServerConnection { Id = Guid.NewGuid() }`, opens dialog, then adds result to `Project.Connections`.
- **Edit existing**: Caller opens dialog with existing `ServerConnection`, replaces in `Project.Connections` with returned result.

**Always** call `Connection.UpdateConnection(databaseType, connectionString)` rather than setting properties directly. The method resets the `QueryGenerator` cache.

## ProjectBrowserDialog

Replaces native OS file dialogs for browsing, opening, and saving projects. Supports two modes via `ProjectBrowserMode` enum.

- **Open mode**: Shows sorted project list; selecting and confirming returns `ProjectBrowserResult(id, name)`.
- **SaveAs mode**: Text input for project name + list of existing projects (clicking one pre-fills the name). Returns `ProjectBrowserResult(existingId_or_empty, typedName)` — empty ID means new project.
- Inline delete (trash icon per item) reloads the list automatically via `IProjectRepository.DeleteProjectAsync`.

> ⚠️ **Known gap (tracked issue):** The inline delete currently has **no confirmation dialog** — one click irreversibly deletes the project. All other destructive actions in NavMenu (New, Open, Close) already guard against data loss. A confirmation step matching the pattern of `ShowUnsavedChangesDialogAsync` should be added here before this dialog is considered production-safe.

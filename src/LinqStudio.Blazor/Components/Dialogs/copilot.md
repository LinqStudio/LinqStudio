# Dialogs

MudBlazor dialog components for user interactions.

## EditProjectDialog

Allows editing a project's database connection settings (type + connection string).

**Key methods:**
- `OnInitialized()`: Reads both `Project.ConnectionString` AND `Project.DatabaseType` into local state
- `Save()`: Calls `Project.UpdateConnection(_databaseType, _connectionString)` — always updates BOTH type and connection string atomically
- `ValidateConnection()`: Tests the connection using `Project.TestConnectionAsync` before saving

**Important**: Always call `Project.UpdateConnection(databaseType, connectionString)` rather than setting `Project.ConnectionString` directly. The `UpdateConnection` method correctly updates both `DatabaseType` and `ConnectionString`, which resets the `QueryGenerator` cache and triggers the `DatabaseTreeView` to reload.

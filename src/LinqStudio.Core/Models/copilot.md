# Core Models

## Project

Multi-connection project model. Key changes from v1:
- `ConnectionString` and `DatabaseType` removed — replaced by `Connections` list.
- `List<ServerConnection> Connections { get; set; } = []` — each entry is a fully-configured DB connection.
- Old `.linq` files without `Connections` will deserialize with an empty list (no migration needed).

## ServerConnection

Represents a single database server connection within a project.

- `Id` (Guid) — unique within a project.
- `DatabaseType` / `ConnectionString` — setting either resets the `QueryGenerator` cache.
- `QueryGenerator` — lazy `IDatabaseQueryGenerator` property; recreated when connection properties change.
- `UpdateConnection()` — atomically updates type + string + resets generator.
- `GetServerDisplayName()` — parses host:port from connection string for display.
- `GetDatabaseName()` — parses `Initial Catalog=` / `Database=` from connection string.

## SavedQuery

- `ConnectionId` (Guid?) — which `ServerConnection` this query runs against. `null` = use first connection.

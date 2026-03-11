# LinqStudio Core Services

## ConnectionService
Singleton service that manages database connection information and provides connection testing capabilities.

### Key Features
- Stores current connection string and database type
- Creates appropriate query generators (MSSQL, MySQL) based on database type
- Tests database connections with configurable timeout (5s, 10s, 15s, 30s, 60s)
- Integrates with `IDatabaseQueryGenerator` for schema operations

### Usage
```csharp
// Registered as singleton in DI
services.AddSingleton<ConnectionService>();

// Update connection
connectionService.UpdateConnection(DatabaseType.Mssql, connectionString);

// Test connection with 10 second timeout
await connectionService.TestConnectionAsync(DatabaseType.Mssql, connectionString, 10);
```

### Testing
- Unit tests in `tests/LinqStudio.Core.Tests/ConnectionServiceTests.cs`
- Integration tests via database generator tests
- E2E tests in `tests/LinqStudio.App.WebServer.E2ETests/ConnectionE2ETests.cs`

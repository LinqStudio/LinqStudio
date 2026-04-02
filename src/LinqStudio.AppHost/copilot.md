# LinqStudio.AppHost

Aspire orchestration layer for LinqStudio development environment.

## Selecting Which Apps to Start

Control which apps Aspire starts via `appsettings.json` (or override in `appsettings.Development.json`):

```json
{
  "LinqStudio": {
    "Apps": {
      "WebServer": true,   // ASP.NET Core Blazor Server (default: true)
      "Maui": false        // MAUI Blazor Hybrid desktop app, Windows only (default: false)
    }
  }
}
```

- Any combination is valid (both can run simultaneously).
- The DB containers and seeder always start regardless of app flags.
- Only WebServer receives DB connection references (MAUI uses the file system).

## Database Container Ports

Aspire maps container ports to **random host ports** on each fresh start. The `port:` parameter in `AddSqlServer`/`AddMySql` does NOT reliably control the Docker host port for persistent containers.

To find the actual host ports at runtime:
```powershell
docker port demo-mssql-78b93e94   # e.g. 1433/tcp -> 127.0.0.1:49293
docker port demo-mysql-78b93e94   # e.g. 3306/tcp -> 127.0.0.1:49292
```

## ⚠️ IMPORTANT: Use `127.0.0.1` not `localhost`

Docker containers bind to `127.0.0.1` only. On Windows, `localhost` resolves to IPv6 `::1` first — which **fails**. Always use `127.0.0.1` in connection strings.

### Connection String Templates (replace PORT with actual port from `docker port`):

**SQL Server:**
```
Server=127.0.0.1,{PORT};Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true
```

**MySQL:**
```
Server=127.0.0.1;Port={PORT};Database=linqstudio-mysql-demo;User=root;Password=root_password_123;
```

## ⚠️ CRITICAL: Always specify `Database=` in connection strings

If you omit `Database=` from the SQL Server connection string, you connect to the `master` database and see system tables (`spt_fallback_db`, `MSreplication_options`, etc.) instead of your tables. Always include `Database=linqstudio-mssql-demo`.

## Live Testing Workflow for Agents

When running live tests against these Aspire databases:

1. Check containers are running: `docker ps | Select-String demo`
2. Get actual ports: `docker port demo-mssql-78b93e94` and `docker port demo-mysql-78b93e94`
3. Start Aspire: `cd src\LinqStudio.AppHost && dotnet run`
4. Wait for WebServer on http://localhost:5077 (use `netstat -ano | Select-String ":5077"`)
5. In the app: Project → New, then Project → Properties → enter connection string with actual port and `Database=linqstudio-mssql-demo`
6. The 4 seeded tables appear: `dbo.Customers`, `dbo.OrderItems`, `dbo.Orders`, `dbo.Products`

## Container Configuration
- **Persistent lifetime**: Containers survive Aspire restarts (use `docker ps` to check)
- **Hardcoded passwords**: For local dev/testing only (never for production)
- **Seeder dependency**: WebServer waits for DatabaseSeeder to complete before starting
- **Projects not persisted**: Projects (connection strings) are in-memory only — must be re-created each WebServer restart via the UI

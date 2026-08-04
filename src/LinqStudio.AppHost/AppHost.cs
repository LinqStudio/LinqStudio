var builder = DistributedApplication.CreateBuilder(args);

// Hardcoded passwords for local dev/testing - consistent credentials across restarts
// SQL Server SA: Password123!
// MySQL root: root_password_123
var sqlPassword = builder.AddParameter("sql-password", value: "Password123!", secret: false);
var mysqlPassword = builder.AddParameter("mysql-password", value: "root_password_123", secret: false);

// Fixed host ports for reliable external connections during live testing
// NOTE: Port numbers below are for Aspire service discovery only - actual Docker host ports may differ.
// Use `docker port <container-name>` to find the actual host ports mapped to containers.
// On Windows, use 127.0.0.1 (NOT localhost) - localhost resolves to IPv6 ::1 which Docker doesn't bind to.
// SQL Server: Server=127.0.0.1,14330;Database=linqstudio-mssql-demo;User Id=sa;Password=Password123!;TrustServerCertificate=true
// MySQL: Server=127.0.0.1;Port=13306;Database=linqstudio-mysql-demo;User=root;Password=root_password_123;
var mssql = builder.AddSqlServer("demo-mssql", password: sqlPassword, port: 14330)
	.WithLifetime(ContainerLifetime.Persistent);
var mssql2024Db = mssql.AddDatabase("linqstudio-mssql-2024");
var mssql2025Db = mssql.AddDatabase("linqstudio-mssql-2025");

var mysql = builder.AddMySql("demo-mysql", password: mysqlPassword, port: 13306)
	.WithLifetime(ContainerLifetime.Persistent);
var mysql2024Db = mysql.AddDatabase("linqstudio-mysql-2024");
var mysql2025Db = mysql.AddDatabase("linqstudio-mysql-2025");

var seeder = builder.AddProject<Projects.LinqStudio_DatabaseSeeder>("demo-seeder")
	.WithReference(mssql2024Db, "DemoMssql2024")
	.WithReference(mssql2025Db, "DemoMssql2025")
	.WithReference(mysql2024Db, "DemoMysql2024")
	.WithReference(mysql2025Db, "DemoMysql2025")
	.WaitFor(mssql)
	.WaitFor(mysql);

// Read feature flags from LinqStudio:Apps config section.
// Toggle in appsettings.json (or appsettings.Development.json) to select which apps Aspire starts.
var startWebServer = !bool.TryParse(builder.Configuration["LinqStudio:Apps:WebServer"], out var wsv) || wsv;
var startMaui = bool.TryParse(builder.Configuration["LinqStudio:Apps:Maui"], out var mv) && mv;

if (startWebServer)
{
	builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-webserver")
		.WithReference(mssql2024Db, "DemoMssql2024")
		.WithReference(mssql2025Db, "DemoMssql2025")
		.WithReference(mysql2024Db, "DemoMysql2024")
		.WithReference(mysql2025Db, "DemoMysql2025");
}

if (startMaui)
{
	// MAUI Blazor Hybrid: launches the desktop window (Windows only).
	// No HTTP health endpoint — Aspire tracks it as a process resource.
	builder.AddProject<Projects.LinqStudio_App_Maui>("linqstudio-maui")
		.WithReference(mssql2024Db, "DemoMssql2024")
		.WithReference(mssql2025Db, "DemoMssql2025")
		.WithReference(mysql2024Db, "DemoMysql2024")
		.WithReference(mysql2025Db, "DemoMysql2025");
}

builder.Build().Run();

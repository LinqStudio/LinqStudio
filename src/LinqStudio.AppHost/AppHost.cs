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
// SQL Server: use one of the linqstudio-mssql-demo-1 or linqstudio-mssql-demo-2 databases.
// MySQL: use one of the linqstudio-mysql-demo-1 or linqstudio-mysql-demo-2 databases.
var mssql = builder.AddSqlServer("demo-mssql", password: sqlPassword, port: 14330)
	.WithLifetime(ContainerLifetime.Persistent);
var mssql1Db = mssql.AddDatabase("linqstudio-mssql-demo-1");
var mssql2Db = mssql.AddDatabase("linqstudio-mssql-demo-2");

var mysql = builder.AddMySql("demo-mysql", password: mysqlPassword, port: 13306)
	.WithLifetime(ContainerLifetime.Persistent);
var mysql1Db = mysql.AddDatabase("linqstudio-mysql-demo-1");
var mysql2Db = mysql.AddDatabase("linqstudio-mysql-demo-2");

var seeder = builder.AddProject<Projects.LinqStudio_DatabaseSeeder>("demo-seeder")
	.WithReference(mssql1Db, "DemoMssql1")
	.WithReference(mssql2Db, "DemoMssql2")
	.WithReference(mysql1Db, "DemoMysql1")
	.WithReference(mysql2Db, "DemoMysql2")
	.WaitFor(mssql)
	.WaitFor(mysql);

// Read feature flags from LinqStudio:Apps config section.
// Toggle in appsettings.json (or appsettings.Development.json) to select which apps Aspire starts.
var startWebServer = !bool.TryParse(builder.Configuration["LinqStudio:Apps:WebServer"], out var wsv) || wsv;
var startMaui = bool.TryParse(builder.Configuration["LinqStudio:Apps:Maui"], out var mv) && mv;

if (startWebServer)
{
	builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-webserver")
		.WithReference(mssql1Db, "DemoMssql1")
		.WithReference(mssql2Db, "DemoMssql2")
		.WithReference(mysql1Db, "DemoMysql1")
		.WithReference(mysql2Db, "DemoMysql2");
}

if (startMaui)
{
	// MAUI Blazor Hybrid: launches the desktop window (Windows only).
	// No HTTP health endpoint — Aspire tracks it as a process resource.
	builder.AddProject<Projects.LinqStudio_App_Maui>("linqstudio-maui")
		.WithReference(mssql1Db, "DemoMssql1")
		.WithReference(mssql2Db, "DemoMssql2")
		.WithReference(mysql1Db, "DemoMysql1")
		.WithReference(mysql2Db, "DemoMysql2");
}

builder.Build().Run();

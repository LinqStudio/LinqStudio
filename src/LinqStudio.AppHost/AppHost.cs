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
var mssqlDb = mssql.AddDatabase("linqstudio-mssql-demo");

var mysql = builder.AddMySql("demo-mysql", password: mysqlPassword, port: 13306)
	.WithLifetime(ContainerLifetime.Persistent);
var mysqlDb = mysql.AddDatabase("linqstudio-mysql-demo");

var seeder = builder.AddProject<Projects.LinqStudio_DatabaseSeeder>("demo-seeder")
	.WithReference(mssqlDb, "DemoMssql")
	.WithReference(mysqlDb, "DemoMysql")
	.WaitFor(mssql)
	.WaitFor(mysql);

builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")
	.WithReference(mssqlDb, "DemoMssql")
	.WithReference(mysqlDb, "DemoMysql");

builder.Build().Run();

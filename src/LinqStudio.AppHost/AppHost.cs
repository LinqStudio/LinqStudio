var builder = DistributedApplication.CreateBuilder(args);

var mssql = builder.AddSqlServer("demo-mssql")
	.WithLifetime(ContainerLifetime.Persistent);
var mssqlDb = mssql.AddDatabase("linqstudio-mssql-demo");

var mysql = builder.AddMySql("demo-mysql")
	.WithLifetime(ContainerLifetime.Persistent);
var mysqlDb = mysql.AddDatabase("linqstudio-mysql-demo");

var seeder = builder.AddProject<Projects.LinqStudio_DatabaseSeeder>("demo-seeder")
	.WithReference(mssqlDb, "DemoMssql")
	.WithReference(mysqlDb, "DemoMysql")
	.WaitFor(mssql)
	.WaitFor(mysql);

builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")
	.WithReference(mssqlDb, "DemoMssql")
	.WithReference(mysqlDb, "DemoMysql")
	.WaitForCompletion(seeder);

builder.Build().Run();

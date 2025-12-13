var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server for testing
var sqlServer = builder.AddSqlServer("sqlserver")
	.WithDataVolume()
	.AddDatabase("testdb");

builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver")
	.WithReference(sqlServer);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LinqStudio_App_WebServer>("linqstudio-app-webserver");

builder.Build().Run();

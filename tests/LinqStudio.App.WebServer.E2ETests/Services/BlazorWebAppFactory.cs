using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LinqStudio.App.WebServer.E2ETests.Services;

internal class BlazorWebAppFactory : WebApplicationFactory<Program>
{
	public MockFileSystemService MockFileSystemService { get; }
	public MockQueryExecutionService MockQueryExecutionService { get; }

	public BlazorWebAppFactory()
	{
		MockFileSystemService = new MockFileSystemService();
		MockQueryExecutionService = new MockQueryExecutionService();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// Configure Kestrel to use a random port (0 = any available port)
		builder.UseUrls("http://127.0.0.1:0");

		builder.ConfigureServices(services =>
		{
			// Point file-system repository storage at the mock's test directory
			var storageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(FileSystemStorageOptions));
			if (storageDescriptor is not null)
				services.Remove(storageDescriptor);
			services.AddSingleton(new FileSystemStorageOptions { BasePath = MockFileSystemService.GetTestFilesDirectory() });

			// Replace IQueryExecutionService with mock so tests get a real async delay,
			// allowing Blazor to render the IsExecuting=true state before execution completes.
			var qeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IQueryExecutionService));
			if (qeDescriptor is not null)
				services.Remove(qeDescriptor);
			services.AddSingleton<IQueryExecutionService>(MockQueryExecutionService);
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			MockFileSystemService.Cleanup();
		}
		base.Dispose(disposing);
	}
}

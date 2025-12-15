using LinqStudio.Blazor.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LinqStudio.App.WebServer.E2ETests.Services;

internal class BlazorWebAppFactory : WebApplicationFactory<Program>
{
	public MockFileSystemService MockFileSystemService { get; }

	public BlazorWebAppFactory()
	{
		MockFileSystemService = new MockFileSystemService();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			// Remove the real IFileSystemService registration
			var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileSystemService));
			if (descriptor is not null)
			{
				services.Remove(descriptor);
			}

			// Add our mock service as a singleton so we can control it from tests
			services.AddSingleton<IFileSystemService>(MockFileSystemService);
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

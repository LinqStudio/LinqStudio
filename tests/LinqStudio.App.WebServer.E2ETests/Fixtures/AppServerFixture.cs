using LinqStudio.App.WebServer.E2ETests.Services;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests.Fixtures;

public class AppServerFixture : IAsyncLifetime
{
	private readonly BlazorWebAppFactory _factory;

	public Uri BaseUrl => _factory.ClientOptions.BaseAddress ?? throw new InvalidOperationException("BlazorWebAppFactory not initialized");
	public MockFileSystemService MockFileSystemService => _factory.MockFileSystemService;
	public MockQueryExecutionService MockQueryExecutionService => _factory.MockQueryExecutionService;

	public AppServerFixture()
	{
		_factory = new BlazorWebAppFactory();
	}

	public async Task InitializeAsync()
	{
		_factory.UseKestrel();
		_factory.StartServer();
	}

	public async Task DisposeAsync()
	{
		await _factory.DisposeAsync();
	}
}

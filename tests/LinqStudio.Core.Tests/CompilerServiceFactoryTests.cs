using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests;

public class CompilerServiceFactoryTests : IDisposable
{
	private readonly CompilerServiceProvider _provider;

	public CompilerServiceFactoryTests()
	{
		// Create a provider that will be shared across test methods
		_provider = new CompilerServiceProvider();
	}

	[Fact]
	public async Task CreateAsync_ProducesCompilerService_WithCompletions()
	{
		var factory = new CompilerServiceFactory(_provider);

		var svc = await factory.CreateAsync();

		Assert.NotNull(svc);

		var query = "context.People.";
		var completions = await svc.GetCompletionsAsync(query, query.Length);

		Assert.NotNull(completions);
		Assert.NotEmpty(completions);
	}

	[Fact]
	public async Task CreateAsync_ProducesCompilerService_WithHoverInfo()
	{
		var factory = new CompilerServiceFactory(_provider);

		var svc = await factory.CreateAsync();

		Assert.NotNull(svc);

		var query = "context.People";
		var cursor = query.IndexOf("People") + 1;
		var hover = await svc.GetHoverAsync(query, cursor);

		Assert.NotNull(hover);
		Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
		Assert.Contains("DbSet", hover!.Markdown!);
	}

	[Fact]
	public async Task CreateAsync_ProducesCompilerService_WithHover_ForWhereMethod()
	{
		var factory = new CompilerServiceFactory(_provider);

		var svc = await factory.CreateAsync();

		Assert.NotNull(svc);

		var query = "context.People.Where(p => p.Name == \"Bob\")";
		var cursor = query.IndexOf("Where") + 1;
		var hover = await svc.GetHoverAsync(query, cursor);

		Assert.NotNull(hover);
		Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
		Assert.Contains("Where", hover!.Markdown!);
	}

	[Fact]
	public async Task CreateAsync_ReusesSameInstance_ForSameConfiguration()
	{
		var factory = new CompilerServiceFactory(_provider);

		var svc1 = await factory.CreateAsync();
		var svc2 = await factory.CreateAsync();

		// Should return the same instance since configuration hasn't changed
		Assert.Same(svc1, svc2);
	}

	[Fact]
	public async Task CompilerService_CanBeUsed_AfterFactoryDisposed()
	{
		CompilerService svc;

		// Create factory in a scope
		{
			var factory = new CompilerServiceFactory(_provider);
			svc = await factory.CreateAsync();
		}
		// Factory is now out of scope, but compiler service should still work

		var query = "context.People";
		var cursor = query.IndexOf("People") + 1;
		var hover = await svc.GetHoverAsync(query, cursor);

		Assert.NotNull(hover);
		Assert.Contains("DbSet", hover!.Markdown!);
	}

	public void Dispose()
	{
		// Dispose the provider at the end of all tests
		_provider?.Dispose();
	}
}

using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests;

public class CompilerServiceFactoryTests
{
    [Fact]
    public async Task CreateAsync_ProducesCompilerService_WithCompletions()
    {
        var factory = new CompilerServiceFactory();

        var svc = await factory.CreateAsync();

        Assert.NotNull(svc);

        var query = "context.People.";
        var completions = await svc.GetCompletionsAsync(query, query.Length);

        Assert.NotNull(completions);
        Assert.NotEmpty(completions);
    }
}

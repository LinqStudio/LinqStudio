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

    [Fact]
    public async Task CreateAsync_ProducesCompilerService_WithHoverInfo()
    {
        var factory = new CompilerServiceFactory();

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
        var factory = new CompilerServiceFactory();

        var svc = await factory.CreateAsync();

        Assert.NotNull(svc);

        var query = "context.People.Where(p => p.Name == \"Bob\")";
        var cursor = query.IndexOf("Where") + 1;
        var hover = await svc.GetHoverAsync(query, cursor);

        Assert.NotNull(hover);
        Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
        Assert.Contains("Where", hover!.Markdown!);
    }
}

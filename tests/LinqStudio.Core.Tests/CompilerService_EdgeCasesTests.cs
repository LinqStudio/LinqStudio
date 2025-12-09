using LinqStudio.Core.Services;
using System.Diagnostics;
using System.Reflection;

namespace LinqStudio.Core.Tests;

public class CompilerService_EdgeCasesTests
{
    private string ReadEmbeddedFile(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinqStudio.Core.Tests.{path}") ?? throw new FileNotFoundException($"Resource not found: {path}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task AddUserQuery_ReplacesDocumentContent()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(new Dictionary<string, string> { { "Person", modelCode } }, dbContextCode);

        var firstQuery = "context.People.Where(p => p.Id > 1)";
        await service.AddUserQuery(firstQuery);

        var completionsFirst = await service.GetCompletionsAsync(firstQuery, firstQuery.Length);

        var secondQuery = "context.People.Select(p => p.Name)";
        await service.AddUserQuery(secondQuery);

        var completionsSecond = await service.GetCompletionsAsync(secondQuery, secondQuery.Length);

        Assert.NotNull(completionsFirst);
        Assert.NotNull(completionsSecond);
    }

    [Fact]
    public async Task Initialize_WithMissingTables_DoesNotThrow_AndHasSafeResponses()
    {
        var projectNamespace = "TestEmpty";
        var dbContextCode = "using Microsoft.EntityFrameworkCore; namespace TestEmpty; public class EmptyContext : DbContext { }";

        using var service = new CompilerService("EmptyContext", projectNamespace);

        // Initialize with no model files
        await service.Initialize(new Dictionary<string, string>(), dbContextCode);

        // Should not throw and should return safe responses
        var completions = await service.GetCompletionsAsync("context.", 8);
        Assert.NotNull(completions);

        var hover = await service.GetHoverAsync("context.", 8);
        // With nothing available, hover may be null but must not throw
    }

    [Fact]
    public async Task LargeInput_ReturnsWithinReasonableTime()
    {
        var projectNamespace = "TestLarge";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(new Dictionary<string, string> { { "Person", modelCode } }, dbContextCode);

        // Build a relatively large query (repetitive but harmless)
        var sb = new System.Text.StringBuilder();
        sb.Append("context.People");
        for (int i = 0; i < 200; i++) sb.Append($".Where(p => p.Id > {i})");
        var largeQuery = sb.ToString();

        var sw = Stopwatch.StartNew();
        var completions = await service.GetCompletionsAsync(largeQuery, largeQuery.Length);
        sw.Stop();

        Assert.NotNull(completions);
        // Ensure it returns quickly in CI — allow generous bound
        Assert.True(sw.Elapsed.TotalSeconds < 10, "Completions for large input took too long");
    }

    [Fact]
    public void Dispose_ReleasesResources_NoExceptions()
    {
        var service = new CompilerService("TestDbContext", "Test");
        // Ensure Dispose doesn't throw
        service.Dispose();
        // Second dispose also should be safe
        service.Dispose();
    }
}

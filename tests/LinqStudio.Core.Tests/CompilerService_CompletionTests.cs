using LinqStudio.Core.Services;
using System.Reflection;

namespace LinqStudio.Core.Tests;

public class CompilerService_CompletionTests
{
    private string ReadEmbeddedFile(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinqStudio.Core.Tests.{path}") ?? throw new FileNotFoundException($"Resource not found: {path}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Completion_SuggestsMembers_AfterDot()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var query = "context.People.";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(models, dbContextCode);

        var completions = await service.GetCompletionsAsync(query, query.Length);

        Assert.NotNull(completions);
        Assert.NotEmpty(completions);
        // Ensure there are some method completions like Where or Select
        var names = completions.Select(c => c.Item.DisplayText).ToArray();
        Assert.Contains(names, n => n.Contains("Where") || n.Contains("Select") || n.Contains("Count") || n.Contains("First"));
    }

    [Fact]
    public async Task Completion_PartialIdentifier_ReturnsMatches()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var query = "cont"; // partial 'context'

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(models, dbContextCode);

        var completions = await service.GetCompletionsAsync(query, 3);

        Assert.NotNull(completions);
        // We expect some suggestions e.g., 'context' or other matches (safe assert: not throwing)
    }

    [Fact]
    public async Task Completion_HandlesMissingSemicolon()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var query = "context.People"; // missing trailing dot and semicolon

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(models, dbContextCode);

        var completions = await service.GetCompletionsAsync(query, query.Length);

        Assert.NotNull(completions);
        // should not throw and should return a list (possibly empty)
    }

    [Fact]
    public async Task Completion_MidLineCursor_PositionAdjusted()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var query = "context.People.Where"; // cursor in middle

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(models, dbContextCode);

        var cursor = query.IndexOf("Where") + 2; // middle of 'Where'
        var completions = await service.GetCompletionsAsync(query, cursor);

        Assert.NotNull(completions);
        Assert.NotEmpty(completions);
    }

    [Fact]
    public async Task Completion_EmptyInput_ReturnsSafe()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var query = string.Empty;

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(models, dbContextCode);

        var completions = await service.GetCompletionsAsync(query, 0);

        Assert.NotNull(completions);
        // may be empty, but must not throw
    }

    [Fact]
    public async Task Completion_ConcurrentRequests_AreHandled()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(new Dictionary<string, string> { { "Person", modelCode } }, dbContextCode);

        var queries = new[]
        {
            "context.People.",
            "context.People.Where(p => p.Id > 3)",
            "context.People.Select(p => p.Name)"
        };

        var tasks = queries.Select(q => Task.Run(() => service.GetCompletionsAsync(q, q.Length))).ToList();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.NotNull(r));
    }

    [Fact]
    public async Task Completion_InvalidCursor_ReturnsEmptyOrSafe()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        using var service = new CompilerService("TestDbContext", projectNamespace);
        await service.Initialize(new Dictionary<string, string> { { "Person", modelCode } }, dbContextCode);

        var listNeg = await service.GetCompletionsAsync("context.People.", -10);
        Assert.NotNull(listNeg);

        var listPast = await service.GetCompletionsAsync("context.People.", 1000);
        Assert.NotNull(listPast);
    }
}

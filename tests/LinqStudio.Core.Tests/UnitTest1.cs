using LinqStudio.Core.Services;
using System.Reflection;

namespace LinqStudio.Core.Tests;

public class UnitTest1
{
    [Fact]
    public async Task GetCompletionsAsync_ReturnsCompletions_ForUserQuery()
    {
        var projectNamespace = "Test";
        // Load generated files from the embedded resources
        var modelCode = ReadEmbeddedFile("Generated.Person.cs");
        var dbContextCode = ReadEmbeddedFile("Generated.TestDbContext.cs");

        var userQuery = "context.People.";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        var service = new CompilerService("TestDbContext", projectNamespace);

        service.Initialize(models, dbContextCode);

        var cursorPosition = userQuery.Length;

        // Act
        var completions = await service.GetCompletionsAsync(userQuery, cursorPosition);

        // Assert
        Assert.NotNull(completions);
        Assert.NotEmpty(completions); // Should return some completions
    }

    private string ReadEmbeddedFile(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinqStudio.Core.Tests.{path}") ?? throw new FileNotFoundException($"Resource not found: {path}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

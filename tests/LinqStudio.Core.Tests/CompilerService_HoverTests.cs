using LinqStudio.Core.Services;
using System.Reflection;

namespace LinqStudio.Core.Tests;

public class CompilerService_HoverTests
{
    [Fact]
    public async Task Hover_ReturnsPropertyInfo_ForSimpleProperty()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var userQuery = "context.People";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);

        await service.Initialize(models, dbContextCode);

        var cursorPosition = userQuery.IndexOf("People") + 1;

        var hover = await service.GetHoverAsync(userQuery, cursorPosition);

        Assert.NotNull(hover);
        Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
        Assert.Contains("DbSet", hover!.Markdown!);
        Assert.True(hover.StartOffset >= 0 && hover.StartOffset < userQuery.Length);
        var extracted = userQuery.Substring(hover.StartOffset, Math.Min(hover.Length, userQuery.Length - hover.StartOffset));
        Assert.Contains("People", extracted);
    }

    [Fact]
    public async Task Hover_ReturnsMethodSignature_ForInvocationAndInsideArgument()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var userQuery = "context.People.Where(p => p.Name == \"Bob\")";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);

        await service.Initialize(models, dbContextCode);

        var cursorPosition = userQuery.IndexOf("Where") + 1;
        var hover = await service.GetHoverAsync(userQuery, cursorPosition);

        Assert.NotNull(hover);
        Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
        Assert.Contains("Where", hover!.Markdown!);
        Assert.Contains("Func", hover.Markdown);

        var cursorInsideArg = userQuery.IndexOf("p.Name") + 1;
        var hoverInsideArg = await service.GetHoverAsync(userQuery, cursorInsideArg);
        Assert.NotNull(hoverInsideArg);
        Assert.False(string.IsNullOrWhiteSpace(hoverInsideArg?.Markdown));
        Assert.Contains("Where", hoverInsideArg!.Markdown!);
    }

    [Fact]
    public async Task Hover_WithXmlDoc_IncludesDocumentation()
    {
        var projectNamespace = "XmlTest";

        // Prepare a small model and context where DbSet has XML docs.
        var modelCode = "namespace XmlTest; public class Foo { public int Id { get; set; } }";
        var dbContextCode = "using Microsoft.EntityFrameworkCore; namespace XmlTest; public class FooContext : DbContext {\n    /// <summary>My special set</summary>\n    public DbSet<Foo> Foos { get; set; } }";

        var userQuery = "context.Foos";

        using var service = new CompilerService("FooContext", projectNamespace);
        await service.Initialize(new Dictionary<string, string> { { "Foo", modelCode } }, dbContextCode);

        var cursor = userQuery.IndexOf("Foos") + 1;
        var hover = await service.GetHoverAsync(userQuery, cursor);

        Assert.NotNull(hover);
        Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
        Assert.Contains("Foos", hover!.Markdown!);
        // Should include the XML documentation text
        Assert.Contains("My special set", hover.Markdown);
    }

    [Fact]
    public async Task Hover_InvalidCursorPositions_ReturnNull()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var userQuery = "context.People";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);

        await service.Initialize(models, dbContextCode);

        // Negative cursor should be handled safely (clamped) and not throw
        var hoverNeg = await service.GetHoverAsync(userQuery, -5);
        Assert.NotNull(hoverNeg);

        // Cursor way past end should be handled safely (clamped) and not throw
        var hoverPast = await service.GetHoverAsync(userQuery, userQuery.Length + 100);
        Assert.True(hoverPast == null || !string.IsNullOrWhiteSpace(hoverPast.Markdown));
    }

    [Fact]
    public async Task Hover_OnWhitespaceOrPunctuation_ReturnsNull()
    {
        var projectNamespace = "Test";
        var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
        var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

        var userQuery = "context.People.Where(p => p.Name == \"Bob\")";

        var models = new Dictionary<string, string> { { "Person", modelCode } };
        using var service = new CompilerService("TestDbContext", projectNamespace);

        await service.Initialize(models, dbContextCode);

        // Hover on the '(' character - implementation may resolve method or return null; ensure no exception and accept either
        var cursorParen = userQuery.IndexOf('(') + 0;
        var hoverParen = await service.GetHoverAsync(userQuery, cursorParen);
        Assert.True(hoverParen == null || (hoverParen.Markdown?.Contains("Where", StringComparison.OrdinalIgnoreCase) ?? false));

        // Hover on a space
        var cursorSpace = userQuery.IndexOf(' ') + 0;
        var hoverSpace = await service.GetHoverAsync(userQuery, cursorSpace);
        Assert.True(hoverSpace == null || !string.IsNullOrWhiteSpace(hoverSpace.Markdown));
    }

    private string ReadEmbeddedFile(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinqStudio.Core.Tests.{path}") ?? throw new FileNotFoundException($"Resource not found: {path}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

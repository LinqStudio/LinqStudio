using LinqStudio.Core.Services;
using System.Reflection;

namespace LinqStudio.Core.Tests;

public class CompilerServiceTests
{
	[Fact]
	public async Task GetCompletionsAsync_ReturnsCompletions_ForUserQuery()
	{
		var projectNamespace = "Test";
		// Load generated files from the embedded resources
		var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
		var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

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

	[Fact]
	public async Task GetHoverAsync_ReturnsHover_ForUserQuery()
	{
		var projectNamespace = "Test";
		// Load generated files from the embedded resources
		var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
		var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

		var userQuery = "context.People";

		var models = new Dictionary<string, string> { { "Person", modelCode } };
		var service = new CompilerService("TestDbContext", projectNamespace);

		service.Initialize(models, dbContextCode);

		// position somewhere inside 'People'
		var cursorPosition = userQuery.IndexOf("People") + 1;

		// Act
		var hover = await service.GetHoverAsync(userQuery, cursorPosition);

		// Assert
		Assert.NotNull(hover);
		Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
		Assert.Contains("DbSet", hover!.Markdown!);
		// ensure span maps back to the 'People' token
		Assert.True(hover.StartOffset >= 0 && hover.StartOffset < userQuery.Length);
		var extracted = userQuery.Substring(hover.StartOffset, Math.Min(hover.Length, userQuery.Length - hover.StartOffset));
		Assert.Contains("People", extracted);
	}

	[Fact]
	public async Task GetHoverAsync_ReturnsHover_ForMethodInvocation()
	{
		var projectNamespace = "Test";
		var modelCode = ReadEmbeddedFile("TestFiles.Person.cs");
		var dbContextCode = ReadEmbeddedFile("TestFiles.TestDbContext.cs");

		var userQuery = "context.People.Where(p => p.Name == \"Bob\")";

		var models = new Dictionary<string, string> { { "Person", modelCode } };
		var service = new CompilerService("TestDbContext", projectNamespace);

		service.Initialize(models, dbContextCode);

		var cursorPosition = userQuery.IndexOf("Where") + 1;

		var hover = await service.GetHoverAsync(userQuery, cursorPosition);

		Assert.NotNull(hover);
		Assert.False(string.IsNullOrWhiteSpace(hover?.Markdown));
		Assert.Contains("Where", hover!.Markdown!);
		Assert.Contains("Func", hover.Markdown);

		// Hover inside invocation argument (lambda body should still return method information, not "Lambda")
		var cursorInsideArg = userQuery.IndexOf("p.Name") + 1;
		var hoverInsideArg = await service.GetHoverAsync(userQuery, cursorInsideArg);
		Assert.NotNull(hoverInsideArg);
		Assert.False(string.IsNullOrWhiteSpace(hoverInsideArg?.Markdown));
		Assert.Contains("Where", hoverInsideArg!.Markdown!);
	}

	private string ReadEmbeddedFile(string path)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinqStudio.Core.Tests.{path}") ?? throw new FileNotFoundException($"Resource not found: {path}");
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}

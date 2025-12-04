using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests;

public class UnitTest1
{
    [Fact]
    public async Task GetCompletionsAsync_ReturnsCompletions_ForUserQuery()
    {
        // Arrange: minimal EF Core model and DbContext
        var modelCode = """
            namespace Test;

            public class Person
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """;
        var dbContextCode = """
            using Microsoft.EntityFrameworkCore;

            namespace Test;
            public class TestDbContext : DbContext
            {
                public DbSet<Person> People { get; set; }
            }
            """;
        var models = new Dictionary<string, string> { { "Person", modelCode } };
        var service = new CompilerService("TestDbContext");
        service.Initialize(models, dbContextCode);

        var userQuery = "var x = context.People.Where( x => x.";
        var cursorPosition = userQuery.Length;

        // Act
        var completions = await service.GetCompletionsAsync(userQuery, cursorPosition);

        // Assert
        Assert.NotNull(completions);
        Assert.NotEmpty(completions); // Should return some completions
    }
}

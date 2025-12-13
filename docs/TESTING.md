# Testing Guidelines for LinqStudio

## Testing Practices

### Do Not Use Mocking Libraries

Do not use mocking libraries like Moq. Instead, create simple fake implementations for testing purposes. This keeps tests simple and maintainable.

Example:
```csharp
// Don't do this:
var mockGenerator = new Mock<IDatabaseQueryGenerator>();
mockGenerator.Setup(g => g.GetTablesAsync(...)).ReturnsAsync(tables);

// Do this instead:
private class FakeDatabaseQueryGenerator : IDatabaseQueryGenerator
{
    public List<DatabaseTableName> Tables { get; set; } = new();
    
    public Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DatabaseTableName>>(Tables);
    }
}
```

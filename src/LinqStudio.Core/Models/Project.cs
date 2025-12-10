namespace LinqStudio.Core.Models;

public record Project
{
	public int SchemaVersion { get; init; } = 1;
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string ConnectionString { get; init; } = string.Empty;
	public DateTimeOffset CreatedDate { get; init; }
	public DateTimeOffset ModifiedDate { get; init; }

	// Future properties
	public Dictionary<string, string>? Models { get; init; }
	public string? DbContextCode { get; init; }
	public List<SavedQuery>? Queries { get; init; }
}


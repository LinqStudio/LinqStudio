namespace LinqStudio.Core.Models;

public class Project
{
	public int SchemaVersion { get; set; } = ProjectConstants.CurrentSchemaVersion;
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string ConnectionString { get; set; } = string.Empty;
	public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset ModifiedDate { get; set; }

	// Future properties
	public Dictionary<string, string>? Models { get; set; }
	public string? DbContextCode { get; set; }
}


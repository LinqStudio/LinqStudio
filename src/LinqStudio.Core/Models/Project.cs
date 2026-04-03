namespace LinqStudio.Core.Models;

public class Project
{
	public int SchemaVersion { get; set; } = ProjectConstants.CurrentSchemaVersion;
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;

	public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset ModifiedDate { get; set; }

	/// <summary>
	/// The list of database server connections associated with this project.
	/// Each entry holds its own connection string, database type, and schema generator.
	/// </summary>
	public List<ServerConnection> Connections { get; set; } = [];

	// Future properties
	public Dictionary<string, string>? Models { get; set; }
	public string? DbContextCode { get; set; }
}


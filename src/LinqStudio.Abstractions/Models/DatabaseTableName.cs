namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Represents a database table with its schema and table name
/// </summary>
public record DatabaseTableName
{
	/// <summary>
	/// Schema name (e.g., "dbo", "public"). May be null for databases without schema support.
	/// </summary>
	public string? Schema { get; init; }

	/// <summary>
	/// Name of the table.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Full qualified name in the format "schema.name" or just "name" if schema is null.
	/// </summary>
	public string FullName => Schema != null ? $"{Schema}.{Name}" : Name;

}

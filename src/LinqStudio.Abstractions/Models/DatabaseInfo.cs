namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Identifies a database/catalog available through a database connection.
/// </summary>
public sealed record DatabaseInfo
{
	/// <summary>
	/// Database/catalog name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Whether this database was selected explicitly by the connection string.
	/// </summary>
	public bool IsExplicitlySelected { get; init; }
}

namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Represents detailed information about a database table, including its columns and foreign keys.
/// </summary>
public record DatabaseTableDetail : DatabaseTableName
{
	/// <summary>
	/// List of columns in the table. Populated only when retrieving detailed table information.
	/// </summary>
	public required IReadOnlyList<TableColumn> Columns { get; init; }

	/// <summary>
	/// List of foreign keys in the table. Populated only when retrieving detailed table information.
	/// </summary>
	public required IReadOnlyList<ForeignKey> ForeignKeys { get; init; }
}

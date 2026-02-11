namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Represents a foreign key constraint in a database table.
/// </summary>
public record ForeignKey
{
	/// <summary>
	/// Name of the foreign key constraint.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Name of the column in the current table.
	/// </summary>
	public required string ColumnName { get; init; }

	/// <summary>
	/// Name of the referenced table (including schema if applicable).
	/// </summary>
	public required string ReferencedTable { get; init; }

	/// <summary>
	/// Name of the referenced column in the referenced table.
	/// </summary>
	public required string ReferencedColumn { get; init; }
}

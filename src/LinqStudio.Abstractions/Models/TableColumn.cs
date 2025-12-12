namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Represents a column in a database table.
/// </summary>
public record TableColumn
{
	/// <summary>
	/// Name of the column.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Data type of the column (e.g., "int", "varchar", "datetime").
	/// </summary>
	public required string DataType { get; init; }

	/// <summary>
	/// Whether the column allows NULL values.
	/// </summary>
	public required bool IsNullable { get; init; }

	/// <summary>
	/// Whether the column is part of the primary key.
	/// </summary>
	public required bool IsPrimaryKey { get; init; }

	/// <summary>
	/// Whether the column is an identity/auto-increment column.
	/// </summary>
	public required bool IsIdentity { get; init; }

	/// <summary>
	/// Maximum length of the column (for string types), null if not applicable.
	/// </summary>
	public int? MaxLength { get; init; }

	/// <summary>
	/// Precision of the column (for numeric types), null if not applicable.
	/// </summary>
	public int? Precision { get; init; }

	/// <summary>
	/// Scale of the column (for numeric types), null if not applicable.
	/// </summary>
	public int? Scale { get; init; }
}

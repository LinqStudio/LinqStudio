namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Result of executing a LINQ query against a database.
/// Contains the rows, column names, execution time, and any error information.
/// </summary>
public record QueryExecutionResult
{
	/// <summary>
	/// The data rows returned by the query. Each row is a dictionary of column name to value.
	/// </summary>
	public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }

	/// <summary>
	/// The column names in the result set, in the order they appear.
	/// </summary>
	public required IReadOnlyList<string> ColumnNames { get; init; }

	/// <summary>
	/// Time elapsed during query execution.
	/// </summary>
	public required TimeSpan Elapsed { get; init; }

	/// <summary>
	/// Error message if the query failed, null if successful.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Indicates whether the error was a compile-time error (vs runtime error).
	/// </summary>
	public bool IsCompileError { get; init; }

	/// <summary>
	/// True if the query executed successfully (no error), false otherwise.
	/// </summary>
	public bool Success => ErrorMessage is null;

	/// <summary>
	/// Creates an empty result with no rows or columns.
	/// </summary>
	public static QueryExecutionResult Empty(TimeSpan elapsed) => new()
	{
		Rows = [],
		ColumnNames = [],
		Elapsed = elapsed
	};

	/// <summary>
	/// Creates a result representing a query error.
	/// </summary>
	public static QueryExecutionResult FromError(string message, bool isCompileError, TimeSpan elapsed) => new()
	{
		Rows = [],
		ColumnNames = [],
		Elapsed = elapsed,
		ErrorMessage = message,
		IsCompileError = isCompileError
	};
}

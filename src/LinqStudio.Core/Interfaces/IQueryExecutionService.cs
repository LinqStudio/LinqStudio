using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.Interfaces;

/// <summary>
/// Service for executing LINQ queries against a database and returning results.
/// </summary>
public interface IQueryExecutionService : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Executes a user-provided LINQ query string and returns the results.
	/// </summary>
	/// <param name="userQuery">The LINQ query code to execute.</param>
	/// <param name="project">The project containing connection string and database configuration.</param>
	/// <param name="cancellationToken">Cancellation token to stop execution.</param>
	/// <returns>The query execution result containing rows, columns, timing, and any errors.</returns>
	Task<QueryExecutionResult> ExecuteQueryAsync(
		string userQuery,
		Project project,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Determines whether the current result contains tracked entities from the retained DbContext.
	/// </summary>
	bool IsEntityResult(QueryExecutionResult result);

	/// <summary>
	/// Gets the scalar entity properties that can be edited in the result grid.
	/// </summary>
	IReadOnlySet<string> GetEditableColumns(QueryExecutionResult result);

	/// <summary>
	/// Updates a scalar entity property from its grid text representation.
	/// </summary>
	void UpdateEntityProperty(
		object entity,
		string propertyName,
		string? value);

	/// <summary>
	/// Persists changes tracked by the retained DbContext.
	/// </summary>
	Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

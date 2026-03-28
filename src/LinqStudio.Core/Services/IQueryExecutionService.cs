using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Service for executing LINQ queries against a database and returning results.
/// </summary>
public interface IQueryExecutionService
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
}

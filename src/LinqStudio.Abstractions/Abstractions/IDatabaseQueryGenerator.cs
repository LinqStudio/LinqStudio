using LinqStudio.Abstractions.Models;

namespace LinqStudio.Abstractions.Abstractions;

/// <summary>
/// Interface for generating database schema information.
/// </summary>
public interface IDatabaseQueryGenerator
{
	/// <summary>
	/// Gets a flat list of all tables in the database with their schema and name.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of database tables with basic information (schema and name only).</returns>
	Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets detailed information about a specific table including columns and foreign keys.
	/// </summary>
	/// <param name="tableName">Full table name in format "schema.name" or just "name".</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Detailed table information including columns and foreign keys.</returns>
	Task<DatabaseTable> GetTableAsync(string tableName, CancellationToken cancellationToken = default);
}

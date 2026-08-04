using LinqStudio.Abstractions.Models;

namespace LinqStudio.Abstractions;

/// <summary>
/// Interface for generating database schema information.
/// </summary>
public interface IDatabaseQueryGenerator
{
	/// <summary>
	/// Gets databases/catalogs visible to the connection.
	/// </summary>
	async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(CancellationToken cancellationToken = default)
	{
		var tables = await GetTablesAsync(cancellationToken);
		return tables
			.Select(table => table.DatabaseName)
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(name => new DatabaseInfo { Name = name! })
			.ToList();
	}

	/// <summary>
	/// Gets the tables in the connected database with their database, schema, and name.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of database tables with basic information.</returns>
	Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets tables from a specific database/catalog.
	/// </summary>
	async Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(
		string databaseName,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName, nameof(databaseName));
		var tables = await GetTablesAsync(cancellationToken);
		return tables
			.Where(table => string.Equals(table.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}

	/// <summary>
	/// Gets detailed information about a specific table including columns and foreign keys.
	/// </summary>
	/// <param name="tableName">Full table name in format "schema.name" or just "name".</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Detailed table information including columns and foreign keys.</returns>
	Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets detailed information about a specific table including columns and foreign keys.
	/// </summary>
	/// <param name="table">DatabaseTableName instance</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Detailed table information including columns and foreign keys.</returns>
	public Task<DatabaseTableDetail> GetTableAsync(DatabaseTableName table, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(table);
		return GetTableAsync(table.FullName, cancellationToken);
	}

	/// <summary>
	/// Tests the database connection.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for timeout control.</param>
	/// <returns>Task that completes successfully if connection is valid, throws exception otherwise.</returns>
	Task TestConnectionAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Maps a database-specific data type to a generic DbColumnType.
	/// </summary>
	/// <param name="dataType">Database-specific type name (e.g., "int", "varchar", "timestamp").</param>
	/// <returns>Corresponding generic DbColumnType.</returns>
	DbColumnType MapToGenericType(string dataType);
}

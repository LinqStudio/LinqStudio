using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Represents a single database connection in the object explorer.
/// </summary>
public class DatabaseConnection
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public required string Name { get; init; }
	public required DatabaseType DatabaseType { get; init; }
	public required string ConnectionString { get; init; }
	public required IDatabaseQueryGenerator QueryGenerator { get; init; }
	
	/// <summary>
	/// Cache of table names. Null means not loaded yet.
	/// </summary>
	public IReadOnlyList<DatabaseTableName>? CachedTables { get; set; }
	
	/// <summary>
	/// Cache of table details. Key is the table's FullName.
	/// </summary>
	public Dictionary<string, DatabaseTableDetail> CachedTableDetails { get; init; } = new();
}

/// <summary>
/// Singleton service that manages multiple database connections and their cached metadata.
/// Used by the Object Explorer panel to display database schemas.
/// </summary>
public class ObjectExplorerService
{
	private readonly List<DatabaseConnection> _connections = new();
	private readonly object _lock = new();

	/// <summary>
	/// Event raised when connections are added, removed, or refreshed.
	/// </summary>
	public event Action? ConnectionsChanged;

	/// <summary>
	/// Gets all database connections.
	/// </summary>
	public IReadOnlyList<DatabaseConnection> Connections
	{
		get
		{
			lock (_lock)
			{
				return _connections.ToList();
			}
		}
	}

	/// <summary>
	/// Adds a new database connection to the object explorer.
	/// </summary>
	/// <param name="name">Display name for the connection.</param>
	/// <param name="databaseType">Type of database.</param>
	/// <param name="connectionString">Connection string.</param>
	/// <param name="queryGenerator">Query generator instance.</param>
	/// <returns>The created connection.</returns>
	public DatabaseConnection AddConnection(string name, DatabaseType databaseType, string connectionString, IDatabaseQueryGenerator queryGenerator)
	{
		var connection = new DatabaseConnection
		{
			Name = name,
			DatabaseType = databaseType,
			ConnectionString = connectionString,
			QueryGenerator = queryGenerator
		};

		lock (_lock)
		{
			_connections.Add(connection);
		}

		ConnectionsChanged?.Invoke();
		return connection;
	}

	/// <summary>
	/// Removes a connection from the object explorer.
	/// </summary>
	public void RemoveConnection(Guid connectionId)
	{
		lock (_lock)
		{
			_connections.RemoveAll(c => c.Id == connectionId);
		}

		ConnectionsChanged?.Invoke();
	}

	/// <summary>
	/// Gets the list of tables for a connection, using cache if available.
	/// </summary>
	public async Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
	{
		if (connection.CachedTables != null)
		{
			return connection.CachedTables;
		}

		var tables = await connection.QueryGenerator.GetTablesAsync(cancellationToken);
		connection.CachedTables = tables;
		return tables;
	}

	/// <summary>
	/// Gets detailed information about a table, using cache if available.
	/// </summary>
	public async Task<DatabaseTableDetail> GetTableDetailAsync(DatabaseConnection connection, DatabaseTableName table, CancellationToken cancellationToken = default)
	{
		if (connection.CachedTableDetails.TryGetValue(table.FullName, out var cachedDetail))
		{
			return cachedDetail;
		}

		var detail = await connection.QueryGenerator.GetTableAsync(table, cancellationToken);
		connection.CachedTableDetails[table.FullName] = detail;
		return detail;
	}

	/// <summary>
	/// Clears all cached data for a specific connection and reloads tables.
	/// </summary>
	public async Task RefreshConnectionAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
	{
		connection.CachedTables = null;
		connection.CachedTableDetails.Clear();
		await GetTablesAsync(connection, cancellationToken);
		ConnectionsChanged?.Invoke();
	}

	/// <summary>
	/// Clears all cached data for all connections and reloads.
	/// </summary>
	public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
	{
		var connections = Connections;
		foreach (var connection in connections)
		{
			connection.CachedTables = null;
			connection.CachedTableDetails.Clear();
			await GetTablesAsync(connection, cancellationToken);
		}
		ConnectionsChanged?.Invoke();
	}
}

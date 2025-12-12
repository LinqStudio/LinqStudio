using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using System.Data.Common;

namespace LinqStudio.Databases;

/// <summary>
/// Base class for database generators that use ADO.NET to fetch schema information.
/// </summary>
public abstract class AdoNetDatabaseGeneratorBase : IDatabaseQueryGenerator
{
	/// <summary>
	/// Connection string for the database.
	/// </summary>
	protected string ConnectionString { get; }

	/// <summary>
	/// Creates a new instance of the ADO.NET database generator.
	/// </summary>
	/// <param name="connectionString">Connection string for the database.</param>
	protected AdoNetDatabaseGeneratorBase(string connectionString)
	{
		ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
	}

	/// <summary>
	/// Creates a database connection for the specific database type.
	/// </summary>
	/// <returns>A database connection.</returns>
	protected abstract DbConnection CreateConnection();

	/// <inheritdoc/>
	public abstract Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(CancellationToken cancellationToken = default);

	/// <inheritdoc/>
	public abstract Task<DatabaseTable> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Parses a table name into schema and name components.
	/// </summary>
	/// <param name="tableName">Full table name in format "schema.name" or just "name".</param>
	/// <returns>Tuple of (schema, name). Schema will be null if not specified.</returns>
	protected static (string? schema, string name) ParseTableName(string tableName)
	{
		var parts = tableName.Split('.');
		if (parts.Length == 2)
			return (parts[0], parts[1]);
		if (parts.Length == 1)
			return (null, parts[0]);
		
		throw new ArgumentException($"Invalid table name format: {tableName}. Expected 'schema.name' or 'name'.", nameof(tableName));
	}
}

using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases;

/// <summary>
/// Base class for database generators that use ADO.NET to fetch schema information.
/// </summary>
public abstract class AdoNetDatabaseGeneratorBase : IDatabaseQueryGenerator
{
	/// <summary>
	/// Database facade for accessing the underlying database connection.
	/// </summary>
	protected DatabaseFacade Database { get; }

	/// <summary>
	/// Creates a new instance of the ADO.NET database generator.
	/// </summary>
	/// <param name="database">EF Core database facade.</param>
	protected AdoNetDatabaseGeneratorBase(DatabaseFacade database)
	{
		Database = database ?? throw new ArgumentNullException(nameof(database));
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(CancellationToken cancellationToken = default)
	{
		var tables = new List<DatabaseTable>();
		var connection = Database.GetDbConnection();
		
		var wasOpen = connection.State == ConnectionState.Open;
		if (!wasOpen)
			await connection.OpenAsync(cancellationToken);

		try
		{
			// Use ADO.NET GetSchema to retrieve tables
			var tablesSchema = await Task.Run(() => connection.GetSchema("Tables"), cancellationToken);

			foreach (DataRow row in tablesSchema.Rows)
			{
				var table = ParseTableFromSchemaRow(row);
				if (table != null)
					tables.Add(table);
			}
		}
		finally
		{
			if (!wasOpen)
				await connection.CloseAsync();
		}

		return tables;
	}

	/// <inheritdoc/>
	public abstract Task<DatabaseTable> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Parses a table from a DataRow from the Tables schema collection.
	/// </summary>
	protected abstract DatabaseTable? ParseTableFromSchemaRow(DataRow row);

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

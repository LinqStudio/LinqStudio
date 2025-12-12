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
	public async Task<DatabaseTable> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		var (schema, name) = ParseTableName(tableName);
		schema = NormalizeSchemaName(schema);

		var connection = Database.GetDbConnection();
		
		var wasOpen = connection.State == ConnectionState.Open;
		if (!wasOpen)
			await connection.OpenAsync(cancellationToken);

		try
		{
			// Get columns
			var columns = await GetColumnsAsync(connection, schema, name, cancellationToken);

			// Get foreign keys
			var foreignKeys = await GetForeignKeysAsync(connection, schema, name, cancellationToken);

			return new DatabaseTable
			{
				Schema = schema,
				Name = name,
				Columns = columns,
				ForeignKeys = foreignKeys
			};
		}
		finally
		{
			if (!wasOpen)
				await connection.CloseAsync();
		}
	}

	/// <summary>
	/// Gets columns for a specific table using ADO.NET GetSchema.
	/// </summary>
	protected virtual async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// Use ADO.NET GetSchema to retrieve columns
		var restrictions = CreateColumnRestrictions(schema, tableName);
		var columnsSchema = await Task.Run(() => connection.GetSchema("Columns", restrictions), cancellationToken);

		// Get primary keys
		var primaryKeys = await GetPrimaryKeysAsync(connection, schema, tableName, cancellationToken);

		foreach (DataRow row in columnsSchema.Rows)
		{
			var column = ParseColumnFromSchemaRow(row, primaryKeys);
			if (column != null)
				columns.Add(column);
		}

		return columns;
	}

	/// <summary>
	/// Gets primary keys for a specific table using ADO.NET GetSchema.
	/// </summary>
	protected virtual async Task<HashSet<string>> GetPrimaryKeysAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var primaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			var restrictions = CreateIndexRestrictions(schema, tableName);
			var indexColumns = await Task.Run(() => connection.GetSchema("IndexColumns", restrictions), cancellationToken);

			foreach (DataRow row in indexColumns.Rows)
			{
				var columnName = row["column_name"]?.ToString();
				if (!string.IsNullOrEmpty(columnName))
					primaryKeys.Add(columnName);
			}
		}
		catch
		{
			// If IndexColumns is not supported, fall back to other methods
		}

		return primaryKeys;
	}

	/// <summary>
	/// Gets foreign keys for a specific table.
	/// </summary>
	protected abstract Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken);

	/// <summary>
	/// Parses a table from a DataRow from the Tables schema collection.
	/// </summary>
	protected abstract DatabaseTable? ParseTableFromSchemaRow(DataRow row);

	/// <summary>
	/// Parses a column from a DataRow from the Columns schema collection.
	/// </summary>
	protected abstract TableColumn? ParseColumnFromSchemaRow(DataRow row, HashSet<string> primaryKeys);

	/// <summary>
	/// Normalizes the schema name (provides default schema if null).
	/// </summary>
	protected abstract string? NormalizeSchemaName(string? schema);

	/// <summary>
	/// Creates restrictions array for Columns schema collection.
	/// </summary>
	protected abstract string?[] CreateColumnRestrictions(string? schema, string tableName);

	/// <summary>
	/// Creates restrictions array for IndexColumns schema collection.
	/// </summary>
	protected virtual string?[] CreateIndexRestrictions(string? schema, string tableName)
	{
		return new string?[] { null, schema, tableName, null };
	}

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

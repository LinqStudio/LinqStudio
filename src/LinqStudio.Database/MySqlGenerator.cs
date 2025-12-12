using LinqStudio.Abstractions.Models;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases;

/// <summary>
/// Database generator for MySQL using ADO.NET.
/// </summary>
public class MySqlGenerator : AdoNetDatabaseGeneratorBase
{
	/// <summary>
	/// Creates a new instance of the MySQL generator.
	/// </summary>
	/// <param name="database">EF Core database facade.</param>
	public MySqlGenerator(DbConnection connection) : base(connection)
	{
	}

	/// <inheritdoc/>
	protected override DatabaseTableName? ParseTableFromSchemaRow(DataRow row)
	{
		var schema = row["TABLE_SCHEMA"]?.ToString();
		var tableName = row["TABLE_NAME"]?.ToString();
		var tableType = row["TABLE_TYPE"]?.ToString();

		// Only return base tables (not views)
		if (tableType != "BASE TABLE" || string.IsNullOrEmpty(tableName))
			return null;

		return new DatabaseTableName
		{
			Schema = schema,
			Name = tableName
		};
	}

	/// <inheritdoc/>
	public override async Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		var (schema, name) = ParseTableName(tableName);
		schema ??= DbConnection.Database; // Default to current database

		var wasOpen = DbConnection.State == ConnectionState.Open;
		if (!wasOpen)
			await DbConnection.OpenAsync(cancellationToken);

		try
		{
			// Get columns using database-specific query
			var columns = await GetColumnsAsync(DbConnection, schema, name, cancellationToken);

			// Get foreign keys using database-specific query
			var foreignKeys = await GetForeignKeysAsync(DbConnection, schema, name, cancellationToken);

			return new DatabaseTableDetail
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
				await DbConnection.CloseAsync();
		}
	}

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// MySQL doesn't always work well with GetSchema("Columns"), use query instead
		const string query = """
			SELECT 
				c.COLUMN_NAME,
				c.DATA_TYPE,
				c.IS_NULLABLE,
				c.COLUMN_KEY,
				c.EXTRA,
				c.CHARACTER_MAXIMUM_LENGTH,
				c.NUMERIC_PRECISION,
				c.NUMERIC_SCALE
			FROM INFORMATION_SCHEMA.COLUMNS c
			WHERE c.TABLE_SCHEMA = @Schema
				AND c.TABLE_NAME = @TableName
			ORDER BY c.ORDINAL_POSITION
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

		var schemaParam = command.CreateParameter();
		schemaParam.ParameterName = "@Schema";
		schemaParam.Value = schema ?? (object)DBNull.Value;
		command.Parameters.Add(schemaParam);

		var tableParam = command.CreateParameter();
		tableParam.ParameterName = "@TableName";
		tableParam.Value = tableName;
		command.Parameters.Add(tableParam);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var columnKey = reader.GetString(3);
			var extra = reader.GetString(4);

			// Parse max length safely - can be very large for LONGTEXT
			int? maxLength = null;
			if (!reader.IsDBNull(5))
			{
				var value = reader.GetValue(5);
				if (long.TryParse(value.ToString(), out var longValue))
				{
					maxLength = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
				}
			}

			// Parse precision and scale safely
			int? precision = null;
			if (!reader.IsDBNull(6))
			{
				var value = reader.GetValue(6);
				if (long.TryParse(value.ToString(), out var longValue))
				{
					precision = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
				}
			}

			int? scale = null;
			if (!reader.IsDBNull(7))
			{
				var value = reader.GetValue(7);
				if (long.TryParse(value.ToString(), out var longValue))
				{
					scale = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
				}
			}

			columns.Add(new TableColumn
			{
				Name = reader.GetString(0),
				DataType = reader.GetString(1),
				IsNullable = reader.GetString(2) == "YES",
				IsPrimaryKey = columnKey == "PRI",
				IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
				MaxLength = maxLength,
				Precision = precision,
				Scale = scale
			});
		}


		return columns;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

		// MySQL: use INFORMATION_SCHEMA query for foreign keys
		const string query = """
			SELECT 
				kcu.CONSTRAINT_NAME,
				kcu.COLUMN_NAME,
				CONCAT(kcu.REFERENCED_TABLE_SCHEMA, '.', kcu.REFERENCED_TABLE_NAME) AS ReferencedTable,
				kcu.REFERENCED_COLUMN_NAME
			FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
			WHERE kcu.TABLE_SCHEMA = @Schema
				AND kcu.TABLE_NAME = @TableName
				AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
			ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

		var schemaParam = command.CreateParameter();
		schemaParam.ParameterName = "@Schema";
		schemaParam.Value = schema ?? (object)DBNull.Value;
		command.Parameters.Add(schemaParam);

		var tableParam = command.CreateParameter();
		tableParam.ParameterName = "@TableName";
		tableParam.Value = tableName;
		command.Parameters.Add(tableParam);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			foreignKeys.Add(new ForeignKey
			{
				Name = reader.GetString(0),
				ColumnName = reader.GetString(1),
				ReferencedTable = reader.GetString(2),
				ReferencedColumn = reader.GetString(3)
			});
		}

		return foreignKeys;
	}
}

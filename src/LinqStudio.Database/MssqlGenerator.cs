using LinqStudio.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases.MSSQL;

/// <summary>
/// Database generator for Microsoft SQL Server using ADO.NET.
/// </summary>
public class MssqlGenerator : AdoNetDatabaseGeneratorBase
{
	/// <summary>
	/// Creates a new instance of the MSSQL generator.
	/// </summary>
	/// <param name="database">EF Core database facade.</param>
	public MssqlGenerator(DatabaseFacade database) : base(database)
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
		schema ??= "dbo"; // Default schema for SQL Server

		var connection = Database.GetDbConnection();
		
		var wasOpen = connection.State == ConnectionState.Open;
		if (!wasOpen)
			await connection.OpenAsync(cancellationToken);

		try
		{
			// Get columns using database-specific query for better information
			var columns = await GetColumnsAsync(connection, schema, name, cancellationToken);

			// Get foreign keys using database-specific query
			var foreignKeys = await GetForeignKeysAsync(connection, schema, name, cancellationToken);

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
				await connection.CloseAsync();
		}
	}

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// Use GetSchema for columns as it's database-independent
		var restrictions = new string?[] { null, schema, tableName, null };
		var columnsSchema = await Task.Run(() => connection.GetSchema("Columns", restrictions), cancellationToken);

		// Get primary key information from Indexes schema
		var primaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var indexesSchema = await Task.Run(() => connection.GetSchema("IndexColumns", restrictions), cancellationToken);
			foreach (DataRow row in indexesSchema.Rows)
			{
				var columnName = row["column_name"]?.ToString();
				if (!string.IsNullOrEmpty(columnName))
					primaryKeys.Add(columnName);
			}
		}
		catch
		{
			// If IndexColumns not supported, continue without PK info
		}

		foreach (DataRow row in columnsSchema.Rows)
		{
			var columnName = row["COLUMN_NAME"]?.ToString();
			if (string.IsNullOrEmpty(columnName))
				continue;

			var dataType = row["DATA_TYPE"]?.ToString() ?? "unknown";
			var isNullable = row["IS_NULLABLE"]?.ToString() == "YES";
			var isPrimaryKey = primaryKeys.Contains(columnName);

			// Parse max length
			int? maxLength = null;
			if (row.Table.Columns.Contains("CHARACTER_MAXIMUM_LENGTH") && !row.IsNull("CHARACTER_MAXIMUM_LENGTH"))
			{
				var maxLengthValue = row["CHARACTER_MAXIMUM_LENGTH"];
				if (maxLengthValue != DBNull.Value)
					maxLength = Convert.ToInt32(maxLengthValue);
			}

			// Parse precision and scale
			int? precision = null;
			int? scale = null;
			if (row.Table.Columns.Contains("NUMERIC_PRECISION") && !row.IsNull("NUMERIC_PRECISION"))
			{
				var precisionValue = row["NUMERIC_PRECISION"];
				if (precisionValue != DBNull.Value)
					precision = Convert.ToInt32(precisionValue);
			}
			if (row.Table.Columns.Contains("NUMERIC_SCALE") && !row.IsNull("NUMERIC_SCALE"))
			{
				var scaleValue = row["NUMERIC_SCALE"];
				if (scaleValue != DBNull.Value)
					scale = Convert.ToInt32(scaleValue);
			}

			// GetSchema doesn't provide identity info, so we'll default to false
			var isIdentity = false;

			columns.Add(new TableColumn
			{
				Name = columnName,
				DataType = dataType,
				IsNullable = isNullable,
				IsPrimaryKey = isPrimaryKey,
				IsIdentity = isIdentity,
				MaxLength = maxLength,
				Precision = precision,
				Scale = scale
			});
		}

		return columns;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

		// SQL Server doesn't support GetSchema("ForeignKeys"), use query instead
		const string query = """
			SELECT 
				fk.name AS ForeignKeyName,
				c.name AS ColumnName,
				rs.name + '.' + rt.name AS ReferencedTable,
				rc.name AS ReferencedColumn
			FROM sys.foreign_keys fk
			INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
			INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
			INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
			INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
			INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
			WHERE fk.parent_object_id = OBJECT_ID(@TableName)
			ORDER BY fk.name
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;
		
		var parameter = command.CreateParameter();
		parameter.ParameterName = "@TableName";
		parameter.Value = $"{schema}.{tableName}";
		command.Parameters.Add(parameter);

		try
		{
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
		}
		catch
		{
			// If query fails, return empty list
		}

		return foreignKeys;
	}
}

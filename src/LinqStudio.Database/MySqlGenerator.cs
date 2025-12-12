using LinqStudio.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases.MySQL;

/// <summary>
/// Database generator for MySQL using ADO.NET.
/// </summary>
public class MySqlGenerator : AdoNetDatabaseGeneratorBase
{
	/// <summary>
	/// Creates a new instance of the MySQL generator.
	/// </summary>
	/// <param name="database">EF Core database facade.</param>
	public MySqlGenerator(DatabaseFacade database) : base(database)
	{
	}

	/// <inheritdoc/>
	protected override DatabaseTable? ParseTableFromSchemaRow(DataRow row)
	{
		var schema = row["TABLE_SCHEMA"]?.ToString();
		var tableName = row["TABLE_NAME"]?.ToString();
		var tableType = row["TABLE_TYPE"]?.ToString();

		// Only return base tables (not views)
		if (tableType != "BASE TABLE" || string.IsNullOrEmpty(tableName))
			return null;

		return new DatabaseTable
		{
			Schema = schema,
			Name = tableName
		};
	}

	/// <inheritdoc/>
	protected override TableColumn? ParseColumnFromSchemaRow(DataRow row, HashSet<string> primaryKeys)
	{
		var columnName = row["COLUMN_NAME"]?.ToString();
		if (string.IsNullOrEmpty(columnName))
			return null;

		var dataType = row["DATA_TYPE"]?.ToString() ?? "unknown";
		var isNullable = row["IS_NULLABLE"]?.ToString() == "YES";
		var isPrimaryKey = primaryKeys.Contains(columnName);

		// Parse max length
		int? maxLength = null;
		if (row.Table.Columns.Contains("CHARACTER_MAXIMUM_LENGTH") && !row.IsNull("CHARACTER_MAXIMUM_LENGTH"))
		{
			var maxLengthValue = row["CHARACTER_MAXIMUM_LENGTH"];
			if (maxLengthValue != DBNull.Value)
			{
				if (long.TryParse(maxLengthValue.ToString(), out var longValue))
					maxLength = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
			}
		}

		// Parse precision and scale
		int? precision = null;
		int? scale = null;
		if (row.Table.Columns.Contains("NUMERIC_PRECISION") && !row.IsNull("NUMERIC_PRECISION"))
		{
			var precisionValue = row["NUMERIC_PRECISION"];
			if (precisionValue != DBNull.Value)
			{
				if (long.TryParse(precisionValue.ToString(), out var longValue))
					precision = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
			}
		}
		if (row.Table.Columns.Contains("NUMERIC_SCALE") && !row.IsNull("NUMERIC_SCALE"))
		{
			var scaleValue = row["NUMERIC_SCALE"];
			if (scaleValue != DBNull.Value)
			{
				if (long.TryParse(scaleValue.ToString(), out var longValue))
					scale = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
			}
		}

		// Check for auto increment (MySQL specific)
		var isIdentity = false;
		if (row.Table.Columns.Contains("EXTRA") && !row.IsNull("EXTRA"))
		{
			var extra = row["EXTRA"]?.ToString() ?? "";
			isIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);
		}

		return new TableColumn
		{
			Name = columnName,
			DataType = dataType,
			IsNullable = isNullable,
			IsPrimaryKey = isPrimaryKey,
			IsIdentity = isIdentity,
			MaxLength = maxLength,
			Precision = precision,
			Scale = scale
		};
	}

	/// <inheritdoc/>
	protected override async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

		try
		{
			var restrictions = new string?[] { null, schema, tableName, null };
			var foreignKeysSchema = await Task.Run(() => connection.GetSchema("Foreign Keys", restrictions), cancellationToken);

			foreach (DataRow row in foreignKeysSchema.Rows)
			{
				var constraintName = row["CONSTRAINT_NAME"]?.ToString();
				var columnName = row["COLUMN_NAME"]?.ToString();
				var referencedSchema = row["REFERENCED_TABLE_SCHEMA"]?.ToString();
				var referencedTable = row["REFERENCED_TABLE_NAME"]?.ToString();
				var referencedColumn = row["REFERENCED_COLUMN_NAME"]?.ToString();

				if (string.IsNullOrEmpty(constraintName) || string.IsNullOrEmpty(columnName) ||
					string.IsNullOrEmpty(referencedTable) || string.IsNullOrEmpty(referencedColumn))
					continue;

				var fullReferencedTable = !string.IsNullOrEmpty(referencedSchema)
					? $"{referencedSchema}.{referencedTable}"
					: referencedTable;

				foreignKeys.Add(new ForeignKey
				{
					Name = constraintName,
					ColumnName = columnName,
					ReferencedTable = fullReferencedTable,
					ReferencedColumn = referencedColumn
				});
			}
		}
		catch
		{
			// If Foreign Keys schema collection is not supported, return empty list
		}

		return foreignKeys;
	}

	/// <inheritdoc/>
	protected override string? NormalizeSchemaName(string? schema)
	{
		// For MySQL, if no schema is provided, we'll use the current database from connection
		return schema ?? Database.GetDbConnection().Database;
	}

	/// <inheritdoc/>
	protected override string?[] CreateColumnRestrictions(string? schema, string tableName)
	{
		// Catalog, Schema, Table, Column
		return new string?[] { schema, null, tableName, null };
	}

	/// <inheritdoc/>
	protected override string?[] CreateIndexRestrictions(string? schema, string tableName)
	{
		// Catalog, Schema, Table, Index, Column
		return new string?[] { schema, null, tableName, null, null };
	}
}

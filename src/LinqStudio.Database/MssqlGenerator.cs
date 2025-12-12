using LinqStudio.Abstractions.Models;
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

		// Check if identity (SQL Server specific - need to query separately)
		var isIdentity = false;
		if (row.Table.Columns.Contains("AUTOINCREMENT") && !row.IsNull("AUTOINCREMENT"))
		{
			isIdentity = Convert.ToBoolean(row["AUTOINCREMENT"]);
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
			var foreignKeysSchema = await Task.Run(() => connection.GetSchema("ForeignKeys", restrictions), cancellationToken);

			foreach (DataRow row in foreignKeysSchema.Rows)
			{
				var constraintName = row["CONSTRAINT_NAME"]?.ToString();
				var columnName = row["FKEY_FROM_COLUMN"]?.ToString();
				var referencedSchema = row["FKEY_TO_SCHEMA"]?.ToString();
				var referencedTable = row["FKEY_TO_TABLE"]?.ToString();
				var referencedColumn = row["FKEY_TO_COLUMN"]?.ToString();

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
			// If ForeignKeys schema collection is not supported, return empty list
		}

		return foreignKeys;
	}

	/// <inheritdoc/>
	protected override string? NormalizeSchemaName(string? schema)
	{
		return schema ?? "dbo";
	}

	/// <inheritdoc/>
	protected override string?[] CreateColumnRestrictions(string? schema, string tableName)
	{
		// Catalog, Schema, Table, Column
		return new string?[] { null, schema, tableName, null };
	}

	/// <inheritdoc/>
	protected override string?[] CreateIndexRestrictions(string? schema, string tableName)
	{
		// Catalog, Schema, Table, Constraint, Column
		return new string?[] { null, schema, tableName, null, null };
	}
}

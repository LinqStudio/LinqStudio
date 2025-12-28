using LinqStudio.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases.SQLite;

/// <summary>
/// Database generator for SQLite using ADO.NET.
/// </summary>
public class SqliteGenerator : AdoNetDatabaseGeneratorBase
{
	/// <summary>
	/// Creates a new instance of the SQLite generator.
	/// </summary>
	/// <param name="database">EF Core database facade.</param>
	public SqliteGenerator(DatabaseFacade database) : base(database)
	{
	}

	/// <inheritdoc/>
	protected override DatabaseTableName? ParseTableFromSchemaRow(DataRow row)
	{
		// This method is not used by SQLite since we override GetTablesAsync
		throw new NotImplementedException("SQLite uses a custom implementation for GetTablesAsync");
	}

	/// <inheritdoc/>
	public override async Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken cancellationToken = default)
	{
		var tables = new List<DatabaseTableName>();
		var connection = Database.GetDbConnection();

		var wasOpen = connection.State == ConnectionState.Open;
		if (!wasOpen)
			await connection.OpenAsync(cancellationToken);

		try
		{
			// SQLite: query sqlite_master for tables
			const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

			await using var command = connection.CreateCommand();
			command.CommandText = query;

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var tableName = reader.GetString(0);
				tables.Add(new DatabaseTableName
				{
					Schema = "main",
					Name = tableName
				});
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
	public override async Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		var (schema, name) = ParseTableName(tableName);
		schema ??= "main"; // Default schema for SQLite

		var connection = Database.GetDbConnection();

		var wasOpen = connection.State == ConnectionState.Open;
		if (!wasOpen)
			await connection.OpenAsync(cancellationToken);

		try
		{
			// Get columns using database-specific query
			var columns = await GetColumnsAsync(connection, name, cancellationToken);

			// Get foreign keys using database-specific query
			var foreignKeys = await GetForeignKeysAsync(connection, name, cancellationToken);

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

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// SQLite: use PRAGMA table_info to get columns
		var query = $"PRAGMA table_info({tableName})";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			// SQLite PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
			var columnName = reader.GetString(1);
			var dataType = reader.GetString(2);
			var notNull = reader.GetInt32(3) == 1;
			var isPrimaryKey = reader.GetInt32(5) > 0;

			// Parse max length from type if present (e.g., VARCHAR(100))
			int? maxLength = null;
			int? precision = null;
			int? scale = null;

			if (dataType.Contains('('))
			{
				var startIdx = dataType.IndexOf('(');
				var endIdx = dataType.IndexOf(')');
				if (startIdx > 0 && endIdx > startIdx)
				{
					var sizeStr = dataType.Substring(startIdx + 1, endIdx - startIdx - 1);
					if (sizeStr.Contains(','))
					{
						// For DECIMAL(p,s) type
						var parts = sizeStr.Split(',');
						if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var p) && int.TryParse(parts[1].Trim(), out var s))
						{
							precision = p;
							scale = s;
						}
					}
					else if (int.TryParse(sizeStr, out var size))
					{
						maxLength = size;
					}
				}
			}

			// Determine if column is autoincrement (identity)
			var isIdentity = false;
			if (isPrimaryKey)
			{
				// Check if this is an INTEGER PRIMARY KEY with AUTOINCREMENT
				// This requires checking the table's CREATE statement
				isIdentity = await IsAutoIncrementColumnAsync(connection, tableName, columnName, cancellationToken);
			}

			columns.Add(new TableColumn
			{
				Name = columnName,
				DataType = dataType,
				IsNullable = !notNull,
				IsPrimaryKey = isPrimaryKey,
				IsIdentity = isIdentity,
				MaxLength = maxLength,
				Precision = precision,
				Scale = scale
			});
		}

		return columns;
	}

	private async Task<bool> IsAutoIncrementColumnAsync(DbConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
	{
		// Check if the column is an INTEGER PRIMARY KEY (which is auto-increment in SQLite)
		// or explicitly has AUTOINCREMENT
		var query = $"SELECT sql FROM sqlite_master WHERE type='table' AND name=@TableName";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

		var param = command.CreateParameter();
		param.ParameterName = "@TableName";
		param.Value = tableName;
		command.Parameters.Add(param);

		var createSql = await command.ExecuteScalarAsync(cancellationToken) as string;
		if (createSql != null)
		{
			// Check if the column is INTEGER PRIMARY KEY (implicit autoincrement)
			// or has explicit AUTOINCREMENT keyword
			var upperSql = createSql.ToUpperInvariant();
			var upperColumnName = columnName.ToUpperInvariant();

			// Look for patterns like: "Id" INTEGER PRIMARY KEY or "Id" INTEGER PRIMARY KEY AUTOINCREMENT
			return upperSql.Contains($"{upperColumnName}\" INTEGER PRIMARY KEY") ||
				   upperSql.Contains($"{upperColumnName} INTEGER PRIMARY KEY") ||
				   upperSql.Contains($"\"{upperColumnName}\" INTEGER") && upperSql.Contains("PRIMARY KEY") && upperSql.Contains("AUTOINCREMENT");
		}

		return false;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

		// SQLite: use PRAGMA foreign_key_list to get foreign keys
		var query = $"PRAGMA foreign_key_list({tableName})";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			// SQLite PRAGMA foreign_key_list returns: id, seq, table, from, to, on_update, on_delete, match
			var fkId = reader.GetInt32(0);
			var referencedTable = reader.GetString(2);
			var columnName = reader.GetString(3);
			var referencedColumn = reader.GetString(4);

			foreignKeys.Add(new ForeignKey
			{
				Name = $"FK_{tableName}_{referencedTable}_{fkId}",
				ColumnName = columnName,
				ReferencedTable = $"main.{referencedTable}",
				ReferencedColumn = referencedColumn
			});
		}

		return foreignKeys;
	}
}

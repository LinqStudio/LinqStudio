using LinqStudio.Abstractions.Models;
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
	/// <param name="connection">Database connection.</param>
	public SqliteGenerator(DbConnection connection) : base(connection)
	{
	}

	/// <inheritdoc/>
	public override DbColumnType MapToGenericType(string dataType)
	{
		var type = dataType.ToLowerInvariant();

		// Remove size specifications like VARCHAR(100) or DECIMAL(10,2)
		var parenIndex = type.IndexOf('(');
		if (parenIndex > 0)
		{
			type = type[..parenIndex];
		}

		// SQLite type affinity rules - check contains rather than exact match
		// INTEGER affinity
		if (type.Contains("int") || type.Contains("integer"))
		{
			// Check for specific size hints
			if (type.Contains("tiny"))
				return DbColumnType.SByte;
			if (type.Contains("small"))
				return DbColumnType.Int16;
			if (type.Contains("big"))
				return DbColumnType.Int64;

			return DbColumnType.Int32;
		}

		// TEXT affinity
		if (type.Contains("char") || type.Contains("clob") || type.Contains("text") || type.Contains("string"))
		{
			return DbColumnType.String;
		}

		// BLOB affinity
		if (type.Contains("blob"))
		{
			return DbColumnType.Binary;
		}

		// REAL affinity
		if (type.Contains("real") || type.Contains("floa") || type.Contains("doub"))
		{
			if (type.Contains("float"))
				return DbColumnType.Float;

			return DbColumnType.Double;
		}

		// NUMERIC affinity (could be decimal or datetime)
		if (type.Contains("numeric") || type.Contains("decimal") || type.Contains("money"))
		{
			return DbColumnType.Decimal;
		}

		// Date/Time types (stored as TEXT, REAL, or INTEGER)
		if (type.Contains("date") || type.Contains("time"))
		{
			// TIMESTAMP should be DateTime, not TimeSpan
			if (type.Contains("stamp"))
				return DbColumnType.DateTime;
			
			// Pure time (not datetime) is TimeSpan
			if (type.Contains("time") && !type.Contains("date"))
				return DbColumnType.TimeSpan;

			return DbColumnType.DateTime;
		}

		// Boolean (stored as INTEGER 0/1)
		if (type.Contains("bool"))
		{
			return DbColumnType.Boolean;
		}

		// GUID (stored as TEXT or BLOB)
		if (type.Contains("guid") || type.Contains("uuid"))
		{
			return DbColumnType.Guid;
		}

		// Exact matches for common SQLite types
		return type switch
		{
			"integer" => DbColumnType.Int32,
			"text" => DbColumnType.String,
			"real" => DbColumnType.Double,
			"blob" => DbColumnType.Binary,
			"numeric" => DbColumnType.Decimal,
			_ => DbColumnType.Unknown
		};
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

		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			// SQLite: query sqlite_master for tables
			const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

			await using var command = Connection.CreateCommand();
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
				await Connection.CloseAsync();
		}

		return tables;
	}

	/// <inheritdoc/>
	public override async Task<DatabaseTableDetail> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		var (schema, name) = ParseTableName(tableName);
		schema ??= "main"; // Default schema for SQLite

		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			// Get columns using database-specific query
			var columns = await GetColumnsAsync(Connection, name, cancellationToken);

			// Get foreign keys using database-specific query
			var foreignKeys = await GetForeignKeysAsync(Connection, name, cancellationToken);

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
				await Connection.CloseAsync();
		}
	}

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// SQLite: use PRAGMA table_info to get columns
		// Note: PRAGMA commands don't support parameterized table names
		// Sanitize table name by ensuring it only contains valid identifier characters
		var sanitizedTableName = SanitizeIdentifier(tableName);
		var query = $"PRAGMA table_info({sanitizedTableName})";

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
				GenericType = MapToGenericType(dataType),
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
			var pattern1 = upperSql.Contains($"{upperColumnName}\" INTEGER PRIMARY KEY");
			var pattern2 = upperSql.Contains($"{upperColumnName} INTEGER PRIMARY KEY");
			var pattern3 = upperSql.Contains($"\"{upperColumnName}\" INTEGER") && 
			                upperSql.Contains("PRIMARY KEY") && 
			                upperSql.Contains("AUTOINCREMENT");
			
			return pattern1 || pattern2 || pattern3;
		}

		return false;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

		// SQLite: use PRAGMA foreign_key_list to get foreign keys
		// Note: PRAGMA commands don't support parameterized table names
		// Sanitize table name by ensuring it only contains valid identifier characters
		var sanitizedTableName = SanitizeIdentifier(tableName);
		var query = $"PRAGMA foreign_key_list({sanitizedTableName})";

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
				Name = $"FK_{sanitizedTableName}_{referencedTable}_{fkId}",
				ColumnName = columnName,
				ReferencedTable = $"main.{referencedTable}",
				ReferencedColumn = referencedColumn
			});
		}

		return foreignKeys;
	}

	/// <summary>
	/// Sanitizes a table name to prevent SQL injection in PRAGMA commands.
	/// SQLite PRAGMA commands don't support parameterized table names.
	/// </summary>
	private static string SanitizeIdentifier(string identifier)
	{
		// Remove any characters that aren't alphanumeric or underscore
		// This is a conservative approach that prevents SQL injection
		var sanitized = new string(identifier.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
		
		if (string.IsNullOrEmpty(sanitized))
			throw new ArgumentException($"Invalid table name: {identifier}", nameof(identifier));
		
		return sanitized;
	}
}

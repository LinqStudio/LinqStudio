using LinqStudio.Abstractions.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases;

/// <summary>
/// Database generator for Microsoft SQL Server using ADO.NET.
/// </summary>
public class MssqlGenerator : AdoNetDatabaseGeneratorBase
{
	/// <summary>
	/// Creates a new instance of the MSSQL generator.
	/// </summary>
	/// <param name="connection">Database connection.</param>
	public MssqlGenerator(DbConnection connection) : base(connection)
	{
	}

	/// <summary>
	/// Creates a new MSSQL generator from a connection string.
	/// The connection string must explicitly specify a target database.
	/// </summary>
	/// <param name="connectionString">SQL Server connection string.</param>
	/// <returns>A new MSSQL generator instance.</returns>
	public static MssqlGenerator Create(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

		var builder = new SqlConnectionStringBuilder(connectionString);
		if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
			throw new ArgumentException(
				"Connection string must specify a target database (e.g., 'Database=MyDb;' or 'Initial Catalog=MyDb;'). " +
				"Omitting the database causes unpredictable behavior when the server hosts multiple databases.",
				nameof(connectionString));

		return new(new SqlConnection(connectionString));
	}


	/// <inheritdoc/>
	public override DbColumnType MapToGenericType(string dataType)
	{
		var type = dataType.ToLowerInvariant();

		return type switch
		{
			// Boolean
			"bit" => DbColumnType.Boolean,

			// Integer types
			"tinyint" => DbColumnType.SByte,
			"smallint" => DbColumnType.Int16,
			"int" => DbColumnType.Int32,
			"bigint" => DbColumnType.Int64,

			// Floating point
			"real" => DbColumnType.Float,
			"float" => DbColumnType.Double,

			// Decimal/Money
			"decimal" or "numeric" or "money" or "smallmoney" => DbColumnType.Decimal,

			// String types
			"char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" => DbColumnType.String,

			// Date/Time types
			"date" or "datetime" or "datetime2" or "smalldatetime" => DbColumnType.DateTime,
			"time" => DbColumnType.TimeSpan,
			"datetimeoffset" => DbColumnType.DateTimeOffset,

			// GUID
			"uniqueidentifier" => DbColumnType.Guid,

			// Binary
			"binary" or "varbinary" or "image" or "timestamp" or "rowversion" => DbColumnType.Binary,

			// XML
			"xml" => DbColumnType.Xml,

			// Geographic/Geometry (treat as binary)
			"geography" or "geometry" => DbColumnType.Binary,

			// Hierarchyid (treat as binary)
			"hierarchyid" => DbColumnType.Binary,

			// sql_variant (unknown)
			"sql_variant" => DbColumnType.Unknown,

			// Default
			_ => DbColumnType.Unknown
		};
	}

	/// <inheritdoc/>
	public override async Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(CancellationToken cancellationToken = default)
	{
		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			// This dynamic SQL iterates through all online databases the user has access to,
			// excluding system databases (master, tempdb, model, msdb).
			// It builds a single massive UNION ALL query to fetch all tables.
			const string query = """
            DECLARE @sql NVARCHAR(MAX) = N'';

            SELECT @sql += N'SELECT ' +
                           N'''' + REPLACE(name, '''', '''''') + N''' AS DatabaseName, ' +
                           N's.name COLLATE DATABASE_DEFAULT AS SchemaName, ' +
                           N't.name COLLATE DATABASE_DEFAULT AS TableName ' +
                           N'FROM ' + QUOTENAME(name) + N'.sys.tables t ' +
                           N'INNER JOIN ' + QUOTENAME(name) + N'.sys.schemas s ON t.schema_id = s.schema_id ' +
                           N'WHERE t.is_ms_shipped = 0 ' +
                           N'UNION ALL '
            FROM sys.databases
            WHERE state = 0 AND HAS_DBACCESS(name) = 1
              AND name NOT IN ('master', 'tempdb', 'model', 'msdb');

            IF LEN(@sql) > 0
            BEGIN
                -- Strip off the trailing 'UNION ALL ' (10 characters) and execute
                SET @sql = LEFT(@sql, LEN(@sql) - 10);
                EXEC sp_executesql @sql;
            END
            ELSE
            BEGIN
                -- Return an empty result set if no databases matched to prevent reader errors
                SELECT '' AS DatabaseName, '' AS SchemaName, '' AS TableName WHERE 1 = 0;
            END
            """;

			var tables = new List<DatabaseTableName>();

			await using var command = Connection.CreateCommand();
			command.CommandText = query;
			command.CommandTimeout = 30;

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				// Column 0 is DatabaseName, Column 1 is SchemaName, Column 2 is TableName
				tables.Add(new DatabaseTableName
				{
					// If your DatabaseTableName class has a property for Database, you can map it here:
					// Database = reader.GetString(0),
					Schema = reader.GetString(1),
					Name = reader.GetString(2)
				});
			}

			return tables;
		}
		finally
		{
			if (!wasOpen)
				await Connection.CloseAsync();
		}
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
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));

		var (schema, name) = ParseTableName(tableName);
		schema ??= "dbo"; // Default schema for SQL Server

		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			var columns = await GetColumnsAsync(Connection, schema, name, cancellationToken);
			var foreignKeys = await GetForeignKeysAsync(Connection, schema, name, cancellationToken);

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

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// Use GetSchema for columns as it's database-independent
		var restrictions = new string?[] { null, schema, tableName, null };
		var columnsSchema = await connection.GetSchemaAsync("Columns", restrictions, cancellationToken);

		// Get primary key information from Indexes schema
		var primaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var indexesSchema = await connection.GetSchemaAsync("IndexColumns", restrictions, cancellationToken);
		foreach (DataRow row in indexesSchema.Rows)
		{
			var columnName = row["column_name"]?.ToString();
			if (!string.IsNullOrEmpty(columnName))
				primaryKeys.Add(columnName);
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
				GenericType = MapToGenericType(dataType),
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
		command.CommandTimeout = 30;

		var parameter = command.CreateParameter();
		parameter.ParameterName = "@TableName";
		parameter.Value = $"{schema}.{tableName}";
		command.Parameters.Add(parameter);

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

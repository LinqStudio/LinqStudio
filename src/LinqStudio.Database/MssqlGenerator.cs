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
	private readonly string _connectionString;
	private readonly string? _explicitDatabaseName;

	/// <summary>
	/// Creates a new instance of the MSSQL generator.
	/// </summary>
	/// <param name="connection">Database connection.</param>
	public MssqlGenerator(DbConnection connection) : base(connection)
	{
		_connectionString = connection.ConnectionString;
		var builder = new SqlConnectionStringBuilder(_connectionString);
		_explicitDatabaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog)
			|| builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase)
			? null
			: builder.InitialCatalog;
	}

	/// <summary>
	/// Creates a new MSSQL generator from a connection string.
	/// The database is optional; when omitted, databases can be enumerated with
	/// <see cref="GetDatabasesAsync"/>.
	/// </summary>
	/// <param name="connectionString">SQL Server connection string.</param>
	/// <returns>A new MSSQL generator instance.</returns>
	public static MssqlGenerator Create(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

		return new(new SqlConnection(connectionString));
	}

	public override async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(CancellationToken cancellationToken = default)
	{
		if (_explicitDatabaseName is not null)
			return [new DatabaseInfo { Name = _explicitDatabaseName, IsExplicitlySelected = true }];

		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			const string query = """
				SELECT name
				FROM sys.databases
				WHERE state = 0
					AND name NOT IN ('master', 'model', 'msdb', 'tempdb')
					AND HAS_DBACCESS(name) = 1
				ORDER BY name
				""";
			await using var command = Connection.CreateCommand();
			command.CommandText = query;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			var databases = new List<DatabaseInfo>();
			while (await reader.ReadAsync(cancellationToken))
				databases.Add(new DatabaseInfo { Name = reader.GetString(0) });
			return databases;
		}
		finally
		{
			if (!wasOpen)
				await Connection.CloseAsync();
		}
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
			"date" => DbColumnType.DateOnly,
			"datetime" or "datetime2" or "smalldatetime" => DbColumnType.DateTime,
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
			const string query = """
			SELECT DB_NAME() AS DatabaseName, s.name AS SchemaName, t.name AS TableName
			FROM sys.tables t
			INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
			WHERE t.is_ms_shipped = 0
			ORDER BY s.name, t.name
			""";

			var tables = new List<DatabaseTableName>();

			await using var command = Connection.CreateCommand();
			command.CommandText = query;
			command.CommandTimeout = 30;

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				tables.Add(new DatabaseTableName
				{
					DatabaseName = reader.GetString(0),
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

	public override async Task<IReadOnlyList<DatabaseTableName>> GetTablesAsync(
		string databaseName,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName, nameof(databaseName));
		if (string.Equals(Connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
			return await GetTablesAsync(cancellationToken);

		await using var connection = CreateDatabaseConnection(databaseName);
		await connection.OpenAsync(cancellationToken);
		return await GetTablesFromConnectionAsync(connection, cancellationToken);
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
			DatabaseName = Connection.Database,
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
				DatabaseName = Connection.Database,
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

	public override async Task<DatabaseTableDetail> GetTableAsync(
			DatabaseTableName table,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(table);
			if (string.IsNullOrWhiteSpace(table.DatabaseName)
				|| string.Equals(Connection.Database, table.DatabaseName, StringComparison.OrdinalIgnoreCase))
				return await GetTableAsync(table.FullName, cancellationToken);

			await using var connection = CreateDatabaseConnection(table.DatabaseName);
			await connection.OpenAsync(cancellationToken);
			var schema = table.Schema ?? "dbo";
			return new DatabaseTableDetail
			{
				DatabaseName = table.DatabaseName,
				Schema = schema,
				Name = table.Name,
				Columns = await GetColumnsAsync(connection, schema, table.Name, cancellationToken),
				ForeignKeys = await GetForeignKeysAsync(connection, schema, table.Name, cancellationToken)
			};
		}

		private async Task<IReadOnlyList<DatabaseTableName>> GetTablesFromConnectionAsync(
			DbConnection connection,
			CancellationToken cancellationToken)
		{
			const string query = """
				SELECT DB_NAME() AS DatabaseName, s.name AS SchemaName, t.name AS TableName
				FROM sys.tables t
				INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
				WHERE t.is_ms_shipped = 0
				ORDER BY s.name, t.name
				""";
			await using var command = connection.CreateCommand();
			command.CommandText = query;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			var tables = new List<DatabaseTableName>();
			while (await reader.ReadAsync(cancellationToken))
				tables.Add(new DatabaseTableName
				{
					DatabaseName = reader.GetString(0),
					Schema = reader.GetString(1),
					Name = reader.GetString(2)
				});
			return tables;
		}

		private SqlConnection CreateDatabaseConnection(string databaseName)
		{
			var builder = new SqlConnectionStringBuilder(_connectionString)
			{
				InitialCatalog = databaseName
			};
			return new SqlConnection(builder.ConnectionString);
		}
	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// Use GetSchema for columns as it's database-independent
		var restrictions = new string?[] { null, schema, tableName, null };
		var columnsSchema = await connection.GetSchemaAsync("Columns", restrictions, cancellationToken);

		// Get primary key information using a direct SQL query
		// IndexColumns returns ALL indexed columns (not just PK), so we query sys.indexes directly
		var primaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		const string pkQuery = """
			SELECT 
				c.name AS column_name,
				ic.key_ordinal AS key_ordinal
			FROM sys.indexes i
			INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
			INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
			WHERE i.is_primary_key = 1 
			  AND i.object_id = OBJECT_ID(@tableName)
			ORDER BY ic.key_ordinal
			""";

		await using (var pkCommand = connection.CreateCommand())
		{
			pkCommand.CommandText = pkQuery;
			pkCommand.CommandTimeout = 30;

			var pkParameter = pkCommand.CreateParameter();
			pkParameter.ParameterName = "@tableName";
			pkParameter.Value = $"{schema}.{tableName}";
			pkCommand.Parameters.Add(pkParameter);

			await using var pkReader = await pkCommand.ExecuteReaderAsync(cancellationToken);
			while (await pkReader.ReadAsync(cancellationToken))
			{
				var columnName = pkReader.GetString(0);
				if (!string.IsNullOrEmpty(columnName))
					primaryKeys.Add(columnName);
			}
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

using LinqStudio.Abstractions.Models;
using System.Data;
using System.Data.Common;

namespace LinqStudio.Databases.PostgreSQL;

/// <summary>
/// Database generator for PostgreSQL using ADO.NET.
/// </summary>
public class PostgreSqlGenerator : AdoNetDatabaseGeneratorBase
{
	private readonly string _connectionString;
	private readonly string? _explicitDatabaseName;

	/// <summary>
	/// Creates a new instance of the PostgreSQL generator.
	/// </summary>
	/// <param name="connection">Database connection.</param>
	public PostgreSqlGenerator(DbConnection connection) : base(connection)
	{
		_connectionString = connection.ConnectionString;
		var builder = new Npgsql.NpgsqlConnectionStringBuilder(_connectionString);
		_explicitDatabaseName = string.IsNullOrWhiteSpace(builder.Database) ? null : builder.Database;
	}

	public static PostgreSqlGenerator Create(string connectionString) => new(new Npgsql.NpgsqlConnection(connectionString));

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
				SELECT datname
				FROM pg_database
				WHERE datallowconn = true
					AND has_database_privilege(current_user, datname, 'CONNECT')
				ORDER BY datname
				""";
			await using var command = Connection.CreateCommand();
			command.CommandText = query;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			var result = new List<DatabaseInfo>();
			while (await reader.ReadAsync(cancellationToken))
				result.Add(new DatabaseInfo
				{
					Name = reader.GetString(0),
					IsExplicitlySelected = string.Equals(reader.GetString(0), Connection.Database, StringComparison.OrdinalIgnoreCase)
				});
			return result;
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
			if (string.Equals(databaseName, Connection.Database, StringComparison.OrdinalIgnoreCase))
				return await GetTablesAsync(cancellationToken);

			await using var connection = CreateDatabaseConnection(databaseName);
			await connection.OpenAsync(cancellationToken);
			return await GetTablesFromConnectionAsync(connection, cancellationToken);
		}

	/// <inheritdoc/>
	public override DbColumnType MapToGenericType(string dataType)
	{
		var type = dataType.ToLowerInvariant();

		return type switch
		{
			// Boolean
			"boolean" or "bool" => DbColumnType.Boolean,

			// Integer types
			"smallint" or "int2" or "smallserial" or "serial2" => DbColumnType.Int16,
			"integer" or "int" or "int4" or "serial" or "serial4" => DbColumnType.Int32,
			"bigint" or "int8" or "bigserial" or "serial8" => DbColumnType.Int64,

			// Floating point
			"real" or "float4" => DbColumnType.Float,
			"double precision" or "float8" => DbColumnType.Double,

			// Decimal/Money
			"numeric" or "decimal" or "money" => DbColumnType.Decimal,

			// String types
			"character varying" or "varchar" or "character" or "char" or "text" or "name" => DbColumnType.String,

			// Date/Time types
			"date" => DbColumnType.DateOnly,
			"timestamp" or "timestamp without time zone" => DbColumnType.DateTime,
			"timestamp with time zone" or "timestamptz" => DbColumnType.DateTimeOffset,
			"time" or "time without time zone" or "interval" => DbColumnType.TimeSpan,

			// UUID
			"uuid" => DbColumnType.Guid,

			// Binary
			"bytea" => DbColumnType.Binary,

			// XML
			"xml" => DbColumnType.Xml,

			// JSON
			"json" or "jsonb" => DbColumnType.Json,

			// Network address types (treat as string)
			"inet" or "cidr" or "macaddr" or "macaddr8" => DbColumnType.String,

			// Bit strings (treat as binary)
			"bit" or "bit varying" => DbColumnType.Binary,

			// Text search types (treat as string)
			"tsvector" or "tsquery" => DbColumnType.String,

			// Geometric types (treat as binary)
			"point" or "line" or "lseg" or "box" or "path" or "polygon" or "circle" => DbColumnType.Binary,

			// Range types (treat as string)
			"int4range" or "int8range" or "numrange" or "tsrange" or "tstzrange" or "daterange" => DbColumnType.String,

			// Array types (detect by [])
			_ when type.EndsWith("[]") => DbColumnType.Unknown,

			// User-defined types
			"user-defined" => DbColumnType.Unknown,

			// Default
			_ => DbColumnType.Unknown
		};
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
		schema ??= "public"; // Default schema for PostgreSQL

		var wasOpen = Connection.State == ConnectionState.Open;
		if (!wasOpen)
			await Connection.OpenAsync(cancellationToken);

		try
		{
			// Get columns using database-specific query
			var columns = await GetColumnsAsync(Connection, schema, name, cancellationToken);

			// Get foreign keys using database-specific query
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
			|| string.Equals(table.DatabaseName, Connection.Database, StringComparison.OrdinalIgnoreCase))
			return await GetTableAsync(table.FullName, cancellationToken);

		await using var connection = CreateDatabaseConnection(table.DatabaseName);
		await connection.OpenAsync(cancellationToken);
		var schema = table.Schema ?? "public";
		return new DatabaseTableDetail
		{
			DatabaseName = table.DatabaseName,
			Schema = schema,
			Name = table.Name,
			Columns = await GetColumnsAsync(connection, schema, table.Name, cancellationToken),
			ForeignKeys = await GetForeignKeysAsync(connection, schema, table.Name, cancellationToken)
		};
	}

	private Npgsql.NpgsqlConnection CreateDatabaseConnection(string databaseName)
	{
		var builder = new Npgsql.NpgsqlConnectionStringBuilder(_connectionString)
		{
			Database = databaseName
		};
		return new Npgsql.NpgsqlConnection(builder.ConnectionString);
	}

	private static async Task<IReadOnlyList<DatabaseTableName>> GetTablesFromConnectionAsync(
		DbConnection connection,
		CancellationToken cancellationToken)
	{
		const string query = """
			SELECT table_catalog, table_schema, table_name
			FROM information_schema.tables
			WHERE table_type = 'BASE TABLE'
				AND table_schema NOT IN ('pg_catalog', 'information_schema')
			ORDER BY table_schema, table_name
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

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string? schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

		// PostgreSQL: use INFORMATION_SCHEMA query for columns
		const string query = """
			SELECT 
				c.column_name,
				c.data_type,
				c.is_nullable,
				c.character_maximum_length,
				c.numeric_precision,
				c.numeric_scale,
				CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_primary_key,
				CASE WHEN c.column_default LIKE 'nextval%' THEN 'YES' ELSE 'NO' END AS is_identity
			FROM information_schema.columns c
			LEFT JOIN (
				SELECT ku.column_name
				FROM information_schema.table_constraints tc
				JOIN information_schema.key_column_usage ku
					ON tc.constraint_name = ku.constraint_name
					AND tc.table_schema = ku.table_schema
					AND tc.table_name = ku.table_name
				WHERE tc.constraint_type = 'PRIMARY KEY'
					AND tc.table_schema = @Schema
					AND tc.table_name = @TableName
			) pk ON c.column_name = pk.column_name
			WHERE c.table_schema = @Schema
				AND c.table_name = @TableName
			ORDER BY c.ordinal_position
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;
		command.CommandTimeout = 30;

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
			var isPrimaryKey = reader.GetString(6) == "YES";
			var isIdentity = reader.GetString(7) == "YES";

			// Parse max length safely
			int? maxLength = null;
			if (!reader.IsDBNull(3))
			{
				var value = reader.GetValue(3);
				if (int.TryParse(value.ToString(), out var intValue))
				{
					maxLength = intValue;
				}
			}

			// Parse precision and scale safely
			int? precision = null;
			if (!reader.IsDBNull(4))
			{
				var value = reader.GetValue(4);
				if (int.TryParse(value.ToString(), out var intValue))
				{
					precision = intValue;
				}
			}

			int? scale = null;
			if (!reader.IsDBNull(5))
			{
				var value = reader.GetValue(5);
				if (int.TryParse(value.ToString(), out var intValue))
				{
					scale = intValue;
				}
			}

			columns.Add(new TableColumn
			{
				Name = reader.GetString(0),
				DataType = reader.GetString(1),
				GenericType = MapToGenericType(reader.GetString(1)),
				IsNullable = reader.GetString(2) == "YES",
				IsPrimaryKey = isPrimaryKey,
				IsIdentity = isIdentity,
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

		// PostgreSQL: use INFORMATION_SCHEMA query for foreign keys
		const string query = """
			SELECT 
				tc.constraint_name,
				kcu.column_name,
				CONCAT(ccu.table_schema, '.', ccu.table_name) AS referenced_table,
				ccu.column_name AS referenced_column
			FROM information_schema.table_constraints tc
			JOIN information_schema.key_column_usage kcu
				ON tc.constraint_name = kcu.constraint_name
				AND tc.table_schema = kcu.table_schema
			JOIN information_schema.constraint_column_usage ccu
				ON tc.constraint_name = ccu.constraint_name
				AND tc.table_schema = ccu.table_schema
			WHERE tc.constraint_type = 'FOREIGN KEY'
				AND tc.table_schema = @Schema
				AND tc.table_name = @TableName
			ORDER BY tc.constraint_name, kcu.ordinal_position
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;
		command.CommandTimeout = 30;

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

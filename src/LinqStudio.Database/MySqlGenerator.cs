using LinqStudio.Abstractions.Models;
using MySql.Data.MySqlClient;
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
	/// <param name="connectionString">MySQL connection string.</param>
	public MySqlGenerator(string connectionString) : base(connectionString)
	{
	}

	/// <inheritdoc/>
	protected override DbConnection CreateConnection() => new MySqlConnection(ConnectionString);

	/// <inheritdoc/>
	public override async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(CancellationToken cancellationToken = default)
	{
		var tables = new List<DatabaseTable>();

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		// Get the current database name
		var database = connection.Database;

		// Get all tables from information_schema
		const string query = """
			SELECT 
				TABLE_SCHEMA,
				TABLE_NAME
			FROM INFORMATION_SCHEMA.TABLES
			WHERE TABLE_SCHEMA = @Database
				AND TABLE_TYPE = 'BASE TABLE'
			ORDER BY TABLE_SCHEMA, TABLE_NAME
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;
		
		var parameter = command.CreateParameter();
		parameter.ParameterName = "@Database";
		parameter.Value = database;
		command.Parameters.Add(parameter);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			tables.Add(new DatabaseTable
			{
				Schema = reader.GetString(0),
				Name = reader.GetString(1)
			});
		}

		return tables;
	}

	/// <inheritdoc/>
	public override async Task<DatabaseTable> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
	{
		var (schema, name) = ParseTableName(tableName);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		schema ??= connection.Database; // Default to current database

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

	private async Task<IReadOnlyList<TableColumn>> GetColumnsAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var columns = new List<TableColumn>();

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
		schemaParam.Value = schema;
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
			
			columns.Add(new TableColumn
			{
				Name = reader.GetString(0),
				DataType = reader.GetString(1),
				IsNullable = reader.GetString(2) == "YES",
				IsPrimaryKey = columnKey == "PRI",
				IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
				MaxLength = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
				Precision = reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6)),
				Scale = reader.IsDBNull(7) ? null : Convert.ToInt32(reader.GetValue(7))
			});
		}

		return columns;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

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
		schemaParam.Value = schema;
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

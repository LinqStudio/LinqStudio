using LinqStudio.Abstractions.Models;
using Microsoft.Data.SqlClient;
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
	/// <param name="connectionString">SQL Server connection string.</param>
	public MssqlGenerator(string connectionString) : base(connectionString)
	{
	}

	/// <inheritdoc/>
	protected override DbConnection CreateConnection() => new SqlConnection(ConnectionString);

	/// <inheritdoc/>
	public override async Task<IReadOnlyList<DatabaseTable>> GetTablesAsync(CancellationToken cancellationToken = default)
	{
		var tables = new List<DatabaseTable>();

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		// Get all user tables from sys.tables
		const string query = """
			SELECT 
				s.name AS SchemaName,
				t.name AS TableName
			FROM sys.tables t
			INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
			ORDER BY s.name, t.name
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;

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
		schema ??= "dbo"; // Default schema for SQL Server

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

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
				c.name AS ColumnName,
				t.name AS DataType,
				c.is_nullable AS IsNullable,
				c.is_identity AS IsIdentity,
				c.max_length AS MaxLength,
				c.precision AS Precision,
				c.scale AS Scale,
				CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
			FROM sys.columns c
			INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
			LEFT JOIN (
				SELECT ic.object_id, ic.column_id
				FROM sys.index_columns ic
				INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
				WHERE i.is_primary_key = 1
			) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
			WHERE c.object_id = OBJECT_ID(@TableName)
			ORDER BY c.column_id
			""";

		await using var command = connection.CreateCommand();
		command.CommandText = query;
		
		var parameter = command.CreateParameter();
		parameter.ParameterName = "@TableName";
		parameter.Value = $"{schema}.{tableName}";
		command.Parameters.Add(parameter);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var dataType = reader.GetString(1);
			var maxLength = reader.GetInt16(4);
			
			columns.Add(new TableColumn
			{
				Name = reader.GetString(0),
				DataType = dataType,
				IsNullable = reader.GetBoolean(2),
				IsIdentity = reader.GetBoolean(3),
				IsPrimaryKey = reader.GetInt32(7) == 1,
				MaxLength = (dataType == "nvarchar" || dataType == "varchar" || dataType == "nchar" || dataType == "char") && maxLength > 0 ? maxLength : null,
				Precision = reader.GetByte(5) > 0 ? reader.GetByte(5) : null,
				Scale = reader.GetByte(6) > 0 ? reader.GetByte(6) : null
			});
		}

		return columns;
	}

	private async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(DbConnection connection, string schema, string tableName, CancellationToken cancellationToken)
	{
		var foreignKeys = new List<ForeignKey>();

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


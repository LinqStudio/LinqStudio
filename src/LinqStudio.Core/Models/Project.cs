using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.SQLite;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Models;

public class Project
{
	public int SchemaVersion { get; set; } = ProjectConstants.CurrentSchemaVersion;
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;

	public DatabaseType DatabaseType
	{
		get;
		set
		{
			field = value;
			QueryGenerator = null; // Reset the query generator so it will be recreated with the new database type when accessed
		}
	} = DatabaseType.Mssql;

	public string? ConnectionString
	{
		get;
		set
		{
			field = value;
			QueryGenerator = null; // Reset the query generator so it will be recreated with the new connection string when accessed
		}
	}

	public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset ModifiedDate { get; set; }

	// Future properties
	public Dictionary<string, string>? Models { get; set; }
	public string? DbContextCode { get; set; }


	#region Connection String handling

	/// <summary>
	/// Query generator used to fetch information about the DB such as list of tables.
	/// </summary>
	[JsonIgnore]
	public IDatabaseQueryGenerator? QueryGenerator
	{
		get
		{
			if (field != null)
			{
				return field;
			}

			if (!string.IsNullOrEmpty(ConnectionString))
			{
				field = DatabaseType switch
				{
					DatabaseType.Mssql => MssqlGenerator.Create(ConnectionString),
					DatabaseType.MySql => MySqlGenerator.Create(ConnectionString),
					DatabaseType.PostgreSql => PostgreSqlGenerator.Create(ConnectionString),
					DatabaseType.Sqlite => SqliteGenerator.Create(ConnectionString),
					_ => throw new NotSupportedException($"Database type {DatabaseType} is not supported.")
				};

				return field;
			}

			return null;
		}
		private set;
	}

	public void UpdateConnection(DatabaseType databaseType, string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

		DatabaseType = databaseType;
		ConnectionString = connectionString;
	}

	/// <summary>
	/// Tests the database connection with the specified timeout.
	/// </summary>
	/// <param name="databaseType">Type of database to test.</param>
	/// <param name="connectionString">Connection string to test.</param>
	/// <param name="timeoutSeconds">Timeout in seconds for the test.</param>
	/// <returns>Task that completes successfully if connection is valid, throws exception otherwise.</returns>
	public async Task TestConnectionAsync(DatabaseType databaseType, string connectionString, int timeoutSeconds)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
		}

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

		IDatabaseQueryGenerator generator = databaseType switch
		{
			DatabaseType.Mssql => MssqlGenerator.Create(connectionString),
			DatabaseType.MySql => MySqlGenerator.Create(connectionString),
			DatabaseType.PostgreSql => PostgreSqlGenerator.Create(connectionString),
			DatabaseType.Sqlite => SqliteGenerator.Create(connectionString),
			_ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
		};

		await generator.TestConnectionAsync(cts.Token);
	}

	#endregion

}


using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases;
using LinqStudio.Databases.PostgreSQL;
using LinqStudio.Databases.SQLite;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Models;

/// <summary>
/// Represents a single database server connection within a project.
/// A project may contain multiple <see cref="ServerConnection"/> entries,
/// each pointing to a different host/database.
/// </summary>
public class ServerConnection
{
	/// <summary>Unique identifier for this connection.</summary>
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Optional user-friendly label. When null, <see cref="GetServerDisplayName"/> is used.</summary>
	public string? DisplayName { get; set; }

	public DatabaseType DatabaseType
	{
		get;
		set
		{
			field = value;
			QueryGenerator = null; // Reset so it is recreated on next access
		}
	} = DatabaseType.Mssql;

	public string? ConnectionString
	{
		get;
		set
		{
			field = value;
			QueryGenerator = null; // Reset so it is recreated on next access
		}
	}

	#region Connection helpers

	/// <summary>
	/// Query generator used to fetch schema information (tables, columns).
	/// Lazily created from <see cref="ConnectionString"/> and <see cref="DatabaseType"/>.
	/// </summary>
	[JsonIgnore]
	public IDatabaseQueryGenerator? QueryGenerator
	{
		get
		{
			if (field != null)
				return field;

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
			}

			return field;
		}
		private set;
	}

	/// <summary>Updates the connection and resets the cached query generator.</summary>
	public void UpdateConnection(DatabaseType databaseType, string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

		DatabaseType = databaseType;
		ConnectionString = connectionString;
	}

	/// <summary>Tests the database connection using the specified timeout.</summary>
	public async Task TestConnectionAsync(DatabaseType databaseType, string connectionString, int timeoutSeconds)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

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

	#region Display helpers

	/// <summary>
	/// Returns a user-friendly server identifier from the connection string.
	/// Falls back to <see cref="DisplayName"/> when set, then parses host/port.
	/// </summary>
	public string GetServerDisplayName()
	{
		if (!string.IsNullOrWhiteSpace(DisplayName))
			return DisplayName;

		if (string.IsNullOrWhiteSpace(ConnectionString))
			return "(no connection)";

		return DatabaseType switch
		{
			DatabaseType.Mssql => ParseMssqlServer(ConnectionString),
			DatabaseType.MySql => ParseMySqlServer(ConnectionString),
			DatabaseType.PostgreSql => ParsePostgreSqlServer(ConnectionString),
			DatabaseType.Sqlite => ParseSqliteFile(ConnectionString),
			_ => ConnectionString
		};
	}

	/// <summary>
	/// Returns the database name from the connection string.
	/// Used as the display name for the database node in the tree.
	/// </summary>
	public string GetDatabaseName()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
			return "(unknown)";

		// Try "Initial Catalog=" (MSSQL / generic ADO.NET)
		var catalog = GetKeyValue(ConnectionString, "Initial Catalog");
		if (!string.IsNullOrEmpty(catalog))
			return catalog;

		// Try "Database=" (MySQL, PostgreSQL, SQLite)
		var database = GetKeyValue(ConnectionString, "Database");
		if (!string.IsNullOrEmpty(database))
			return database;

		// SQLite fallback — use filename
		if (DatabaseType == DatabaseType.Sqlite)
			return ParseSqliteFile(ConnectionString);

		return "(unknown)";
	}

	#endregion

	#region Private parsing helpers

	private static string ParseMssqlServer(string cs)
	{
		// Try "Server=" first, then "Data Source="
		var server = GetKeyValue(cs, "Server") ?? GetKeyValue(cs, "Data Source");
		return string.IsNullOrEmpty(server) ? cs : $"{server} (MSSQL)";
	}

	private static string ParseMySqlServer(string cs)
	{
		var host = GetKeyValue(cs, "Server") ?? GetKeyValue(cs, "Host") ?? "";
		var port = GetKeyValue(cs, "Port");
		var display = string.IsNullOrEmpty(port) ? host : $"{host},{port}";
		return string.IsNullOrEmpty(display) ? cs : $"{display} (MySQL)";
	}

	private static string ParsePostgreSqlServer(string cs)
	{
		var host = GetKeyValue(cs, "Host") ?? GetKeyValue(cs, "Server") ?? "";
		var port = GetKeyValue(cs, "Port");
		var display = string.IsNullOrEmpty(port) ? host : $"{host},{port}";
		return string.IsNullOrEmpty(display) ? cs : $"{display} (PostgreSQL)";
	}

	private static string ParseSqliteFile(string cs)
	{
		// "Data Source=..." or "Filename=..." patterns
		var path = GetKeyValue(cs, "Data Source") ?? GetKeyValue(cs, "Filename");
		if (!string.IsNullOrEmpty(path))
			return Path.GetFileName(path);
		return "(SQLite)";
	}

	/// <summary>
	/// Extracts a value from a semicolon-delimited key=value connection string.
	/// The comparison is case-insensitive and trims surrounding whitespace.
	/// </summary>
	private static string? GetKeyValue(string connectionString, string key)
	{
		foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var eq = part.IndexOf('=');
			if (eq < 0)
				continue;

			var k = part[..eq].Trim();
			var v = part[(eq + 1)..].Trim();

			if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
				return v;
		}

		return null;
	}

	#endregion
}

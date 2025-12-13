using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;

namespace LinqStudio.Databases;

/// <summary>
/// Factory for creating database query generators.
/// </summary>
public static class DatabaseQueryGeneratorFactory
{
	/// <summary>
	/// Creates a database query generator for the specified database type.
	/// </summary>
	/// <param name="databaseType">Type of database.</param>
	/// <param name="connectionString">Connection string.</param>
	/// <returns>Database query generator instance.</returns>
	/// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
	public static IDatabaseQueryGenerator Create(DatabaseType databaseType, string connectionString)
	{
		return databaseType switch
		{
			DatabaseType.Mssql => MssqlGenerator.Create(connectionString),
			DatabaseType.MySql => MySqlGenerator.Create(connectionString),
			_ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
		};
	}
}

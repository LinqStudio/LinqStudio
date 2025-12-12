using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Databases;

namespace LinqStudio.Core.Services;

/// <summary>
/// Singleton service that stores the current database connection information.
/// </summary>
public class ConnectionService
{
	/// <summary>
	/// Gets or sets the current connection string.
	/// </summary>
	public string? ConnectionString { get; private set; }

	/// <summary>
	/// Query generator used to fetch information about the DB such as list of tables.
	/// </summary>
	public IDatabaseQueryGenerator? QueryGenerator { get; private set; }

	public void UpdateConnection(DatabaseType databaseType, string connectionString)
	{
		ConnectionString = connectionString;

		QueryGenerator = databaseType switch
		{
			DatabaseType.Mssql => MssqlGenerator.Create(connectionString),
			DatabaseType.MySql => MySqlGenerator.Create(connectionString),
			_ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
		};
	}
}

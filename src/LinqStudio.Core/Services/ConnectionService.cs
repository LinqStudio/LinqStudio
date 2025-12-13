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
		QueryGenerator = DatabaseQueryGeneratorFactory.Create(databaseType, connectionString);
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
		
		var generator = DatabaseQueryGeneratorFactory.Create(databaseType, connectionString);

		await generator.TestConnectionAsync(cts.Token);
	}
}

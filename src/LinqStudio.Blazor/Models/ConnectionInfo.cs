using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Blazor.Models;

/// <summary>
/// Represents a database connection shown as a root node in the schema tree.
/// Designed for future multi-server support: the tree can hold multiple ConnectionInfo instances.
/// </summary>
public record ConnectionInfo
{
	/// <summary>Label shown on the connection root node (e.g., "MyApp (Mssql)").</summary>
	public required string DisplayName { get; init; }

	/// <summary>
	/// Optional server name / host extracted from the connection string.
	/// Populated even now so future UI can show it without a breaking change.
	/// </summary>
	public string? ServerName { get; init; }

	/// <summary>
	/// Optional database name extracted from the connection string.
	/// Populated even now so future UI can show it without a breaking change.
	/// </summary>
	public string? DatabaseName { get; init; }

	public required string ConnectionString { get; init; }
	public required DatabaseType DatabaseType { get; init; }

	/// <summary>
	/// Creates a <see cref="ConnectionInfo"/> from the given <paramref name="project"/>.
	/// Derives <see cref="DisplayName"/> as "{project.Name} ({project.DatabaseType})" and
	/// performs best-effort parsing of <see cref="ServerName"/> and <see cref="DatabaseName"/>
	/// from the connection string (semicolon-delimited key=value pairs).
	/// </summary>
	public static ConnectionInfo FromProject(Project project)
	{
		var displayName = $"{project.Name} ({project.DatabaseType})";
		string? serverName = null;
		string? databaseName = null;

		if (!string.IsNullOrEmpty(project.ConnectionString))
		{
			var parts = project.ConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
			foreach (var part in parts)
			{
				var kv = part.Split('=', 2);
				if (kv.Length != 2)
					continue;

				var key = kv[0].Trim().ToLowerInvariant();
				var value = kv[1].Trim();

				if (key is "server" or "host" or "data source" or "datasource" or "server name" or "servername")
					serverName = value;
				else if (key is "database" or "initial catalog" or "dbname" or "db")
					databaseName = value;
			}
		}

		return new ConnectionInfo
		{
			DisplayName = displayName,
			ServerName = serverName,
			DatabaseName = databaseName,
			ConnectionString = project.ConnectionString ?? string.Empty,
			DatabaseType = project.DatabaseType,
		};
	}
}

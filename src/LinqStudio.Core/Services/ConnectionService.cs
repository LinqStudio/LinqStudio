namespace LinqStudio.Core.Services;

/// <summary>
/// Singleton service that stores the current database connection information.
/// </summary>
public class ConnectionService
{
	/// <summary>
	/// Gets or sets the current connection string.
	/// </summary>
	public string? ConnectionString { get; set; }
}

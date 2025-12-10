namespace LinqStudio.Core.Models;

/// <summary>
/// Constants for project file schema versioning.
/// </summary>
public static class ProjectConstants
{
	/// <summary>
	/// Current schema version supported by this version of LinqStudio.
	/// Version history:
	/// - 1: Initial version (Id, Name, ConnectionString, dates, optional Models/Queries/DbContextCode)
	/// </summary>
	public const int CURRENT_SCHEMA_VERSION = 1;

	/// <summary>
	/// Minimum supported schema version for backward compatibility.
	/// </summary>
	public const int MIN_SUPPORTED_SCHEMA_VERSION = 1;
}
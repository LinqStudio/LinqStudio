namespace LinqStudio.Core.Models;

/// <summary>
/// Configuration for project file schema versioning.
/// Immutable configuration object for dependency injection.
/// </summary>
public sealed class ProjectVersionConfig(int currentVersion, int minVersion)
{
	public int CurrentSchemaVersion { get; } = currentVersion;
	public int MinSupportedSchemaVersion { get; } = minVersion;

	// Default production values
	public ProjectVersionConfig()
		: this(currentVersion: 1, minVersion: 1)
	{
	}
}
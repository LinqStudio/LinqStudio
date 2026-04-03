using LinqStudio.Core.Models;
using LinqStudio.Core.Extensions;
using System.Text.Json;

namespace LinqStudio.Core.Services;

/// <summary>
/// Service responsible for project file I/O operations.
/// Handles loading, saving, and creating LinqStudio project files (.linq).
/// </summary>
public class ProjectService
{
	private readonly ProjectVersionConfig _versionConfig;

	/// <summary>
	/// Initializes a new instance of ProjectService with default version configuration.
	/// </summary>
	public ProjectService() : this(new ProjectVersionConfig())
	{
	}

	/// <summary>
	/// Initializes a new instance of ProjectService with custom version configuration.
	/// </summary>
	/// <param name="versionConfig">Version configuration controlling supported schema version range.</param>
	public ProjectService(ProjectVersionConfig versionConfig)
	{
		_versionConfig = versionConfig;
	}

	/// <summary>
	/// Creates a new project instance with default values.
	/// The name is typically derived from the file name.
	/// </summary>
	/// <param name="name">Display name for the project, usually derived from the chosen file name.</param>
	/// <returns>A new <see cref="Project"/> stamped with the current schema version.</returns>
	public Project CreateNew(string name)
	{
		return new Project
		{
			Name = name,
			SchemaVersion = _versionConfig.CurrentSchemaVersion
		};
	}

	/// <summary>
	/// Loads a project from a file path.
	/// Validates schema version compatibility.
	/// </summary>
	/// <param name="filePath">Absolute or relative path to the <c>.linq</c> project file.</param>
	/// <returns>
	/// The deserialized <see cref="Project"/>, or <see langword="null"/> if the file does not exist.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the file is empty, cannot be deserialized, or has an incompatible schema version.
	/// </exception>
	/// <exception cref="System.Text.Json.JsonException">
	/// Wrapped as <see cref="InvalidOperationException"/> when the JSON is malformed.
	/// </exception>
	public async Task<Project?> LoadProjectAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		try
		{
			await using var stream = File.OpenRead(filePath);

			// Check if file is empty
			if (stream.Length == 0)
			{
				throw new InvalidOperationException($"Project file '{filePath}' is empty or corrupted.");
			}

			var project = await JsonSerializer.DeserializeAsync<Project>(stream, JsonSerializerOptions.Default)
				?? throw new InvalidOperationException($"Project file '{filePath}' could not be loaded.");

			// Validate schema version
			ValidateSchemaVersion(project, filePath);

			// Validate project data
			ValidateProject(project);

			return project;
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException($"Project file '{filePath}' is corrupted or in an invalid format.", ex);
		}
	}

	/// <summary>
	/// Saves a project to a file path using atomic write-then-replace.
	/// Updates the ModifiedDate and SchemaVersion before saving.
	/// Original file is preserved if serialization fails.
	/// </summary>
	/// <param name="project">The project to persist. <see cref="Project.ModifiedDate"/> and <see cref="Project.SchemaVersion"/> are updated in-place before writing.</param>
	/// <param name="filePath">Absolute or relative path where the <c>.linq</c> file should be written.</param>
	/// <exception cref="System.Text.Json.JsonException">Thrown when the project cannot be serialized.</exception>
	/// <exception cref="IOException">Thrown when the file system denies the write.</exception>
	public async Task SaveProjectAsync(Project project, string filePath)
	{
		// Validate project before saving
		ValidateProject(project);

		// Issue 8: use CreateDirectory (idempotent) instead of throwing if directory is missing.
		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		project.ModifiedDate = DateTimeOffset.UtcNow;
		project.SchemaVersion = _versionConfig.CurrentSchemaVersion;

		// Write to temporary file first (atomic save pattern)
		// Use a unique name to avoid conflicts with concurrent saves
		var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = File.Create(tempFilePath))
			{
				await JsonSerializer.SerializeAsync(stream, project, JsonSerializerOptions.Indented);
			}

			// Only replace original if serialization succeeded
			File.Move(tempFilePath, filePath, overwrite: true);
		}
		catch
		{
			// Clean up temp file on failure
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
					// Ignore cleanup failures
				}
			}
			// Re-throw original exception
			throw;
		}
	}

	/// <summary>
	/// Validates that the project's schema version is compatible with this version of LinqStudio.
	/// </summary>
	/// <param name="project">The project whose schema version is checked.</param>
	/// <param name="filePath">Path used in exception messages to identify which file is problematic.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="Project.SchemaVersion"/> is newer than the supported maximum,
	/// or older than the supported minimum.
	/// </exception>
	private void ValidateSchemaVersion(Project project, string filePath)
	{
		if (project.SchemaVersion > _versionConfig.CurrentSchemaVersion)
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' requires LinqStudio v{project.SchemaVersion} or newer. " +
				$"This version of LinqStudio supports up to v{_versionConfig.CurrentSchemaVersion}.");
		}

		if (project.SchemaVersion < _versionConfig.MinSupportedSchemaVersion)
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' version {project.SchemaVersion} is too old to be opened. " +
				$"Minimum supported version is {_versionConfig.MinSupportedSchemaVersion}.");
		}
	}

	/// <summary>
	/// Validates that the project has valid data.
	/// Auto-repairs <see cref="Project.Id"/> and date fields when they are at their default values,
	/// rather than throwing, to gracefully handle partially-constructed or migrated projects.
	/// </summary>
	/// <param name="project">The project to validate and auto-repair.</param>
	private static void ValidateProject(Project project)
	{
		if (project.Id == Guid.Empty)
		{
			// Lets just generate a new one
			project.Id = Guid.NewGuid();
		}

		if (project.CreatedDate == default || project.ModifiedDate == default)
		{
			// Lets just say it is now
			project.CreatedDate = DateTimeOffset.UtcNow;
			project.ModifiedDate = DateTimeOffset.UtcNow;
		}

		// Additional validations can be added here in the future as needed
	}
}
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
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
	public ProjectService(ProjectVersionConfig versionConfig)
	{
		_versionConfig = versionConfig;
	}

	/// <summary>
	/// Creates a new project instance with default values.
	/// The name is typically derived from the file name.
	/// </summary>
	public Project CreateNew(string name, string connectionString = "")
	{
		return new Project
		{
			Id = Guid.NewGuid(),
			Name = name,
			ConnectionString = connectionString,
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = _versionConfig.CurrentSchemaVersion
		};
	}

	/// <summary>
	/// Loads a project from a file path.
	/// Validates schema version compatibility.
	/// </summary>
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
	public async Task SaveProjectAsync(Project project, string filePath)
	{
		// Validate project before saving
		ValidateProject(project);

		// Ensure directory exists
		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException($"Directory '{directory}' does not exist.");
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
	/// </summary>
	private static void ValidateProject(Project project)
	{
		if (project.Id == Guid.Empty)
		{
			throw new InvalidOperationException("Cannot save project with invalid project ID (Guid.Empty).");
		}

		if (project.CreatedDate == default || project.ModifiedDate == default)
		{
			throw new InvalidOperationException("Project has invalid dates.");
		}
	}
}
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using System.Text.Json;

namespace LinqStudio.Core.Services;

public class ProjectService
{
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly ProjectVersionConfig _versionConfig;

	public ProjectService(ProjectVersionConfig versionConfig)
	{
		_versionConfig = versionConfig;
	}

	// Convenience constructor using default config
	public ProjectService() : this(new ProjectVersionConfig())
	{
	}

	public async Task<Project?> LoadProjectAsync(string filePath)
	{
		await _lock.WaitAsync();
		try
		{
			if (!File.Exists(filePath))
			{
				return null;
			}

			await using var file = File.OpenRead(filePath);

			// Check if file is empty
			if (file.Length == 0)
			{
				throw new InvalidOperationException(
					$"Project file '{filePath}' is empty or corrupted.");
			}

			Project? project;
			try
			{
				project = await JsonSerializer.DeserializeAsync<Project>(file)
					?? throw new InvalidOperationException(
						$"Project file '{filePath}' could not be loaded. The file may be corrupted.");
			}
			catch (JsonException ex)
			{
				throw new InvalidOperationException(
					$"Failed to read project file '{filePath}'. The file may be corrupted or in an invalid format.",
					ex);
			}

			// Validate required fields
			ValidateProject(project, filePath);

			// Version compatibility checks
			if (project.SchemaVersion > _versionConfig.CurrentSchemaVersion)
			{
				throw new InvalidOperationException(
					$"Project file '{filePath}' requires LinqStudio v{project.SchemaVersion} or newer. " +
					$"Current version supports up to v{_versionConfig.CurrentSchemaVersion}.");
			}

			if (project.SchemaVersion < _versionConfig.MinSupportedSchemaVersion)
			{
				throw new InvalidOperationException(
					$"Project file '{filePath}' version {project.SchemaVersion} is too old. " +
					$"Minimum supported version is {_versionConfig.MinSupportedSchemaVersion}.");
			}

			return project;
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task SaveProjectAsync(Project project, string filePath)
	{
		await _lock.WaitAsync();
		try
		{
			// Validate project before saving
			ValidateProject(project, filePath);

			// Ensure project has current schema version and updated modified date
			var projectToSave = project with
			{
				SchemaVersion = _versionConfig.CurrentSchemaVersion,
				ModifiedDate = DateTimeOffset.UtcNow
			};

			await using var file = File.Open(filePath, FileMode.Create, FileAccess.Write);

			try
			{
				await JsonSerializer.SerializeAsync(file, projectToSave, JsonSerializerOptions.Indented);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(
					$"Failed to save project to '{filePath}'.",
					ex);
			}
		}
		finally
		{
			_lock.Release();
		}
	}

	public Project CreateNew(string name, string connectionString) => new()
	{
		Id = Guid.NewGuid(),
		Name = name,
		ConnectionString = connectionString,
		CreatedDate = DateTimeOffset.UtcNow,
		ModifiedDate = DateTimeOffset.UtcNow,
		SchemaVersion = _versionConfig.CurrentSchemaVersion
	};

	/// <summary>
	/// Validates that a project has all required fields populated.
	/// </summary>
	/// <param name="project">The project to validate.</param>
	/// <param name="filePath">The file path (for error messages).</param>
	/// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
	private static void ValidateProject(Project project, string filePath)
	{
		if (project.Id == Guid.Empty)
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' is invalid: missing or invalid project ID.");
		}

		if (string.IsNullOrWhiteSpace(project.Name))
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' is invalid: project name is required.");
		}

		// Note: ConnectionString can be empty for new projects that haven't been configured yet
		// So we don't validate it here

		if (project.CreatedDate == default)
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' is invalid: missing creation date.");
		}

		if (project.ModifiedDate == default)
		{
			throw new InvalidOperationException(
				$"Project file '{filePath}' is invalid: missing modification date.");
		}
	}
}
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
	/// <summary>
	/// Creates a new project instance with default values.
	/// The name is typically derived from the file name.
	/// </summary>
	public Project CreateNew(string name, string connectionString)
	{
		return new Project
		{
			Id = Guid.NewGuid(),
			Name = name,
			ConnectionString = connectionString,
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			Queries = new List<SavedQuery>()
		};
	}

	/// <summary>
	/// Loads a project from a file path.
	/// </summary>
	public async Task<Project?> LoadProjectAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		await using var stream = File.OpenRead(filePath);
		var project = await JsonSerializer.DeserializeAsync<Project>(stream, JsonSerializerOptions.Default);
		return project;
	}

	/// <summary>
	/// Saves a project to a file path.
	/// Updates the ModifiedDate and Name (from file path) before saving.
	/// </summary>
	public async Task SaveProjectAsync(Project project, string filePath)
	{
		// Ensure directory exists
		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// Extract name from file path and update project
		var nameFromFile = Path.GetFileNameWithoutExtension(filePath);

		var updatedProject = project with
		{
			Name = nameFromFile,
			ModifiedDate = DateTimeOffset.UtcNow
		};

		await using var stream = File.Create(filePath);
		await JsonSerializer.SerializeAsync(stream, updatedProject, JsonSerializerOptions.Indented);
	}
}
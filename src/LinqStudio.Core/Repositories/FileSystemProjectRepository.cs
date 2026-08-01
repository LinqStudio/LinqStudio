using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Core.Repositories;

/// <summary>
/// File-system-backed implementation of <see cref="IProjectRepository"/>.
/// Each project is stored as a single <c>.linq</c> file under
/// <see cref="FileSystemStorageOptions.BasePath"/>. The file's name without extension
/// is the project ID.
/// </summary>
public sealed class FileSystemProjectRepository(ProjectService projectService, FileSystemStorageOptions options, ILogger<FileSystemProjectRepository>? logger = null) : IProjectRepository
{
	private readonly ProjectService _projectService = projectService;
	private readonly FileSystemStorageOptions _options = options;
	private readonly ILogger<FileSystemProjectRepository>? _logger = logger;

	/// <summary>
	/// Returns summaries of all projects found in <see cref="FileSystemStorageOptions.BasePath"/>,
	/// sorted most-recently-modified first.
	/// </summary>
	/// <remarks>
	/// Each project file is loaded independently inside its own try/catch block so that a single
	/// corrupt or unreadable file does not prevent the rest of the list from being returned.
	/// Failures are logged as warnings and the offending file is silently skipped.
	/// </remarks>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>
	/// A read-only list of <see cref="ProjectSummary"/> records; empty if the base path does
	/// not yet exist.
	/// </returns>
	public async Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
	{
		if (!Directory.Exists(_options.BasePath))
			return [];

		var summaries = new List<ProjectSummary>();
		foreach (var filePath in Directory.EnumerateFiles(_options.BasePath, "*.linq", SearchOption.TopDirectoryOnly))
		{
			// Load each file in isolation so one bad file doesn't abort the whole listing.
			try
			{
				var project = await _projectService.LoadProjectAsync(filePath);
				if (project is null)
					continue;

				var projectId = Path.GetFileNameWithoutExtension(filePath);
				summaries.Add(new ProjectSummary(projectId, project.Name, project.CreatedDate, project.ModifiedDate));
			}
			catch (Exception ex)
			{
				_logger?.LogWarning(ex, "Skipping corrupt project file '{FilePath}'.", filePath);
			}
		}

		summaries.Sort((a, b) => b.ModifiedDate.CompareTo(a.ModifiedDate));
		return summaries;
	}

	/// <summary>
	/// Loads the full <see cref="Project"/> for the given <paramref name="projectId"/>.
	/// </summary>
	/// <param name="projectId">
	/// The stable project identifier (file name without the <c>.linq</c> extension).
	/// Must be a plain filename component — no path separators.
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>The deserialized <see cref="Project"/>.</returns>
	/// <exception cref="FileNotFoundException">Thrown when no <c>.linq</c> file exists for <paramref name="projectId"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the file exists but cannot be deserialized.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	public async Task<Project> LoadProjectAsync(string projectId, CancellationToken cancellationToken = default)
	{
		var filePath = GetProjectFilePath(projectId);
		if (!File.Exists(filePath))
			throw new FileNotFoundException($"Project '{projectId}' not found.", filePath);

		return await _projectService.LoadProjectAsync(filePath)
			?? throw new InvalidOperationException($"Failed to load project '{projectId}'.");
	}

	/// <summary>
	/// Saves <paramref name="project"/> to disk. When <paramref name="projectId"/> is
	/// <see langword="null"/> a new file is created whose name is derived from
	/// <paramref name="project"/>.<see cref="Project.Name"/>; otherwise the existing file
	/// at <paramref name="projectId"/> is overwritten.
	/// </summary>
	/// <param name="project">The project data to persist.</param>
	/// <param name="projectId">
	/// ID of an existing project to overwrite, or <see langword="null"/> to create a new one.
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>
	/// The project ID used for the save — either the supplied <paramref name="projectId"/> or
	/// the normalized name derived from <paramref name="project"/>.<see cref="Project.Name"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="projectId"/> is <see langword="null"/> and
	/// <paramref name="project"/>.<see cref="Project.Name"/> contains invalid filename characters
	/// or is a reserved Windows device name (e.g. CON, NUL, COM1).
	/// </exception>
	public async Task<string> SaveProjectAsync(Project project, string? projectId = null, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(_options.BasePath);
		var id = projectId ?? ValidateAndNormalizeProjectName(project.Name);
		var filePath = GetProjectFilePath(id);
		await _projectService.SaveProjectAsync(project, filePath);
		return id;
	}

	/// <summary>
	/// Permanently deletes the project file and its associated queries directory.
	/// If either the file or the directory does not exist the corresponding step is skipped.
	/// </summary>
	/// <remarks>
	/// Queries are stored in a sibling directory named <c>{projectId}.linq.queries/</c>.
	/// Deleting a project must therefore also remove that directory to avoid orphaned query
	/// files accumulating on disk (cascade delete).
	/// </remarks>
	/// <param name="projectId">
	/// The stable project identifier. Must be a plain filename component — no path separators.
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	public Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
	{
		var filePath = GetProjectFilePath(projectId);
		if (File.Exists(filePath))
			File.Delete(filePath);

		// Cascade: remove the companion queries directory so no orphaned files are left behind.
		var queriesDir = $"{filePath}{FileSystemRepositoryHelper.QueriesDirectorySuffix}";
		if (Directory.Exists(queriesDir))
			Directory.Delete(queriesDir, recursive: true);

		return Task.CompletedTask;
	}

	private string GetProjectFilePath(string projectId) =>
		FileSystemRepositoryHelper.GetValidatedPath(_options.BasePath, projectId, ".linq");

	/// <summary>
	/// Validates that <paramref name="name"/> can be used as a file name and returns it
	/// unchanged if so.
	/// </summary>
	/// <param name="name">The candidate project name.</param>
	/// <returns>The validated name, suitable for use as a file-system identifier.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is blank, contains characters forbidden in file names,
	/// or matches a reserved Windows device name.
	/// </exception>
	private static string ValidateAndNormalizeProjectName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Project name cannot be empty.", nameof(name));

		var invalidChars = Path.GetInvalidFileNameChars();
		var bad = name.Where(c => invalidChars.Contains(c)).Distinct().ToArray();
		if (bad.Length > 0)
			throw new ArgumentException(
				$"Project name contains invalid characters: {string.Join(", ", bad.Select(c => $"'{c}'"))}.",
				nameof(name));

		var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{ "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
			  "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
		if (reserved.Contains(name))
			throw new ArgumentException($"'{name}' is a reserved filename and cannot be used as a project name.", nameof(name));

		return name;
	}
}


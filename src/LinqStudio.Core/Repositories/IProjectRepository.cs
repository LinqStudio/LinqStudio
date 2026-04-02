using LinqStudio.Core.Models;

namespace LinqStudio.Core.Repositories;

/// <summary>
/// Defines the persistence contract for LinqStudio projects.
/// Implementations must guarantee that project IDs are stable, opaque identifiers
/// that survive rename operations (i.e. the file-system name, not the display name).
/// </summary>
public interface IProjectRepository
{
	/// <summary>
	/// Returns a lightweight summary of every available project, sorted most-recently-modified first.
	/// </summary>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>
	/// A read-only list of <see cref="ProjectSummary"/> records. Returns an empty list when no
	/// projects exist; never returns <see langword="null"/>.
	/// </returns>
	Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Loads the full <see cref="Project"/> identified by <paramref name="projectId"/>.
	/// </summary>
	/// <param name="projectId">
	/// The stable project identifier returned by <see cref="SaveProjectAsync"/> or
	/// <see cref="ListProjectsAsync"/>. Must be a plain filename component (no path separators).
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>The deserialized <see cref="Project"/>.</returns>
	/// <exception cref="FileNotFoundException">Thrown when no project with <paramref name="projectId"/> exists.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the project file exists but cannot be parsed.</exception>
	Task<Project> LoadProjectAsync(string projectId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves a project. If <paramref name="projectId"/> is <see langword="null"/>, a new project is
	/// created and its generated ID is returned; otherwise the existing project is overwritten and
	/// the same <paramref name="projectId"/> is returned unchanged.
	/// </summary>
	/// <param name="project">The project data to persist.</param>
	/// <param name="projectId">
	/// The ID of an existing project to overwrite, or <see langword="null"/> to create a new project
	/// whose ID is derived from <paramref name="project"/>.<see cref="Project.Name"/>.
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>The project ID — either the newly generated one or the supplied <paramref name="projectId"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="projectId"/> is <see langword="null"/> and
	/// <paramref name="project"/>.<see cref="Project.Name"/> contains invalid filename characters or
	/// is a reserved Windows device name.
	/// </exception>
	Task<string> SaveProjectAsync(Project project, string? projectId = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Permanently removes the project identified by <paramref name="projectId"/> and all of its
	/// associated queries. This operation is irreversible.
	/// </summary>
	/// <param name="projectId">
	/// The stable project identifier. Must be a plain filename component (no path separators).
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default);
}

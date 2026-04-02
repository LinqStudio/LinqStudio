using LinqStudio.Core.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.Core.Repositories;

/// <summary>
/// File-system-backed implementation of <see cref="IQueryRepository"/>.
/// </summary>
/// <remarks>
/// Query files live in a sibling directory next to the project file, named
/// <c>{projectId}.linq.queries/</c>. The <paramref name="projectId"/> passed to every
/// method is resolved to that directory path via
/// <see cref="FileSystemRepositoryHelper.GetValidatedPath"/>, which also guards against
/// path-traversal attacks.
/// </remarks>
/// <remarks>
/// Initializes a new instance of <see cref="FileSystemQueryRepository"/>.
/// </remarks>
/// <param name="queryService">The service used to read and write individual query files.</param>
/// <param name="options">Storage options that supply the base directory for all project files.</param>
public sealed class FileSystemQueryRepository(QueryService queryService, FileSystemStorageOptions options) : IQueryRepository
{
	private readonly QueryService _queryService = queryService;
	private readonly FileSystemStorageOptions _options = options;

	/// <summary>
	/// Loads all saved queries that belong to <paramref name="projectId"/>.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Translated internally to the project's
	/// <c>.linq</c> file path, which in turn determines the <c>.queries</c> directory.
	/// Must be a plain filename component — no path separators.
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>
	/// A read-only list of <see cref="SavedQuery"/> objects; empty when the project has no
	/// saved queries.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	public async Task<IReadOnlyList<SavedQuery>> LoadQueriesAsync(string projectId, CancellationToken cancellationToken = default)
	{
		// projectId is mapped to the .linq file path; QueryService reads the companion .queries directory.
		var projectFilePath = GetProjectFilePath(projectId);
		return await _queryService.LoadQueriesAsync(projectFilePath);
	}

	/// <summary>
	/// Persists <paramref name="query"/> for the project identified by <paramref name="projectId"/>.
	/// Creates the backing query file if it does not exist, or overwrites it if it does.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Must be a plain filename component —
	/// no path separators.
	/// </param>
	/// <param name="query">The query to save.</param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	public async Task SaveQueryAsync(string projectId, SavedQuery query, CancellationToken cancellationToken = default)
	{
		var projectFilePath = GetProjectFilePath(projectId);
		await _queryService.SaveQueryAsync(projectFilePath, query);
	}

	/// <summary>
	/// Permanently removes the query identified by <paramref name="queryId"/> from
	/// <paramref name="projectId"/>. If the query does not exist the call is a no-op.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Must be a plain filename component —
	/// no path separators.
	/// </param>
	/// <param name="queryId">The unique identifier of the query to delete.</param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	public Task DeleteQueryAsync(string projectId, Guid queryId, CancellationToken cancellationToken = default)
	{
		var projectFilePath = GetProjectFilePath(projectId);
		_queryService.DeleteQuery(projectFilePath, queryId);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Translates a project ID to the validated absolute path of its <c>.linq</c> file.
	/// </summary>
	private string GetProjectFilePath(string projectId) =>
		FileSystemRepositoryHelper.GetValidatedPath(_options.BasePath, projectId, ".linq");
}


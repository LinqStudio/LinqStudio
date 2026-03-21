using LinqStudio.Core.Models;

namespace LinqStudio.Core.Repositories;

/// <summary>
/// Defines the persistence contract for saved LINQ queries that belong to a project.
/// Queries are scoped to a project; callers must always supply the owning project ID.
/// Implementations must guarantee isolation between projects — a query written for
/// project A is never visible when loading queries for project B.
/// </summary>
public interface IQueryRepository
{
	/// <summary>
	/// Loads all saved queries that belong to <paramref name="projectId"/>.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Must be a plain filename component
	/// (no path separators).
	/// </param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <returns>
	/// A read-only list of <see cref="SavedQuery"/> objects. Returns an empty list when the
	/// project has no saved queries; never returns <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	Task<IReadOnlyList<SavedQuery>> LoadQueriesAsync(string projectId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Persists <paramref name="query"/> for the project identified by <paramref name="projectId"/>.
	/// If a query with the same <see cref="SavedQuery.Id"/> already exists it is overwritten;
	/// otherwise a new query file is created.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Must be a plain filename component
	/// (no path separators).
	/// </param>
	/// <param name="query">The query to save. Its <see cref="SavedQuery.Id"/> is used as the file key.</param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	Task SaveQueryAsync(string projectId, SavedQuery query, CancellationToken cancellationToken = default);

	/// <summary>
	/// Permanently removes the query identified by <paramref name="queryId"/> from
	/// <paramref name="projectId"/>. If the query does not exist the call is a no-op.
	/// </summary>
	/// <param name="projectId">
	/// The stable identifier of the owning project. Must be a plain filename component
	/// (no path separators).
	/// </param>
	/// <param name="queryId">The unique identifier of the query to delete.</param>
	/// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="projectId"/> contains path separators or attempts directory traversal.</exception>
	Task DeleteQueryAsync(string projectId, Guid queryId, CancellationToken cancellationToken = default);
}

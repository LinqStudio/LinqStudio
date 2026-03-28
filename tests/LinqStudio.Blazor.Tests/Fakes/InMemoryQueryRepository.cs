using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;

namespace LinqStudio.Blazor.Tests.Fakes;

/// <summary>
/// In-memory IQueryRepository for use in unit tests.
/// </summary>
public sealed class InMemoryQueryRepository : IQueryRepository
{
	private readonly Dictionary<string, Dictionary<Guid, SavedQuery>> _store = new();

	public Task<IReadOnlyList<SavedQuery>> LoadQueriesAsync(string projectId, CancellationToken cancellationToken = default)
	{
		if (!_store.TryGetValue(projectId, out var queries))
			return Task.FromResult<IReadOnlyList<SavedQuery>>([]);

		return Task.FromResult<IReadOnlyList<SavedQuery>>(queries.Values.ToList());
	}

	public Task SaveQueryAsync(string projectId, SavedQuery query, CancellationToken cancellationToken = default)
	{
		if (!_store.TryGetValue(projectId, out var queries))
		{
			queries = new Dictionary<Guid, SavedQuery>();
			_store[projectId] = queries;
		}

		queries[query.Id] = query;
		return Task.CompletedTask;
	}

	public Task DeleteQueryAsync(string projectId, Guid queryId, CancellationToken cancellationToken = default)
	{
		if (_store.TryGetValue(projectId, out var queries))
			queries.Remove(queryId);

		return Task.CompletedTask;
	}
}

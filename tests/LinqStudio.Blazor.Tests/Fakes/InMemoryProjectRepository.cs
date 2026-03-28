using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;

namespace LinqStudio.Blazor.Tests.Fakes;

/// <summary>
/// In-memory IProjectRepository for use in unit tests.
/// </summary>
public sealed class InMemoryProjectRepository : IProjectRepository
{
	private readonly Dictionary<string, Project> _store = new();

	public Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
	{
		var summaries = _store
			.Select(kv => new ProjectSummary(kv.Key, kv.Value.Name, kv.Value.CreatedDate, kv.Value.ModifiedDate))
			.OrderByDescending(p => p.ModifiedDate)
			.ToList();

		return Task.FromResult<IReadOnlyList<ProjectSummary>>(summaries);
	}

	public Task<Project> LoadProjectAsync(string projectId, CancellationToken cancellationToken = default)
	{
		if (!_store.TryGetValue(projectId, out var project))
			throw new KeyNotFoundException($"Project '{projectId}' not found.");

		return Task.FromResult(project);
	}

	public Task<string> SaveProjectAsync(Project project, string? projectId = null, CancellationToken cancellationToken = default)
	{
		var id = projectId ?? project.Id.ToString();
		project.ModifiedDate = DateTimeOffset.UtcNow;
		_store[id] = project;
		return Task.FromResult(id);
	}

	public Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
	{
		_store.Remove(projectId);
		return Task.CompletedTask;
	}
}

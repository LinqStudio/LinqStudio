using LinqStudio.Blazor.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages query-related operations for the current project.
/// Tracks open queries and their states (does NOT own the project).
/// </summary>
public class QueriesWorkspace
{
	private Guid? _currentQueryId;
	private readonly Dictionary<Guid, OpenQueryState> _openQueries = new();

	/// <summary>
	/// Event raised when query state changes.
	/// </summary>
	public event EventHandler? QueriesChanged;

	/// <summary>
	/// Gets the id of the currently active query.
	/// </summary>
	public Guid? CurrentQueryId => _currentQueryId;

	/// <summary>
	/// Gets all currently open queries.
	/// </summary>
	public IReadOnlyDictionary<Guid, OpenQueryState> OpenQueries => _openQueries;

	/// <summary>
	/// Gets whether any queries have unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _openQueries.Values.Any(q => q.HasUnsavedChanges);

	/// <summary>
	/// Gets the currently active query from the provided project.
	/// </summary>
	public SavedQuery? GetCurrentQuery(Project? project)
	{
		if (project?.Queries is null || _currentQueryId is null)
		{
			return null;
		}

		return project.Queries.FirstOrDefault(q => q.Id == _currentQueryId.Value);
	}

	/// <summary>
	/// Gets the state of the currently active query.
	/// </summary>
	public OpenQueryState? CurrentQueryState =>
		_currentQueryId is not null && _openQueries.TryGetValue(_currentQueryId.Value, out var state)
			? state
			: null;

	/// <summary>
	/// Initializes/resets the workspace for a new project.
	/// </summary>
	public void Initialize(Project? project)
	{
		_currentQueryId = null;
		_openQueries.Clear();

		if (project?.Queries?.Count > 0)
		{
			OpenQuery(project, project.Queries[0].Id);
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Clears all query state.
	/// </summary>
	public void Clear()
	{
		_currentQueryId = null;
		_openQueries.Clear();
		OnQueriesChanged();
	}

	/// <summary>
	/// Opens a query by id.
	/// </summary>
	public void OpenQuery(Project project, Guid queryId)
	{
		ArgumentNullException.ThrowIfNull(project);
		ArgumentNullException.ThrowIfNull(project.Queries);

		var query = project.Queries.FirstOrDefault(q => q.Id == queryId);
		if (query is null)
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		if (!_openQueries.ContainsKey(queryId))
		{
			_openQueries[queryId] = new OpenQueryState
			{
				QueryId = queryId,
				CurrentText = query.QueryText,
				HasUnsavedChanges = false,
				LastModified = DateTimeOffset.UtcNow
			};
		}

		_currentQueryId = queryId;
		OnQueriesChanged();
	}

	/// <summary>
	/// Closes a query (removes from open queries list).
	/// </summary>
	public void CloseQuery(Guid queryId)
	{
		if (!_openQueries.ContainsKey(queryId))
		{
			return;
		}

		_openQueries.Remove(queryId);

		if (_currentQueryId == queryId)
		{
			_currentQueryId = _openQueries.Keys.FirstOrDefault();
			if (_currentQueryId == Guid.Empty)
			{
				_currentQueryId = null;
			}
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Creates a new query and opens it.
	/// Returns the new query id.
	/// </summary>
	public Guid CreateNewQuery(Project project, string? name = null)
	{
		ArgumentNullException.ThrowIfNull(project);

		var baseName = !string.IsNullOrWhiteSpace(name) ? name : "Query";
		var finalName = GetUniqueQueryName(project.Queries, baseName);

		var newQuery = new SavedQuery
		{
			Name = finalName,
			QueryText = "// Write your LINQ query here\ncontext.",
			CreatedDate = DateTimeOffset.UtcNow
		};

		project.Queries.Add(newQuery);

		OpenQuery(project, newQuery.Id);
		OnQueriesChanged();

		return newQuery.Id;
	}

	/// <summary>
	/// Updates the text of a query.
	/// </summary>
	public void UpdateQueryText(Project project, Guid queryId, string newText)
	{
		ArgumentNullException.ThrowIfNull(project);
		ArgumentNullException.ThrowIfNull(project.Queries);

		if (!_openQueries.TryGetValue(queryId, out var state))
		{
			throw new InvalidOperationException($"Query '{queryId}' is not open.");
		}

		var query = project.Queries.FirstOrDefault(q => q.Id == queryId)
			?? throw new InvalidOperationException($"Query '{queryId}' not found.");

		state.CurrentText = newText;
		state.HasUnsavedChanges = !string.Equals(newText, query.QueryText, StringComparison.Ordinal);
		state.LastModified = DateTimeOffset.UtcNow;

		OnQueriesChanged();
	}

	/// <summary>
	/// Renames a query.
	/// Returns the updated project.
	/// </summary>
	public Project RenameQuery(Project project, Guid queryId, string newName)
	{
		ArgumentNullException.ThrowIfNull(project);
		ArgumentNullException.ThrowIfNull(project.Queries);

		var query = project.Queries.FirstOrDefault(q => q.Id == queryId)
			?? throw new InvalidOperationException($"Query '{queryId}' not found.");

		query.Name = newName;

		if (_openQueries.TryGetValue(queryId, out var state))
		{
			state.HasUnsavedChanges = true;
		}

		OnQueriesChanged();
		return project;
	}

	/// <summary>
	/// Deletes a query.
	/// Returns the updated project.
	/// </summary>
	public Project DeleteQuery(Project project, Guid queryId)
	{
		ArgumentNullException.ThrowIfNull(project);
		ArgumentNullException.ThrowIfNull(project.Queries);

		var removed = project.Queries.RemoveAll(q => q.Id == queryId);
		if (removed == 0)
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		_openQueries.Remove(queryId);

		if (_currentQueryId == queryId)
		{
			_currentQueryId = project.Queries.FirstOrDefault()?.Id;
			if (_currentQueryId is not null && _currentQueryId == Guid.Empty)
			{
				_currentQueryId = null;
			}
		}

		OnQueriesChanged();
		return project;
	}

	/// <summary>
	/// Commits all open query changes to the project.
	/// Returns the updated project.
	/// </summary>
	public Project CommitChanges(Project project)
	{
		ArgumentNullException.ThrowIfNull(project);
		ArgumentNullException.ThrowIfNull(project.Queries);

		foreach (var (queryId, state) in _openQueries.Where(kvp => kvp.Value.HasUnsavedChanges))
		{
			var query = project.Queries.FirstOrDefault(q => q.Id == queryId);
			if (query is not null)
			{
				query.QueryText = state.CurrentText;
			}
		}

		return project;
	}

	/// <summary>
	/// Clears the unsaved flags for all open queries.
	/// </summary>
	public void ClearUnsavedFlags()
	{
		foreach (var state in _openQueries.Values)
		{
			state.HasUnsavedChanges = false;
		}
		OnQueriesChanged();
	}

	/// <summary>
	/// Syncs open query states with the saved project content.
	/// </summary>
	public void UpdateSavedProject(Project savedProject)
	{
		if (savedProject?.Queries is null)
		{
			return;
		}

		foreach (var (queryId, state) in _openQueries.ToList())
		{
			var savedQuery = savedProject.Queries.FirstOrDefault(q => q.Id == queryId);
			if (savedQuery is not null)
			{
				state.CurrentText = savedQuery.QueryText;
			}
		}

		OnQueriesChanged();
	}

	private static string GetUniqueQueryName(List<SavedQuery> existingQueries, string baseName)
	{
		var existingNames = new HashSet<string>(
			existingQueries.Select(q => q.Name),
			StringComparer.OrdinalIgnoreCase);

		if (!existingNames.Contains(baseName))
		{
			return baseName;
		}

		var counter = 1;
		string candidateName;

		do
		{
			candidateName = $"{baseName} {counter}";
			counter++;
		}
		while (existingNames.Contains(candidateName));

		return candidateName;
	}

	private void OnQueriesChanged()
	{
		QueriesChanged?.Invoke(this, EventArgs.Empty);
	}
}
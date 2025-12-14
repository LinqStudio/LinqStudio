using LinqStudio.Blazor.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages query-related operations for the current project.
/// Tracks open queries and their states (does NOT own the project).
/// </summary>
public class QueriesWorkspace
{
	private int _currentQueryIndex = -1;
	private readonly Dictionary<int, OpenQueryState> _openQueries = new();

	/// <summary>
	/// Event raised when query state changes.
	/// </summary>
	public event EventHandler? QueriesChanged;

	/// <summary>
	/// Gets the index of the currently active query.
	/// </summary>
	public int CurrentQueryIndex => _currentQueryIndex;

	/// <summary>
	/// Gets all currently open queries.
	/// </summary>
	public IReadOnlyDictionary<int, OpenQueryState> OpenQueries => _openQueries;

	/// <summary>
	/// Gets whether any queries have unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _openQueries.Values.Any(q => q.HasUnsavedChanges);

	/// <summary>
	/// Gets the currently active query from the provided project.
	/// </summary>
	public SavedQuery? GetCurrentQuery(Project? project)
	{
		return project?.Queries is not null &&
			_currentQueryIndex >= 0 &&
			_currentQueryIndex < project.Queries.Count
			? project.Queries[_currentQueryIndex]
			: null;
	}

	/// <summary>
	/// Gets the state of the currently active query.
	/// </summary>
	public OpenQueryState? CurrentQueryState =>
		_currentQueryIndex >= 0 && _openQueries.TryGetValue(_currentQueryIndex, out var state)
			? state
			: null;

	/// <summary>
	/// Initializes/resets the workspace for a new project.
	/// </summary>
	public void Initialize(Project? project)
	{
		_currentQueryIndex = -1;
		_openQueries.Clear();

		// Auto-open first query if it exists
		if (project?.Queries?.Count > 0)
		{
			OpenQuery(project, 0);
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Clears all query state.
	/// </summary>
	public void Clear()
	{
		_currentQueryIndex = -1;
		_openQueries.Clear();
		OnQueriesChanged();
	}

	/// <summary>
	/// Opens a query by index.
	/// </summary>
	public void OpenQuery(Project project, int queryIndex)
	{
		ArgumentNullException.ThrowIfNull(project.Queries);

		if (queryIndex < 0 || queryIndex >= project.Queries.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(queryIndex));
		}

		// Add to open queries if not already open
		if (!_openQueries.ContainsKey(queryIndex))
		{
			var query = project.Queries[queryIndex];
			_openQueries[queryIndex] = new OpenQueryState
			{
				QueryIndex = queryIndex,
				CurrentText = query.QueryText,
				HasUnsavedChanges = false,
				LastModified = DateTimeOffset.UtcNow
			};
		}

		_currentQueryIndex = queryIndex;
		OnQueriesChanged();
	}

	/// <summary>
	/// Closes a query (removes from open queries list).
	/// </summary>
	public void CloseQuery(int queryIndex)
	{
		if (!_openQueries.ContainsKey(queryIndex))
		{
			return;
		}

		_openQueries.Remove(queryIndex);

		// If we closed the current query, switch to another open query
		if (_currentQueryIndex == queryIndex)
		{
			_currentQueryIndex = _openQueries.Keys.FirstOrDefault(-1);
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Creates a new query and opens it.
	/// Returns the updated project and the new query index.
	/// </summary>
	public (Project updatedProject, int newQueryIndex) CreateNewQuery(Project project, string? name = null)
	{
		ArgumentNullException.ThrowIfNull(project);

		var queries = project.Queries?.ToList() ?? [];

		// Determine base name
		string baseName = !string.IsNullOrWhiteSpace(name) ? name : "Query";

		// Ensure unique name
		var finalName = GetUniqueQueryName(queries, baseName);

		var newQuery = new SavedQuery
		{
			Name = finalName,
			QueryText = "// Write your LINQ query here\ncontext.",
			CreatedDate = DateTimeOffset.UtcNow
		};

		queries.Add(newQuery);
		var updatedProject = project with { Queries = queries };
		var newQueryIndex = queries.Count - 1;

		// Open the new query
		OpenQuery(updatedProject, newQueryIndex);

		OnQueriesChanged();

		return (updatedProject, newQueryIndex);
	}

	/// <summary>
	/// Updates the text of a query.
	/// </summary>
	public void UpdateQueryText(Project project, int queryIndex, string newText)
	{
		ArgumentNullException.ThrowIfNull(project.Queries);

		if (!_openQueries.TryGetValue(queryIndex, out var state))
		{
			throw new InvalidOperationException($"Query {queryIndex} is not open.");
		}

		var query = project.Queries[queryIndex];

		// Update the open query state
		state.CurrentText = newText;
		state.HasUnsavedChanges = newText != query.QueryText;
		state.LastModified = DateTimeOffset.UtcNow;

		OnQueriesChanged();
	}

	/// <summary>
	/// Renames a query.
	/// Returns the updated project.
	/// </summary>
	public Project RenameQuery(Project project, int queryIndex, string newName)
	{
		ArgumentNullException.ThrowIfNull(project.Queries);

		var queries = project.Queries.ToList();
		if (queryIndex < 0 || queryIndex >= queries.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(queryIndex));
		}

		var updatedQuery = queries[queryIndex] with { Name = newName };
		queries[queryIndex] = updatedQuery;

		var updatedProject = project with { Queries = queries };

		// Mark as unsaved if open
		if (_openQueries.TryGetValue(queryIndex, out var state))
		{
			state.HasUnsavedChanges = true;
		}

		OnQueriesChanged();

		return updatedProject;
	}

	/// <summary>
	/// Deletes a query.
	/// Returns the updated project.
	/// </summary>
	public Project DeleteQuery(Project project, int queryIndex)
	{
		ArgumentNullException.ThrowIfNull(project.Queries);

		var queries = project.Queries.ToList();
		if (queryIndex < 0 || queryIndex >= queries.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(queryIndex));
		}

		queries.RemoveAt(queryIndex);
		var updatedProject = project with { Queries = queries };

		// Close the query if it's open
		_openQueries.Remove(queryIndex);

		// Re-index open queries
		var updatedOpenQueries = new Dictionary<int, OpenQueryState>();
		foreach (var (index, state) in _openQueries)
		{
			if (index > queryIndex)
			{
				updatedOpenQueries[index - 1] = new OpenQueryState
				{
					QueryIndex = index - 1,
					CurrentText = state.CurrentText,
					HasUnsavedChanges = state.HasUnsavedChanges,
					LastModified = state.LastModified
				};
			}
			else if (index < queryIndex)
			{
				updatedOpenQueries[index] = state;
			}
		}
		_openQueries.Clear();
		foreach (var kvp in updatedOpenQueries)
		{
			_openQueries[kvp.Key] = kvp.Value;
		}

		// Adjust current query index
		if (_currentQueryIndex == queryIndex)
		{
			_currentQueryIndex = _openQueries.Keys.FirstOrDefault(-1);
		}
		else if (_currentQueryIndex > queryIndex)
		{
			_currentQueryIndex--;
		}

		OnQueriesChanged();

		return updatedProject;
	}

	/// <summary>
	/// Commits all open query changes to the project.
	/// Returns the updated project.
	/// </summary>
	public Project CommitChanges(Project project)
	{
		ArgumentNullException.ThrowIfNull(project.Queries);

		var queries = project.Queries.ToList();

		foreach (var (queryIndex, state) in _openQueries.Where(kvp => kvp.Value.HasUnsavedChanges))
		{
			if (queryIndex >= 0 && queryIndex < queries.Count)
			{
				queries[queryIndex] = queries[queryIndex] with { QueryText = state.CurrentText };
			}
		}

		return project with { Queries = queries };
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
	/// Updates the saved project reference after a save operation.
	/// This syncs the open query states with the newly saved query text without closing them.
	/// </summary>
	public void UpdateSavedProject(Project savedProject)
	{
		if (savedProject?.Queries is null)
		{
			return;
		}

		// Update the CurrentText in open queries to match the saved text
		// This ensures HasUnsavedChanges will be false after save
		foreach (var (queryIndex, state) in _openQueries.ToList())
		{
			if (queryIndex >= 0 && queryIndex < savedProject.Queries.Count)
			{
				var savedQuery = savedProject.Queries[queryIndex];
				state.CurrentText = savedQuery.QueryText;
				// HasUnsavedChanges is already cleared by ClearUnsavedFlags()
			}
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Generates a unique query name by appending a number if needed.
	/// </summary>
	private static string GetUniqueQueryName(List<SavedQuery> existingQueries, string baseName)
	{
		var existingNames = new HashSet<string>(
			existingQueries.Select(q => q.Name),
			StringComparer.OrdinalIgnoreCase);

		if (!existingNames.Contains(baseName))
		{
			return baseName;
		}

		int counter = 1;
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
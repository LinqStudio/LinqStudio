using LinqStudio.Blazor.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages query-related operations for the current project.
/// Tracks open queries, their states, and manages query persistence.
/// </summary>
public class QueriesWorkspace
{
	private readonly IQueryRepository _queryRepository;
	private readonly ILogger<QueriesWorkspace> _logger;
	private Guid? _currentQueryId;
	private readonly Dictionary<Guid, OpenQueryState> _openQueries = new();
	private readonly Dictionary<Guid, SavedQuery> _allQueries = new();
	private List<SavedQuery>? _cachedAllQueries;
	private string? _projectId;

	/// <summary>
	/// Initializes a new instance of <see cref="QueriesWorkspace"/>.
	/// </summary>
	/// <param name="queryRepository">Repository used to load, save, and delete individual queries.</param>
	/// <param name="logger">Logger for structured diagnostic output.</param>
	public QueriesWorkspace(IQueryRepository queryRepository, ILogger<QueriesWorkspace> logger)
	{
		_queryRepository = queryRepository;
		_logger = logger;
	}

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
	/// Gets all queries for the current project.
	/// </summary>
	/// <remarks>
	/// The list is computed lazily from the internal <c>_allQueries</c> dictionary and cached in
	/// <c>_cachedAllQueries</c>. The cache is invalidated (set to <see langword="null"/>) by
	/// <c>InvalidateQueriesCache()</c> whenever a query is added, removed, or the workspace is
	/// re-initialized — at which point the next access rebuilds the snapshot. This avoids
	/// allocating a new list on every UI render tick while keeping the value fresh after mutations.
	/// </remarks>
	public IReadOnlyList<SavedQuery> AllQueries => _cachedAllQueries ??= [.. _allQueries.Values];

	/// <summary>
	/// Gets whether any queries have unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _openQueries.Values.Any(q => q.HasUnsavedChanges);

	/// <summary>
	/// Gets the currently active query.
	/// </summary>
	/// <returns>
	/// The <see cref="SavedQuery"/> that matches <see cref="CurrentQueryId"/>,
	/// or <see langword="null"/> when no query is active or the ID is not found.
	/// </returns>
	public SavedQuery? GetCurrentQuery()
	{
		if (_currentQueryId is null || !_allQueries.TryGetValue(_currentQueryId.Value, out var query))
		{
			return null;
		}

		return query;
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
	/// Loads all queries from the repository.
	/// </summary>
	/// <param name="projectId">
	/// The project to load queries for, or <see langword="null"/> when setting up a brand-new
	/// unsaved project (no queries are loaded in that case).
	/// </param>
	/// <remarks>
	/// All existing open-query state, the all-queries dictionary, and the cached list are cleared
	/// before loading. If any queries are found the first one is automatically opened so the UI
	/// always has something to display.
	/// </remarks>
	public async Task InitializeAsync(string? projectId)
	{
		_currentQueryId = null;
		_openQueries.Clear();
		_allQueries.Clear();
		InvalidateQueriesCache();
		_projectId = projectId;

		if (!string.IsNullOrEmpty(projectId))
		{
			var queries = await _queryRepository.LoadQueriesAsync(projectId);
			foreach (var query in queries)
			{
				_allQueries[query.Id] = query;
			}
			InvalidateQueriesCache();

			_logger.LogInformation("Initialized queries workspace with {QueryCount} queries for project '{ProjectId}'.", _allQueries.Count, projectId);

			// Open first query if any exist
			if (_allQueries.Count > 0)
			{
				var firstQuery = _allQueries.Values.First();
				OpenQuery(firstQuery.Id);
			}
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
		_allQueries.Clear();
		InvalidateQueriesCache();
		_projectId = null;
		OnQueriesChanged();
	}

	/// <summary>
	/// Opens a query by id, making it the active query in the workspace.
	/// If the query is already open its existing <see cref="OpenQueryState"/> is reused.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query to open.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="queryId"/> does not exist in <see cref="AllQueries"/>.
	/// </exception>
	public void OpenQuery(Guid queryId)
	{
		if (!_allQueries.TryGetValue(queryId, out var query))
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

		_logger.LogInformation("Opened query {QueryId} ('{QueryName}').", queryId, query.Name);
		_currentQueryId = queryId;
		OnQueriesChanged();
	}

	/// <summary>
	/// Closes a query (removes it from the open queries list).
	/// If the closed query was active, the next open query (if any) becomes active.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query to close.</param>
	/// <remarks>
	/// This is a no-op when the query is not currently open.
	/// Closing a query does <em>not</em> delete it from the project; use <see cref="DeleteQueryAsync"/> for that.
	/// </remarks>
	public void CloseQuery(Guid queryId)
	{
		if (!_openQueries.ContainsKey(queryId))
		{
			return;
		}

		_openQueries.Remove(queryId);

		if (_currentQueryId == queryId)
		{
			_currentQueryId = _openQueries.Count > 0 ? _openQueries.Keys.First() : (Guid?)null;
		}

		_logger.LogInformation("Closed query {QueryId}.", queryId);
		OnQueriesChanged();
	}

	/// <summary>
	/// Creates a new query, adds it to the workspace, and makes it the active query.
	/// </summary>
	/// <param name="name">
	/// The desired display name for the new query. When <see langword="null"/> or whitespace,
	/// defaults to <c>"Query"</c>. A numeric suffix is appended automatically if the name
	/// conflicts with an existing query (case-insensitive).
	/// </param>
	/// <returns>The <see cref="Guid"/> assigned to the newly created query.</returns>
	/// <remarks>
	/// The new query's <see cref="OpenQueryState.HasUnsavedChanges"/> is set to
	/// <see langword="true"/> immediately because it has never been persisted.
	/// </remarks>
	public Guid CreateNewQuery(string? name = null)
	{
		var baseName = !string.IsNullOrWhiteSpace(name) ? name : "Query";
		var finalName = GetUniqueQueryName(baseName);

		var newQuery = new SavedQuery
		{
			Name = finalName,
			QueryText = "// Write your LINQ query here\ncontext.",
			CreatedDate = DateTimeOffset.UtcNow
		};

		_allQueries[newQuery.Id] = newQuery;
		InvalidateQueriesCache();

		// Open the query and mark it as having unsaved changes (new query)
		if (!_openQueries.ContainsKey(newQuery.Id))
		{
			_openQueries[newQuery.Id] = new OpenQueryState
			{
				QueryId = newQuery.Id,
				CurrentText = newQuery.QueryText,
				HasUnsavedChanges = true, // Mark as unsaved since it's a new query
				LastModified = DateTimeOffset.UtcNow
			};
		}

		_currentQueryId = newQuery.Id;
		_logger.LogInformation("Created new query {QueryId} ('{QueryName}').", newQuery.Id, newQuery.Name);
		OnQueriesChanged();

		return newQuery.Id;
	}

	/// <summary>
	/// Updates the editor text of an open query and recomputes its dirty state.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query whose text is changing.</param>
	/// <param name="newText">The full updated query text from the editor.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the query is not currently open, or when the query does not exist.
	/// </exception>
	/// <remarks>
	/// <see cref="OpenQueryState.HasUnsavedChanges"/> is determined by an ordinal string
	/// comparison against the last-persisted <see cref="SavedQuery.QueryText"/>, so a user
	/// reverting their edits back to the saved text will clear the dirty flag.
	/// </remarks>
	public void UpdateQueryText(Guid queryId, string newText)
	{
		if (!_openQueries.TryGetValue(queryId, out var state))
		{
			throw new InvalidOperationException($"Query '{queryId}' is not open.");
		}

		if (!_allQueries.TryGetValue(queryId, out var query))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		state.CurrentText = newText;
		state.HasUnsavedChanges = !string.Equals(newText, query.QueryText, StringComparison.Ordinal);
		state.LastModified = DateTimeOffset.UtcNow;

		OnQueriesChanged();
	}

	/// <summary>
	/// Renames a query and marks it as dirty if it is currently open.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query to rename.</param>
	/// <param name="newName">The new display name to assign.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="queryId"/> does not exist in <see cref="AllQueries"/>.
	/// </exception>
	public void RenameQuery(Guid queryId, string newName)
	{
		if (!_allQueries.TryGetValue(queryId, out var query))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		query.Name = newName;

		if (_openQueries.TryGetValue(queryId, out var state))
		{
			state.HasUnsavedChanges = true;
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Deletes a query from the workspace and, if a project is open, from the repository.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query to delete.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="queryId"/> does not exist in <see cref="AllQueries"/>.
	/// </exception>
	/// <remarks>
	/// If the deleted query was the active one, the active query is switched to the first
	/// remaining query, or set to <see langword="null"/> if none remain.
	/// The repository delete is skipped when no project ID is set (unsaved project).
	/// </remarks>
	public async Task DeleteQueryAsync(Guid queryId)
	{
		if (!_allQueries.ContainsKey(queryId))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		_allQueries.Remove(queryId);
		_openQueries.Remove(queryId);
		InvalidateQueriesCache();

		if (_currentQueryId == queryId)
		{
			_currentQueryId = _allQueries.Count > 0 ? _allQueries.Keys.First() : (Guid?)null;
		}

		if (!string.IsNullOrEmpty(_projectId))
		{
			await _queryRepository.DeleteQueryAsync(_projectId, queryId);
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Saves a specific query to the project repository, flushing any dirty in-memory edits first.
	/// </summary>
	/// <param name="queryId">The unique identifier of the query to save.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no project ID has been set (project must be saved before saving individual queries),
	/// or when the query does not exist.
	/// </exception>
	/// <remarks>
	/// If the query is open and has unsaved changes, the in-memory text is written back to the
	/// <see cref="SavedQuery"/> object before persisting, and <see cref="OpenQueryState.HasUnsavedChanges"/>
	/// is cleared. Queries that are not open (or have no changes) are written as-is.
	/// </remarks>
	public async Task SaveQueryAsync(Guid queryId)
	{
		if (string.IsNullOrEmpty(_projectId))
		{
			throw new InvalidOperationException("No project ID set. Save the project first.");
		}

		if (!_allQueries.TryGetValue(queryId, out var query))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		if (_openQueries.TryGetValue(queryId, out var state) && state.HasUnsavedChanges)
		{
			query.QueryText = state.CurrentText;
			state.HasUnsavedChanges = false;
		}

		await _queryRepository.SaveQueryAsync(_projectId, query);
		OnQueriesChanged();
	}

	/// <summary>
	/// Saves all queries that have unsaved changes to the project repository.
	/// </summary>
	/// <remarks>
	/// "Dirty" means the query's <see cref="OpenQueryState.HasUnsavedChanges"/> flag is
	/// <see langword="true"/> — set whenever the editor text diverges from the last-persisted
	/// <see cref="SavedQuery.QueryText"/>. Only open (in-tab) queries can be dirty; queries
	/// that have never been opened since the last save are already clean.
	/// This method is a no-op when no project ID is set.
	/// </remarks>
	public async Task SaveAllDirtyQueriesAsync()
	{
		if (string.IsNullOrEmpty(_projectId))
		{
			return;
		}

		var dirtyIds = _openQueries
			.Where(kv => kv.Value.HasUnsavedChanges)
			.Select(kv => kv.Key)
			.ToList();

		foreach (var queryId in dirtyIds)
		{
			await SaveQueryAsync(queryId);
		}
	}

	/// <summary>
	/// Applies any dirty open edits to the in-memory query objects and saves ALL queries
	/// to the specified project directory. Used by SaveAs to migrate queries to the new location.
	/// </summary>
	/// <param name="projectId">
	/// The target project ID to save all queries into. This is intentionally a separate parameter
	/// (rather than the current <c>_projectId</c>) so that the method can write to a <em>new</em>
	/// project directory during a "Save As" operation — before the workspace has been re-initialized
	/// with the new ID. Passing the wrong ID here would silently write queries to the wrong location.
	/// </param>
	/// <remarks>
	/// Unlike <see cref="SaveAllDirtyQueriesAsync"/>, this method saves every query (including ones
	/// with no pending edits) so the new project directory is a complete copy. Dirty flags are
	/// cleared as part of the flush.
	/// </remarks>
	public async Task SaveAllToProjectAsync(string projectId)
	{
		// Flush dirty edits from open query states into the backing query objects
		foreach (var (queryId, state) in _openQueries)
		{
			if (_allQueries.TryGetValue(queryId, out var query) && state.HasUnsavedChanges)
			{
				query.QueryText = state.CurrentText;
				state.HasUnsavedChanges = false;
			}
		}

		// Save every query (including ones that were never dirty) to the new project location
		foreach (var query in _allQueries.Values)
		{
			await _queryRepository.SaveQueryAsync(projectId, query);
		}

		_logger.LogInformation("Saved {Count} queries to project '{ProjectId}'.", _allQueries.Count, projectId);
	}

	private string GetUniqueQueryName(string baseName)
	{
		var existingNames = new HashSet<string>(
			_allQueries.Values.Select(q => q.Name),
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

	private void InvalidateQueriesCache() => _cachedAllQueries = null;
}
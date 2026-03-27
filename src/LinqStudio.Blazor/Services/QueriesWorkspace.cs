using LinqStudio.Blazor.Constants;
using LinqStudio.Blazor.Models;
using LinqStudio.Core.Services;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages query-related operations for the current project.
/// Tracks open queries, their states, and manages query file I/O.
/// </summary>
public class QueriesWorkspace
{
	private readonly QueryService _queryService;
	private readonly ILogger<QueriesWorkspace> _logger;
	private Guid? _currentQueryId;
	private readonly Dictionary<Guid, OpenQueryState> _openQueries = new();
	private readonly Dictionary<Guid, SavedQuery> _allQueries = new();
	private string? _projectFilePath;

	public QueriesWorkspace(QueryService queryService, ILogger<QueriesWorkspace> logger)
	{
		_queryService = queryService;
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
	public IReadOnlyList<SavedQuery> AllQueries => _allQueries.Values.ToList();

	/// <summary>
	/// Gets whether any queries have unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _openQueries.Values.Any(q => q.HasUnsavedChanges);

	/// <summary>
	/// Gets the currently active query.
	/// </summary>
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
	/// Loads all queries from disk.
	/// </summary>
	public async Task InitializeAsync(string? projectFilePath)
	{
		_currentQueryId = null;
		_openQueries.Clear();
		_allQueries.Clear();
		_projectFilePath = projectFilePath;

		if (!string.IsNullOrEmpty(projectFilePath))
		{
			var queries = await _queryService.LoadQueriesAsync(projectFilePath);
			foreach (var query in queries)
			{
				_allQueries[query.Id] = query;
			}

			_logger.LogInformation("Initialized queries workspace with {QueryCount} queries from '{FilePath}'.", _allQueries.Count, projectFilePath);

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
		_projectFilePath = null;
		OnQueriesChanged();
	}

	/// <summary>
	/// Opens a query by id.
	/// </summary>
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
			_currentQueryId = _openQueries.Count > 0 ? _openQueries.Keys.First() : (Guid?)null;
		}

		_logger.LogInformation("Closed query {QueryId}.", queryId);
		OnQueriesChanged();
	}

	/// <summary>
	/// Creates a new query and opens it.
	/// Returns the new query id.
	/// </summary>
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
	/// Updates the text of a query.
	/// </summary>
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
	/// Renames a query.
	/// </summary>
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
	/// Deletes a query.
	/// </summary>
	public async Task DeleteQueryAsync(Guid queryId)
	{
		if (!_allQueries.ContainsKey(queryId))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		_allQueries.Remove(queryId);
		_openQueries.Remove(queryId);

		if (_currentQueryId == queryId)
		{
			_currentQueryId = _allQueries.Count > 0 ? _allQueries.Keys.First() : (Guid?)null;
		}

		// Delete from disk if project file path is set
		if (!string.IsNullOrEmpty(_projectFilePath))
		{
			_queryService.DeleteQuery(_projectFilePath, queryId);
		}

		OnQueriesChanged();
	}

	/// <summary>
	/// Saves a specific query to disk.
	/// </summary>
	public async Task SaveQueryAsync(Guid queryId)
	{
		if (string.IsNullOrEmpty(_projectFilePath))
		{
			throw new InvalidOperationException("No project file path set.");
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

		await _queryService.SaveQueryAsync(_projectFilePath, query);
		OnQueriesChanged();
	}

	/// <summary>
	/// Saves a specific query to disk using a file dialog to prompt for location.
	/// </summary>
	public async Task<bool> SaveQueryWithDialogAsync(Guid queryId, Func<string, Task<string?>> promptSaveFile)
	{
		if (!_allQueries.TryGetValue(queryId, out var query))
		{
			throw new InvalidOperationException($"Query '{queryId}' not found.");
		}

		// Update query text from open state if available
		if (_openQueries.TryGetValue(queryId, out var state))
		{
			query.QueryText = state.CurrentText;
		}

		var defaultFileName = query.Name.EnsureHasExtension(FileExtensions.Query);
		var filePath = await promptSaveFile(defaultFileName);
		if (string.IsNullOrEmpty(filePath))
		{
			return false; // User cancelled
		}

		query.FilePath = filePath;
		query.Name = GetQueryNameFromFilePath(filePath);

		// Save to file
		await _queryService.SaveQueryToFileAsync(filePath, query);

		// Mark as saved
		if (_openQueries.TryGetValue(queryId, out var openState))
		{
			openState.HasUnsavedChanges = false;
		}

		OnQueriesChanged();
		return true;
	}

	private static string GetQueryNameFromFilePath(string filePath)
	{
		var fileName = Path.GetFileName(filePath);
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return "Query";
		}

		var name = fileName;
		var queryExtWithDot = FileExtensions.Query.WithDot();
		if (name.EndsWith(queryExtWithDot, StringComparison.OrdinalIgnoreCase))
		{
			name = name[..^queryExtWithDot.Length];
		}
		else
		{
			name = Path.GetFileNameWithoutExtension(fileName);
		}

		return string.IsNullOrWhiteSpace(name) ? "Query" : name;
	}

	/// <summary>
	/// Opens a query from a file selected via file dialog.
	/// </summary>
	public async Task<Guid?> OpenQueryFromFileAsync(string filePath)
	{
		var query = await _queryService.LoadQueryFromFileAsync(filePath);
		if (query is null)
		{
			return null;
		}

		query.Name = GetQueryNameFromFilePath(filePath);

		// Add to all queries if not already present
		if (!_allQueries.ContainsKey(query.Id))
		{
			_allQueries[query.Id] = query;
		}

		// Open the query
		OpenQuery(query.Id);

		return query.Id;
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
}
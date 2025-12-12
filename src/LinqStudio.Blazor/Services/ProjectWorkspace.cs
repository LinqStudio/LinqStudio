using LinqStudio.Core.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages the current working project in the IDE workspace.
/// Blazor-specific service that tracks the open project state per user session.
/// </summary>
public class ProjectWorkspace
{
	private readonly ProjectService _projectService;
	private Project? _currentProject;
	private string? _currentFilePath;
	private bool _hasUnsavedChanges;
	private int _currentQueryIndex = -1;

	public ProjectWorkspace(ProjectService projectService)
	{
		_projectService = projectService;
	}

	/// <summary>
	/// Gets the currently open project, or null if no project is open.
	/// </summary>
	public Project? CurrentProject => _currentProject;

	/// <summary>
	/// Gets the file path of the currently open project.
	/// </summary>
	public string? CurrentFilePath => _currentFilePath;

	/// <summary>
	/// Gets the project name. Uses stored name if available, otherwise derives from file path.
	/// </summary>
	public string CurrentProjectName
	{
		get
		{
			if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.Name))
			{
				return _currentProject.Name;
			}

			if (!string.IsNullOrEmpty(_currentFilePath))
			{
				return Path.GetFileNameWithoutExtension(_currentFilePath);
			}

			return "Untitled";
		}
	}

	/// <summary>
	/// Gets whether the current project has unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges => _hasUnsavedChanges;

	/// <summary>
	/// Gets whether a project is currently open in the workspace.
	/// </summary>
	public bool IsProjectOpen => _currentProject != null;

	/// <summary>
	/// Gets the index of the currently active query in the editor.
	/// Returns -1 if no query is active.
	/// </summary>
	public int CurrentQueryIndex => _currentQueryIndex;

	/// <summary>
	/// Gets the currently active query being edited.
	/// </summary>
	public SavedQuery? CurrentQuery =>
		_currentProject?.Queries != null &&
		_currentQueryIndex >= 0 &&
		_currentQueryIndex < _currentProject.Queries.Count
			? _currentProject.Queries[_currentQueryIndex]
			: null;

	/// <summary>
	/// Event raised when the workspace state changes.
	/// Blazor components can subscribe to this for reactive UI updates.
	/// </summary>
	public event EventHandler? WorkspaceChanged;

	/// <summary>
	/// Creates a new project (in-memory only, not saved).
	/// </summary>
	public void CreateNew(string name, string connectionString)
	{
		_currentProject = _projectService.CreateNew(name, connectionString);
		_currentFilePath = null;
		_hasUnsavedChanges = true;
		_currentQueryIndex = -1;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Creates a new project and saves it immediately to the specified path.
	/// The project name is automatically derived from the file name.
	/// </summary>
	public async Task CreateAndSaveAsync(string filePath, string connectionString)
	{
		// Extract name from file path
		var name = Path.GetFileNameWithoutExtension(filePath);

		_currentProject = _projectService.CreateNew(name, connectionString);
		await _projectService.SaveProjectAsync(_currentProject, filePath);
		_currentFilePath = filePath;

		// Reload to get updated modified date
		_currentProject = await _projectService.LoadProjectAsync(_currentFilePath);
		_hasUnsavedChanges = false;
		_currentQueryIndex = -1;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Loads and opens a project from the specified file path.
	/// </summary>
	public async Task LoadAsync(string filePath)
	{
		var project = await _projectService.LoadProjectAsync(filePath);

		if (project == null)
		{
			throw new InvalidOperationException($"Project file not found: {filePath}");
		}

		_currentProject = project;
		_currentFilePath = filePath;
		_hasUnsavedChanges = false;
		_currentQueryIndex = project.Queries?.Any() == true ? 0 : -1;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves the current project.
	/// </summary>
	public async Task SaveAsync()
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		if (string.IsNullOrEmpty(_currentFilePath))
		{
			throw new InvalidOperationException("Project file path is not set. Use SaveAsAsync instead.");
		}

		await _projectService.SaveProjectAsync(_currentProject, _currentFilePath);

		// Reload to get updated modified date and name
		_currentProject = await _projectService.LoadProjectAsync(_currentFilePath);
		_hasUnsavedChanges = false;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves the current project to a new file path.
	/// The project name will be updated to match the new file name.
	/// </summary>
	public async Task SaveAsAsync(string filePath)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		await _projectService.SaveProjectAsync(_currentProject, filePath);
		_currentFilePath = filePath;

		// Reload to get updated modified date and name
		_currentProject = await _projectService.LoadProjectAsync(_currentFilePath);
		_hasUnsavedChanges = false;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Updates the current project.
	/// </summary>
	public void Update(Project updatedProject)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		_currentProject = updatedProject;
		_hasUnsavedChanges = true;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Updates the current project using a mutation function.
	/// </summary>
	public void Update(Func<Project, Project> updateFunc)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		_currentProject = updateFunc(_currentProject);
		_hasUnsavedChanges = true;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Closes the current project.
	/// </summary>
	public void Close()
	{
		_currentProject = null;
		_currentFilePath = null;
		_hasUnsavedChanges = false;
		_currentQueryIndex = -1;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Sets the currently active query by index.
	/// </summary>
	public void SetCurrentQuery(int queryIndex)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		if (queryIndex < -1 || queryIndex >= (_currentProject.Queries?.Count ?? 0))
		{
			throw new ArgumentOutOfRangeException(nameof(queryIndex));
		}

		_currentQueryIndex = queryIndex;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Creates a new query and adds it to the project.
	/// </summary>
	public int CreateNewQuery(string? name = null)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		var queries = _currentProject.Queries?.ToList() ?? new List<SavedQuery>();
		var queryNumber = queries.Count + 1;
		var newQuery = new SavedQuery
		{
			Name = name ?? $"Query {queryNumber}",
			QueryText = "// Write your LINQ query here\ncontext.",
			CreatedDate = DateTimeOffset.UtcNow
		};

		queries.Add(newQuery);
		_currentProject = _currentProject with { Queries = queries };
		_hasUnsavedChanges = true;
		_currentQueryIndex = queries.Count - 1;

		OnWorkspaceChanged();

		return _currentQueryIndex;
	}

	/// <summary>
	/// Updates the text of the currently active query.
	/// </summary>
	public void UpdateCurrentQueryText(string newText)
	{
		if (_currentProject == null || _currentQueryIndex < 0)
		{
			throw new InvalidOperationException("No query is currently active.");
		}

		var queries = _currentProject.Queries?.ToList() ?? new List<SavedQuery>();
		if (_currentQueryIndex >= queries.Count)
		{
			throw new InvalidOperationException("Current query index is out of range.");
		}

		var updatedQuery = queries[_currentQueryIndex] with { QueryText = newText };
		queries[_currentQueryIndex] = updatedQuery;

		_currentProject = _currentProject with { Queries = queries };
		_hasUnsavedChanges = true;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Renames the currently active query.
	/// </summary>
	public void RenameCurrentQuery(string newName)
	{
		if (_currentProject == null || _currentQueryIndex < 0)
		{
			throw new InvalidOperationException("No query is currently active.");
		}

		var queries = _currentProject.Queries?.ToList() ?? new List<SavedQuery>();
		if (_currentQueryIndex >= queries.Count)
		{
			throw new InvalidOperationException("Current query index is out of range.");
		}

		var updatedQuery = queries[_currentQueryIndex] with { Name = newName };
		queries[_currentQueryIndex] = updatedQuery;

		_currentProject = _currentProject with { Queries = queries };
		_hasUnsavedChanges = true;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Deletes a query at the specified index.
	/// </summary>
	public void DeleteQuery(int queryIndex)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		var queries = _currentProject.Queries?.ToList() ?? new List<SavedQuery>();
		if (queryIndex < 0 || queryIndex >= queries.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(queryIndex));
		}

		queries.RemoveAt(queryIndex);
		_currentProject = _currentProject with { Queries = queries };
		_hasUnsavedChanges = true;

		// Adjust current query index if needed
		if (_currentQueryIndex == queryIndex)
		{
			_currentQueryIndex = queries.Any() ? Math.Max(0, queryIndex - 1) : -1;
		}
		else if (_currentQueryIndex > queryIndex)
		{
			_currentQueryIndex--;
		}

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Adds a model file to the current project.
	/// </summary>
	public void AddModel(string fileName, string code)
	{
		if (_currentProject == null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		var models = _currentProject.Models ?? new Dictionary<string, string>();
		models[fileName] = code;

		_currentProject = _currentProject with { Models = models };
		_hasUnsavedChanges = true;
		OnWorkspaceChanged();
	}

	private void OnWorkspaceChanged()
	{
		WorkspaceChanged?.Invoke(this, EventArgs.Empty);
	}
}
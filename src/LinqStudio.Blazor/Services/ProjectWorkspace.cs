using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using System.Text.Json;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages the current working project in the IDE workspace.
/// Blazor-specific service that tracks the open project state per user session.
/// </summary>
public class ProjectWorkspace
{
	private readonly ProjectService _projectService;
	private readonly QueriesWorkspace _queriesWorkspace;
	private Project? _currentProject;
	private Project? _savedProject;
	private string? _currentFilePath;

	public ProjectWorkspace(ProjectService projectService, QueriesWorkspace queriesWorkspace)
	{
		_projectService = projectService;
		_queriesWorkspace = queriesWorkspace;

		// Subscribe to query changes to propagate workspace changes
		_queriesWorkspace.QueriesChanged += (s, e) => OnWorkspaceChanged();
	}

	/// <summary>
	/// Gets the queries workspace for query-specific operations.
	/// </summary>
	public QueriesWorkspace Queries => _queriesWorkspace;

	/// <summary>
	/// Gets the currently open project, or null if no project is open.
	/// </summary>
	public Project? CurrentProject => _currentProject;

	/// <summary>
	/// Gets the file path of the currently open project.
	/// </summary>
	public string? CurrentFilePath => _currentFilePath;

	/// <summary>
	/// Gets the project name.
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
	/// Gets whether the current project has ANY unsaved changes (properties or queries).
	/// </summary>
	public bool HasUnsavedChanges
	{
		get
		{
			// Check if queries have unsaved changes
			if (_queriesWorkspace.HasUnsavedChanges)
			{
				return true;
			}

			// Check if project properties have changed
			if (_currentProject is not null && _savedProject is not null)
			{
				return _currentProject.ConnectionString != _savedProject.ConnectionString ||
					   _currentProject.Name != _savedProject.Name;
			}

			// New project that hasn't been saved yet
			return _currentProject is not null && _savedProject is null;
		}
	}

	/// <summary>
	/// Gets whether a project is currently open in the workspace.
	/// </summary>
	public bool IsProjectOpen => _currentProject is not null;

	/// <summary>
	/// Event raised when the workspace state changes.
	/// </summary>
	public event EventHandler? WorkspaceChanged;

	/// <summary>
	/// Creates a new project (in-memory only, not saved).
	/// </summary>
	public async Task CreateNewAsync(string name)
	{
		_currentProject = _projectService.CreateNew(name);
		_savedProject = null; // New project, not saved yet
		_currentFilePath = null;

		await _queriesWorkspace.InitializeAsync(null);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Loads and opens a project from the specified file path.
	/// </summary>
	public async Task LoadAsync(string filePath)
	{
		var project = await _projectService.LoadProjectAsync(filePath)
			?? throw new InvalidOperationException($"Project file not found: {filePath}");

		_currentProject = project;
		_savedProject = CloneProject(project);
		_currentFilePath = filePath;

		await _queriesWorkspace.InitializeAsync(filePath);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves all changes (project properties and queries).
	/// </summary>
	public async Task SaveAsync()
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		if (string.IsNullOrEmpty(_currentFilePath))
		{
			throw new InvalidOperationException("Project file path is not set. Use SaveAsAsync instead.");
		}

		// Save all query changes to disk
		await _queriesWorkspace.SaveAllQueriesAsync();

		// Save project file
		await _projectService.SaveProjectAsync(_currentProject, _currentFilePath);

		// Reload to get updated modified date
		_currentProject = await _projectService.LoadProjectAsync(_currentFilePath)
			?? throw new InvalidOperationException($"Project file not found: {_currentFilePath}");
		_savedProject = _currentProject;

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves the current project to a new file path. Updates the project name accordingly.
	/// </summary>
	public async Task SaveAsAsync(string filePath)
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		// Update name with new file name
		var name = Path.GetFileNameWithoutExtension(filePath);
		_currentProject.Name = name;

		// Save project file
		await _projectService.SaveProjectAsync(_currentProject, filePath);
		_currentFilePath = filePath;

		// Save all query changes to disk (with new project path)
		await _queriesWorkspace.InitializeAsync(filePath); // Reinitialize with new path
		await _queriesWorkspace.SaveAllQueriesAsync();

		// Reload to get updated modified date
		_currentProject = await _projectService.LoadProjectAsync(_currentFilePath)
			?? throw new InvalidOperationException($"Project file not found: {_currentFilePath}");
		_savedProject = _currentProject;

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Updates the current project properties (connection string, etc.).
	/// </summary>
	public void Update(Project updatedProject)
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		_currentProject = updatedProject;
		// Don't update _savedProject so we can track unsaved changes
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Closes the current project.
	/// </summary>
	public void Close()
	{
		_currentProject = null;
		_savedProject = null;
		_currentFilePath = null;
		_queriesWorkspace.Clear();
		OnWorkspaceChanged();
	}

	private void OnWorkspaceChanged()
	{
		WorkspaceChanged?.Invoke(this, EventArgs.Empty);
	}

	private static Project CloneProject(Project project)
	{
		var json = JsonSerializer.Serialize(project, JsonSerializerOptions.Indented);
		return JsonSerializer.Deserialize<Project>(json, JsonSerializerOptions.Indented)
			?? throw new InvalidOperationException("Failed to clone project.");
	}
}
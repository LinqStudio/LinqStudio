using LinqStudio.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Manages the current working project in the IDE workspace.
/// Blazor-specific service that tracks the open project state per user session.
/// </summary>
public class ProjectWorkspace : IDisposable
{
	private readonly IProjectRepository _projectRepository;
	private readonly QueriesWorkspace _queriesWorkspace;
	private readonly ILogger<ProjectWorkspace> _logger;
	private Project? _currentProject;
	private bool _isDirty;
	private string? _currentProjectId;

	/// <summary>
	/// Initializes a new instance of <see cref="ProjectWorkspace"/>, wiring up the
	/// cross-workspace event subscription so that dirty query edits propagate the
	/// <see cref="WorkspaceChanged"/> event to UI consumers.
	/// </summary>
	/// <param name="projectRepository">Repository used to load and persist project metadata.</param>
	/// <param name="queriesWorkspace">
	/// The companion workspace that manages the query lifecycle for this project.
	/// </param>
	/// <param name="logger">Logger for structured diagnostic output.</param>
	/// <remarks>
	/// The constructor subscribes to <see cref="QueriesWorkspace.QueriesChanged"/> using a
	/// named private handler (<c>OnQueriesChangedHandler</c>) so the subscription can be
	/// cleanly removed in <see cref="Dispose"/> without capturing a lambda reference.
	/// </remarks>
	public ProjectWorkspace(IProjectRepository projectRepository, QueriesWorkspace queriesWorkspace, ILogger<ProjectWorkspace> logger)
	{
		_projectRepository = projectRepository;
		_queriesWorkspace = queriesWorkspace;
		_logger = logger;

		// Subscribe to query changes to propagate workspace changes
		_queriesWorkspace.QueriesChanged += OnQueriesChangedHandler;
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
	/// Gets the ID of the currently open project (e.g. the file path for file-system projects).
	/// </summary>
	public string? CurrentProjectId => _currentProjectId;

	/// <summary>
	/// Gets the project name.
	/// </summary>
	public string CurrentProjectName =>
		_currentProject != null && !string.IsNullOrEmpty(_currentProject.Name)
			? _currentProject.Name
			: "Untitled";

	/// <summary>
	/// Gets whether the current project has ANY unsaved changes (properties or queries).
	/// Uses a dirty flag rather than JSON comparison to avoid per-keystroke serialization cost.
	/// </summary>
	public bool HasUnsavedChanges => _isDirty || _queriesWorkspace.HasUnsavedChanges;

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
	/// <param name="name">Display name for the new project.</param>
	/// <remarks>
	/// The project is created with <c>_isDirty = true</c> because it has never been persisted.
	/// <see cref="QueriesWorkspace.InitializeAsync"/> is called with <see langword="null"/> to
	/// reset the query workspace without loading from any repository.
	/// </remarks>
	public async Task CreateNewAsync(string name)
	{
		_currentProject = new Project { Name = name };
		_isDirty = true; // New project is unsaved by definition
		_currentProjectId = null;

		await _queriesWorkspace.InitializeAsync(null);
		_logger.LogInformation("New project '{ProjectName}' created.", name);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Loads and opens a project by its ID.
	/// </summary>
	/// <param name="projectId">
	/// The unique identifier of the project (typically its file-system path for file-based projects).
	/// </param>
	/// <exception cref="Exception">
	/// Propagates any exception thrown by the underlying <see cref="IProjectRepository.LoadProjectAsync"/>.
	/// </exception>
	public async Task LoadAsync(string projectId)
	{
		var project = await _projectRepository.LoadProjectAsync(projectId);

		_currentProject = project;
		_isDirty = false;
		_currentProjectId = projectId;

		await _queriesWorkspace.InitializeAsync(projectId);
		_logger.LogInformation("Project '{ProjectName}' loaded from '{ProjectId}'.", project.Name, projectId);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves all changes to the current project.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no project is open, or when the project has never been saved and has no ID yet
	/// (callers should use <see cref="SaveAsAsync"/> in that case).
	/// </exception>
	/// <remarks>
	/// After persisting, <see cref="QueriesWorkspace.SaveAllDirtyQueriesAsync"/> is called to flush
	/// any in-memory query edits. The project is then reloaded from the repository so that fields
	/// written server-side (such as <c>ModifiedDate</c>) are reflected in-memory.
	/// <c>_isDirty</c> is reset here (not inside the query workspace) because only this method
	/// owns the project-level dirty state.
	/// </remarks>
	public async Task SaveAsync()
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		if (string.IsNullOrEmpty(_currentProjectId))
		{
			throw new InvalidOperationException("Project has not been saved yet. Use SaveAsAsync instead.");
		}

		await _projectRepository.SaveProjectAsync(_currentProject, _currentProjectId);

		// Flush any dirty queries before marking as clean.
		await _queriesWorkspace.SaveAllDirtyQueriesAsync();

		_isDirty = false;

		// Reload to get the updated ModifiedDate written by the repository.
		try
		{
			_currentProject = await _projectRepository.LoadProjectAsync(_currentProjectId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to reload project after Save; ModifiedDate may be stale.");
		}

		_logger.LogInformation("Project '{ProjectName}' saved to '{ProjectId}'.", _currentProject.Name, _currentProjectId);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Saves the current project under a new name, optionally to a specific existing ID/path.
	/// All open queries (including dirty edits) are migrated to the new project location.
	/// </summary>
	/// <param name="name">The new display name for the project.</param>
	/// <param name="existingProjectId">
	/// An optional target project ID/path. When <see langword="null"/> a new location is assigned
	/// by the repository. Pass an existing ID to overwrite a previously-chosen target path
	/// (e.g., when the "Save As" dialog is confirmed a second time).
	/// </param>
	/// <exception cref="InvalidOperationException">Thrown when no project is currently open.</exception>
	/// <remarks>
	/// The query migration order is intentional:
	/// <list type="number">
	///   <item><description>
	///     <see cref="QueriesWorkspace.SaveAllToProjectAsync"/> is called with the new ID
	///     <em>before</em> <see cref="QueriesWorkspace.InitializeAsync"/> so that in-memory dirty
	///     edits are written to the new location and not lost during the subsequent reload.
	///   </description></item>
	///   <item><description>
	///     <c>_isDirty</c> is reset after the repository write — the same pattern as
	///     <see cref="SaveAsync"/> — ensuring <see cref="HasUnsavedChanges"/> returns
	///     <see langword="false"/> immediately after a successful save.
	///   </description></item>
	/// </list>
	/// </remarks>
	public async Task SaveAsAsync(string name, string? existingProjectId = null)
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		_currentProject.Name = name;

		var newId = await _projectRepository.SaveProjectAsync(_currentProject, existingProjectId);
		_currentProjectId = newId;

		_isDirty = false;

		// Persist all open queries (applying dirty edits) to the new project location BEFORE
		// reinitializing — avoids data loss when _projectId was null (first save) or when the
		// project is being renamed (old directory is never written).
		await _queriesWorkspace.SaveAllToProjectAsync(newId);

		await _queriesWorkspace.InitializeAsync(newId);

		// Reload to get the updated ModifiedDate written by the repository.
		try
		{
			_currentProject = await _projectRepository.LoadProjectAsync(_currentProjectId);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to reload project after Save As; ModifiedDate may be stale.");
		}

		_logger.LogInformation("Project saved as '{ProjectName}' to '{ProjectId}'.", _currentProject.Name, _currentProjectId);

		OnWorkspaceChanged();
	}

	/// <summary>
	/// Updates the current project properties (connection string, etc.).
	/// </summary>
	/// <param name="updatedProject">The replacement project object with the new property values.</param>
	/// <exception cref="InvalidOperationException">Thrown when no project is currently open.</exception>
	public void Update(Project updatedProject)
	{
		if (_currentProject is null)
		{
			throw new InvalidOperationException("No project is currently open.");
		}

		_currentProject = updatedProject;
		_isDirty = true;
		OnWorkspaceChanged();
	}

	/// <summary>
	/// Closes the current project and resets all workspace state.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="QueriesWorkspace.Clear"/> to also tear down open query state.
	/// Does not save unsaved changes; callers are responsible for prompting the user before calling this.
	/// </remarks>
	public void Close()
	{
		_currentProject = null;
		_isDirty = false;
		_currentProjectId = null;
		_queriesWorkspace.Clear();
		_logger.LogInformation("Project '{ProjectName}' closed.", CurrentProjectName);
		OnWorkspaceChanged();
	}

	private void OnQueriesChangedHandler(object? sender, EventArgs e) => OnWorkspaceChanged();

	private void OnWorkspaceChanged()
	{
		WorkspaceChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Releases resources held by this workspace by unsubscribing from
	/// <see cref="QueriesWorkspace.QueriesChanged"/>.
	/// </summary>
	public void Dispose()
	{
		_queriesWorkspace.QueriesChanged -= OnQueriesChangedHandler;
	}
}
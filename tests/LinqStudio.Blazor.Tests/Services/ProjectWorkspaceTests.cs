using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinqStudio.Blazor.Tests.Services;

public class ProjectWorkspaceTests : IDisposable
{
	private readonly ProjectService _projectService;
	private readonly QueryService _queryService;
	private readonly FileSystemProjectRepository _projectRepository;
	private readonly FileSystemQueryRepository _queryRepository;
	private readonly QueriesWorkspace _queriesWorkspace;
	private readonly ProjectWorkspace _workspace;
	private readonly string _testDirectory;

	public ProjectWorkspaceTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		_projectService = new ProjectService();
		_queryService = new QueryService();
		var options = new FileSystemStorageOptions { BasePath = _testDirectory };
		_projectRepository = new FileSystemProjectRepository(_projectService, options);
		_queryRepository = new FileSystemQueryRepository(_queryService, options);
		_queriesWorkspace = new QueriesWorkspace(_queryRepository, NullLogger<QueriesWorkspace>.Instance);
		_workspace = new ProjectWorkspace(_projectRepository, _queriesWorkspace, NullLogger<ProjectWorkspace>.Instance);
	}

	#region CreateNew Tests

	[Fact]
	public async Task CreateNewAsync_RaisesWorkspaceChangedEvent()
	{
		// Arrange
		var eventRaised = false;
		_workspace.WorkspaceChanged += (s, e) => eventRaised = true;

		// Act
		await _workspace.CreateNewAsync("Test");

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public async Task CreateNewAsync_InitializesQueriesWorkspace()
	{
		// Act
		await _workspace.CreateNewAsync("Test");

		// Assert
		Assert.Null(_queriesWorkspace.CurrentQueryId); // No queries created by default
	}

	#endregion

	#region LoadAsync Tests

	[Fact]
	public async Task LoadAsync_LoadsProjectFromFile()
	{
		// Arrange — save a project to the repository first
		var project = new Project { Name = "TestProject" };
		await _projectRepository.SaveProjectAsync(project);

		// Also create a query file
		var query = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(Path.Combine(_testDirectory, "TestProject.linq"), query);

		// Act
		await _workspace.LoadAsync("TestProject");

		// Assert
		Assert.True(_workspace.IsProjectOpen);
		Assert.NotNull(_workspace.CurrentProject);
		Assert.Equal("TestProject", _workspace.CurrentProjectId);
		Assert.False(_workspace.HasUnsavedChanges);
		Assert.Equal("TestProject", _workspace.CurrentProjectName);
	}

	[Fact]
	public async Task LoadAsync_OpensFirstQueryIfExists()
	{
		// Arrange
		var project = new Project { Name = "Test" };
		await _projectRepository.SaveProjectAsync(project);
		var query = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(Path.Combine(_testDirectory, "Test.linq"), query);

		// Act
		await _workspace.LoadAsync("Test");

		// Assert
		Assert.NotNull(_queriesWorkspace.CurrentQueryId);
		Assert.True(_queriesWorkspace.OpenQueries.ContainsKey(_queriesWorkspace.CurrentQueryId!.Value));
	}

	[Fact]
	public async Task LoadAsync_ThrowsException_WhenProjectNotFound()
	{
		// Act & Assert
		await Assert.ThrowsAsync<FileNotFoundException>(() => _workspace.LoadAsync("nonexistent"));
	}

	#endregion

	#region SaveAsync Tests

	[Fact]
	public async Task SaveAsync_SavesProject_AndUpdatesState()
	{
		// Arrange
		await _workspace.CreateNewAsync("save_test");
		await _workspace.SaveAsAsync("save_test");

		// Make a change
		var updatedProject = _workspace.CurrentProject!;
		updatedProject.Connections.Add(new ServerConnection { ConnectionString = "Server=newhost;" });
		_workspace.Update(_workspace.CurrentProject!);

		// Act
		await _workspace.SaveAsync();

		// Assert
		Assert.False(_workspace.HasUnsavedChanges);

		// Verify the file was actually saved
		var loadedProject = await _projectService.LoadProjectAsync(Path.Combine(_testDirectory, "save_test.linq"));
		Assert.NotNull(loadedProject);
		Assert.Single(loadedProject.Connections);
		Assert.Equal("Server=newhost;", loadedProject.Connections[0].ConnectionString);
	}

	[Fact]
	public async Task SaveAsync_ThrowsException_WhenNoProjectOpen()
	{
		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.SaveAsync());
	}

	[Fact]
	public async Task SaveAsync_ThrowsException_WhenNoFilePathSet()
	{
		// Arrange
		await _workspace.CreateNewAsync("Test");

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.SaveAsync());
	}

	#endregion

	#region SaveAsAsync Tests

	[Fact]
	public async Task SaveAsAsync_SavesProjectToNewName()
	{
		// Arrange
		await _workspace.CreateNewAsync("original");
		await _workspace.SaveAsAsync("original");

		// Act
		await _workspace.SaveAsAsync("new");

		// Assert
		Assert.Equal("new", _workspace.CurrentProjectId);
		Assert.Equal("new", _workspace.CurrentProjectName);
		Assert.True(File.Exists(Path.Combine(_testDirectory, "new.linq")));
	}

	[Fact]
	public async Task SaveAsAsync_UpdatesProjectName()
	{
		// Arrange
		await _workspace.CreateNewAsync("OldName");

		// Act
		await _workspace.SaveAsAsync("NewProjectName");

		// Assert
		Assert.Equal("NewProjectName", _workspace.CurrentProjectName);

		// Verify saved file has correct name
		var loadedProject = await _projectService.LoadProjectAsync(Path.Combine(_testDirectory, "NewProjectName.linq"));
		Assert.NotNull(loadedProject);
		Assert.Equal("NewProjectName", loadedProject.Name);
	}

	[Fact]
	public async Task SaveAsAsync_PreservesQueryContent_WhenSavingNewProject()
	{
		// Arrange - new project (never saved, so _projectId is null in QueriesWorkspace)
		await _workspace.CreateNewAsync("original");
		var queryId = _queriesWorkspace.CreateNewQuery("TestQuery");
		_queriesWorkspace.UpdateQueryText(queryId, "context.Items.ToList()");

		// Act - first save (the fix: was losing query data when _projectId was null)
		await _workspace.SaveAsAsync("myproject");

		// Assert - verify queries are present in workspace after SaveAs
		Assert.Single(_queriesWorkspace.AllQueries);

		// Reload from disk to confirm queries were actually persisted
		await _workspace.LoadAsync("myproject");
		Assert.Single(_queriesWorkspace.AllQueries);
		Assert.Equal("context.Items.ToList()", _queriesWorkspace.AllQueries[0].QueryText);
	}

	[Fact]
	public async Task SaveAsAsync_PreservesDirtyQueryContent_WhenRenamingExistingProject()
	{
		// Arrange - existing project with a saved query
		var project = new Project { Name = "original" };
		await _projectRepository.SaveProjectAsync(project);
		var query = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(Path.Combine(_testDirectory, "original.linq"), query);

		await _workspace.LoadAsync("original");

		// Modify the query (make it dirty — simulates unsaved edits before rename)
		var qid = _queriesWorkspace.AllQueries[0].Id;
		_queriesWorkspace.UpdateQueryText(qid, "context.People.Where(p => p.Age > 18)");

		// Act - rename/SaveAs
		await _workspace.SaveAsAsync("renamed");

		// Assert - the modified query text must be in the renamed project
		await _workspace.LoadAsync("renamed");
		Assert.Single(_queriesWorkspace.AllQueries);
		Assert.Equal("context.People.Where(p => p.Age > 18)", _queriesWorkspace.AllQueries[0].QueryText);
	}

	#endregion

	#region Update Tests

	[Fact]
	public async Task Update_UpdatesCurrentProject()
	{
		// Arrange
		await _workspace.CreateNewAsync("Test");
		var updatedProject = _workspace.CurrentProject!;
		updatedProject.Connections.Add(new ServerConnection { ConnectionString = "Updated" });

		// Act
		_workspace.Update(updatedProject);

		// Assert
		Assert.Equal("Updated", _workspace.CurrentProject!.Connections[0].ConnectionString);
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public void Update_ThrowsException_WhenNoProjectOpen()
	{
		// Arrange
		var project = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Test",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 1
		};

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => _workspace.Update(project));
	}

	#endregion

	#region Close Tests

	[Fact]
	public async Task Close_ClosesProject_AndClearsState()
	{
		// Arrange
		await _workspace.CreateNewAsync("Test");

		// Act
		_workspace.Close();

		// Assert
		Assert.False(_workspace.IsProjectOpen);
		Assert.Null(_workspace.CurrentProject);
		Assert.Null(_workspace.CurrentProjectId);
		Assert.False(_workspace.HasUnsavedChanges);
		Assert.Null(_queriesWorkspace.CurrentQueryId);
	}

	#endregion

	#region HasUnsavedChanges Tests

	[Fact]
	public async Task HasUnsavedChanges_ReturnsTrue_WhenProjectPropertiesChanged()
	{
		// Arrange
		await _workspace.CreateNewAsync("Test");
		await _workspace.SaveAsAsync("Test");

		// Quick assert no changes yet
		Assert.False(_workspace.HasUnsavedChanges);

		var updatedProject = _workspace.CurrentProject!;
		updatedProject.Connections.Add(new ServerConnection { ConnectionString = "Updated" });

		// Act
		_workspace.Update(updatedProject);

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public async Task HasUnsavedChanges_ReturnsTrue_WhenQueryHasChanges()
	{
		// Arrange
		var project = new Project { Name = "Test" };
		await _projectRepository.SaveProjectAsync(project);
		var query = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		await _queryService.SaveQueryAsync(Path.Combine(_testDirectory, "Test.linq"), query);

		await _workspace.LoadAsync("Test");
		Assert.False(_workspace.HasUnsavedChanges);

		// Act
		var qid = _queriesWorkspace.AllQueries[0].Id;
		_queriesWorkspace.UpdateQueryText(qid, "context.People.ToList()");

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public async Task HasUnsavedChanges_ReturnsTrue_ForNewProject()
	{
		// Act
		await _workspace.CreateNewAsync("Test");

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	#endregion

	#region CurrentProjectName Tests

	[Fact]
	public async Task CurrentProjectName_ReturnsProjectName_WhenSet()
	{
		// Act
		await _workspace.CreateNewAsync("MyProject");

		// Assert
		Assert.Equal("MyProject", _workspace.CurrentProjectName);
	}

	[Fact]
	public void CurrentProjectName_ReturnsUntitled_WhenNoProjectOrFile()
	{
		// Act & Assert
		Assert.Equal("Untitled", _workspace.CurrentProjectName);
	}

	#endregion

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			try
			{
				Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}
}

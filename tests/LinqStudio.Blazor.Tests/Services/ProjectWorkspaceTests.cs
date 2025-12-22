using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.Blazor.Tests.Services;

public class ProjectWorkspaceTests : IDisposable
{
	private readonly ProjectService _projectService;
	private readonly QueriesWorkspace _queriesWorkspace;
	private readonly ProjectWorkspace _workspace;
	private readonly string _testDirectory;

	public ProjectWorkspaceTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		_projectService = new ProjectService(); // Real implementation
		_queriesWorkspace = new QueriesWorkspace();
		_workspace = new ProjectWorkspace(_projectService, _queriesWorkspace);
	}

	#region CreateNew Tests

	[Fact]
	public void CreateNew_RaisesWorkspaceChangedEvent()
	{
		// Arrange
		var eventRaised = false;
		_workspace.WorkspaceChanged += (s, e) => eventRaised = true;

		// Act
		_workspace.CreateNew("Test");

		// Assert
		Assert.True(eventRaised);
	}

	[Fact]
	public void CreateNew_InitializesQueriesWorkspace()
	{
		// Act
		_workspace.CreateNew("Test");

		// Assert
		Assert.Null(_queriesWorkspace.CurrentQueryId); // No queries created by default
	}

	#endregion

	#region LoadAsync Tests

	[Fact]
	public async Task LoadAsync_LoadsProjectFromFile()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "TestProject.linq");

		// Create and save a project first
		var project = _projectService.CreateNew("TestProject", "Server=localhost;");
		project.Queries = [
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		];
		await _projectService.SaveProjectAsync(project, filePath);

		// Act
		await _workspace.LoadAsync(filePath);

		// Assert
		Assert.True(_workspace.IsProjectOpen);
		Assert.NotNull(_workspace.CurrentProject);
		Assert.Equal(filePath, _workspace.CurrentFilePath);
		Assert.False(_workspace.HasUnsavedChanges); // Loaded project has no changes
		Assert.Equal("TestProject", _workspace.CurrentProjectName); // Name comes from filename
	}

	[Fact]
	public async Task LoadAsync_OpensFirstQueryIfExists()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "test.linq");
		var project = _projectService.CreateNew("Test", "");
		project.Queries = [
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		];
		await _projectService.SaveProjectAsync(project, filePath);

		// Act
		await _workspace.LoadAsync(filePath);

		// Assert
		Assert.NotNull(_queriesWorkspace.CurrentQueryId);
		Assert.True(_queriesWorkspace.OpenQueries.ContainsKey(_queriesWorkspace.CurrentQueryId!.Value));
	}

	[Fact]
	public async Task LoadAsync_ThrowsException_WhenProjectNotFound()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "nonexistent.linq");

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.LoadAsync(filePath));
	}

	#endregion

	#region SaveAsync Tests

	[Fact]
	public async Task SaveAsync_SavesProject_AndUpdatesState()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "save_test.linq");
		_workspace.CreateNew("save_test");
		await _workspace.SaveAsAsync(filePath);

		// Make a change
		var updatedProject = _workspace.CurrentProject!;
		updatedProject.ConnectionString = "Server=newhost;";
		_workspace.Update(_workspace.CurrentProject!);

		// Act
		await _workspace.SaveAsync();

		// Assert
		Assert.False(_workspace.HasUnsavedChanges);

		// Verify the file was actually saved
		var loadedProject = await _projectService.LoadProjectAsync(filePath);
		Assert.NotNull(loadedProject);
		Assert.Equal("Server=newhost;", loadedProject.ConnectionString);
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
		_workspace.CreateNew("Test");

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.SaveAsync());
	}

	[Fact]
	public async Task SaveAsync_CommitsAllQueryChanges()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "query_save.linq");
		var project = _projectService.CreateNew("Test");
		project.Queries = [
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		];
		await _projectService.SaveProjectAsync(project, filePath);
		await _workspace.LoadAsync(filePath);

		// Modify query
		var qid = _workspace.CurrentProject!.Queries[0].Id;
		_queriesWorkspace.UpdateQueryText(_workspace.CurrentProject!, qid, "context.People.ToList()");

		// Act
		await _workspace.SaveAsync();

		// Assert
		var savedProject = await _projectService.LoadProjectAsync(filePath);
		Assert.NotNull(savedProject);
		Assert.Equal("context.People.ToList()", savedProject.Queries![0].QueryText);
		Assert.False(_workspace.HasUnsavedChanges);
	}

	#endregion

	#region SaveAsAsync Tests

	[Fact]
	public async Task SaveAsAsync_SavesProjectToNewPath()
	{
		// Arrange
		var originalPath = Path.Combine(_testDirectory, "original.linq");
		var newPath = Path.Combine(_testDirectory, "new.linq");
		_workspace.CreateNew("original");
		await _workspace.SaveAsAsync(originalPath);

		// Act
		await _workspace.SaveAsAsync(newPath);

		// Assert
		Assert.Equal(newPath, _workspace.CurrentFilePath);
		Assert.Equal("new", _workspace.CurrentProjectName);
		Assert.True(File.Exists(newPath));
	}

	[Fact]
	public async Task SaveAsAsync_UpdatesProjectName_FromFileName()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "NewProjectName.linq");
		_workspace.CreateNew("OldName");

		// Act
		await _workspace.SaveAsAsync(filePath);

		// Assert
		Assert.Equal("NewProjectName", _workspace.CurrentProjectName);

		// Verify saved file has correct name
		var loadedProject = await _projectService.LoadProjectAsync(filePath);
		Assert.NotNull(loadedProject);
		Assert.Equal("NewProjectName", loadedProject.Name);
	}

	#endregion

	#region Update Tests

	[Fact]
	public void Update_UpdatesCurrentProject()
	{
		// Arrange
		_workspace.CreateNew("Test");
		var updatedProject = _workspace.CurrentProject!;
		updatedProject.ConnectionString = "Updated";

		// Act
		_workspace.Update(updatedProject);

		// Assert
		Assert.Equal("Updated", _workspace.CurrentProject!.ConnectionString);
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
			ConnectionString = "",
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
	public void Close_ClosesProject_AndClearsState()
	{
		// Arrange
		_workspace.CreateNew("Test");

		// Act
		_workspace.Close();

		// Assert
		Assert.False(_workspace.IsProjectOpen);
		Assert.Null(_workspace.CurrentProject);
		Assert.Null(_workspace.CurrentFilePath);
		Assert.False(_workspace.HasUnsavedChanges);
		Assert.Null(_queriesWorkspace.CurrentQueryId);
	}

	#endregion

	#region HasUnsavedChanges Tests

	[Fact]
	public async Task HasUnsavedChanges_ReturnsTrue_WhenProjectPropertiesChanged()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "test.linq");
		_workspace.CreateNew("Test");
		await _workspace.SaveAsAsync(filePath);

		// Quick assert no changes yet
		Assert.False(_workspace.HasUnsavedChanges);

		var updatedProject = _workspace.CurrentProject!;
		updatedProject.ConnectionString = "Updated";

		// Act
		_workspace.Update(updatedProject);

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public async Task HasUnsavedChanges_ReturnsTrue_WhenQueryHasChanges()
	{
		// Arrange
		var filePath = Path.Combine(_testDirectory, "test.linq");
		var project = _projectService.CreateNew("Test");
		project.Queries = [
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		];
		await _projectService.SaveProjectAsync(project, filePath);
		await _workspace.LoadAsync(filePath);

		// Quick assert no changes yet
		Assert.False(_workspace.HasUnsavedChanges);

		// Act
		var qid2 = _workspace.CurrentProject!.Queries[0].Id;
		_queriesWorkspace.UpdateQueryText(_workspace.CurrentProject!, qid2, "context.People.ToList()");

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public void HasUnsavedChanges_ReturnsTrue_ForNewProject()
	{
		// Act
		_workspace.CreateNew("Test");

		// Assert
		Assert.True(_workspace.HasUnsavedChanges);
	}

	#endregion

	#region CurrentProjectName Tests

	[Fact]
	public void CurrentProjectName_ReturnsProjectName_WhenSet()
	{
		// Act
		_workspace.CreateNew("MyProject");

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
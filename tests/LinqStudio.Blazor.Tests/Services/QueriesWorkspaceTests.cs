using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;

namespace LinqStudio.Blazor.Tests.Services;

public class QueriesWorkspaceTests
{
	private readonly QueriesWorkspace _workspace;

	public QueriesWorkspaceTests()
	{
		_workspace = new QueriesWorkspace();
	}

	#region Initialize Tests

	[Fact]
	public void Initialize_WithNoQueries_SetsCurrentIndexToNegative()
	{
		// Arrange
		var project = CreateTestProject();

		// Act
		_workspace.Initialize(project);

		// Assert
		Assert.Equal(-1, _workspace.CurrentQueryIndex);
		Assert.Empty(_workspace.OpenQueries);
	}

	[Fact]
	public void Initialize_WithQueries_OpensFirstQuery()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		_workspace.Initialize(project);

		// Assert
		Assert.Equal(0, _workspace.CurrentQueryIndex);
		Assert.Single(_workspace.OpenQueries);
		Assert.True(_workspace.OpenQueries.ContainsKey(0));
	}

	[Fact]
	public void Initialize_ClearsPreviousState()
	{
		// Arrange
		var project1 = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.Initialize(project1);

		var project2 = CreateTestProject();

		// Act
		_workspace.Initialize(project2);

		// Assert
		Assert.Equal(-1, _workspace.CurrentQueryIndex);
		Assert.Empty(_workspace.OpenQueries);
	}

	[Fact]
	public void Initialize_RaisesQueriesChangedEvent()
	{
		// Arrange
		var eventRaised = false;
		_workspace.QueriesChanged += (s, e) => eventRaised = true;
		var project = CreateTestProject();

		// Act
		_workspace.Initialize(project);

		// Assert
		Assert.True(eventRaised);
	}

	#endregion

	#region OpenQuery Tests

	[Fact]
	public void OpenQuery_OpensQueryAndSetsAsCurrent()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "context.Orders", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		_workspace.OpenQuery(project, 1);

		// Assert
		Assert.Equal(1, _workspace.CurrentQueryIndex);
		Assert.True(_workspace.OpenQueries.ContainsKey(1));
		Assert.Equal("context.Orders", _workspace.CurrentQueryState!.CurrentText);
	}

	[Fact]
	public void OpenQuery_DoesNotDuplicateIfAlreadyOpen()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		_workspace.OpenQuery(project, 0);
		_workspace.OpenQuery(project, 0);

		// Assert
		Assert.Single(_workspace.OpenQueries);
	}

	[Fact]
	public void OpenQuery_ThrowsException_ForInvalidIndex()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.OpenQuery(project, 10));
		Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.OpenQuery(project, -1));
	}

	#endregion

	#region CloseQuery Tests

	[Fact]
	public void CloseQuery_RemovesQueryFromOpenList()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		_workspace.CloseQuery(0);

		// Assert
		Assert.Empty(_workspace.OpenQueries);
	}

	[Fact]
	public void CloseQuery_SwitchesToAnotherOpenQuery_WhenClosingCurrent()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.OpenQuery(project, 1);

		// Act
		_workspace.CloseQuery(1);

		// Assert
		Assert.Equal(0, _workspace.CurrentQueryIndex);
	}

	[Fact]
	public void CloseQuery_DoesNothing_WhenQueryNotOpen()
	{
		// Arrange
		var project = CreateTestProject();

		// Act & Assert (should not throw)
		_workspace.CloseQuery(5);
		Assert.Empty(_workspace.OpenQueries);
	}

	#endregion

	#region CreateNewQuery Tests

	[Fact]
	public void CreateNewQuery_CreatesAndOpensQuery()
	{
		// Arrange
		var project = CreateTestProject();

		// Act
		var (updatedProject, newIndex) = _workspace.CreateNewQuery(project);

		// Assert
		Assert.NotNull(updatedProject.Queries);
		Assert.Single(updatedProject.Queries);
		Assert.Equal("Query", updatedProject.Queries[0].Name);
		Assert.Equal(0, newIndex);
		Assert.Equal(0, _workspace.CurrentQueryIndex);
		Assert.True(_workspace.OpenQueries.ContainsKey(0));
	}

	[Fact]
	public void CreateNewQuery_WithCustomName_UsesProvidedName()
	{
		// Arrange
		var project = CreateTestProject();

		// Act
		var (updatedProject, _) = _workspace.CreateNewQuery(project, "MyCustomQuery");

		// Assert
		Assert.Equal("MyCustomQuery", updatedProject.Queries![0].Name);
	}

	[Fact]
	public void CreateNewQuery_GeneratesUniqueName_WhenDuplicateExists()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		var (updatedProject, _) = _workspace.CreateNewQuery(project);

		// Assert
		Assert.Equal(2, updatedProject.Queries!.Count);
		Assert.Equal("Query 1", updatedProject.Queries[1].Name);
	}

	[Fact]
	public void CreateNewQuery_IncrementsNumberForMultipleDuplicates()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query 1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		var (updatedProject, _) = _workspace.CreateNewQuery(project);

		// Assert
		Assert.Equal("Query 2", updatedProject.Queries![2].Name);
	}

	[Fact]
	public void CreateNewQuery_IsCaseInsensitive_ForDuplicateCheck()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "query", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		var (updatedProject, _) = _workspace.CreateNewQuery(project, "QUERY");

		// Assert
		Assert.Equal("QUERY 1", updatedProject.Queries![1].Name);
	}

	#endregion

	#region UpdateQueryText Tests

	[Fact]
	public void UpdateQueryText_UpdatesCurrentText()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		_workspace.UpdateQueryText(project, 0, "updated");

		// Assert
		Assert.Equal("updated", _workspace.CurrentQueryState!.CurrentText);
	}

	[Fact]
	public void UpdateQueryText_SetsHasUnsavedChanges_WhenTextDiffers()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		_workspace.UpdateQueryText(project, 0, "updated");

		// Assert
		Assert.True(_workspace.CurrentQueryState!.HasUnsavedChanges);
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public void UpdateQueryText_DoesNotSetUnsaved_WhenTextSameAsSaved()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		_workspace.UpdateQueryText(project, 0, "original");

		// Assert
		Assert.False(_workspace.CurrentQueryState!.HasUnsavedChanges);
	}

	[Fact]
	public void UpdateQueryText_ThrowsException_WhenQueryNotOpen()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => _workspace.UpdateQueryText(project, 0, "new"));
	}

	#endregion

	#region RenameQuery Tests

	[Fact]
	public void RenameQuery_UpdatesQueryName()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "OldName", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		var updatedProject = _workspace.RenameQuery(project, 0, "NewName");

		// Assert
		Assert.Equal("NewName", updatedProject.Queries![0].Name);
	}

	[Fact]
	public void RenameQuery_MarksAsUnsaved_WhenOpen()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "OldName", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		var updatedProject = _workspace.RenameQuery(project, 0, "NewName");

		// Assert
		Assert.True(_workspace.CurrentQueryState!.HasUnsavedChanges);
	}

	[Fact]
	public void RenameQuery_ThrowsException_ForInvalidIndex()
	{
		// Arrange
		var project = CreateTestProject();

		// Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.RenameQuery(project, 0, "New"));
	}

	#endregion

	#region DeleteQuery Tests

	[Fact]
	public void DeleteQuery_RemovesQueryFromProject()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		var updatedProject = _workspace.DeleteQuery(project, 0);

		// Assert
		Assert.Single(updatedProject.Queries!);
		Assert.Equal("Query2", updatedProject.Queries[0].Name);
	}

	[Fact]
	public void DeleteQuery_ReindexesOpenQueries()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query3", QueryText = "test3", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 1);
		_workspace.OpenQuery(project, 2);

		// Act
		var updatedProject = _workspace.DeleteQuery(project, 0);

		// Assert
		Assert.True(_workspace.OpenQueries.ContainsKey(0)); // Was index 1
		Assert.True(_workspace.OpenQueries.ContainsKey(1)); // Was index 2
		Assert.False(_workspace.OpenQueries.ContainsKey(2));
	}

	[Fact]
	public void DeleteQuery_SwitchesToAnotherQuery_WhenDeletingCurrent()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.OpenQuery(project, 1);

		// Act
		_workspace.DeleteQuery(project, 1);

		// Assert
		Assert.Equal(0, _workspace.CurrentQueryIndex);
	}

	#endregion

	#region CommitChanges Tests

	[Fact]
	public void CommitChanges_UpdatesSavedQueryText()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.UpdateQueryText(project, 0, "updated");

		// Act
		var updatedProject = _workspace.CommitChanges(project);

		// Assert
		Assert.Equal("updated", updatedProject.Queries![0].QueryText);
	}

	[Fact]
	public void CommitChanges_OnlyCommitsQueriesWithUnsavedChanges()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original1", CreatedDate = DateTimeOffset.UtcNow },
			new SavedQuery { Name = "Query2", QueryText = "original2", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.OpenQuery(project, 1);
		_workspace.UpdateQueryText(project, 0, "updated1");
		// Query 1 not modified

		// Act
		var updatedProject = _workspace.CommitChanges(project);

		// Assert
		Assert.Equal("updated1", updatedProject.Queries![0].QueryText);
		Assert.Equal("original2", updatedProject.Queries[1].QueryText);
	}

	#endregion

	#region ClearUnsavedFlags Tests

	[Fact]
	public void ClearUnsavedFlags_ResetsAllUnsavedFlags()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.UpdateQueryText(project, 0, "updated");

		// Act
		_workspace.ClearUnsavedFlags();

		// Assert
		Assert.False(_workspace.CurrentQueryState!.HasUnsavedChanges);
		Assert.False(_workspace.HasUnsavedChanges);
	}

	#endregion

	#region UpdateSavedProject Tests

	[Fact]
	public void UpdateSavedProject_SyncsCurrentTextWithSavedText()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);
		_workspace.UpdateQueryText(project, 0, "updated");

		var savedProject = project with
		{
			Queries = [new SavedQuery { Name = "Query1", QueryText = "updated", CreatedDate = DateTimeOffset.UtcNow }]
		};

		// Act
		_workspace.UpdateSavedProject(savedProject);

		// Assert
		Assert.Equal("updated", _workspace.CurrentQueryState!.CurrentText);
	}

	#endregion

	#region GetCurrentQuery Tests

	[Fact]
	public void GetCurrentQuery_ReturnsCurrentQuery()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);
		_workspace.OpenQuery(project, 0);

		// Act
		var current = _workspace.GetCurrentQuery(project);

		// Assert
		Assert.NotNull(current);
		Assert.Equal("Query1", current.Name);
	}

	[Fact]
	public void GetCurrentQuery_ReturnsNull_WhenNoCurrentQuery()
	{
		// Arrange
		var project = CreateTestProject();

		// Act
		var current = _workspace.GetCurrentQuery(project);

		// Assert
		Assert.Null(current);
	}

	#endregion

	#region Helper Methods

	private static Project CreateTestProject(params SavedQuery[] queries)
	{
		return new Project
		{
			Id = Guid.NewGuid(),
			Name = "TestProject",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 1,
			Queries = [.. queries]
		};
	}

	#endregion
}
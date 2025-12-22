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
		Assert.Null(_workspace.CurrentQueryId);
		Assert.Empty(_workspace.OpenQueries);
	}

	[Fact]
	public void Initialize_WithQueries_OpensFirstQuery()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);

		// Act
		_workspace.Initialize(project);

		// Assert
		Assert.Equal(q1.Id, _workspace.CurrentQueryId);
		Assert.Single(_workspace.OpenQueries);
		Assert.True(_workspace.OpenQueries.ContainsKey(q1.Id));
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
		Assert.Null(_workspace.CurrentQueryId);
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
		var q1 = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "context.Orders", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2);

		// Act
		_workspace.OpenQuery(project, q2.Id);

		// Assert
		Assert.Equal(q2.Id, _workspace.CurrentQueryId);
		Assert.True(_workspace.OpenQueries.ContainsKey(q2.Id));
		Assert.Equal("context.Orders", _workspace.CurrentQueryState!.CurrentText);
	}

	[Fact]
	public void OpenQuery_DoesNotDuplicateIfAlreadyOpen()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "context.People", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);

		// Act
		_workspace.OpenQuery(project, q1.Id);
		_workspace.OpenQuery(project, q1.Id);

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
		var missingId = Guid.NewGuid();

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => _workspace.OpenQuery(project, missingId));
	}

	#endregion

	#region CloseQuery Tests

	[Fact]
	public void CloseQuery_RemovesQueryFromOpenList()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);

		// Act
		_workspace.CloseQuery(q1.Id);

		// Assert
		Assert.Empty(_workspace.OpenQueries);
	}

	[Fact]
	public void CloseQuery_SwitchesToAnotherOpenQuery_WhenClosingCurrent()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2);
		_workspace.OpenQuery(project, q1.Id);
		_workspace.OpenQuery(project, q2.Id);

		// Act
		_workspace.CloseQuery(q2.Id);

		// Assert
		Assert.Equal(q1.Id, _workspace.CurrentQueryId);
	}

	[Fact]
	public void CloseQuery_DoesNothing_WhenQueryNotOpen()
	{
		// Arrange
		var project = CreateTestProject();

		// Act & Assert (should not throw)
		_workspace.CloseQuery(Guid.NewGuid());
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
		var newId = _workspace.CreateNewQuery(project);

		// Assert
		Assert.NotNull(project.Queries);
		Assert.Single(project.Queries);
		Assert.Equal("Query", project.Queries[0].Name);
		Assert.Equal(newId, _workspace.CurrentQueryId);
		Assert.True(_workspace.OpenQueries.ContainsKey(newId));
	}

	[Fact]
	public void CreateNewQuery_WithCustomName_UsesProvidedName()
	{
		// Arrange
		var project = CreateTestProject();

		// Act
		_ = _workspace.CreateNewQuery(project, "MyCustomQuery");

		// Assert
		Assert.Equal("MyCustomQuery", project.Queries![0].Name);
	}

	[Fact]
	public void CreateNewQuery_GeneratesUniqueName_WhenDuplicateExists()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "Query", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		_ = _workspace.CreateNewQuery(project);

		// Assert
		Assert.Equal(2, project.Queries!.Count);
		Assert.Equal("Query 1", project.Queries[1].Name);
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
		_ = _workspace.CreateNewQuery(project);

		// Assert
		Assert.Equal("Query 2", project.Queries![2].Name);
	}

	[Fact]
	public void CreateNewQuery_IsCaseInsensitive_ForDuplicateCheck()
	{
		// Arrange
		var project = CreateTestProject(
			new SavedQuery { Name = "query", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow }
		);

		// Act
		_ = _workspace.CreateNewQuery(project, "QUERY");

		// Assert
		Assert.Equal("QUERY 1", project.Queries![1].Name);
	}

	#endregion

	#region UpdateQueryText Tests

	[Fact]
	public void UpdateQueryText_UpdatesCurrentText()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);

		// Act
		_workspace.UpdateQueryText(project, q1.Id, "updated");

		// Assert
		Assert.Equal("updated", _workspace.CurrentQueryState!.CurrentText);
	}

	[Fact]
	public void UpdateQueryText_SetsHasUnsavedChanges_WhenTextDiffers()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);

		// Act
		_workspace.UpdateQueryText(project, q1.Id, "updated");

		// Assert
		Assert.True(_workspace.CurrentQueryState!.HasUnsavedChanges);
		Assert.True(_workspace.HasUnsavedChanges);
	}

	[Fact]
	public void UpdateQueryText_DoesNotSetUnsaved_WhenTextSameAsSaved()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);

		// Act
		_workspace.UpdateQueryText(project, q1.Id, "original");

		// Assert
		Assert.False(_workspace.CurrentQueryState!.HasUnsavedChanges);
	}

	[Fact]
	public void UpdateQueryText_ThrowsException_WhenQueryNotOpen()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => _workspace.UpdateQueryText(project, q1.Id, "new"));
	}

	#endregion

	#region RenameQuery Tests

	[Fact]
	public void RenameQuery_UpdatesQueryName()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "OldName", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);

		// Act
		var updatedProject = _workspace.RenameQuery(project, q1.Id, "NewName");

		// Assert
		Assert.Equal("NewName", updatedProject.Queries![0].Name);
	}

	[Fact]
	public void RenameQuery_MarksAsUnsaved_WhenOpen()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "OldName", QueryText = "test", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);

		// Act
		_ = _workspace.RenameQuery(project, q1.Id, "NewName");

		// Assert
		Assert.True(_workspace.CurrentQueryState!.HasUnsavedChanges);
	}

	[Fact]
	public void RenameQuery_ThrowsException_ForInvalidIndex()
	{
		// Arrange
		var project = CreateTestProject();

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => _workspace.RenameQuery(project, Guid.NewGuid(), "New"));
	}

	#endregion

	#region DeleteQuery Tests

	[Fact]
	public void DeleteQuery_RemovesQueryFromProject()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2);

		// Act
		var updatedProject = _workspace.DeleteQuery(project, q1.Id);

		// Assert
		Assert.Single(updatedProject.Queries!);
		Assert.Equal("Query2", updatedProject.Queries[0].Name);
	}

	[Fact]
	public void DeleteQuery_ReindexesOpenQueries()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow };
		var q3 = new SavedQuery { Name = "Query3", QueryText = "test3", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2, q3);
		_workspace.OpenQuery(project, q2.Id);
		_workspace.OpenQuery(project, q3.Id);

		// Act
		var updatedProject = _workspace.DeleteQuery(project, q1.Id);

		// Assert
		Assert.True(_workspace.OpenQueries.ContainsKey(q2.Id));
		Assert.True(_workspace.OpenQueries.ContainsKey(q3.Id));
		Assert.False(_workspace.OpenQueries.ContainsKey(q1.Id));
	}

	[Fact]
	public void DeleteQuery_SwitchesToAnotherQuery_WhenDeletingCurrent()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "test1", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "test2", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2);
		_workspace.OpenQuery(project, q1.Id);
		_workspace.OpenQuery(project, q2.Id);

		// Act
		_ = _workspace.DeleteQuery(project, q2.Id);

		// Assert
		Assert.Equal(q1.Id, _workspace.CurrentQueryId);
	}

	#endregion

	#region CommitChanges Tests

	[Fact]
	public void CommitChanges_UpdatesSavedQueryText()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);
		_workspace.UpdateQueryText(project, q1.Id, "updated");

		// Act
		var updatedProject = _workspace.CommitChanges(project);

		// Assert
		Assert.Equal("updated", updatedProject.Queries![0].QueryText);
	}

	[Fact]
	public void CommitChanges_OnlyCommitsQueriesWithUnsavedChanges()
	{
		// Arrange
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original1", CreatedDate = DateTimeOffset.UtcNow };
		var q2 = new SavedQuery { Name = "Query2", QueryText = "original2", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1, q2);
		_workspace.OpenQuery(project, q1.Id);
		_workspace.OpenQuery(project, q2.Id);
		_workspace.UpdateQueryText(project, q1.Id, "updated1");
		// Query 2 not modified

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
		var q1 = new SavedQuery { Name = "Query1", QueryText = "original", CreatedDate = DateTimeOffset.UtcNow };
		var project = CreateTestProject(q1);
		_workspace.OpenQuery(project, q1.Id);
		_workspace.UpdateQueryText(project, q1.Id, "updated");

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
		var q1 = project.Queries.First();
		_workspace.OpenQuery(project, q1.Id);
		_workspace.UpdateQueryText(project, q1.Id, "updated");

		var savedProject = project;
		savedProject.Queries = [new SavedQuery { Name = "Query1", QueryText = "updated", CreatedDate = DateTimeOffset.UtcNow }];

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
		_workspace.OpenQuery(project, project.Queries.First().Id);

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
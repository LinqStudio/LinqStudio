using LinqStudio.Core.Services;
using Microsoft.CodeAnalysis;

namespace LinqStudio.Core.Tests;

public class RoslynWorkspaceServiceTests
{
	private static RoslynWorkspaceService CreateService() => new();

	private static (Solution solution, ProjectId projectId) CreateTestProject()
	{
		var workspace = new AdhocWorkspace();
		var projectInfo = ProjectInfo.Create(
			ProjectId.CreateNewId(),
			VersionStamp.Create(),
			"TestProject",
			"TestProject",
			LanguageNames.CSharp);
		var solution = workspace.CurrentSolution.AddProject(projectInfo);
		return (solution, projectInfo.Id);
	}

	[Fact]
	public void AddDocuments_AddsModelFiles_ToSolution()
	{
		// Arrange
		var service = CreateService();
		var (solution, projectId) = CreateTestProject();
		var modelFiles = new Dictionary<string, string>
		{
			["Model1.cs"] = "public class Model1 { }",
			["Model2.cs"] = "public class Model2 { }"
		};
		var dbContextCode = "public class DbContext { }";
		var wrappedQuery = "public class QueryContainer { }";

		// Act
		var updatedSolution = service.AddDocuments(solution, projectId, modelFiles, dbContextCode, wrappedQuery);

		// Assert
		var project = updatedSolution.GetProject(projectId);
		Assert.NotNull(project);
		var docNames = project.Documents.Select(d => d.Name).ToList();
		Assert.Contains("Model1.cs", docNames);
		Assert.Contains("Model2.cs", docNames);
	}

	[Fact]
	public void AddDocuments_AddsDbContextFile_ToSolution()
	{
		// Arrange
		var service = CreateService();
		var (solution, projectId) = CreateTestProject();
		var modelFiles = new Dictionary<string, string>();
		var dbContextCode = "public class MyDbContext : DbContext { }";
		var wrappedQuery = "public class QueryContainer { }";

		// Act
		var updatedSolution = service.AddDocuments(solution, projectId, modelFiles, dbContextCode, wrappedQuery);

		// Assert
		var project = updatedSolution.GetProject(projectId);
		Assert.NotNull(project);
		var docNames = project.Documents.Select(d => d.Name).ToList();
		Assert.Contains("DbContext.cs", docNames);
	}

	[Fact]
	public void AddDocuments_AddsQueryContainerFile_WithDefaultName()
	{
		// Arrange
		var service = CreateService();
		var (solution, projectId) = CreateTestProject();
		var modelFiles = new Dictionary<string, string>();
		var dbContextCode = "public class DbContext { }";
		var wrappedQuery = "public class QueryContainer { }";

		// Act - Not specifying queryFileName, should use default
		var updatedSolution = service.AddDocuments(solution, projectId, modelFiles, dbContextCode, wrappedQuery);

		// Assert
		var project = updatedSolution.GetProject(projectId);
		Assert.NotNull(project);
		var docNames = project.Documents.Select(d => d.Name).ToList();
		Assert.Contains("QueryContainer.cs", docNames);
	}

	[Fact]
	public void AddDocuments_RespectsCustomQueryFileName()
	{
		// Arrange
		var service = CreateService();
		var (solution, projectId) = CreateTestProject();
		var modelFiles = new Dictionary<string, string>();
		var dbContextCode = "public class DbContext { }";
		var wrappedQuery = "public class UserQuery { }";

		// Act
		var updatedSolution = service.AddDocuments(solution, projectId, modelFiles, dbContextCode, wrappedQuery, queryFileName: "UserQuery.cs");

		// Assert
		var project = updatedSolution.GetProject(projectId);
		Assert.NotNull(project);
		var docNames = project.Documents.Select(d => d.Name).ToList();
		Assert.Contains("UserQuery.cs", docNames);
		Assert.DoesNotContain("QueryContainer.cs", docNames);
	}

	[Fact]
	public void AddDocuments_EmptyModelFiles_StillAddsDbContextAndQuery()
	{
		// Arrange
		var service = CreateService();
		var (solution, projectId) = CreateTestProject();
		var modelFiles = new Dictionary<string, string>(); // Empty
		var dbContextCode = "public class DbContext { }";
		var wrappedQuery = "public class QueryContainer { }";

		// Act
		var updatedSolution = service.AddDocuments(solution, projectId, modelFiles, dbContextCode, wrappedQuery);

		// Assert
		var project = updatedSolution.GetProject(projectId);
		Assert.NotNull(project);
		var documents = project.Documents.ToList();
		Assert.Equal(2, documents.Count); // Only DbContext.cs and QueryContainer.cs
		var docNames = documents.Select(d => d.Name).ToList();
		Assert.Contains("DbContext.cs", docNames);
		Assert.Contains("QueryContainer.cs", docNames);
	}
}

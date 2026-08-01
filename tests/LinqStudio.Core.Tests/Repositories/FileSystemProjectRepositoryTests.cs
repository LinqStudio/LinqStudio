using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using LinqStudio.Core.Services;

namespace LinqStudio.Core.Tests.Repositories;

public class FileSystemProjectRepositoryTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly FileSystemProjectRepository _repository;
	private readonly ProjectService _projectService;

	public FileSystemProjectRepositoryTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioRepoTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
		_projectService = new ProjectService();
		_repository = new FileSystemProjectRepository(
			_projectService,
			new FileSystemStorageOptions { BasePath = _testDirectory });
	}

	#region ListProjectsAsync

	[Fact]
	public async Task ListProjectsAsync_ReturnsEmpty_WhenBasePathDoesNotExist()
	{
		// Arrange
		var repo = new FileSystemProjectRepository(
			_projectService,
			new FileSystemStorageOptions { BasePath = Path.Combine(_testDirectory, "nonexistent") });

		// Act
		var result = await repo.ListProjectsAsync();

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public async Task ListProjectsAsync_ReturnsEmpty_WhenNoLinqFiles()
	{
		// Act
		var result = await _repository.ListProjectsAsync();

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public async Task ListProjectsAsync_ReturnsSummary_ForSavedProject()
	{
		// Arrange
		var project = new Project { Name = "TestProject" };
		await _repository.SaveProjectAsync(project);

		// Act
		var summaries = await _repository.ListProjectsAsync();

		// Assert
		Assert.Single(summaries);
		Assert.Equal("TestProject", summaries[0].Id);
		Assert.Equal("TestProject", summaries[0].Name);
	}

	[Fact]
	public async Task ListProjectsAsync_ReturnsSortedByModifiedDateDescending()
	{
		// Arrange — save two projects with a small delay so dates differ
		var project1 = new Project { Name = "Alpha" };
		await _repository.SaveProjectAsync(project1);
		await Task.Delay(20);
		var project2 = new Project { Name = "Beta" };
		await _repository.SaveProjectAsync(project2);

		// Act
		var summaries = await _repository.ListProjectsAsync();

		// Assert — Beta was saved later so should come first
		Assert.Equal(2, summaries.Count);
		Assert.Equal("Beta", summaries[0].Name);
		Assert.Equal("Alpha", summaries[1].Name);
	}

	[Fact]
	public async Task ListProjectsAsync_IgnoresNonLinqFiles()
	{
		// Arrange
		await File.WriteAllTextAsync(Path.Combine(_testDirectory, "readme.txt"), "ignored");
		var project = new Project { Name = "RealProject" };
		await _repository.SaveProjectAsync(project);

		// Act
		var summaries = await _repository.ListProjectsAsync();

		// Assert
		Assert.Single(summaries);
	}

	#endregion

	#region LoadProjectAsync

	[Fact]
	public async Task LoadProjectAsync_ReturnsProject_WhenExists()
	{
		// Arrange
		var project = new Project { Name = "LoadMe" };
		await _repository.SaveProjectAsync(project);

		// Act
		var loaded = await _repository.LoadProjectAsync("LoadMe");

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal("LoadMe", loaded.Name);
	}

	[Fact]
	public async Task LoadProjectAsync_ThrowsFileNotFoundException_WhenNotExists()
	{
		// Act & Assert
		await Assert.ThrowsAsync<FileNotFoundException>(
			() => _repository.LoadProjectAsync("Nonexistent"));
	}

	#endregion

	#region SaveProjectAsync

	[Fact]
	public async Task SaveProjectAsync_CreatesLinqFile()
	{
		// Arrange
		var project = new Project { Name = "NewProject" };

		// Act
		var id = await _repository.SaveProjectAsync(project);

		// Assert
		Assert.Equal("NewProject", id);
		Assert.True(File.Exists(Path.Combine(_testDirectory, "NewProject.linq")));
	}

	[Fact]
	public async Task SaveProjectAsync_CreatesBasePathIfNotExists()
	{
		// Arrange
		var newBasePath = Path.Combine(_testDirectory, "subdir");
		var repo = new FileSystemProjectRepository(
			_projectService,
			new FileSystemStorageOptions { BasePath = newBasePath });
		var project = new Project { Name = "SubProject" };

		// Act
		await repo.SaveProjectAsync(project);

		// Assert
		Assert.True(Directory.Exists(newBasePath));
		Assert.True(File.Exists(Path.Combine(newBasePath, "SubProject.linq")));
	}

	[Fact]
	public async Task SaveProjectAsync_OverwritesExistingFile()
	{
		// Arrange
		var project = new Project { Name = "Overwrite" };
		await _repository.SaveProjectAsync(project);
		project.ConnectionString = "Server=updated";

		// Act
		await _repository.SaveProjectAsync(project);

		// Assert — should not throw, file should contain updated data
		var loaded = await _repository.LoadProjectAsync("Overwrite");
		Assert.Equal("Server=updated", loaded.ConnectionString);
	}

	[Fact]
	public async Task SaveProjectAsync_ReturnsProjectName_AsId()
	{
		// Arrange
		var project = new Project { Name = "MyProject" };

		// Act
		var id = await _repository.SaveProjectAsync(project);

		// Assert
		Assert.Equal("MyProject", id);
	}

	#endregion

	#region DeleteProjectAsync

	[Fact]
	public async Task DeleteProjectAsync_RemovesLinqFile()
	{
		// Arrange
		var project = new Project { Name = "ToDelete" };
		await _repository.SaveProjectAsync(project);
		Assert.True(File.Exists(Path.Combine(_testDirectory, "ToDelete.linq")));

		// Act
		await _repository.DeleteProjectAsync("ToDelete");

		// Assert
		Assert.False(File.Exists(Path.Combine(_testDirectory, "ToDelete.linq")));
	}

	[Fact]
	public async Task DeleteProjectAsync_RemovesQueriesDirectory()
	{
		// Arrange
		var project = new Project { Name = "WithQueries" };
		await _repository.SaveProjectAsync(project);
		var queriesDir = Path.Combine(_testDirectory, "WithQueries.linq.queries");
		Directory.CreateDirectory(queriesDir);
		await File.WriteAllTextAsync(Path.Combine(queriesDir, "query.txt"), "test");

		// Act
		await _repository.DeleteProjectAsync("WithQueries");

		// Assert
		Assert.False(Directory.Exists(queriesDir));
	}

	[Fact]
	public async Task DeleteProjectAsync_DoesNotThrow_WhenFileNotExists()
	{
		// Act & Assert — should not throw
		await _repository.DeleteProjectAsync("Nonexistent");
	}

	#endregion

	#region Path traversal security

	[Theory]
	[InlineData("../secret")]
	[InlineData(@"..\secret")]
	[InlineData("sub/file")]
	[InlineData(@"sub\file")]
	[InlineData(@"C:\Windows\system32")]
	public async Task LoadProjectAsync_PathTraversalId_ThrowsArgumentException(string maliciousId)
	{
		await Assert.ThrowsAsync<ArgumentException>(
			() => _repository.LoadProjectAsync(maliciousId));
	}

	[Theory]
	[InlineData("../secret")]
	[InlineData(@"..\secret")]
	[InlineData("sub/file")]
	[InlineData(@"sub\file")]
	public async Task SaveProjectAsync_PathTraversalName_ThrowsArgumentException(string maliciousName)
	{
		var project = new Project { Name = maliciousName };

		await Assert.ThrowsAsync<ArgumentException>(
			() => _repository.SaveProjectAsync(project));
	}

	[Theory]
	[InlineData("../secret")]
	[InlineData(@"..\secret")]
	[InlineData("sub/file")]
	[InlineData(@"sub\file")]
	public async Task DeleteProjectAsync_PathTraversalId_ThrowsArgumentException(string maliciousId)
	{
		await Assert.ThrowsAsync<ArgumentException>(
			() => _repository.DeleteProjectAsync(maliciousId));
	}

	#endregion

	#region IDisposable

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			try { Directory.Delete(_testDirectory, recursive: true); }
			catch { /* ignore cleanup errors */ }
		}
	}

	#endregion
}

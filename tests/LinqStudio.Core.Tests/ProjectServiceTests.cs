using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using System.Text.Json;

namespace LinqStudio.Core.Tests;

public class ProjectServiceTests : IDisposable
{
	private readonly string _testDirectory;

	public ProjectServiceTests()
	{
		// Create unique test directory for each test run
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);
	}

	#region CreateNew Tests

	[Fact]
	public void CreateNew_SetsCurrentSchemaVersion()
	{
		// Arrange
		var versionConfig = new ProjectVersionConfig();
		var service = new ProjectService(versionConfig);

		// Act
		var project = service.CreateNew("Test Project", "Server=localhost;Database=Test;");

		// Assert
		Assert.Equal(versionConfig.CurrentSchemaVersion, project.SchemaVersion);
		Assert.Equal("Test Project", project.Name);
		Assert.Equal("Server=localhost;Database=Test;", project.ConnectionString);
		Assert.NotEqual(Guid.Empty, project.Id);
		Assert.True(project.CreatedDate <= DateTimeOffset.UtcNow);
		Assert.True(project.ModifiedDate <= DateTimeOffset.UtcNow);
	}

	[Fact]
	public void CreateNew_GeneratesUniqueIds()
	{
		// Arrange
		var service = new ProjectService();

		// Act
		var project1 = service.CreateNew("Project 1", "Connection 1");
		var project2 = service.CreateNew("Project 2", "Connection 2");

		// Assert
		Assert.NotEqual(project1.Id, project2.Id);
	}

	[Fact]
	public void CreateNew_WithEmptyStrings_DoesNotThrow()
	{
		// Arrange
		var service = new ProjectService();

		// Act
		var project = service.CreateNew(string.Empty, string.Empty);

		// Assert
		Assert.NotNull(project);
		Assert.Equal(string.Empty, project.Name);
		Assert.Equal(string.Empty, project.ConnectionString);
	}

	#endregion

	#region SaveProjectAsync Tests

	[Fact]
	public async Task SaveProjectAsync_CreatesFileWithCorrectContent()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "test_save.linq");
		var project = service.CreateNew("Save Test", "Server=localhost;");

		// Act
		await service.SaveProjectAsync(project, filePath);

		// Assert
		Assert.True(File.Exists(filePath));

		var fileContent = await File.ReadAllTextAsync(filePath);
		Assert.Contains("Save Test", fileContent);
		Assert.Contains("Server=localhost;", fileContent);
	}

	[Fact]
	public async Task SaveProjectAsync_UpdatesSchemaVersionToCurrentVersion()
	{
		// Arrange
		var versionConfig = new ProjectVersionConfig();
		var service = new ProjectService(versionConfig);
		var filePath = Path.Combine(_testDirectory, "version_update.linq");

		// Create project with old version
		var oldProject = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Old Version Project",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 0 // Simulate old version
		};

		// Act
		await service.SaveProjectAsync(oldProject, filePath);

		// Read back the saved file
		await using var file = File.OpenRead(filePath);
		var savedProject = await JsonSerializer.DeserializeAsync<Project>(file);

		// Assert
		Assert.NotNull(savedProject);
		Assert.Equal(versionConfig.CurrentSchemaVersion, savedProject.SchemaVersion);
		Assert.Equal("Old Version Project", savedProject.Name);
	}

	[Fact]
	public async Task SaveProjectAsync_UpdatesModifiedDate()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "modified_date.linq");
		var originalDate = DateTimeOffset.UtcNow.AddDays(-1);

		var project = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Date Test",
			ConnectionString = "Server=localhost;",
			CreatedDate = originalDate,
			ModifiedDate = originalDate,
			SchemaVersion = 1
		};

		// Act
		await Task.Delay(10); // Ensure time passes
		await service.SaveProjectAsync(project, filePath);

		var savedProject = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(savedProject);
		Assert.True(savedProject.ModifiedDate > originalDate);
		Assert.Equal(originalDate, savedProject.CreatedDate); // CreatedDate should not change
	}

	[Fact]
	public async Task SaveProjectAsync_OverwritesExistingFile()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "overwrite.linq");

		var project1 = service.CreateNew("First Version", "Connection1");
		var project2 = project1 with { Name = "Second Version", ConnectionString = "Connection2" };

		// Act
		await service.SaveProjectAsync(project1, filePath);
		await service.SaveProjectAsync(project2, filePath);

		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal("Second Version", loaded.Name);
		Assert.Equal("Connection2", loaded.ConnectionString);
	}

	[Fact]
	public async Task SaveProjectAsync_PreservesOptionalProperties()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "optional_props.linq");

		var project = service.CreateNew("Test", "Server=localhost;");
		var projectWithOptionals = project with
		{
			Models = new Dictionary<string, string> { ["Person.cs"] = "public class Person { }" },
			DbContextCode = "public class TestContext : DbContext { }",
			Queries = new List<SavedQuery>
			{
				new()
				{
					Name = "Test Query",
					QueryText = "context.People.ToList()",
					CreatedDate = DateTimeOffset.UtcNow
				}
			}
		};

		// Act
		await service.SaveProjectAsync(projectWithOptionals, filePath);
		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.NotNull(loaded.Models);
		Assert.Single(loaded.Models);
		Assert.Equal("public class Person { }", loaded.Models["Person.cs"]);
		Assert.Equal("public class TestContext : DbContext { }", loaded.DbContextCode);
		Assert.NotNull(loaded.Queries);
		Assert.Single(loaded.Queries);
		Assert.Equal("Test Query", loaded.Queries[0].Name);
	}

	[Fact]
	public async Task SaveProjectAsync_WithInvalidPath_ThrowsException()
	{
		// Arrange
		var service = new ProjectService();
		var invalidPath = Path.Combine(_testDirectory, "nonexistent_folder", "test.linq");
		var project = service.CreateNew("Test", "Connection");

		// Act & Assert
		await Assert.ThrowsAsync<DirectoryNotFoundException>(
			() => service.SaveProjectAsync(project, invalidPath)
		);
	}

	#endregion

	#region LoadProjectAsync Tests

	[Fact]
	public async Task LoadProjectAsync_ReturnsNull_WhenFileDoesNotExist()
	{
		// Arrange
		var service = new ProjectService();
		var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.linq");

		// Act
		var result = await service.LoadProjectAsync(nonExistentPath);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public async Task LoadProjectAsync_LoadsValidProject()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "valid_project.linq");

		var project = service.CreateNew("Valid Project", "Server=localhost;Database=MyDb;");
		await service.SaveProjectAsync(project, filePath);

		// Act
		var loadedProject = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loadedProject);
		Assert.Equal(project.Id, loadedProject.Id);
		Assert.Equal("Valid Project", loadedProject.Name);
		Assert.Equal("Server=localhost;Database=MyDb;", loadedProject.ConnectionString);
		Assert.Equal(project.SchemaVersion, loadedProject.SchemaVersion);
	}

	[Fact]
	public async Task LoadProjectAsync_PreservesAllProperties()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "full_project.linq");

		var originalProject = service.CreateNew("Full Project", "Server=localhost;");
		var fullProject = originalProject with
		{
			Models = new Dictionary<string, string>
			{
				["Person.cs"] = "namespace Test; public class Person { public int Id { get; set; } }",
				["Order.cs"] = "namespace Test; public class Order { public int Id { get; set; } }"
			},
			DbContextCode = "using Microsoft.EntityFrameworkCore; public class MyContext : DbContext { }",
			Queries = new List<SavedQuery>
			{
				new() { Name = "Query 1", QueryText = "context.People.ToList()", CreatedDate = DateTimeOffset.UtcNow },
				new() { Name = "Query 2", QueryText = "context.Orders.Where(o => o.Id > 5)", CreatedDate = DateTimeOffset.UtcNow }
			}
		};

		await service.SaveProjectAsync(fullProject, filePath);

		// Act
		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(fullProject.Id, loaded.Id);
		Assert.Equal(fullProject.Name, loaded.Name);
		Assert.NotNull(loaded.Models);
		Assert.Equal(2, loaded.Models.Count);
		Assert.Contains("Person.cs", loaded.Models.Keys);
		Assert.Contains("Order.cs", loaded.Models.Keys);
		Assert.NotNull(loaded.Queries);
		Assert.Equal(2, loaded.Queries.Count);
		Assert.Equal("Query 1", loaded.Queries[0].Name);
		Assert.Equal("Query 2", loaded.Queries[1].Name);
	}

	#endregion

	#region Version Compatibility Tests

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WhenVersionTooNew()
	{
		// Arrange
		var versionConfig = new ProjectVersionConfig(currentVersion: 1, minVersion: 1);
		var service = new ProjectService(versionConfig);
		var filePath = Path.Combine(_testDirectory, "future_project.linq");

		// Create a project with future version directly in JSON
		var futureProject = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Future Project",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 999 // Future version
		};

		await using (var file = File.Create(filePath))
		{
			await JsonSerializer.SerializeAsync(file, futureProject);
		}

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("requires LinqStudio v999", exception.Message);
		Assert.Contains("supports up to v1", exception.Message);
	}

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WhenVersionTooOld()
	{
		// Arrange
		var versionConfig = new ProjectVersionConfig(currentVersion: 5, minVersion: 3);
		var service = new ProjectService(versionConfig);
		var filePath = Path.Combine(_testDirectory, "old_project.linq");

		// Create a project with old version
		var oldProject = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Old Project",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 1 // Too old
		};

		await using (var file = File.Create(filePath))
		{
			await JsonSerializer.SerializeAsync(file, oldProject);
		}

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("version 1 is too old", exception.Message);
		Assert.Contains("Minimum supported version is 3", exception.Message);
	}

	[Fact]
	public async Task LoadProjectAsync_AcceptsVersionWithinSupportedRange()
	{
		// Arrange
		var versionConfig = new ProjectVersionConfig(currentVersion: 5, minVersion: 3);
		var service = new ProjectService(versionConfig);
		var filePath = Path.Combine(_testDirectory, "compatible_project.linq");

		// Create project with version 4 (within supported range 3-5)
		var project = new Project
		{
			Id = Guid.NewGuid(),
			Name = "Compatible Project",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 4
		};

		await using (var file = File.Create(filePath))
		{
			await JsonSerializer.SerializeAsync(file, project);
		}

		// Act
		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal("Compatible Project", loaded.Name);
		Assert.Equal(4, loaded.SchemaVersion);
	}

	#endregion

	#region Concurrency Tests

	[Fact]
	public async Task SaveProjectAsync_ConcurrentCalls_AreHandledSafely()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "concurrent_save.linq");

		var projects = Enumerable.Range(1, 5)
			.Select(i => service.CreateNew($"Project {i}", $"Connection {i}"))
			.ToArray();

		// Act - Save multiple projects concurrently to the same file
		var tasks = projects.Select(p => service.SaveProjectAsync(p, filePath)).ToArray();
		await Task.WhenAll(tasks);

		// Assert - File should exist and contain one of the projects
		Assert.True(File.Exists(filePath));
		var loaded = await service.LoadProjectAsync(filePath);
		Assert.NotNull(loaded);
		Assert.Matches(@"Project \d", loaded.Name);
	}

	[Fact]
	public async Task LoadProjectAsync_ConcurrentCalls_AreHandledSafely()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "concurrent_load.linq");

		var project = service.CreateNew("Concurrent Load Test", "Server=localhost;");
		await service.SaveProjectAsync(project, filePath);

		// Act - Load the same file concurrently
		var tasks = Enumerable.Range(1, 10)
			.Select(_ => service.LoadProjectAsync(filePath))
			.ToArray();
		var results = await Task.WhenAll(tasks);

		// Assert - All loads should succeed
		Assert.All(results, loaded =>
		{
			Assert.NotNull(loaded);
			Assert.Equal("Concurrent Load Test", loaded.Name);
			Assert.Equal(project.Id, loaded.Id);
		});
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task LoadProjectAsync_WithCorruptedJson_ThrowsInvalidOperationException()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "corrupted.linq");

		await File.WriteAllTextAsync(filePath, "{ invalid json content }");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("corrupted or in an invalid format", exception.Message);
		Assert.NotNull(exception.InnerException);
		Assert.IsType<JsonException>(exception.InnerException);
	}

	[Fact]
	public async Task SaveProjectAsync_WithVeryLongName_Succeeds()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "long_name.linq");
		var longName = new string('A', 1000);

		var project = service.CreateNew(longName, "Connection");

		// Act
		await service.SaveProjectAsync(project, filePath);
		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(longName, loaded.Name);
	}

	[Fact]
	public async Task SaveProjectAsync_WithSpecialCharactersInProperties_Succeeds()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "special_chars.linq");

		var project = service.CreateNew(
			"Test \"Project\" with 'quotes' and\nnewlines\ttabs",
			"Server=localhost;Password=P@ssw0rd!#$%"
		);

		// Act
		await service.SaveProjectAsync(project, filePath);
		var loaded = await service.LoadProjectAsync(filePath);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(project.Name, loaded.Name);
		Assert.Equal(project.ConnectionString, loaded.ConnectionString);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WithEmptyFile()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "empty.linq");
		await File.WriteAllTextAsync(filePath, string.Empty);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("empty or corrupted", exception.Message);
	}

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WithInvalidJson()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "invalid.linq");
		await File.WriteAllTextAsync(filePath, "{ invalid json !!!");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("corrupted or in an invalid format", exception.Message);
		Assert.NotNull(exception.InnerException);
		Assert.IsType<JsonException>(exception.InnerException);
	}

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WithMissingRequiredFields()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "missing_fields.linq");

		// Create invalid JSON with missing required fields
		var invalidJson = """
		{
			"SchemaVersion": 1,
			"Id": "00000000-0000-0000-0000-000000000000",
			"Name": "",
			"ConnectionString": "Server=localhost;"
		}
		""";

		await File.WriteAllTextAsync(filePath, invalidJson);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("invalid", exception.Message);
	}

	[Fact]
	public async Task SaveProjectAsync_ThrowsException_WithInvalidProject()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "invalid_save.linq");

		var invalidProject = new Project
		{
			Id = Guid.Empty, // Invalid
			Name = "Test",
			ConnectionString = "Server=localhost;",
			CreatedDate = DateTimeOffset.UtcNow,
			ModifiedDate = DateTimeOffset.UtcNow,
			SchemaVersion = 1
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.SaveProjectAsync(invalidProject, filePath)
		);

		Assert.Contains("invalid project ID", exception.Message);
	}

	[Fact]
	public async Task LoadProjectAsync_ThrowsException_WithNullJsonContent()
	{
		// Arrange
		var service = new ProjectService();
		var filePath = Path.Combine(_testDirectory, "null_content.linq");
		await File.WriteAllTextAsync(filePath, "null");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.LoadProjectAsync(filePath)
		);

		Assert.Contains("could not be loaded", exception.Message);
	}

	#endregion

	#region IDisposable Implementation

	public void Dispose()
	{
		// Cleanup test directory after each test
		if (Directory.Exists(_testDirectory))
		{
			try
			{
				Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors in tests
			}
		}
	}

	#endregion
}
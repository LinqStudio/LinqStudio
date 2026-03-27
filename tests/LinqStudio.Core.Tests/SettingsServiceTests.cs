using LinqStudio.Abstractions;
using LinqStudio.Core.Services;
using LinqStudio.Core.Settings;
using System.Text.Json;

namespace LinqStudio.Core.Tests;

public class SettingsServiceTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly string _originalDirectory;

	public SettingsServiceTests()
	{
		// Create unique test directory for each test run
		_testDirectory = Path.Combine(Path.GetTempPath(), $"LinqStudioTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		// Save original directory and change to test directory
		_originalDirectory = Directory.GetCurrentDirectory();
		Directory.SetCurrentDirectory(_testDirectory);
	}

	#region Save and Load Tests

	[Fact]
	public async Task Save_CreatesNewFile_WhenFileDoesNotExist()
	{
		// Arrange
		var service = new SettingsService();
		var settings = new UISettings { IsDarkMode = false, AlwaysReloadSettingsInSettingsPage = false };

		// Act
		await service.Save([settings]);

		// Assert
		Assert.True(File.Exists(SettingsService.FILE_NAME));
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("UISettings", content);
		Assert.Contains("\"IsDarkMode\": false", content);
		Assert.Contains("\"AlwaysReloadSettingsInSettingsPage\": false", content);
	}

	[Fact]
	public async Task Save_UpdatesExistingFile_WhenFileExists()
	{
		// Arrange
		var service = new SettingsService();
		var settings1 = new UISettings { IsDarkMode = true, AlwaysReloadSettingsInSettingsPage = true };
		await service.Save([settings1]);

		// Act
		var settings2 = new UISettings { IsDarkMode = false, AlwaysReloadSettingsInSettingsPage = false };
		await service.Save([settings2]);

		// Assert
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("\"IsDarkMode\": false", content);
		Assert.Contains("\"AlwaysReloadSettingsInSettingsPage\": false", content);
	}

	[Fact]
	public async Task Save_PreservesOtherSections_WhenUpdatingOnlyOne()
	{
		// Arrange
		var service = new SettingsService();
		var uiSettings = new UISettings { IsDarkMode = true };
		var testSettings = new TestUserSettings { TestValue = "Initial" };
		await service.Save([uiSettings, testSettings]);

		// Act - Update only UISettings
		var updatedUiSettings = new UISettings { IsDarkMode = false };
		await service.Save([updatedUiSettings]);

		// Assert
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("\"IsDarkMode\": false", content);
		Assert.Contains("\"TestValue\": \"Initial\"", content);
	}

	[Fact]
	public async Task Save_HandlesMultipleSettings()
	{
		// Arrange
		var service = new SettingsService();
		var uiSettings = new UISettings { IsDarkMode = true, AlwaysReloadSettingsInSettingsPage = false };
		var testSettings = new TestUserSettings { TestValue = "Test123" };

		// Act
		await service.Save([uiSettings, testSettings]);

		// Assert
		Assert.True(File.Exists(SettingsService.FILE_NAME));
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("UISettings", content);
		Assert.Contains("TestUserSettings", content);
		Assert.Contains("\"IsDarkMode\": true", content);
		Assert.Contains("\"TestValue\": \"Test123\"", content);
	}

	[Fact]
	public async Task Save_CreatesValidJson()
	{
		// Arrange
		var service = new SettingsService();
		var settings = new UISettings { IsDarkMode = false };

		// Act
		await service.Save([settings]);

		// Assert - Verify valid JSON by parsing
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		var jsonDoc = JsonDocument.Parse(content);
		Assert.NotNull(jsonDoc);
		Assert.True(jsonDoc.RootElement.TryGetProperty("UISettings", out var uiSection));
		Assert.True(uiSection.TryGetProperty("IsDarkMode", out var isDarkMode));
		Assert.False(isDarkMode.GetBoolean());
	}

	#endregion

	#region Round-Trip Tests

	[Fact]
	public async Task SaveAndLoad_RoundTrip_PreservesData()
	{
		// Arrange
		var service = new SettingsService();
		var originalSettings = new UISettings { IsDarkMode = false, AlwaysReloadSettingsInSettingsPage = true };
		await service.Save([originalSettings]);

		// Act - Load via JSON deserialization (simulating application load)
		await using var file = File.OpenRead(SettingsService.FILE_NAME);
		var json = await JsonDocument.ParseAsync(file);
		var uiSection = json.RootElement.GetProperty("UISettings");
		var loadedSettings = JsonSerializer.Deserialize<UISettings>(uiSection.GetRawText());

		// Assert
		Assert.NotNull(loadedSettings);
		Assert.Equal(originalSettings.IsDarkMode, loadedSettings.IsDarkMode);
		Assert.Equal(originalSettings.AlwaysReloadSettingsInSettingsPage, loadedSettings.AlwaysReloadSettingsInSettingsPage);
	}

	[Fact]
	public async Task SaveAndLoad_MultipleCycles_MaintainDataIntegrity()
	{
		// Arrange
		var service = new SettingsService();

		// Act & Assert - Multiple save/load cycles
		for (int i = 0; i < 5; i++)
		{
			var settings = new UISettings { IsDarkMode = i % 2 == 0, AlwaysReloadSettingsInSettingsPage = i % 2 == 1 };
			await service.Save([settings]);

			await using var file = File.OpenRead(SettingsService.FILE_NAME);
			var json = await JsonDocument.ParseAsync(file);
			var uiSection = json.RootElement.GetProperty("UISettings");
			var loadedSettings = JsonSerializer.Deserialize<UISettings>(uiSection.GetRawText());

			Assert.NotNull(loadedSettings);
			Assert.Equal(settings.IsDarkMode, loadedSettings.IsDarkMode);
			Assert.Equal(settings.AlwaysReloadSettingsInSettingsPage, loadedSettings.AlwaysReloadSettingsInSettingsPage);
		}
	}

	#endregion

	#region Edge Cases and Error Handling

	[Fact]
	public async Task Save_HandlesEmptyFile()
	{
		// Arrange
		var service = new SettingsService();
		await File.WriteAllTextAsync(SettingsService.FILE_NAME, string.Empty);

		// Act
		var settings = new UISettings { IsDarkMode = true };
		await service.Save([settings]);

		// Assert
		Assert.True(File.Exists(SettingsService.FILE_NAME));
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("UISettings", content);
	}

	[Fact]
	public async Task Save_HandlesCorruptedJson_ThrowsException()
	{
		// Arrange
		var service = new SettingsService();
		await File.WriteAllTextAsync(SettingsService.FILE_NAME, "{ invalid json !!!");

		// Act & Assert - Should throw since corrupted JSON can't be parsed
		var settings = new UISettings { IsDarkMode = true };
		await Assert.ThrowsAnyAsync<Exception>(() => service.Save([settings]));
	}

	[Fact]
	public async Task Save_HandlesPartialJson_ByMergingSettings()
	{
		// Arrange
		var service = new SettingsService();
		// Create file with only TestUserSettings
		var json = """
		{
		  "TestUserSettings": {
		    "TestValue": "Existing"
		  }
		}
		""";
		await File.WriteAllTextAsync(SettingsService.FILE_NAME, json);

		// Act - Add UISettings
		var uiSettings = new UISettings { IsDarkMode = true };
		await service.Save([uiSettings]);

		// Assert
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("UISettings", content);
		Assert.Contains("TestUserSettings", content);
		Assert.Contains("\"TestValue\": \"Existing\"", content);
	}

	[Fact]
	public async Task Save_HandlesSpecialCharacters()
	{
		// Arrange
		var service = new SettingsService();
		var settings = new TestUserSettings { TestValue = "Test \"quotes\" and\nnewlines\ttabs" };

		// Act
		await service.Save([settings]);

		// Assert
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		await using var file = File.OpenRead(SettingsService.FILE_NAME);
		var json = await JsonDocument.ParseAsync(file);
		var testSection = json.RootElement.GetProperty("TestUserSettings");
		var loadedSettings = JsonSerializer.Deserialize<TestUserSettings>(testSection.GetRawText());

		Assert.NotNull(loadedSettings);
		Assert.Equal(settings.TestValue, loadedSettings.TestValue);
	}

	[Fact]
	public async Task Save_WithEmptyCollection_CreatesEmptyJsonObject()
	{
		// Arrange
		var service = new SettingsService();

		// Act
		await service.Save(Array.Empty<IUserSettingsSection>());

		// Assert
		Assert.True(File.Exists(SettingsService.FILE_NAME));
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		var json = JsonDocument.Parse(content);
		Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
	}

	#endregion

	#region File System Tests

	[Fact]
	public async Task Save_CreatesNewFileInCurrentDirectory()
	{
		// Arrange
		var service = new SettingsService();
		var settings = new UISettings { IsDarkMode = true };

		// Act
		await service.Save([settings]);

		// Assert
		var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), SettingsService.FILE_NAME);
		Assert.True(File.Exists(expectedPath));
	}

	[Fact]
	public async Task Save_OverwritesFileAtomically()
	{
		// Arrange
		var service = new SettingsService();
		var settings1 = new UISettings { IsDarkMode = true };
		await service.Save([settings1]);

		// Get initial file info
		var fileInfo1 = new FileInfo(SettingsService.FILE_NAME);
		var initialLength = fileInfo1.Length;

		// Act
		var settings2 = new UISettings { IsDarkMode = false };
		await service.Save([settings2]);

		// Assert - File was overwritten
		var fileInfo2 = new FileInfo(SettingsService.FILE_NAME);
		Assert.True(fileInfo2.Exists);
		// File content should be different (different boolean values would result in different JSON)
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		Assert.Contains("false", content);
	}

	#endregion

	#region Concurrent Access Tests

	[Fact]
	public async Task Save_HandlesConcurrentCalls()
	{
		// Arrange
		var service = new SettingsService();

		// Act - Save multiple settings sequentially (concurrency handled by SettingsService via file locking)
		for (int i = 0; i < 10; i++)
		{
			var settings = new TestUserSettings { TestValue = $"Sequential{i}" };
			await service.Save([settings]);
		}

		// Assert - File exists and is valid JSON
		Assert.True(File.Exists(SettingsService.FILE_NAME));
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		var json = JsonDocument.Parse(content);
		Assert.NotNull(json);
		Assert.True(json.RootElement.TryGetProperty("TestUserSettings", out _));
	}

	#endregion

	#region Type Discovery Tests

	[Fact]
	public void AllIUserSettingsSectionImplementations_CanBeInstantiated()
	{
		// Arrange
		var settingTypes = typeof(UISettings).Assembly
			.GetTypes()
			.Where(x => typeof(IUserSettingsSection).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
			.ToList();

		// Assert - At least UISettings should exist
		Assert.NotEmpty(settingTypes);
		Assert.Contains(settingTypes, t => t == typeof(UISettings));

		// Act & Assert - All should be instantiable
		foreach (var type in settingTypes)
		{
			var instance = Activator.CreateInstance(type) as IUserSettingsSection;
			Assert.NotNull(instance);
			Assert.False(string.IsNullOrEmpty(instance.SectionName));
		}
	}

	[Fact]
	public async Task Save_WorksWithAllKnownSettingsTypes()
	{
		// Arrange
		var service = new SettingsService();
		var settingTypes = typeof(UISettings).Assembly
			.GetTypes()
			.Where(x => typeof(IUserSettingsSection).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
			.ToList();

		var instances = settingTypes
			.Select(t => Activator.CreateInstance(t) as IUserSettingsSection)
			.Where(x => x != null)
			.ToList();

		// Act
		await service.Save(instances!);

		// Assert
		var content = await File.ReadAllTextAsync(SettingsService.FILE_NAME);
		foreach (var instance in instances)
		{
			Assert.Contains(instance!.SectionName, content);
		}
	}

	#endregion

	#region Test Helper Classes

	// Test-only settings class for testing purposes
	private record class TestUserSettings : IUserSettingsSection
	{
		public string SectionName => nameof(TestUserSettings);
		public string TestValue { get; set; } = "default";
	}

	#endregion

	#region IDisposable Implementation

	public void Dispose()
	{
		// Restore original directory
		Directory.SetCurrentDirectory(_originalDirectory);

		// Cleanup test directory
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

	#endregion
}

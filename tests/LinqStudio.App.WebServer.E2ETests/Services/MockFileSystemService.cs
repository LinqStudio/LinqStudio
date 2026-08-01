namespace LinqStudio.App.WebServer.E2ETests.Services;

/// <summary>
/// Test helper that manages a temporary directory for E2E test file storage.
/// Used to configure FileSystemStorageOptions.BasePath so repositories write to a temp dir.
/// </summary>
public class MockFileSystemService
{
	private readonly string _testFilesDirectory;

	public MockFileSystemService()
	{
		// Create a temp directory for test files
		_testFilesDirectory = Path.Combine(Path.GetTempPath(), "LinqStudio.E2ETests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_testFilesDirectory);
	}

	/// <summary>
	/// Creates a test file with the given content and returns its path.
	/// </summary>
	public string CreateTestFile(string fileName, string content)
	{
		var filePath = Path.Combine(_testFilesDirectory, fileName);
		File.WriteAllText(filePath, content);
		return filePath;
	}

	/// <summary>
	/// Gets the content of a test file.
	/// </summary>
	public string ReadTestFile(string fileName)
	{
		var filePath = Path.Combine(_testFilesDirectory, fileName);
		return File.ReadAllText(filePath);
	}

	/// <summary>
	/// Checks if a test file exists.
	/// </summary>
	public bool TestFileExists(string fileName)
	{
		var filePath = Path.Combine(_testFilesDirectory, fileName);
		return File.Exists(filePath);
	}

	/// <summary>
	/// Gets the test files directory path.
	/// </summary>
	public string GetTestFilesDirectory()
	{
		return _testFilesDirectory;
	}

	/// <summary>
	/// Cleans up test files.
	/// </summary>
	public void Cleanup()
	{
		try
		{
			if (Directory.Exists(_testFilesDirectory))
			{
				Directory.Delete(_testFilesDirectory, recursive: true);
			}
		}
		catch
		{
			// Ignore cleanup errors
		}
	}
}
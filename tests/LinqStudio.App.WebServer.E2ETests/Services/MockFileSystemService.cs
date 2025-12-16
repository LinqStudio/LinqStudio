using LinqStudio.Blazor.Abstractions;

namespace LinqStudio.App.WebServer.E2ETests.Services;

/// <summary>
/// Mock file system service for E2E testing that simulates file dialogs without UI.
/// </summary>
public class MockFileSystemService : IFileSystemService
{
	private readonly string _testFilesDirectory;
	private string? _nextOpenFileResult;
	private string? _nextSaveFileResult;

	public MockFileSystemService()
	{
		// Create a temp directory for test files
		_testFilesDirectory = Path.Combine(Path.GetTempPath(), "LinqStudio.E2ETests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_testFilesDirectory);
	}

	/// <summary>
	/// Sets the result that will be returned by the next call to PromptOpenFileAsync.
	/// </summary>
	public void SetNextOpenFileResult(string fileName)
	{
		_nextOpenFileResult = Path.Combine(_testFilesDirectory, fileName);
	}

	/// <summary>
	/// Sets the result that will be returned by the next call to PromptSaveFileAsync.
	/// </summary>
	public void SetNextSaveFileResult(string fileName)
	{
		_nextSaveFileResult = Path.Combine(_testFilesDirectory, fileName);
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

	public Task<string?> PromptOpenFileAsync(string fileExtension = ".linq", string? defaultPath = null)
	{
		// Return the pre-configured result (simulates user selecting a file)
		var result = _nextOpenFileResult;
		_nextOpenFileResult = null; // Reset after use
		return Task.FromResult(result);
	}

	public Task<string?> PromptSaveFileAsync(string defaultFileName, string? defaultPath = null)
	{
		// If a specific result is set, use it
		if (_nextSaveFileResult != null)
		{
			var result = _nextSaveFileResult;
			_nextSaveFileResult = null; // Reset after use
			return Task.FromResult<string?>(result);
		}

		// Otherwise, auto-generate a file path in the test directory
		var filePath = Path.Combine(_testFilesDirectory, defaultFileName);
		if (!filePath.EndsWith(".linq", StringComparison.OrdinalIgnoreCase))
		{
			filePath += ".linq";
		}
		return Task.FromResult<string?>(filePath);
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
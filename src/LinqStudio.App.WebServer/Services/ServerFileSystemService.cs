using LinqStudio.Blazor.Abstractions;

namespace LinqStudio.App.WebServer.Services;

/// <summary>
/// Server-side implementation of file system operations.
/// Uses direct file system access available in Blazor Server.
/// </summary>
public class ServerFileSystemService : IFileSystemService
{
	private readonly string _defaultProjectsPath;

	public ServerFileSystemService()
	{
		_defaultProjectsPath = GetDefaultProjectsPath();
	}

	public Task<string?> PromptOpenFileAsync(string fileExtension = ".linq", string? defaultPath = null)
	{
		// Use default projects path if none specified
		var path = defaultPath ?? _defaultProjectsPath;
		return Task.FromResult<string?>(path);
	}

	public Task<string?> PromptSaveFileAsync(string defaultFileName, string? defaultPath = null)
	{
		// Combine default path with filename
		var path = defaultPath ?? _defaultProjectsPath;
		var fullPath = Path.Combine(path, defaultFileName);
		return Task.FromResult<string?>(fullPath);
	}

	public string GetDefaultDocumentsPath()
	{
		return _defaultProjectsPath;
	}

	/// <summary>
	/// Gets the default LinqStudio projects directory path.
	/// If ~/Documents/LinqStudio/ exists, use it. Otherwise, use ~/Documents/
	/// If the ~/Documents/ folder doesn't exist, fall back to the Current Directory.
	/// </summary>
	private static string GetDefaultProjectsPath()
	{
		// Get user's Documents folder (cross-platform)
		var documentsPath = Environment.GetFolderPath(
			Environment.SpecialFolder.MyDocuments,
			Environment.SpecialFolderOption.DoNotVerify);

		// Fallback to user profile if Documents doesn't exist
		if (string.IsNullOrEmpty(documentsPath))
		{
			documentsPath = Environment.GetFolderPath(
				Environment.SpecialFolder.UserProfile,
				Environment.SpecialFolderOption.DoNotVerify);
		}

		// Last resort: use current directory
		if (string.IsNullOrEmpty(documentsPath))
		{
			documentsPath = Directory.GetCurrentDirectory();
		}

		// Check if LinqStudio subfolder exists
		var linqStudioPath = Path.Combine(documentsPath, "LinqStudio");

		// Use LinqStudio folder if it exists, otherwise use Documents directly
		return Directory.Exists(linqStudioPath) ? linqStudioPath : documentsPath;
	}
}
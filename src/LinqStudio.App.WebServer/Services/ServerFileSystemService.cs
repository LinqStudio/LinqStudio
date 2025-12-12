using LinqStudio.Blazor.Abstractions;
using NativeFileDialogSharp;

namespace LinqStudio.App.WebServer.Services;

/// <summary>
/// Server-side implementation of file system operations.
/// Uses native file dialogs via NativeFileDialogSharp (cross-platform).
/// </summary>
public class ServerFileSystemService : IFileSystemService
{
	private readonly string _defaultProjectsPath;

	public ServerFileSystemService()
	{
		_defaultProjectsPath = GetDefaultProjectsPath();
	}

	public async Task<string?> PromptOpenFileAsync(string fileExtension = ".linq", string? defaultPath = null)
	{
		// Run native dialog on background thread to avoid blocking UI
		return await Task.Run(() =>
		{
			var dialog = Dialog.FileOpen(fileExtension, defaultPath ?? _defaultProjectsPath);

			if (dialog.IsOk)
			{
				return dialog.Path;
			}

			return null;
		});
	}

	public async Task<string?> PromptSaveFileAsync(string defaultFileName, string? defaultPath = null)
	{
		return await Task.Run(() =>
		{
			var dialog = Dialog.FileSave("linq", defaultPath ?? _defaultProjectsPath);

			if (dialog.IsOk)
			{
				var path = dialog.Path;
				// Ensure .linq extension
				if (!path.EndsWith(".linq", StringComparison.OrdinalIgnoreCase))
				{
					path += ".linq";
				}
				return path;
			}

			return null;
		});
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
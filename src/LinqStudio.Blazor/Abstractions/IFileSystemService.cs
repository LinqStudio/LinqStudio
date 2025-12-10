namespace LinqStudio.Blazor.Abstractions;

/// <summary>
/// Abstraction for file system operations to support both Blazor Server and WebAssembly.
/// This is a UI-layer service for handling file picker interactions.
/// </summary>
public interface IFileSystemService
{
	/// <summary>
	/// Prompts user to select a file path for opening.
	/// </summary>
	Task<string?> PromptOpenFileAsync(string fileExtension = ".linq", string? defaultPath = null);

	/// <summary>
	/// Prompts user to select a file path for saving.
	/// </summary>
	Task<string?> PromptSaveFileAsync(string defaultFileName, string? defaultPath = null);

	/// <summary>
	/// Gets the default documents folder path.
	/// </summary>
	string GetDefaultDocumentsPath();
}
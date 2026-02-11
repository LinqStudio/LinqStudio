namespace LinqStudio.Blazor.Constants;

public static class FileExtensions
{
	public const string Project = "linq";
	public const string Query = "linquery";

	public static string WithDot(string extension) => $".{extension}";

	public static string EnsureHasExtension(string fileName, string extension)
	{
		var extWithDot = WithDot(extension);
		return fileName.EndsWith(extWithDot, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + extWithDot;
	}
}

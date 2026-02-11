namespace LinqStudio.Blazor.Constants;

public static class FileExtensions
{
	public const string Project = "linq";
	public const string Query = "linquery";

	public static string WithDot(this string extension)
	{
		if (string.IsNullOrWhiteSpace(extension))
		{
			return string.Empty;
		}

		extension = extension.Trim();
		extension = extension.TrimStart('.');
		return string.IsNullOrWhiteSpace(extension) ? string.Empty : $".{extension}";
	}

	public static string EnsureHasExtension(this string fileName, string extension)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}

		var extWithDot = extension.WithDot();
		if (string.IsNullOrEmpty(extWithDot))
		{
			return fileName;
		}

		fileName = fileName.TrimEnd();
		if (fileName.EndsWith(extWithDot, StringComparison.OrdinalIgnoreCase))
		{
			return fileName;
		}

		fileName = fileName.TrimEnd('.');
		return fileName + extWithDot;
	}
}

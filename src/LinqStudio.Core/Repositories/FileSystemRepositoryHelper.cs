namespace LinqStudio.Core.Repositories;

/// <summary>
/// Shared path-validation utilities used by file-system repository implementations.
/// All methods are pure and stateless; no I/O is performed.
/// </summary>
internal static class FileSystemRepositoryHelper
{
	/// <summary>
	/// Suffix appended to a project file path to derive its queries directory.
	/// Example: "MyProject.linq" → "MyProject.linq.queries/"
	/// </summary>
	internal const string QueriesDirectorySuffix = ".queries";

	/// <summary>
	/// Returns the absolute, validated path for a resource identified by <paramref name="id"/>
	/// inside <paramref name="basePath"/>.
	/// </summary>
	/// <remarks>
	/// Two-stage path-traversal guard:
	/// <list type="number">
	///   <item>
	///     <description>
	///       <c>Path.GetFileName(id) != id</c> — rejects any <paramref name="id"/> that already
	///       contains a directory separator (e.g. <c>../secret</c> or <c>sub/file</c>).
	///       This catches the most obvious traversal attempts before touching the file system.
	///     </description>
	///   </item>
	///   <item>
	///     <description>
	///       The combined path is resolved with <c>Path.GetFullPath</c> (which normalises
	///       <c>..</c>, symlinks, and redundant separators) and then checked with
	///       <c>StartsWith(fullBase + DirectorySeparatorChar)</c>. The trailing separator is
	///       critical: without it, a base of <c>/data/foo</c> would incorrectly allow
	///       <c>/data/foobar/secret</c> because that string does start with <c>/data/foo</c>.
	///     </description>
	///   </item>
	/// </list>
	/// Together the two checks prevent path-traversal attacks regardless of OS conventions
	/// or unusual input encoding.
	/// </remarks>
	/// <param name="basePath">The root directory that all resolved paths must remain inside.</param>
	/// <param name="id">
	/// A plain filename component (no directory separators). Used as the stem of the resulting path.
	/// </param>
	/// <param name="extension">
	/// File extension to append (including the leading dot, e.g. <c>".linq"</c>).
	/// </param>
	/// <returns>The resolved absolute path: <c>{basePath}/{id}{extension}</c>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="id"/> contains path separators, resolves outside
	/// <paramref name="basePath"/>, or is otherwise invalid.
	/// </exception>
	internal static string GetValidatedPath(string basePath, string id, string extension)
	{
		// First check: reject any id that is not a plain filename (contains / or \ or is empty).
		if (Path.GetFileName(id) != id)
			throw new ArgumentException($"Invalid ID '{id}'.", nameof(id));

		var fullBase = Path.GetFullPath(basePath);
		var fullPath = Path.GetFullPath(Path.Combine(fullBase, $"{id}{extension}"));

		// Second check: after full normalisation, confirm the resolved path is still inside basePath.
		// The trailing DirectorySeparatorChar prevents "/base/foo" from matching "/base/foobar".
		if (!fullPath.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException($"Invalid ID '{id}'.", nameof(id));

		return fullPath;
	}
}

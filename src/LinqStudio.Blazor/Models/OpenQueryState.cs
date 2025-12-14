namespace LinqStudio.Blazor.Models;

/// <summary>
/// Represents the state of an open query in the editor.
/// </summary>
public class OpenQueryState
{
	/// <summary>
	/// Index of the query in the project's query list.
	/// </summary>
	public int QueryIndex { get; set; }

	/// <summary>
	/// Whether this query has unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges { get; set; }

	/// <summary>
	/// The current text in the editor (may differ from saved text).
	/// </summary>
	public string CurrentText { get; set; } = string.Empty;

	/// <summary>
	/// When this query was last modified in the editor.
	/// </summary>
	public DateTimeOffset LastModified { get; set; }
}
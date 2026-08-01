namespace LinqStudio.Blazor.Models;

/// <summary>
/// Represents the state of an open query in the editor.
/// </summary>
public class OpenQueryState
{
	/// <summary>
	/// Unique identifier for the query.
	/// </summary>
	public Guid QueryId { get; set; }

	/// <summary>
	/// The current text in the editor (may differ from saved text).
	/// </summary>
	public string CurrentText { get; set; } = string.Empty;

	/// <summary>
	/// Whether this query has unsaved changes.
	/// </summary>
	public bool HasUnsavedChanges { get; set; }

	/// <summary>
	/// Whether the query should execute once after its editor has initialized.
	/// </summary>
	public bool ExecuteOnOpen { get; set; }

	/// <summary>
	/// When this query was last modified in the editor.
	/// </summary>
	public DateTimeOffset LastModified { get; set; }
}
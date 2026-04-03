namespace LinqStudio.Core.Models;

/// <summary>
/// Represents a LINQ query saved to disk, tracking both its content and
/// the file path it was last persisted to.
/// </summary>
/// <remarks>
/// <c>Id</c> and <c>CreatedDate</c> are domain-identity properties (init-only).
/// <c>FilePath</c> is a persistence detail set by <see cref="Services.QueryService"/>
/// after a successful save and is <see langword="null"/> until the query has been
/// written to disk at least once.
/// </remarks>
public class SavedQuery
{
	/// <summary>
	/// Unique identifier for this query. Initialized to a new <see cref="Guid"/> on construction.
	/// </summary>
	public Guid Id { get; init; } = Guid.NewGuid();

	/// <summary>
	/// User-visible display name of the query.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The LINQ/C# query code written by the user.
	/// </summary>
	public string QueryText { get; set; } = string.Empty;

	/// <summary>
	/// UTC timestamp when the query was first created. Immutable after construction (init-only).
	/// </summary>
	public DateTimeOffset CreatedDate { get; init; }

	/// <summary>
	/// The ID of the <see cref="ServerConnection"/> this query runs against.
	/// When <see langword="null"/> the first connection in the project is used as a fallback.
	/// </summary>
	public Guid? ConnectionId { get; set; }

	/// <summary>
	/// File path where this query is saved. <see langword="null"/> if the query has never been persisted to disk.
	/// </summary>
	public string? FilePath { get; set; }
}
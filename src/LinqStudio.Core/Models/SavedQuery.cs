public class SavedQuery
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string QueryText { get; set; } = string.Empty;
	public DateTimeOffset CreatedDate { get; init; }
	
	/// <summary>
	/// File path where this query is saved. Null if query has never been saved.
	/// </summary>
	public string? FilePath { get; set; }
}
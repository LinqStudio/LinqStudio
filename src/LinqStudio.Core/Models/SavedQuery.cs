public record SavedQuery
{
	public string Name { get; init; } = string.Empty;
	public string QueryText { get; init; } = string.Empty;
	public DateTimeOffset CreatedDate { get; init; }
}
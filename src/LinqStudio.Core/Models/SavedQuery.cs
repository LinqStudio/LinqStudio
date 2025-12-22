public class SavedQuery
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string QueryText { get; set; } = string.Empty;
	public DateTimeOffset CreatedDate { get; init; }
}
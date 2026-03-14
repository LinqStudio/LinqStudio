using LinqStudio.Abstractions;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Settings;

public record class QueryExecutionSettings : IUserSettingsSection
{
	[JsonIgnore]
	public string SectionName => nameof(QueryExecutionSettings);

	public int TimeoutSeconds { get; set; } = 30;
}

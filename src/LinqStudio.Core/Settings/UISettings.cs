
using LinqStudio.Core.Abstractions;
using System.Text.Json.Serialization;

namespace LinqStudio.Core.Settings;

public record class UISettings : IUserSettingsSection
{
	[JsonIgnore]
	public string SectionName => nameof(UISettings);

	public bool IsDarkMode { get; set; } = true;

	public bool AlwaysReloadSettingsInSettingsPage { get; set; } = true;

}

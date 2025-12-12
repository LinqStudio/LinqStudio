namespace LinqStudio.Abstractions;

/// <summary>
/// Interface used by all user settings. Each implementation must provide a static SectionName property, which will be mapped to the settings file.
/// Usually this should be the same as the class name.
/// </summary>
public interface IUserSettingsSection
{
	public string SectionName { get; }
}

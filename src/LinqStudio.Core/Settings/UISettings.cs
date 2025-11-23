
using LinqStudio.Core.Abstractions;

namespace LinqStudio.Core.Settings;

public record class UISettings: IUserSettingsSection
{
    public static string SectionName => nameof(UISettings);

    public bool IsDarkMode { get; init; } = true;

}

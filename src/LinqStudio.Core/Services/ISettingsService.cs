using LinqStudio.Abstractions;

namespace LinqStudio.Core.Services;

/// <summary>
/// Handles persistence (write path) for user settings.
/// Settings are read via <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/> injected from the
/// configuration system, which loads <c>usersettings.json</c> at startup and monitors for changes.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Persists one or more settings sections to <c>usersettings.json</c>.
    /// Each section is serialized under its <see cref="IUserSettingsSection.SectionName"/> key,
    /// merging with any existing sections rather than replacing the entire file.
    /// </summary>
    /// <param name="settings">One or more settings sections to persist.</param>
    Task Save(params IEnumerable<IUserSettingsSection> settings);
}

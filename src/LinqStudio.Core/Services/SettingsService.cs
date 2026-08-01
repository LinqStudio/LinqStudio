using LinqStudio.Abstractions;
using LinqStudio.Core.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinqStudio.Core.Services;

/// <summary>
/// Concrete singleton implementation of <see cref="ISettingsService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dual-path settings model:</b> settings are <em>read</em> through
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/>, which loads
/// <c>usersettings.json</c> at startup and watches for external file changes.
/// Settings are <em>written</em> exclusively via <see cref="Save"/>, which
/// merges and rewrites the file in a single pass.
/// </para>
/// <para>
/// A <see cref="SemaphoreSlim"/>(1,1) serializes all write calls so that
/// concurrent callers do not race on a file opened with the default
/// <c>FileShare.None</c> exclusion. The semaphore acquires an OS handle
/// on first use — always dispose it. Because this class is registered as a
/// singleton, the DI host automatically calls <see cref="Dispose"/> on
/// application shutdown.
/// </para>
/// </remarks>
public class SettingsService : ISettingsService, IDisposable
{
	/// <summary>
	/// File name of the user settings JSON file, relative to the application working directory.
	/// </summary>
	public const string FILE_NAME = "usersettings.json";

	private readonly SemaphoreSlim _lock = new(1, 1);

	/// <inheritdoc/>
	/// <remarks>
	/// Opens <c>usersettings.json</c> once with <c>FileAccess.ReadWrite</c> so the
	/// existing sections can be read, merged with the incoming <paramref name="settings"/>,
	/// and rewritten in a single operation. The <see cref="_lock"/> semaphore prevents
	/// a second concurrent caller from opening the same file while it is still being
	/// written (the OS would otherwise throw an <see cref="IOException"/> due to
	/// <c>FileShare.None</c>).
	/// </remarks>
	public async Task Save(params IEnumerable<IUserSettingsSection> settings)
	{
		await _lock.WaitAsync();
		try
		{
			// Open the file a single time to prevent concurrency issues
			await using var file = File.Open(FILE_NAME, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			JsonNode document;
			if (file.Length == 0)
			{
				document = new JsonObject();
			}
			else
			{
				document = (await JsonNode.ParseAsync(file)) ?? new JsonObject();
			}
			foreach (var setting in settings)
			{
				document[setting.SectionName] = JsonNode.Parse(JsonSerializer.Serialize((object)setting));
			}
			// Re-write the entire file
			file.Position = 0;
			file.SetLength(0);

			await JsonSerializer.SerializeAsync(file, document, JsonSerializerOptions.Indented);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Releases the <see cref="SemaphoreSlim"/> used to serialize file writes.
	/// Called automatically by the DI host when the application shuts down.
	/// </summary>
	public void Dispose() => _lock.Dispose();

}

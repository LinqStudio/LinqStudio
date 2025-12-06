using LinqStudio.Core.Abstractions;
using LinqStudio.Core.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinqStudio.Core.Services;

public class SettingsService
{
	public const string FILE_NAME = "usersettings.json";

	public async Task Save(params IEnumerable<IUserSettingsSection> settings)
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

}

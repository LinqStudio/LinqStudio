using LinqStudio.Core.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinqStudio.Core.Services;

public class SettingsService
{
    public const string FILE_NAME = "usersettings.json";

    public Task Save<T>(T setting, CancellationToken cancellationToken) where T : IUserSettingsSection
    {
        return Save(T.SectionName, JsonSerializer.Serialize(setting), cancellationToken);
    }

    public async Task Save(string section, string config, CancellationToken cancellationToken)
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
            document = (await JsonNode.ParseAsync(file, cancellationToken: cancellationToken)) ?? new JsonObject();
        }

        document[section] = JsonNode.Parse(config);

        // Re-write the entire file
        file.Position = 0;
        file.SetLength(0);

        await JsonSerializer.SerializeAsync(file, document, new JsonSerializerOptions()
        {
            WriteIndented = true
        }, cancellationToken);
    }
}

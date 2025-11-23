using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Abstractions;
using LinqStudio.Core.Resources;
using Microsoft.AspNetCore.Components;
using System.Text;
using System.Text.Json;

namespace LinqStudio.Blazor.Components.Pages.Settings;

public partial class SettingsEditor<TSettings> : ComponentBase, IDisposable
    where TSettings : IUserSettingsSection
{
    private IDisposable? _providerDisposable;
    private bool _disposed = false;
    private StandaloneCodeEditor? _editor;
    private bool _loaded = false;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "json",
            Value = JsonSerializer.Serialize(UISettings.CurrentValue, new JsonSerializerOptions() { WriteIndented = true }),
            Hover = new()
            {
                Enabled = true
            }
        };
    }

    private async Task OnEditorInitialized()
    {
        if (_editor == null)
        {
            return;
        }

        _providerDisposable = await MonacoProvidersService.RegisterHoverProviderAsync(_editor, "json", async (uri, position, context) =>
        {
            var model = await BlazorMonaco.Editor.Global.GetModel(JSRuntime, uri);
            if (model == null)
                return null;

            // Do we have a word we are hovering over? If not bail.
            var word = await model.GetWordAtPosition(position);
            if (word == null)
                return null;

            var settingsProperty = typeof(TSettings).GetProperty(word.Word);
            if (settingsProperty == null) // We want to be sure it's actually a setting
                return null;

            // Check if we're hovering one of the key (one of the settings), or another random word somewhere else in the JSON.
            if (!await IsFirstLevelJsonKey(word))
                return null;

            var translatedDescription = SharedResource.ResourceManager.GetString($"UserSettings.{TSettings.SectionName}.{word.Word}", SharedResource.Culture);
            return new Hover
            {
                Contents =
                [
                    new MarkdownString { Value = translatedDescription, SupportThemeIcons = false }
                ],
                Range = new BlazorMonaco.Range
                {
                    StartLineNumber = position.LineNumber,
                    EndLineNumber = position.LineNumber,
                    StartColumn = word.StartColumn,
                    EndColumn = word.EndColumn
                }
            };
        });
    }

    /// <summary>
    /// Determines if the given word is a first-level JSON key in the current document.
    /// This will return false if we're hovering a value, or a nested key.
    /// </summary>
    private async Task<bool> IsFirstLevelJsonKey(WordAtPosition word)
    {
        if (_editor == null)
            return false;

        try
        {
            var json = await _editor.GetValue();
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    if (word.Word == propertyName)
                    {
                        // We found the property name, now check if it's at the first level (i.e., depth 1)
                        if (reader.CurrentDepth == 1)
                        {
                            return true;
                        }
                    }
                    long position = reader.TokenStartIndex; // byte offset in JSON
                    Console.WriteLine($"Property '{propertyName}' starts at position {position}");
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, can't determine structure. User is probably just going crazy typing.
            return false;
        }

        // We never found it as a first-level key
        return false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!_loaded)
        {
            _loaded = true;
            await Task.Delay(100); // Give it a moment to load monaco resources

            StateHasChanged();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _providerDisposable?.Dispose();
        GC.SuppressFinalize(this);
    }
}

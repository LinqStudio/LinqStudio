using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Abstractions;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Resources;
using Microsoft.AspNetCore.Components;
using System.Text;
using System.Text.Json;

namespace LinqStudio.Blazor.Components.Pages.Settings;

public partial class SettingsEditor : ComponentBase, IDisposable
{
	private IDisposable? _providerDisposable;
	private bool _disposed = false;
	private StandaloneCodeEditor? _editor;
	private bool _loaded = false;

	private string PanelHeaderText => SharedResource.ResourceManager.GetString($"UserSettings.{Setting.SectionName}", SharedResource.Culture) ?? Setting.SectionName;

	[Parameter, EditorRequired]
	public IUserSettingsSection Setting { get; set; }

	[Parameter, EditorRequired]
	public Settings SettingsPage { get; set; }

	private IUserSettingsSection? _previousSetting;

	public async Task<string?> GetText()
	{
		if (_editor == null)
			return null;

		return await _editor.GetValue();
	}

	protected override void OnInitialized()
	{
		base.OnInitialized();

		SettingsPage.AddEditor(this);
	}

	private bool _popupVisible = false;
	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		if (_previousSetting != Setting)
		{
			if (_previousSetting == null || _popupVisible)
			{
				// First time initialization, no need to prompt
				_previousSetting = Setting;
				return;
			}

			_previousSetting = Setting;

			_popupVisible = true;
			try
			{
				bool? result = true;
				if (!UISettings.CurrentValue.AlwaysReloadSettingsInSettingsPage)
				{
					result = await DialogService.ShowMessageBox(
						SharedResource.SettingsPage_MessageBox_ReloadTitle,
						SharedResource.SettingsPage_MessageBox_ReloadSettings,
						yesText: SharedResource.Global_MessageBox_Yes, noText: SharedResource.SettingsPage_MessageBox_Always, cancelText: SharedResource.Global_MessageBox_No);
				}

				if (result != null && _editor != null)
				{
					if (result == false)
					{
						// User selected "Always"
						await SettingsService.Save(UISettings.CurrentValue with
						{
							AlwaysReloadSettingsInSettingsPage = true
						});
					}

					// reload the JSON
					await _editor.SetValue(JsonSerializer.Serialize((object)Setting, JsonSerializerOptions.Indented));

					await _editor.UpdateOptions(new()
					{
						Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : "vs-white"
					});

					StateHasChanged();
				}
			}
			finally
			{
				_popupVisible = false;
			}
		}
	}

	private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
	{
		return new StandaloneEditorConstructionOptions
		{
			AutomaticLayout = true,
			Language = "json",
			Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : null,
			Value = JsonSerializer.Serialize((object)Setting, JsonSerializerOptions.Indented),
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

		_providerDisposable = await MonacoProvidersService.RegisterHoverProviderAsync(_editor, async (uri, position, context) =>
		{
			var model = await BlazorMonaco.Editor.Global.GetModel(JSRuntime, uri);
			if (model == null)
				return null;

			// Do we have a word we are hovering over? If not bail.
			var word = await model.GetWordAtPosition(position);
			if (word == null)
				return null;

			var settingsProperty = Setting.GetType().GetProperty(word.Word);
			if (settingsProperty == null) // We want to be sure it's actually a setting
				return null;

			// Check if we're hovering one of the key (one of the settings), or another random word somewhere else in the JSON.
			if (!await IsFirstLevelJsonKey(word))
				return null;

			var translatedDescription = SharedResource.ResourceManager.GetString($"UserSettings.{Setting.SectionName}.{word.Word}", SharedResource.Culture);
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
			await Task.Delay(500); // Give it a moment to load monaco resources.. probably a better way to do this.. right ?

			StateHasChanged();
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_providerDisposable?.Dispose();
		SettingsPage.RemoveEditor(this);
		GC.SuppressFinalize(this);
	}
}

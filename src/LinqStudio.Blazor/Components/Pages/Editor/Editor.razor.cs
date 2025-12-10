using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis.Tags;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable
{
	[Inject] private IDialogService DialogService { get; set; } = null!;

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;
	private CompilerService? _compiler;
	private string _lastQueryText = string.Empty;

	private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor ed) => new()
	{
		AutomaticLayout = true,
		Language = "csharp",
		Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : null,
		Hover = new() { Enabled = true },
		Value = GetInitialQueryText(),
		QuickSuggestions = new QuickSuggestionsOptions
		{
			Other = "on",
			Comments = "off",
			Strings = "off"
		},
		SuggestOnTriggerCharacters = true,
		AcceptSuggestionOnCommitCharacter = true,
		AcceptSuggestionOnEnter = "on"
	};

	private bool Delay = true;

	protected override void OnParametersSet()
	{
		if (Workspace.IsProjectOpen)
		{
			// Handle "new" route
			if (QueryIndexParam == "new")
			{
				var newIndex = Workspace.CreateNewQuery();
				NavigationManager.NavigateTo($"/editor/{newIndex}", replace: true);
				return;
			}

			// Handle query index route
			if (int.TryParse(QueryIndexParam, out var index))
			{
				Workspace.SetCurrentQuery(index);
			}
			else if (Workspace.CurrentQueryIndex < 0 && Workspace.CurrentProject?.Queries?.Any() == true)
			{
				// Default to first query if none selected
				Workspace.SetCurrentQuery(0);
			}

			// Update editor value if query changed
			if (_editor != null)
			{
				var newText = GetInitialQueryText();
				if (newText != _lastQueryText)
				{
					_lastQueryText = newText;
					_ = _editor.SetValue(newText);
				}
			}
		}
	}

	private string GetInitialQueryText()
	{
		return Workspace.CurrentQuery?.QueryText ?? "// Write your LINQ query here\ncontext.";
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (Delay)
		{
			Delay = false;
			await Task.Delay(500);
			StateHasChanged();
		}
	}

	private async Task OnEditorContentChanged()
	{
		if (_editor == null || !Workspace.IsProjectOpen || Workspace.CurrentQueryIndex < 0)
		{
			return;
		}

		var newText = await _editor.GetValue();
		if (newText != _lastQueryText)
		{
			_lastQueryText = newText;
			Workspace.UpdateCurrentQueryText(newText);
		}
	}

	private async Task RenameQuery()
	{
		if (Workspace.CurrentQuery == null)
		{
			return;
		}

		var parameters = new DialogParameters
		{
			{ "CurrentName", Workspace.CurrentQuery.Name }
		};

		var dialog = await DialogService.ShowAsync<RenameQueryDialog>("Rename Query", parameters);
		var result = await dialog.Result;

		if (result is not null && !result.Canceled && result.Data is string newName && !string.IsNullOrWhiteSpace(newName))
		{
			Workspace.RenameCurrentQuery(newName);
		}
	}

	private async Task OnEditorInitialized()
	{
		if (_editor == null)
		{
			return;
		}

		_lastQueryText = await _editor.GetValue();
		_compiler = await CompilerServiceFactory.CreateAsync();

		_providerDisposable = await MonacoProvidersService.RegisterCompletionProviderAsync(_editor, async (modelUri, position, context) =>
		{
			try
			{
				var text = await _editor.GetValue();
				var model = await _editor.GetModel();
				var cursorOffset = await model.GetOffsetAt(position);

				if (_compiler == null)
				{
					return null;
				}

				var completions = await _compiler.GetCompletionsAsync(text, cursorOffset);
				if (completions == null || completions.Count == 0)
				{
					return null;
				}

				var word = await model.GetWordUntilPosition(position);

				var items = completions.Select(c => new CompletionItem
				{
					InsertText = GetInsertText(c.Item),
					LabelAsString = c.Item.DisplayTextPrefix + c.Item.DisplayText + c.Item.DisplayTextSuffix,
					FilterText = c.Item.FilterText,
					Detail = c.Item.InlineDescription,
					Kind = MapCompletionItemKind(c.Item.Tags),
					DocumentationAsString = c.Description,
					RangeAsObject = new BlazorMonaco.Range
					{
						StartLineNumber = position.LineNumber,
						StartColumn = word?.StartColumn ?? position.Column,
						EndLineNumber = position.LineNumber,
						EndColumn = word?.EndColumn ?? position.Column
					}
				}).ToList();

				return new CompletionList
				{
					Suggestions = items,
					Incomplete = false
				};
			}
			catch
			{
				return null;
			}
		});

		_hoverProviderDisposable = await MonacoProvidersService.RegisterHoverProviderAsync(_editor, async (uri, position, context) =>
		{
			try
			{
				var text = await _editor.GetValue();
				var model = await _editor.GetModel();
				if (model == null)
				{
					return null;
				}

				var cursorOffset = await model.GetOffsetAt(position);

				if (_compiler == null)
				{
					return null;
				}

				var hover = await _compiler.GetHoverAsync(text, cursorOffset);
				if (hover == null)
				{
					return null;
				}

				var startPos = await model.GetPositionAt(hover.StartOffset);
				var endPos = await model.GetPositionAt(hover.StartOffset + hover.Length);

				return new Hover
				{
					Contents = [new MarkdownString { Value = hover.Markdown ?? string.Empty, SupportThemeIcons = false }],
					Range = new BlazorMonaco.Range
					{
						StartLineNumber = startPos.LineNumber,
						EndLineNumber = endPos.LineNumber,
						StartColumn = startPos.Column,
						EndColumn = endPos.Column
					}
				};
			}
			catch
			{
				return null;
			}
		});
	}

	private string GetInsertText(Microsoft.CodeAnalysis.Completion.CompletionItem item)
	{
		var text = item.Properties.TryGetValue("InsertionText", out var v) ? v : item.DisplayText;

		if (item.Properties.TryGetValue("ShouldProvideParenthesisCompletion", out var s) && s.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			text += "(";
		}

		return text;
	}

	private CompletionItemKind MapCompletionItemKind(IEnumerable<string> tags)
	{
		if (tags.Contains(WellKnownTags.Property))
		{
			return CompletionItemKind.Property;
		}

		if (tags.Contains(WellKnownTags.Method) || tags.Contains(WellKnownTags.ExtensionMethod))
		{
			return CompletionItemKind.Method;
		}

		if (tags.Contains(WellKnownTags.Field))
		{
			return CompletionItemKind.Field;
		}

		if (tags.Contains(WellKnownTags.Class))
		{
			return CompletionItemKind.Class;
		}

		return CompletionItemKind.Text;
	}

	public void Dispose()
	{
		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();
		try
		{
			_compiler?.Dispose();
		}
		catch { }
		GC.SuppressFinalize(this);
	}
}

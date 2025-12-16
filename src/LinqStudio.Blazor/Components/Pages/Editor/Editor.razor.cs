using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using LinqStudio.Core.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.Extensions.Options;
using MudBlazor;
using System.Diagnostics;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable
{
	[Inject] private ISnackbar Snackbar { get; set; } = null!;
	[Inject] private MonacoProvidersService MonacoProvidersService { get; set; } = null!;
	[Inject] private CompilerServiceFactory CompilerServiceFactory { get; set; } = null!;
	[Inject] private IOptionsMonitor<UISettings> UISettings { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;

	[Parameter] public string? QueryIndexParam { get; set; }

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;
	private CompilerService? _compiler;
	private string _lastQueryText = string.Empty;

	// Rename state
	private bool _isEditingName;
	private string _editedQueryName = string.Empty;

	// Debounce state
	private CancellationTokenSource? _debounceTokenSource;
	private const int DebounceDelayMs = 300;

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

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
	}

	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		InvokeAsync(StateHasChanged);
	}

	protected override void OnParametersSet()
	{
		if (!Workspace.IsProjectOpen)
		{
			NavigationManager.NavigateTo("/", replace: true);
			return;
		}

		if (QueryIndexParam == "new")
		{
			var (updatedProject, newIndex) = Workspace.Queries.CreateNewQuery(Workspace.CurrentProject!);
			Workspace.Update(updatedProject);
			NavigationManager.NavigateTo($"/editor/{newIndex}", replace: true);
			return;
		}

		if (int.TryParse(QueryIndexParam, out var index))
		{
			Workspace.Queries.OpenQuery(Workspace.CurrentProject!, index);
		}
		else if (Workspace.Queries.CurrentQueryIndex < 0 && Workspace.CurrentProject?.Queries?.Count > 0)
		{
			Workspace.Queries.OpenQuery(Workspace.CurrentProject, 0);
		}

		if (_editor is not null)
		{
			var newText = GetInitialQueryText();
			if (newText != _lastQueryText)
			{
				_lastQueryText = newText;
				_ = _editor.SetValue(newText);
			}
		}
	}

	private string GetInitialQueryText()
	{
		if (Workspace.Queries.CurrentQueryState is not null)
		{
			return Workspace.Queries.CurrentQueryState.CurrentText;
		}

		return Workspace.Queries.GetCurrentQuery(Workspace.CurrentProject)?.QueryText ?? "// Write your LINQ query here\ncontext.";
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
		if (_editor is null || !Workspace.IsProjectOpen || Workspace.Queries.CurrentQueryIndex < 0)
		{
			return;
		}

		var newText = await _editor.GetValue();
		if (newText != _lastQueryText)
		{
			_lastQueryText = newText;
			await DebounceUpdateAsync(newText);
		}
	}

	/// <summary>
	/// Debounces query text updates to avoid excessive workspace updates while typing.
	/// Expected to throw TaskCanceledException when user continues typing.
	/// </summary>
	[DebuggerNonUserCode]
	private async Task DebounceUpdateAsync(string newText)
	{
		_debounceTokenSource?.Cancel();
		_debounceTokenSource = new CancellationTokenSource();

		try
		{
			await Task.Delay(DebounceDelayMs, _debounceTokenSource.Token);
			Workspace.Queries.UpdateQueryText(Workspace.CurrentProject!, Workspace.Queries.CurrentQueryIndex, newText);
		}
		catch (TaskCanceledException)
		{
			// Expected - user typed before delay completed
		}
	}

	private void StartRename()
	{
		var currentQuery = Workspace.Queries.GetCurrentQuery(Workspace.CurrentProject);
		if (currentQuery is null)
		{
			return;
		}

		_editedQueryName = currentQuery.Name;
		_isEditingName = true;
	}

	private void CancelRename()
	{
		_isEditingName = false;
		_editedQueryName = string.Empty;
	}

	private void SaveRename()
	{
		if (Workspace.Queries.CurrentQueryIndex >= 0 && Workspace.CurrentProject != null)
		{
			var updatedProject = Workspace.Queries.RenameQuery(Workspace.CurrentProject, Workspace.Queries.CurrentQueryIndex, _editedQueryName);
			Workspace.Update(updatedProject);
			_isEditingName = false;
			_editedQueryName = string.Empty;
			Snackbar.Add("Query renamed successfully.", Severity.Success);
		}
	}

	private string? ValidateQueryName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Query name cannot be empty.";
		}

		if (Workspace.CurrentProject?.Queries is not null &&
			Workspace.CurrentProject.Queries
				.Where((q, index) => index != Workspace.Queries.CurrentQueryIndex)
				.Select(q => q.Name)
				.Contains(name, StringComparer.OrdinalIgnoreCase))
		{
			return "A query with this name already exists.";
		}

		return null;
	}

	private void OnNameKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			if (ValidateQueryName(_editedQueryName) == null)
			{
				SaveRename();
			}
		}
		else if (e.Key == "Escape")
		{
			CancelRename();
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
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;

		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();

		// DON'T dispose _compiler - it's managed by CompilerServiceProvider
		// The provider will dispose it when the app shuts down

		GC.SuppressFinalize(this);
	}
}

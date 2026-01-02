using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Abstractions;
using LinqStudio.Blazor.Components.Dialogs;
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
	[Inject] private IDialogService DialogService { get; set; } = null!;
	[Inject] private IFileSystemService FileSystemService { get; set; } = null!;

	[Parameter] public Guid? QueryIdParam { get; set; }

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;
	private CompilerService? _compiler;
	private string _lastQueryText = string.Empty;

	private bool _isEditingName;
	private string _editedQueryName = string.Empty;

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

		if (QueryIdParam is not null)
		{
			Workspace.Queries.OpenQuery(QueryIdParam.Value);
		}
		else if (Workspace.Queries.CurrentQueryId is null && Workspace.Queries.AllQueries.Count > 0)
		{
			Workspace.Queries.OpenQuery(Workspace.Queries.AllQueries[0].Id);
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

	private void NavigateToQuery(Guid queryId)
	{
		NavigationManager.NavigateTo($"/editor/{queryId}", replace: true);
	}

	private void CreateNewQuery()
	{
		var queryId = Workspace.Queries.CreateNewQuery();
		NavigationManager.NavigateTo($"/editor/{queryId}", replace: true);
	}

	private string GetInitialQueryText()
	{
		if (Workspace.Queries.CurrentQueryState is not null)
		{
			return Workspace.Queries.CurrentQueryState.CurrentText;
		}

		return Workspace.Queries.GetCurrentQuery()?.QueryText ?? "// Write your LINQ query here\ncontext.";
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
		if (_editor is null || !Workspace.IsProjectOpen || Workspace.Queries.CurrentQueryId is null)
		{
			return;
		}

		var newText = await _editor.GetValue();
		if (newText != _lastQueryText)
		{
			_lastQueryText = newText;
			DebounceUpdate(newText);
		}
	}

	/// <summary>
	/// Debounces query text updates to avoid excessive workspace updates while typing.
	/// Uses a cancellation token without throwing exceptions.
	/// </summary>
	private void DebounceUpdate(string newText)
	{
		_debounceTokenSource?.Cancel();
		_debounceTokenSource = new CancellationTokenSource();
		var token = _debounceTokenSource.Token;

		_ = Task.Run(async () =>
		{
			await Task.Delay(DebounceDelayMs, token);
			
			if (!token.IsCancellationRequested && Workspace.Queries.CurrentQueryId is not null)
			{
				await InvokeAsync(() =>
				{
					if (!token.IsCancellationRequested)
					{
						Workspace.Queries.UpdateQueryText(Workspace.Queries.CurrentQueryId.Value, newText);
					}
				});
			}
		}, token);
	}

	private void StartRename()
	{
		var currentQuery = Workspace.Queries.GetCurrentQuery();
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
		if (Workspace.Queries.CurrentQueryId is null)
		{
			return;
		}

		Workspace.Queries.RenameQuery(Workspace.Queries.CurrentQueryId.Value, _editedQueryName);

		_isEditingName = false;
		_editedQueryName = string.Empty;

		Snackbar.Add("Query renamed successfully.", Severity.Success);
	}

	private string? ValidateQueryName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Query name cannot be empty.";
		}

		var currentId = Workspace.Queries.CurrentQueryId;

		if (Workspace.Queries.AllQueries
				.Where(q => currentId is null || q.Id != currentId.Value)
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

	private async Task<bool> ShowUnsavedChangesDialog(string message)
	{
		var options = new DialogOptions
		{
			CloseOnEscapeKey = true,
			MaxWidth = MaxWidth.Small
		};

		var parameters = new DialogParameters<UnsavedChangesDialog>
		{
			{ x => x.Message, message }
		};

		var dialog = await DialogService.ShowAsync<UnsavedChangesDialog>("Unsaved Changes", parameters, options);
		var result = await dialog.Result;

		return (result is not null) && !result.Canceled && result.Data is bool confirm && confirm;
	}

	private IEnumerable<global::SavedQuery> GetOpenQueriesInOrder()
	{
		var openIds = new HashSet<Guid>(Workspace.Queries.OpenQueries.Keys);
		return Workspace.Queries.AllQueries.Where(q => openIds.Contains(q.Id));
	}

	private async Task CloseCurrentQuery()
	{
		if (!Workspace.IsProjectOpen || Workspace.Queries.CurrentQueryId is null)
		{
			return;
		}

		if (Workspace.Queries.OpenQueries.TryGetValue(Workspace.Queries.CurrentQueryId.Value, out var state) && state.HasUnsavedChanges)
		{
			var confirm = await ShowUnsavedChangesDialog("This query has unsaved changes. Close without saving?");
			if (!confirm)
			{
				return;
			}
		}

		var currentId = Workspace.Queries.CurrentQueryId.Value;
		Workspace.Queries.CloseQuery(currentId);

		// Navigate to the next open query, or to editor home if no queries are open
		if (Workspace.Queries.CurrentQueryId is Guid newId)
		{
			NavigationManager.NavigateTo($"/editor/{newId}", replace: true);
		}
		else
		{
			// No queries left open - stay on editor page but with no query selected
			NavigationManager.NavigateTo("/editor", replace: true);
		}
	}

	private async Task SaveCurrentQuery()
	{
		if (!Workspace.IsProjectOpen || Workspace.Queries.CurrentQueryId is null)
		{
			return;
		}

		var qid = Workspace.Queries.CurrentQueryId.Value;

		try
		{
			var success = await Workspace.Queries.SaveQueryWithDialogAsync(qid, async (defaultFileName) =>
			{
				return await FileSystemService.PromptSaveFileAsync(defaultFileName);
			});

			if (success)
			{
				Snackbar.Add("Query saved successfully.", Severity.Success);
			}
		}
		catch (Exception ex)
		{
			Snackbar.Add($"Failed to save query: {ex.Message}", Severity.Error);
		}
	}

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;

		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();

		_compiler?.Dispose();

		GC.SuppressFinalize(this);
	}
}

using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Abstractions;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Constants;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using LinqStudio.Core.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable, IAsyncDisposable
{
	[Inject] private ILogger<Editor> Logger { get; set; } = null!;
	[Inject] private ISnackbar Snackbar { get; set; } = null!;
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;
	[Inject] private MonacoProvidersService MonacoProvidersService { get; set; } = null!;
	[Inject] private CompilerServiceFactory CompilerServiceFactory { get; set; } = null!;
	[Inject] private IOptionsMonitor<UISettings> UISettings { get; set; } = null!;
	[Inject] private IOptionsMonitor<QueryExecutionSettings> QueryExecutionSettings { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;
	[Inject] private IDialogService DialogService { get; set; } = null!;
	[Inject] private IFileSystemService FileSystemService { get; set; } = null!;
	[Inject] private IQueryExecutionService QueryExecutionService { get; set; } = null!;
	[Inject] private IJSRuntime JSRuntime { get; set; } = null!;

	[Parameter] public Guid? QueryIdParam { get; set; }

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;
	private CompilerService? _compiler;
	private string _lastQueryText = string.Empty;
	private bool _isRefreshingSchema = false;

	private CancellationTokenSource? _debounceTokenSource;
	private const int DebounceDelayMs = 300;

	// Per-tab execution state
	private class QueryExecutionState
	{
		public QueryExecutionResult? Result { get; set; }
		public bool IsExecuting { get; set; }
		public CancellationTokenSource? CancellationTokenSource { get; set; }
		public Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>> SortDefinitions { get; set; } = new();
	}

	private readonly Dictionary<Guid, QueryExecutionState> _executionStates = new();
	private int _selectedTimeout = 30; // Default timeout in seconds

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
	private bool _splitterInitialized;

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
		_selectedTimeout = QueryExecutionSettings.CurrentValue.TimeoutSeconds;
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
			Logger.LogInformation("Loading query {QueryId}.", QueryIdParam.Value);
			Workspace.Queries.OpenQuery(QueryIdParam.Value);
		}
		// Don't auto-open queries - let the user explicitly open them

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

		if (firstRender)
		{
			Delay = false;
			await Task.Delay(500);
			StateHasChanged(); // triggers second render showing Monaco
			return; // DON'T init splitter yet — DOM not ready
		}

		if (!_splitterInitialized)
		{
			try
			{
				_splitterInitialized = await JSRuntime.InvokeAsync<bool>("initSplitter", "editor-results-splitter", "editor-top-panel", "results-bottom-panel");
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Failed to initialize splitter");
			}
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
		_debounceTokenSource?.Dispose();
		_debounceTokenSource = new CancellationTokenSource();
		var token = _debounceTokenSource.Token;

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(DebounceDelayMs, token);
			}
			catch (TaskCanceledException)
			{
				// Expected when user continues typing - just exit silently
				return;
			}

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
		});
	}

	private async Task OnEditorInitialized()
	{
		if (_editor == null)
		{
			return;
		}

		_lastQueryText = await _editor.GetValue();

		try
		{
			_compiler = Workspace.CurrentProject != null
				? await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)
				: await CompilerServiceFactory.CreateAsync();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "[Editor] Failed to initialize CompilerService from project schema, falling back to demo model.");
			_compiler = await CompilerServiceFactory.CreateAsync();
		}

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
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "[Editor] Completion provider error");
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
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "[Editor] Hover provider error");
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

	private IEnumerable<SavedQuery> GetOpenQueriesInOrder()
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
				return await FileSystemService.PromptSaveFileAsync(defaultFileName.EnsureHasExtension(FileExtensions.Query), FileExtensions.Query);
			});

			if (success)
			{
				Logger.LogInformation("Query {QueryId} saved successfully.", qid);
				Snackbar.Add("Query saved successfully.", Severity.Success);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to save query.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save query");
		}
	}

	private async Task RefreshSchemaAsync()
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject?.QueryGenerator is null)
		{
			Snackbar.Add("No database connection configured for this project.", Severity.Warning);
			return;
		}

		_isRefreshingSchema = true;
		StateHasChanged();

		try
		{
			var newCompiler = await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject!);
			_compiler?.Dispose();
			_compiler = newCompiler;
			Snackbar.Add("Schema refreshed. IntelliSense updated.", Severity.Success);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to refresh schema.");
			Snackbar.Add($"Failed to refresh schema: {ex.Message}", Severity.Error);
		}
		finally
		{
			_isRefreshingSchema = false;
			StateHasChanged();
		}
	}

	private QueryExecutionState GetCurrentExecutionState()
	{
		if (Workspace.Queries.CurrentQueryId is null)
		{
			return new QueryExecutionState();
		}

		if (!_executionStates.TryGetValue(Workspace.Queries.CurrentQueryId.Value, out var state))
		{
			state = new QueryExecutionState();
			_executionStates[Workspace.Queries.CurrentQueryId.Value] = state;
		}

		return state;
	}

	private async Task ExecuteCurrentQueryAsync()
	{
		if (_editor is null || Workspace.Queries.CurrentQueryId is null)
		{
			return;
		}

		// Get query text
		var queryText = await _editor.GetValue();
		if (string.IsNullOrWhiteSpace(queryText))
		{
			Snackbar.Add("Query is empty.", Severity.Warning);
			return;
		}

		// Ensure project is available
		if (Workspace.CurrentProject is null)
		{
			Snackbar.Add("No project is open.", Severity.Warning);
			return;
		}

		var queryId = Workspace.Queries.CurrentQueryId.Value;
		var state = GetCurrentExecutionState();

		// Cancel any existing execution
		state.CancellationTokenSource?.Cancel();
		state.CancellationTokenSource?.Dispose();

		// Create cancellation token with timeout
		state.CancellationTokenSource = _selectedTimeout > 0
			? new CancellationTokenSource(TimeSpan.FromSeconds(_selectedTimeout))
			: new CancellationTokenSource();

		state.IsExecuting = true;
		state.Result = null;
		StateHasChanged();

		try
		{
			var result = await QueryExecutionService.ExecuteQueryAsync(queryText, Workspace.CurrentProject, state.CancellationTokenSource.Token);
			state.Result = result;

			if (result.Success)
			{
				Snackbar.Add($"Query executed successfully. {result.Rows.Count} row(s) returned.", Severity.Success);
			}
		}
		catch (OperationCanceledException)
		{
			state.Result = QueryExecutionResult.FromError("Query execution was cancelled.", false, TimeSpan.Zero);
			Snackbar.Add("Query execution cancelled.", Severity.Warning);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Query execution failed with exception.");
			state.Result = QueryExecutionResult.FromError($"Unexpected error: {ex.Message}", false, TimeSpan.Zero);
		}
		finally
		{
			state.IsExecuting = false;
			state.CancellationTokenSource?.Dispose();
			state.CancellationTokenSource = null;
			StateHasChanged();
		}
	}

	private void StopCurrentQuery()
	{
		var state = GetCurrentExecutionState();
		state.CancellationTokenSource?.Cancel();
	}

	public async ValueTask DisposeAsync()
	{
		if (_splitterInitialized)
		{
			try
			{
				await JSRuntime.InvokeVoidAsync("disposeSplitter", "editor-results-splitter");
			}
			catch
			{
				// Ignore JS errors during disposal
			}
		}
		Dispose();
		GC.SuppressFinalize(this);
	}

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;

		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();

		_compiler?.Dispose();

		// Clean up all execution state
		foreach (var state in _executionStates.Values)
		{
			state.CancellationTokenSource?.Cancel();
			state.CancellationTokenSource?.Dispose();
		}
		_executionStates.Clear();

		GC.SuppressFinalize(this);
	}
}

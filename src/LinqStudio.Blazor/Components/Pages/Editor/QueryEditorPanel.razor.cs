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

public partial class QueryEditorPanel : ComponentBase, IDisposable, IAsyncDisposable
{
	[Parameter, EditorRequired] public Guid QueryId { get; set; }
	[Parameter] public CompilerService? Compiler { get; set; }
	[Parameter] public bool IsRefreshingSchema { get; set; }
	[Parameter] public EventCallback OnRefreshSchemaRequested { get; set; }
	[Parameter] public EventCallback<Guid> OnQueryClosed { get; set; }

	[Inject] private ILogger<QueryEditorPanel> Logger { get; set; } = null!;
	[Inject] private ISnackbar Snackbar { get; set; } = null!;
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;
	[Inject] private MonacoProvidersService MonacoProvidersService { get; set; } = null!;
	[Inject] private CompilerServiceFactory CompilerServiceFactory { get; set; } = null!;
	[Inject] private IOptionsMonitor<UISettings> UISettings { get; set; } = null!;
	[Inject] private IOptionsMonitor<QueryExecutionSettings> QueryExecutionSettings { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private IDialogService DialogService { get; set; } = null!;
	[Inject] private IFileSystemService FileSystemService { get; set; } = null!;
	[Inject] private IQueryExecutionService QueryExecutionService { get; set; } = null!;
	[Inject] private IJSRuntime JSRuntime { get; set; } = null!;

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;
	private CompilerService? _localCompiler;
	private string _lastQueryText = string.Empty;

	private CancellationTokenSource? _debounceTokenSource;
	private const int DebounceDelayMs = 300;
	private const int TabActivationLayoutDelayMs = 100;

	private QueryExecutionResult? _result;
	private bool _isExecuting;
	private CancellationTokenSource? _executionCts;
	private int _selectedTimeout = 30;

	private bool _delay = true;
	private bool _splitterInitialized;
	private bool _disposed;

	private string SplitterId => $"editor-results-splitter-{QueryId:N}";
	private string EditorPanelId => $"editor-top-panel-{QueryId:N}";
	private string ResultsPanelId => $"results-bottom-panel-{QueryId:N}";
	private string EditorId => $"editor-{QueryId:N}";

	private SavedQuery? _currentQuery => Workspace.Queries.AllQueries.FirstOrDefault(q => q.Id == QueryId);

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

	protected override void OnInitialized()
	{
		_selectedTimeout = QueryExecutionSettings.CurrentValue.TimeoutSeconds;
	}

	public async Task OnTabActivatedAsync()
	{
		try
		{
			if (_editor is not null)
			{
				// Wait for MudBlazor to remove display:none from the panel before Monaco measures.
				await Task.Delay(TabActivationLayoutDelayMs);
				if (_disposed) return;
				// Call layout() via JS with no explicit dimensions so Monaco auto-reads the container size.
				// Using _editor.Layout(new Dimension{Width=0,Height=0}) would set 0×0 explicitly — wrong.
				await JSRuntime.InvokeVoidAsync("monacoRelayout", EditorId);
			}

			// Belt-and-suspenders: reset any scrollTop MudBlazor set on .mud-tabs during
			// panel activation (the sticky CSS is the primary guard, this cleans up the offset).
			await JSRuntime.InvokeVoidAsync("resetMudTabsScroll");
		}
		catch
		{
			// Ignore JS errors (circuit reconnect, rapid navigation, slow init)
		}
	}

	protected override void OnParametersSet()
	{
		if (Compiler is not null && _localCompiler is not null)
		{
			_localCompiler.Dispose();
			_localCompiler = null;
		}
	}

	private string GetInitialQueryText()
	{
		if (Workspace.Queries.OpenQueries.TryGetValue(QueryId, out var queryState) && queryState is not null)
		{
			return queryState.CurrentText;
		}

		var query = Workspace.Queries.AllQueries.FirstOrDefault(q => q.Id == QueryId);
		return query?.QueryText ?? "// Write your LINQ query here\ncontext.";
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			_delay = false;
			await Task.Delay(500);
			if (_disposed) return;
			StateHasChanged();
			return;
		}

		if (!_splitterInitialized)
		{
			_splitterInitialized = true; // prevent concurrent re-entry
			try
			{
				var ok = await JSRuntime.InvokeAsync<bool>("initSplitter", SplitterId, EditorPanelId, ResultsPanelId);
				if (!ok) _splitterInitialized = false; // DOM not ready yet, retry next render
			}
			catch (Exception ex)
			{
				_splitterInitialized = false;
				Logger.LogWarning(ex, "Failed to initialize splitter for tab {QueryId}", QueryId);
			}
		}
	}

	private async Task OnEditorContentChanged()
	{
		if (_editor is null || !Workspace.IsProjectOpen)
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
				return;
			}

			if (!token.IsCancellationRequested)
			{
				await InvokeAsync(() =>
				{
					if (!token.IsCancellationRequested)
					{
						Workspace.Queries.UpdateQueryText(QueryId, newText);
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

		// _localCompiler exists because Monaco's 500ms init delay can fire before Editor's async
		// compiler initialization completes. If Compiler param is null here, we create a local
		// fallback so completions work immediately. When the real Compiler param arrives via
		// re-render, provider callbacks switch to it via (Compiler ?? _localCompiler). Both
		// coexist until panel disposal, at which point _localCompiler is disposed.
		if (Compiler == null)
		{
			try
			{
				_localCompiler = Workspace.CurrentProject != null
					? await CompilerServiceFactory.CreateFromProjectAsync(Workspace.CurrentProject)
					: await CompilerServiceFactory.CreateAsync();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "[QueryEditorPanel] Fallback compiler creation failed, using demo model.");
				_localCompiler = await CompilerServiceFactory.CreateAsync();
			}
		}

		_providerDisposable = await MonacoProvidersService.RegisterCompletionProviderAsync(_editor, async (modelUri, position, context) =>
		{
			try
			{
				var text = await _editor.GetValue();
				var model = await _editor.GetModel();
				var cursorOffset = await model.GetOffsetAt(position);

				var compiler = Compiler ?? _localCompiler;
				if (compiler == null)
				{
					return null;
				}

				var completions = await compiler.GetCompletionsAsync(text, cursorOffset);
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
				Logger.LogWarning(ex, "[QueryEditorPanel] Completion provider error for {QueryId}", QueryId);
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

				var compiler = Compiler ?? _localCompiler;
				if (compiler == null)
				{
					return null;
				}

				var hover = await compiler.GetHoverAsync(text, cursorOffset);
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
				Logger.LogWarning(ex, "[QueryEditorPanel] Hover provider error for {QueryId}", QueryId);
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

	private async Task CloseCurrentQuery()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		if (Workspace.Queries.OpenQueries.TryGetValue(QueryId, out var state) && state.HasUnsavedChanges)
		{
			var confirm = await ShowUnsavedChangesDialog("This query has unsaved changes. Close without saving?");
			if (!confirm)
			{
				return;
			}
		}

		Workspace.Queries.CloseQuery(QueryId);
		await OnQueryClosed.InvokeAsync(QueryId);
	}

	private async Task SaveCurrentQuery()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			var success = await Workspace.Queries.SaveQueryWithDialogAsync(QueryId, async (defaultFileName) =>
			{
				return await FileSystemService.PromptSaveFileAsync(defaultFileName.EnsureHasExtension(FileExtensions.Query), FileExtensions.Query);
			});

			if (success)
			{
				Logger.LogInformation("Query {QueryId} saved successfully.", QueryId);
				Snackbar.Add("Query saved successfully.", Severity.Success);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to save query.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save query");
		}
	}

	private async Task ExecuteQueryAsync()
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject is null)
		{
			Snackbar.Add("No project is open.", Severity.Warning);
			return;
		}

		if (_editor == null)
		{
			Snackbar.Add("Editor not ready. Please try again.", Severity.Warning);
			return;
		}

		var queryText = await _editor.GetValue();
		if (string.IsNullOrWhiteSpace(queryText))
		{
			Snackbar.Add("Query is empty.", Severity.Warning);
			return;
		}

		_executionCts?.Cancel();
		_executionCts?.Dispose();

		_executionCts = _selectedTimeout > 0
			? new CancellationTokenSource(TimeSpan.FromSeconds(_selectedTimeout))
			: new CancellationTokenSource();

		_isExecuting = true;
		_result = null;
		StateHasChanged();

		try
		{
			var result = await QueryExecutionService.ExecuteQueryAsync(queryText, Workspace.CurrentProject, _executionCts.Token);
			_result = result;

			if (result.Success)
			{
				Snackbar.Add($"Query executed successfully. {result.Rows.Count} row(s) returned.", Severity.Success);
			}
		}
		catch (OperationCanceledException)
		{
			_result = QueryExecutionResult.FromError("Query execution was cancelled.", false, TimeSpan.Zero);
			Snackbar.Add("Query execution cancelled.", Severity.Warning);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Query execution failed with exception.");
			_result = QueryExecutionResult.FromError($"Unexpected error: {ex.Message}", false, TimeSpan.Zero);
		}
		finally
		{
			_isExecuting = false;
			_executionCts?.Dispose();
			_executionCts = null;
			StateHasChanged();
		}
	}

	private void StopQuery()
	{
		_executionCts?.Cancel();
	}

	public async ValueTask DisposeAsync()
	{
		_disposed = true;

		if (_splitterInitialized)
		{
			try
			{
				await JSRuntime.InvokeVoidAsync("disposeSplitter", SplitterId);
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
		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();

		_debounceTokenSource?.Cancel();
		_debounceTokenSource?.Dispose();

		_executionCts?.Cancel();
		_executionCts?.Dispose();

		_localCompiler?.Dispose();
	}
}

using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Constants;
using LinqStudio.Blazor.Extensions;
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

/// <summary>
/// Full-featured query editor panel for a single open LINQ query tab.
/// Combines a BlazorMonaco editor, Roslyn-backed IntelliSense (completions + hover),
/// a JS-driven draggable splitter, and a <see cref="QueryResultGrid"/> for execution output.
/// </summary>
/// <remarks>
/// Implements both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> so Blazor can
/// release JS resources asynchronously while also being safe to dispose synchronously when the
/// circuit terminates. See <see cref="DisposeAsync"/> for the dual-dispose pattern details.
/// </remarks>
public partial class QueryEditorPanel : ComponentBase, IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Gets or sets the unique ID of the query this panel is editing.
	/// Required — the panel is entirely scoped to a single query.
	/// </summary>
	[Parameter, EditorRequired] public Guid QueryId { get; set; }

	/// <summary>
	/// Gets or sets an externally-created <see cref="CompilerService"/> to use for IntelliSense.
	/// When <see langword="null"/> the panel creates its own fallback compiler on editor init.
	/// </summary>
	[Parameter] public CompilerService? Compiler { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the parent is currently refreshing the DB schema.
	/// Used to disable the "Refresh Schema" button in the toolbar while a refresh is in progress.
	/// </summary>
	[Parameter] public bool IsRefreshingSchema { get; set; }

	/// <summary>Gets or sets the callback invoked when the user clicks "Refresh Schema".</summary>
	[Parameter] public EventCallback OnRefreshSchemaRequested { get; set; }

	/// <summary>
	/// Gets or sets the callback invoked when the user closes this query tab.
	/// The <see cref="Guid"/> argument is the <see cref="QueryId"/> of the closed query.
	/// </summary>
	[Parameter] public EventCallback<Guid> OnQueryClosed { get; set; }

	/// <summary>Gets or sets the logger for this component.</summary>
	[Inject] private ILogger<QueryEditorPanel> Logger { get; set; } = null!;

	/// <summary>Gets or sets the snackbar service for transient user notifications.</summary>
	[Inject] private ISnackbar Snackbar { get; set; } = null!;

	/// <summary>Gets or sets the error handling service for surfacing unexpected exceptions.</summary>
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;

	/// <summary>
	/// Gets or sets the service that manages global Monaco provider registrations,
	/// preventing duplicate provider callbacks across multiple editor instances.
	/// </summary>
	[Inject] private MonacoProvidersService MonacoProvidersService { get; set; } = null!;

	/// <summary>Gets or sets the factory used to create <see cref="CompilerService"/> instances.</summary>
	[Inject] private ICompilerServiceFactory CompilerServiceFactory { get; set; } = null!;

	/// <summary>Gets or sets UI settings (dark mode, etc.) monitored for live changes.</summary>
	[Inject] private IOptionsMonitor<UISettings> UISettings { get; set; } = null!;

	/// <summary>Gets or sets query execution settings (default timeout, etc.).</summary>
	[Inject] private IOptionsMonitor<QueryExecutionSettings> QueryExecutionSettings { get; set; } = null!;

	/// <summary>Gets or sets the shared project workspace providing query state.</summary>
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;

	/// <summary>Gets or sets the MudBlazor dialog service for unsaved-changes confirmation.</summary>
	[Inject] private IDialogService DialogService { get; set; } = null!;

	/// <summary>Gets or sets the service that compiles and executes LINQ queries.</summary>
	[Inject] private IQueryExecutionService QueryExecutionService { get; set; } = null!;

	/// <summary>Gets or sets the JS runtime for Monaco relayout and splitter interop calls.</summary>
	[Inject] private IJSRuntime JSRuntime { get; set; } = null!;

	private StandaloneCodeEditor? _editor;
	private IDisposable? _providerDisposable;
	private IDisposable? _hoverProviderDisposable;

	/// <summary>
	/// A locally-owned fallback compiler created when <see cref="Compiler"/> is <see langword="null"/>
	/// at the time the Monaco editor finishes initializing. Disposed on panel teardown.
	/// </summary>
	private CompilerService? _localCompiler;
	private string _lastQueryText = string.Empty;

	private CancellationTokenSource? _debounceTokenSource;
	private const int DebounceDelayMs = 300;
	private const int TabActivationLayoutDelayMs = 300;

	private QueryExecutionResult? _result;
	private bool _isExecuting;
	private CancellationTokenSource? _executionCts;
	private int _selectedTimeout = 30;

	private bool _delay = true;
	private bool _splitterInitialized;

	/// <summary>
	/// Set to <see langword="true"/> as the very first step in both <see cref="Dispose"/>
	/// and <see cref="DisposeAsync"/>, so that any in-flight <see langword="await"/> continuations
	/// (e.g. in <see cref="OnTabActivatedAsync"/> or <see cref="OnAfterRenderAsync"/>) can detect
	/// disposal and bail out before touching disposed resources.
	/// </summary>
	private bool _disposed;

	/// <summary>Gets the unique DOM element ID for the JS splitter handle for this tab.</summary>
	private string SplitterId => $"editor-results-splitter-{QueryId:N}";

	/// <summary>Gets the unique DOM element ID for the top (editor) panel for this tab.</summary>
	private string EditorPanelId => $"editor-top-panel-{QueryId:N}";

	/// <summary>Gets the unique DOM element ID for the bottom (results) panel for this tab.</summary>
	private string ResultsPanelId => $"results-bottom-panel-{QueryId:N}";

	/// <summary>Gets the unique DOM element ID for the Monaco editor instance for this tab.</summary>
	private string EditorId => $"editor-{QueryId:N}";

	/// <summary>
	/// Gets the <see cref="SavedQuery"/> metadata for the currently open query,
	/// or <see langword="null"/> if the query has not yet been persisted.
	/// </summary>
	private SavedQuery? _currentQuery => Workspace.Queries.AllQueries.FirstOrDefault(q => q.Id == QueryId);

	/// <summary>
	/// Provides the construction options for the Monaco editor instance.
	/// Sets language to C#, applies the current dark/light theme, and enables IntelliSense triggers.
	/// </summary>
	/// <param name="ed">The editor instance being constructed (unused but required by the BlazorMonaco delegate signature).</param>
	/// <returns>A <see cref="StandaloneEditorConstructionOptions"/> pre-configured for LINQ editing.</returns>
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

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		_selectedTimeout = QueryExecutionSettings.CurrentValue.TimeoutSeconds;
	}

	/// <summary>
	/// Called by the parent editor page when this query's tab becomes visible.
	/// Triggers Monaco to re-measure the editor container and resets any scroll offset
	/// that MudBlazor may have applied to the tab panel.
	/// </summary>
	/// <remarks>
	/// Monaco measures its container dimensions at initialization time. When a tab is hidden
	/// via <c>display:none</c>, the container reports zero dimensions; this call forces a
	/// relayout after the tab becomes visible so the editor fills its container correctly.
	/// Any JS exceptions (e.g., circuit reconnect, rapid navigation) are silently swallowed.
	/// </remarks>
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

	/// <inheritdoc />
	/// <remarks>
	/// When the <see cref="Compiler"/> parameter is supplied by the parent, any previously
	/// created <see cref="_localCompiler"/> fallback is disposed and cleared to avoid holding
	/// a redundant Roslyn workspace in memory.
	/// </remarks>
	protected override void OnParametersSet()
	{
		if (Compiler is not null && _localCompiler is not null)
		{
			_localCompiler.Dispose();
			_localCompiler = null;
		}
	}

	/// <summary>
	/// Returns the text to pre-populate the Monaco editor with when it first opens.
	/// Prefers the in-memory open-query state (unsaved edits) over the persisted query text.
	/// </summary>
	/// <returns>
	/// The current in-memory query text, the last-saved query text, or a default placeholder
	/// if neither is available.
	/// </returns>
	private string GetInitialQueryText()
	{
		if (Workspace.Queries.OpenQueries.TryGetValue(QueryId, out var queryState) && queryState is not null)
		{
			return queryState.CurrentText;
		}

		var query = Workspace.Queries.AllQueries.FirstOrDefault(q => q.Id == QueryId);
		return query?.QueryText ?? "// Write your LINQ query here\ncontext.";
	}

	/// <inheritdoc />
	/// <remarks>
	/// On the first render, the Monaco editor is intentionally hidden (via <c>_delay</c>) to
	/// work around BlazorMonaco's requirement that its container already have non-zero dimensions
	/// before the JS editor is constructed. After a 500 ms delay the flag is cleared and a
	/// second render shows the editor inside its now-laid-out container.
	/// On subsequent renders the JS splitter is initialized (with automatic retry if the DOM
	/// is not yet fully ready).
	/// </remarks>
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			_delay = false;
			// 500 ms delay before showing Monaco — workaround for BlazorMonaco needing a
			// non-zero container size at construction time.
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

	/// <summary>
	/// Handles the Monaco <c>OnDidChangeModelContent</c> event.
	/// Reads the new editor text and, if it differs from the last known value,
	/// schedules a debounced workspace update.
	/// </summary>
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

	/// <summary>
	/// Cancels any pending debounce timer and starts a new one.
	/// After <see cref="DebounceDelayMs"/> milliseconds without further changes,
	/// writes the new text to the workspace so unsaved-changes tracking stays current.
	/// </summary>
	/// <param name="newText">The updated query text to persist after the debounce interval.</param>
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

	/// <summary>
	/// Handles the Monaco <c>OnDidInit</c> event. Captures the initial editor text,
	/// creates a fallback compiler if needed, and registers the IntelliSense completion
	/// and hover providers.
	/// </summary>
	/// <remarks>
	/// The fallback <see cref="_localCompiler"/> is created here because Monaco's 500 ms init
	/// delay (see <see cref="OnAfterRenderAsync"/>) can fire before the parent has finished
	/// initializing the real <see cref="Compiler"/> parameter. The fallback ensures completions
	/// work immediately. When the real <see cref="Compiler"/> arrives via a parameter update,
	/// <see cref="OnParametersSet"/> disposes the fallback. Until then, provider callbacks use
	/// <c>Compiler ?? _localCompiler</c> so whichever is available is used.
	/// </remarks>
	private async Task OnEditorInitialized()
	{
		if (_editor == null)
		{
			return;
		}

		_lastQueryText = await _editor.GetValue();

		// If the parent-supplied Compiler isn't available yet, create a local fallback so
		// IntelliSense works immediately. OnParametersSet disposes it when Compiler arrives.
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

	/// <summary>
	/// Derives the text to insert into the editor from a Roslyn <see cref="Microsoft.CodeAnalysis.Completion.CompletionItem"/>.
	/// Appends an opening parenthesis for items flagged as needing parenthesis completion (methods/constructors).
	/// </summary>
	/// <param name="item">The Roslyn completion item to extract insertion text from.</param>
	/// <returns>The text to insert, optionally with a trailing <c>(</c>.</returns>
	private string GetInsertText(Microsoft.CodeAnalysis.Completion.CompletionItem item)
	{
		var text = item.Properties.TryGetValue("InsertionText", out var v) ? v : item.DisplayText;

		if (item.Properties.TryGetValue("ShouldProvideParenthesisCompletion", out var s) && s.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			text += "(";
		}

		return text;
	}

	/// <summary>
	/// Maps Roslyn <see cref="WellKnownTags"/> on a completion item to the corresponding
	/// Monaco <see cref="CompletionItemKind"/> for icon rendering in the suggestion list.
	/// </summary>
	/// <param name="tags">The Roslyn glyph tags from the completion item.</param>
	/// <returns>The best-matching <see cref="CompletionItemKind"/>; defaults to <see cref="CompletionItemKind.Text"/>.</returns>
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

	/// <summary>
	/// Shows a confirmation dialog warning the user about unsaved changes on the current query.
	/// </summary>
	/// <param name="message">The message to display in the dialog body.</param>
	/// <returns>
	/// <see langword="true"/> if the user confirmed they want to continue without saving;
	/// otherwise <see langword="false"/>.
	/// </returns>
	private Task<bool> ShowUnsavedChangesDialog(string message)
		=> DialogService.ShowUnsavedChangesDialogAsync(message);

	/// <summary>
	/// Closes the current query tab. Prompts for confirmation first if the query has unsaved changes.
	/// Does nothing if no project is open.
	/// </summary>
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

	/// <summary>
	/// Saves the current query to persistent storage.
	/// Requires the parent project to have been saved first (project ID must be set).
	/// Does nothing if no project is open.
	/// </summary>
	private async Task SaveCurrentQuery()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		if (Workspace.CurrentProjectId == null)
		{
			Snackbar.Add("Save the project first before saving queries.", Severity.Warning);
			return;
		}

		try
		{
			await Workspace.Queries.SaveQueryAsync(QueryId);
			Logger.LogInformation("Query {QueryId} saved successfully.", QueryId);
			Snackbar.Add("Query saved successfully.", Severity.Success);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to save query.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save query");
		}
	}

	/// <summary>
	/// Reads the current editor text and executes it as a LINQ query against the open project's database.
	/// Manages a cancellation token with the user-configured timeout, shows execution state,
	/// and populates <see cref="_result"/> for the result grid.
	/// </summary>
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

		// A timeout of 0 means "no timeout" — use an unconstrained CancellationTokenSource.
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

	/// <summary>
	/// Cancels an in-progress query execution by signalling the execution <see cref="CancellationTokenSource"/>.
	/// </summary>
	private void StopQuery()
	{
		_executionCts?.Cancel();
	}

	/// <summary>
	/// Asynchronous disposal entry point called by Blazor when the component is removed from
	/// the render tree. Sets <see cref="_disposed"/> first so any awaited continuations still
	/// in-flight can detect disposal and bail out, then tears down the JS splitter, and finally
	/// delegates to <see cref="Dispose"/> for synchronous resource cleanup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>Dual-dispose pattern:</strong> This component implements both <see cref="IDisposable"/>
	/// and <see cref="IAsyncDisposable"/>. Blazor always prefers <see cref="DisposeAsync"/> when both
	/// are present. <see cref="DisposeAsync"/> calls <see cref="Dispose"/> internally, so resources
	/// are never freed twice — <c>_disposed = true</c> is an idempotent guard.
	/// </para>
	/// <para>
	/// <c>_disposed = true</c> must be the very first statement so that the <c>await</c> below
	/// (for the JS splitter teardown) does not race with concurrent renders or tab activations
	/// that check <c>_disposed</c> before touching the editor or JS interop.
	/// </para>
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		// Must be first — guards all in-flight awaited continuations (OnTabActivatedAsync, OnAfterRenderAsync).
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

	/// <summary>
	/// Synchronous disposal: releases Monaco provider registrations, cancels any pending
	/// debounce or execution timers, and disposes the locally-owned fallback compiler.
	/// </summary>
	/// <remarks>
	/// Also sets <see cref="_disposed"/> to <see langword="true"/> so the guard is effective
	/// even if <see cref="Dispose"/> is called directly (not through <see cref="DisposeAsync"/>).
	/// </remarks>
	public void Dispose()
	{
		// Ensure the disposed flag is set even when called directly (not via DisposeAsync).
		_disposed = true;

		_providerDisposable?.Dispose();
		_hoverProviderDisposable?.Dispose();

		_debounceTokenSource?.Cancel();
		_debounceTokenSource?.Dispose();

		_executionCts?.Cancel();
		_executionCts?.Dispose();

		_localCompiler?.Dispose();
	}
}

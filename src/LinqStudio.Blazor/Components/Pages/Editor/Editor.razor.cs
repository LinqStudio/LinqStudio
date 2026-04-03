using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable
{
	[Inject] private ILogger<Editor> Logger { get; set; } = null!;
	[Inject] private ISnackbar Snackbar { get; set; } = null!;
	[Inject] private ICompilerServiceFactory CompilerServiceFactory { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;

	[Parameter] public Guid? QueryIdParam { get; set; }

	private CompilerService? _compiler;
	private bool _isRefreshingSchema;
	private readonly Dictionary<Guid, QueryEditorPanel?> _tabPanelRefs = new();

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
	}

	protected override async Task OnInitializedAsync()
	{
		try
		{
			var firstConnection = Workspace.CurrentProject?.Connections.FirstOrDefault();
			_compiler = firstConnection != null
				? await CompilerServiceFactory.CreateFromConnectionAsync(firstConnection)
				: await CompilerServiceFactory.CreateAsync();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "[Editor] Failed to initialize CompilerService from project schema, falling back to demo model.");
			_compiler = await CompilerServiceFactory.CreateAsync();
		}
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

		// Sync tab panel ref dictionary with current open queries
		var openIds = new HashSet<Guid>(Workspace.Queries.OpenQueries.Keys);
		foreach (var key in _tabPanelRefs.Keys.ToList())
		{
			if (!openIds.Contains(key))
				_tabPanelRefs.Remove(key);
		}

		foreach (var q in GetOpenQueriesInOrder())
		{
			_tabPanelRefs.TryAdd(q.Id, null);
		}
	}

	private int GetActivePanelIndex()
	{
		var queries = GetOpenQueriesInOrder().ToList();
		var currentId = Workspace.Queries.CurrentQueryId;
		if (currentId is null) return 0;
		var idx = queries.FindIndex(q => q.Id == currentId.Value);
		return idx >= 0 ? idx : 0;
	}

	private async Task OnActivePanelIndexChanged(int newIndex)
	{
		var queries = GetOpenQueriesInOrder().ToList();
		if (newIndex >= 0 && newIndex < queries.Count)
		{
			var query = queries[newIndex];
			Workspace.Queries.OpenQuery(query.Id);
			NavigationManager.NavigateTo($"/editor/{query.Id}", replace: true);

			if (_tabPanelRefs.TryGetValue(query.Id, out var panel) && panel != null)
				await panel.OnTabActivatedAsync();
		}
	}

	private async Task OnQueryClosedAsync(Guid closedQueryId)
	{
		// Workspace.Queries.CloseQuery was already called by the panel
		// Navigate to the next open query, or to editor home if no queries are open
		if (Workspace.Queries.CurrentQueryId is Guid newId)
		{
			NavigationManager.NavigateTo($"/editor/{newId}", replace: true);
		}
		else
		{
			NavigationManager.NavigateTo("/editor", replace: true);
		}
	}

	private void CreateNewQuery()
	{
		var queryId = Workspace.Queries.CreateNewQuery();
		NavigationManager.NavigateTo($"/editor/{queryId}", replace: true);
	}

	private string GetTabName(SavedQuery q)
	{
		var isOpen = Workspace.Queries.OpenQueries.TryGetValue(q.Id, out var state);
		return (isOpen && state?.HasUnsavedChanges == true) ? $"{q.Name} *" : q.Name;
	}

	private IEnumerable<SavedQuery> GetOpenQueriesInOrder()
	{
		var openIds = new HashSet<Guid>(Workspace.Queries.OpenQueries.Keys);
		return Workspace.Queries.AllQueries.Where(q => openIds.Contains(q.Id));
	}

	private async Task RefreshSchemaAsync()
	{
		var firstConnection = Workspace.CurrentProject?.Connections.FirstOrDefault();
		if (!Workspace.IsProjectOpen || firstConnection?.QueryGenerator is null)
		{
			Snackbar.Add("No database connection configured for this project.", Severity.Warning);
			return;
		}

		_isRefreshingSchema = true;
		StateHasChanged();

		try
		{
			var oldCompiler = _compiler;
			_compiler = await CompilerServiceFactory.CreateFromConnectionAsync(firstConnection!);
			oldCompiler?.Dispose();
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

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
		_compiler?.Dispose();
	}
}

using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Layout;

/// <summary>
/// Unified connection and schema tree view that replaces the legacy NavMenu + DatabaseTreeView.
/// Displays all <see cref="ServerConnection"/> entries in the current project with their
/// database, tables, and columns. Provides right-click context menus for connection management
/// and query creation.
/// </summary>
public partial class ConnectionTreeView : ComponentBase, IDisposable
{
	[Inject] private ILogger<ConnectionTreeView> Logger { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;
	[Inject] private IDialogService DialogService { get; set; } = null!;
	[Inject] private ISnackbar Snackbar { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;

	/// <summary>
	/// Per-connection loading flag: <c>true</c> while tables are being fetched from the DB.
	/// </summary>
	private readonly Dictionary<Guid, bool> _loadingStates = new();

	/// <summary>Per-connection inline error message (null = no error).</summary>
	private readonly Dictionary<Guid, string?> _loadErrors = new();

	/// <summary>
	/// Per-connection table+column cache. Populated eagerly on project open.
	/// Key = ServerConnection.Id; Value = list of fully-loaded <see cref="DatabaseTableDetail"/>.
	/// </summary>
	private readonly Dictionary<Guid, List<DatabaseTableDetail>> _tableDetailsCache = new();

	/// <summary>
	/// Tracks which connection IDs have been loaded to avoid re-querying on unrelated workspace changes.
	/// </summary>
	private readonly HashSet<Guid> _loadedConnections = new();

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
	}

	protected override async Task OnParametersSetAsync()
	{
		if (Workspace.IsProjectOpen)
		{
			await LoadNewConnectionsAsync();
		}
	}

	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		InvokeAsync(async () =>
		{
			// Remove state for connections that no longer exist
			var currentIds = Workspace.CurrentProject?.Connections.Select(c => c.Id).ToHashSet() ?? [];
			foreach (var id in _loadedConnections.Except(currentIds).ToList())
			{
				_loadingStates.Remove(id);
				_loadErrors.Remove(id);
				_tableDetailsCache.Remove(id);
				_loadedConnections.Remove(id);
			}

			StateHasChanged();

			if (Workspace.IsProjectOpen)
			{
				await LoadNewConnectionsAsync();
			}
		});
	}

	/// <summary>
	/// Loads tables+columns for any connections that are not yet in the cache.
	/// This is called on project open and when connections are added.
	/// </summary>
	private async Task LoadNewConnectionsAsync()
	{
		var connections = Workspace.CurrentProject?.Connections ?? [];
		foreach (var conn in connections)
		{
			if (!_loadedConnections.Contains(conn.Id) && conn.QueryGenerator != null)
			{
				await LoadTablesForConnectionAsync(conn);
			}
		}
	}

	/// <summary>
	/// Eagerly loads all tables and their column details for the given connection.
	/// After loading, regenerates the compiler schema so IntelliSense stays fresh.
	/// </summary>
	private async Task LoadTablesForConnectionAsync(ServerConnection connection)
	{
		if (connection.QueryGenerator == null)
			return;

		_loadingStates[connection.Id] = true;
		_loadErrors[connection.Id] = null;
		StateHasChanged();

		try
		{
			var tables = await connection.QueryGenerator.GetTablesAsync();
			Logger.LogInformation("Loaded {TableCount} tables for connection {ConnectionId}.", tables.Count, connection.Id);

			var tableDetails = new List<DatabaseTableDetail>(tables.Count);
			foreach (var table in tables)
			{
				var detail = await connection.QueryGenerator.GetTableAsync(table);
				tableDetails.Add(detail);
			}

			_tableDetailsCache[connection.Id] = tableDetails;
			_loadedConnections.Add(connection.Id); // Only mark as loaded after successful completion
			Logger.LogInformation("Loaded columns for {TableCount} tables from connection {ConnectionId}.", tableDetails.Count, connection.Id);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load tables for connection {ConnectionId}.", connection.Id);
			_loadErrors[connection.Id] = $"Failed to load: {ex.Message}";
			_tableDetailsCache.Remove(connection.Id);
			// Don't add to _loadedConnections so retry is possible on next WorkspaceChanged
		}
		finally
		{
			_loadingStates[connection.Id] = false;
			StateHasChanged();
		}
	}

	/// <summary>Opens the Connection Properties dialog to add a new connection.</summary>
	private async Task AddNewConnection()
	{
		if (!Workspace.IsProjectOpen)
			return;

		var newConnection = new ServerConnection { Id = Guid.NewGuid() };
		var updated = await OpenConnectionDialogAsync(newConnection, "New Connection");
		if (updated is null)
			return;

		Workspace.CurrentProject!.Connections.Add(updated);
		Workspace.Update(Workspace.CurrentProject);
		Snackbar.Add("Connection added. Save the project to persist.", Severity.Info);

		// Load tables for the new connection
		await LoadTablesForConnectionAsync(updated);
	}

	/// <summary>Opens the Connection Properties dialog to edit an existing connection.</summary>
	private async Task EditConnection(ServerConnection connection)
	{
		var updated = await OpenConnectionDialogAsync(connection, "Connection Properties");
		if (updated is null)
			return;

		// Replace in-place
		var idx = Workspace.CurrentProject!.Connections.FindIndex(c => c.Id == updated.Id);
		if (idx >= 0)
			Workspace.CurrentProject.Connections[idx] = updated;

		Workspace.Update(Workspace.CurrentProject);

		// Invalidate cache so we reload with the new connection string
		_loadedConnections.Remove(connection.Id);
		_tableDetailsCache.Remove(connection.Id);
		_loadErrors.Remove(connection.Id);
		_loadingStates.Remove(connection.Id);

		Snackbar.Add("Connection updated. Save the project to persist.", Severity.Info);

		await LoadTablesForConnectionAsync(updated);
	}

	/// <summary>Confirms and removes a server connection from the project.</summary>
	private async Task DisconnectAsync(ServerConnection connection)
	{
		if (!Workspace.IsProjectOpen)
			return;

		var confirm = await DialogService.ShowUnsavedChangesDialogAsync(
			$"Remove connection to '{connection.GetServerDisplayName()}'? This cannot be undone.");
		if (!confirm)
			return;

		Workspace.CurrentProject!.Connections.RemoveAll(c => c.Id == connection.Id);
		_loadingStates.Remove(connection.Id);
		_loadErrors.Remove(connection.Id);
		_tableDetailsCache.Remove(connection.Id);
		_loadedConnections.Remove(connection.Id);

		Workspace.Update(Workspace.CurrentProject);
		Snackbar.Add("Connection removed. Save the project to persist.", Severity.Info);
	}

	/// <summary>Creates a new query tied to the given connection and navigates to the editor.</summary>
	private void CreateNewQuery(ServerConnection connection)
	{
		if (!Workspace.IsProjectOpen)
			return;

		var queryId = Workspace.Queries.CreateNewQuery(connectionId: connection.Id);
		Logger.LogInformation("New query {QueryId} created for connection {ConnectionId}.", queryId, connection.Id);
		NavigationManager.NavigateTo($"/editor/{queryId}");
	}

	private async Task<ServerConnection?> OpenConnectionDialogAsync(ServerConnection connection, string title)
	{
		var parameters = new DialogParameters<EditProjectDialog>
		{
			{ x => x.Connection, connection }
		};
		var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };

		var dialog = await DialogService.ShowAsync<EditProjectDialog>(title, parameters, options);
		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not ServerConnection updated)
			return null;

		return updated;
	}

	private static string GetColumnIcon(TableColumn column)
	{
		if (column.IsPrimaryKey)
			return Icons.Material.Filled.Key;
		if (column.IsIdentity)
			return Icons.Material.Filled.Bolt;
		return Icons.Material.Outlined.ViewColumn;
	}

	private static Color GetColumnIconColor(TableColumn column)
	{
		if (column.IsPrimaryKey)
			return Color.Warning;
		return Color.Default;
	}

	private static readonly HashSet<string> _fixedSizeTypes = new(StringComparer.OrdinalIgnoreCase)
		{ "int", "bigint", "smallint", "tinyint", "bit" };

	private static string FormatColumnType(TableColumn column)
	{
		var typeStr = column.DataType;

		if (!_fixedSizeTypes.Contains(typeStr))
		{
			if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
				typeStr = $"{typeStr}({column.MaxLength.Value})";
			else if (column.Precision.HasValue && column.Scale.HasValue)
				typeStr = $"{typeStr}({column.Precision.Value},{column.Scale.Value})";
			else if (column.Precision.HasValue)
				typeStr = $"{typeStr}({column.Precision.Value})";
		}

		if (column.IsNullable)
			typeStr += "?";

		return typeStr;
	}

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
	}
}

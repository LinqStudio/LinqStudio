using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Models;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Layout;

public partial class DatabaseTreeView : ComponentBase, IDisposable
{
	[Inject] private ILogger<DatabaseTreeView> Logger { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;

	// ── Tree state ──────────────────────────────────────────────────────────

	/// <summary>Root collection for the MudTreeView — contains the single Connection node.</summary>
	private List<SchemaTreeNode> _rootNodes = [];

	/// <summary>Back-reference to the Tables folder node for targeted refresh.</summary>
	private SchemaTreeNode? _tablesFolderNode;

	// ── Column-loading state (retained from original) ────────────────────────
	private Dictionary<string, DatabaseTableDetail> _tableDetailsCache = new();
	private HashSet<string> _loadingTables = [];
	private bool _isLoading;

	/// <summary>Track connection identity to avoid re-querying DB on unrelated workspace changes.</summary>
	private string? _trackedConnectionString;
	private DatabaseType? _trackedDatabaseType;

	/// <summary>
	/// Exposes the Tables folder node for test assertions.
	/// </summary>
	internal SchemaTreeNode? TablesFolderNode => _tablesFolderNode;

	/// <summary>Placeholder node used as Value for the loading-spinner tree item.</summary>
	private static readonly SchemaTreeNode _spinnerNode = new()
	{
		NodeType = SchemaTreeNodeType.Column,
		Label = "Loading...",
		Icon = Icons.Material.Filled.HourglassEmpty,
	};

	// ── Lifecycle ────────────────────────────────────────────────────────────

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
		_trackedConnectionString = Workspace.CurrentProject?.ConnectionString;
		_trackedDatabaseType = Workspace.CurrentProject?.DatabaseType;

		if (Workspace.IsProjectOpen && Workspace.CurrentProject != null)
		{
			BuildTree(Workspace.CurrentProject);
		}
	}

	protected override async Task OnParametersSetAsync()
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject?.QueryGenerator == null)
			return;

		// Build the tree skeleton if it hasn't been built yet.
		if (_rootNodes.Count == 0 && Workspace.CurrentProject != null)
			BuildTree(Workspace.CurrentProject);

		// Load tables if the tables folder exists but has no children yet (initial load).
		if (_tablesFolderNode != null && _tablesFolderNode.Children.Count == 0 && !_isLoading)
			await LoadTablesAsync();
	}

	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		var newConnectionString = Workspace.CurrentProject?.ConnectionString;
		var newDatabaseType = Workspace.CurrentProject?.DatabaseType;
		var wasOpen = _trackedConnectionString != null || _trackedDatabaseType != null;
		var isNowOpen = Workspace.IsProjectOpen;

		// Only rebuild the tree when the DB connection changes or project is opened/closed.
		// Query saves and other workspace events must not trigger a DB round-trip.
		var connectionChanged = newConnectionString != _trackedConnectionString
			|| newDatabaseType != _trackedDatabaseType;
		var openStateChanged = wasOpen != isNowOpen;

		if (!connectionChanged && !openStateChanged)
			return;

		_trackedConnectionString = newConnectionString;
		_trackedDatabaseType = newDatabaseType;

		_rootNodes.Clear();
		_tablesFolderNode = null;
		_tableDetailsCache.Clear();
		_loadingTables.Clear();

		InvokeAsync(async () =>
		{
			if (isNowOpen && Workspace.CurrentProject != null)
				BuildTree(Workspace.CurrentProject);

			StateHasChanged();

			if (isNowOpen && Workspace.CurrentProject?.QueryGenerator != null)
				await LoadTablesAsync();
		});
	}

	// ── Tree construction ─────────────────────────────────────────────────────

	/// <summary>
	/// Builds the Connection → TablesFolder skeleton from the open project.
	/// Tables are populated separately by <see cref="LoadTablesAsync"/>.
	/// </summary>
	private void BuildTree(Project project)
	{
		var connectionInfo = ConnectionInfo.FromProject(project);

		_tablesFolderNode = new SchemaTreeNode
		{
			NodeType = SchemaTreeNodeType.TablesFolder,
			Label = "Tables",
			Icon = Icons.Material.Filled.TableChart,
			ConnectionInfo = connectionInfo,
		};

		var connectionNode = new SchemaTreeNode
		{
			NodeType = SchemaTreeNodeType.Connection,
			Label = connectionInfo.DisplayName,
			Icon = Icons.Material.Filled.Storage,
			ConnectionInfo = connectionInfo,
			Children = [_tablesFolderNode],
		};

		_rootNodes = [connectionNode];
	}

	// ── Data loading ──────────────────────────────────────────────────────────

	private async Task LoadTablesAsync()
	{
		if (Workspace.CurrentProject?.QueryGenerator == null)
			return;

		_isLoading = true;
		StateHasChanged();

		try
		{
			var tables = await Workspace.CurrentProject.QueryGenerator.GetTablesAsync();

			if (_tablesFolderNode != null)
			{
				_tablesFolderNode.Children.Clear();
				foreach (var table in tables)
				{
					_tablesFolderNode.Children.Add(new SchemaTreeNode
					{
						NodeType = SchemaTreeNodeType.Table,
						Label = table.FullName,
						Icon = Icons.Material.Filled.TableRows,
						TableName = table,
					});
				}
			}

			Logger.LogInformation("Loaded {TableCount} tables from database.", tables.Count);
			_tableDetailsCache.Clear();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load database tables.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to load database tables.");
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private async Task OnTableExpandedChanged(SchemaTreeNode tableNode, bool expanded)
	{
		if (!expanded || tableNode.TableName == null)
			return;

		if (_tableDetailsCache.ContainsKey(tableNode.TableName.FullName))
		{
			// Columns already cached — populate node children from cache
			PopulateColumnsFromCache(tableNode);
			return;
		}

		await LoadTableDetailsAsync(tableNode);
	}

	private async Task LoadTableDetailsAsync(SchemaTreeNode tableNode)
	{
		if (Workspace.CurrentProject?.QueryGenerator == null || tableNode.TableName == null)
			return;

		tableNode.IsLoading = true;
		_loadingTables.Add(tableNode.TableName.FullName);
		StateHasChanged();

		try
		{
			var tableDetail = await Workspace.CurrentProject.QueryGenerator.GetTableAsync(tableNode.TableName);
			_tableDetailsCache[tableNode.TableName.FullName] = tableDetail;

			tableNode.Children.Clear();
			foreach (var column in tableDetail.Columns)
			{
				tableNode.Children.Add(new SchemaTreeNode
				{
					NodeType = SchemaTreeNodeType.Column,
					Label = column.Name,
					Icon = GetColumnIcon(column),
					IconColor = GetColumnIconColor(column),
					ColumnDetail = column,
					ColumnTypeDisplay = FormatColumnType(column),
				});
			}

			Logger.LogInformation(
				"Loaded {ColumnCount} columns for table '{TableName}'.",
				tableDetail.Columns.Count, tableNode.TableName.FullName);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load columns for table '{TableName}'.", tableNode.TableName?.FullName);
			await ErrorHandlingService.HandleErrorAsync(
				ex, $"Failed to load columns for table '{tableNode.TableName?.FullName}'.");
		}
		finally
		{
			if (tableNode.TableName != null)
				_loadingTables.Remove(tableNode.TableName.FullName);
			tableNode.IsLoading = false;
			StateHasChanged();
		}
	}

	private void PopulateColumnsFromCache(SchemaTreeNode tableNode)
	{
		if (tableNode.TableName == null
			|| !_tableDetailsCache.TryGetValue(tableNode.TableName.FullName, out var detail))
			return;

		tableNode.Children.Clear();
		foreach (var column in detail.Columns)
		{
			tableNode.Children.Add(new SchemaTreeNode
			{
				NodeType = SchemaTreeNodeType.Column,
				Label = column.Name,
				Icon = GetColumnIcon(column),
				IconColor = GetColumnIconColor(column),
				ColumnDetail = column,
				ColumnTypeDisplay = FormatColumnType(column),
			});
		}

		StateHasChanged();
	}

	// ── Context menu state ────────────────────────────────────────────────────

	/// <summary>
	/// Node whose context menu is currently open (<see langword="null"/> = no menu open).
	/// </summary>
	private SchemaTreeNode? _contextMenuNode;

	/// <summary>Cursor X position at the time of right-click (viewport-relative, px).</summary>
	private double _contextMenuX;

	/// <summary>Cursor Y position at the time of right-click (viewport-relative, px).</summary>
	private double _contextMenuY;

	/// <summary>Inline style for the floating context menu div.</summary>
	private string ContextMenuStyle =>
		$"position:fixed; left:{_contextMenuX}px; top:{_contextMenuY}px; z-index:9999;";

	private void OpenContextMenu(SchemaTreeNode node, MouseEventArgs e)
	{
		_contextMenuNode = node;
		_contextMenuX = e.ClientX;
		_contextMenuY = e.ClientY;
	}

	private void CloseContextMenu() => _contextMenuNode = null;

	private async Task HandleTablesFolderRefreshAsync()
	{
		CloseContextMenu();
		await RefreshTablesFolderAsync();
	}

	private async Task HandleTableRefreshAsync(SchemaTreeNode tableNode)
	{
		CloseContextMenu();
		await RefreshTableNodeAsync(tableNode);
	}

	private void HandleConnectionNewQuery()
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject == null)
			return;

		CloseContextMenu();
		var queryId = Workspace.Queries.CreateNewQuery();
		Logger.LogInformation("New query {QueryId} created from DB context menu.", queryId);
		NavigationManager.NavigateTo($"/editor/{queryId}");
	}

	// ── Context menu actions ──────────────────────────────────────────────────

	/// <summary>
	/// Clears the tables folder children + cache and re-fetches all tables from the database.
	/// Called from the "Refresh" context menu on the Tables folder node.
	/// </summary>
	internal async Task RefreshTablesFolderAsync()
	{
		if (_tablesFolderNode == null)
			return;

		_tablesFolderNode.Children.Clear();
		_tableDetailsCache.Clear();
		_loadingTables.Clear();

		await LoadTablesAsync();
	}

	/// <summary>
	/// Clears the table node's cached columns and re-fetches them from the database.
	/// Called from the "Refresh" context menu on a Table node.
	/// </summary>
	internal async Task RefreshTableNodeAsync(SchemaTreeNode tableNode)
	{
		if (tableNode.TableName == null)
			return;

		tableNode.Children.Clear();
		_tableDetailsCache.Remove(tableNode.TableName.FullName);
		_loadingTables.Remove(tableNode.TableName.FullName);

		await LoadTableDetailsAsync(tableNode);
	}

	// ── Display helpers (retained from original) ──────────────────────────────

	private string GetColumnIcon(TableColumn column)
	{
		if (column.IsPrimaryKey)
			return Icons.Material.Filled.Key;

		if (column.IsIdentity)
			return Icons.Material.Filled.Bolt;

		return Icons.Material.Outlined.ViewColumn;
	}

	private Color GetColumnIconColor(TableColumn column)
		=> column.IsPrimaryKey ? Color.Warning : Color.Default;

	private static readonly HashSet<string> _fixedSizeTypes = new(StringComparer.OrdinalIgnoreCase)
		{ "int", "bigint", "smallint", "tinyint", "bit" };

	private string FormatColumnType(TableColumn column)
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

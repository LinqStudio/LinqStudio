using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Models;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.CodeGeneration;
using LinqStudio.Core.Resources;
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
	[Inject] private IDialogService DialogService { get; set; } = null!;

	// ── Tree state ──────────────────────────────────────────────────────────

	/// <summary>Root collection for the MudTreeView — contains the single Connection node.</summary>
	private List<SchemaTreeNode> _rootNodes = [];

	/// <summary>Tables folders keyed by database name for targeted refresh.</summary>
	private Dictionary<string, SchemaTreeNode> _tablesFolderNodes = new(StringComparer.OrdinalIgnoreCase);

	// ── Column-loading state (retained from original) ────────────────────────
	private Dictionary<string, DatabaseTableDetail> _tableDetailsCache = new();
	private HashSet<string> _loadingTables = [];
	private bool _isLoading;
	private bool _tablesLoaded;
	private string _filterText = string.Empty;
	private string? _loadError;
	private string? _selectedNodeKey;
	private readonly HashSet<string> _expandedNodeKeys = [];

	/// <summary>Track connection identity to avoid re-querying DB on unrelated workspace changes.</summary>
	private string? _trackedConnectionString;
	private DatabaseType? _trackedDatabaseType;

	/// <summary>
	/// Exposes the first Tables folder node for test assertions and compatibility.
	/// </summary>
	internal SchemaTreeNode? TablesFolderNode => _tablesFolderNodes.Values.FirstOrDefault();

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

		// Load databases and tables once the connection skeleton exists.
		if (_rootNodes.Count > 0 && !_tablesLoaded && !_isLoading)
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
		_tablesFolderNodes.Clear();
		_tableDetailsCache.Clear();
		_loadingTables.Clear();
		_tablesLoaded = false;
		_loadError = null;
		_selectedNodeKey = null;
		_expandedNodeKeys.Clear();

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
	/// Builds the connection skeleton from the open project.
	/// Tables are populated separately by <see cref="LoadTablesAsync"/>.
	/// </summary>
	private void BuildTree(Project project)
	{
		var connectionInfo = ConnectionInfo.FromProject(project);

		var connectionNode = new SchemaTreeNode
		{
			NodeType = SchemaTreeNodeType.Connection,
			Label = connectionInfo.DisplayName,
			Icon = Icons.Material.Filled.Storage,
			ConnectionInfo = connectionInfo,
			IsExpanded = _expandedNodeKeys.Contains($"connection:{connectionInfo.DisplayName}"),
		};

		_rootNodes = [connectionNode];
	}

	// ── Data loading ──────────────────────────────────────────────────────────

	private async Task LoadTablesAsync()
	{
		if (Workspace.CurrentProject?.QueryGenerator == null)
			return;

		_isLoading = true;
		_tablesLoaded = true;
		_loadError = null;
		StateHasChanged();

		try
		{
			var databases = await Workspace.CurrentProject.QueryGenerator.GetDatabasesAsync();
			var tablesByDatabase = new Dictionary<string, IReadOnlyList<DatabaseTableName>>(StringComparer.OrdinalIgnoreCase);
			if (databases.Count == 0)
			{
				var tables = await Workspace.CurrentProject.QueryGenerator.GetTablesAsync();
				foreach (var group in tables.GroupBy(table => string.IsNullOrWhiteSpace(table.DatabaseName) ? "Database" : table.DatabaseName, StringComparer.OrdinalIgnoreCase))
					tablesByDatabase[group.Key] = group.ToList();
				databases = tablesByDatabase.Keys
					.Select(name => new DatabaseInfo { Name = name })
					.ToList();
				if (databases.Count == 0)
					databases = [new DatabaseInfo { Name = "Database" }];
			}

			if (_rootNodes.FirstOrDefault() is { } connectionNode)
			{
				connectionNode.Children.Clear();
				_tablesFolderNodes.Clear();
				foreach (var database in databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase))
				{
					var tablesFolderNode = new SchemaTreeNode
					{
						NodeType = SchemaTreeNodeType.TablesFolder,
						Label = SharedResource.DatabaseTreeView_Tables,
						Icon = Icons.Material.Filled.TableChart,
						ConnectionInfo = connectionNode.ConnectionInfo,
						DatabaseInfo = database,
						IsExpanded = _expandedNodeKeys.Contains($"folder:tables:{database.Name}"),
					};
					var databaseNode = new SchemaTreeNode
					{
						NodeType = SchemaTreeNodeType.Database,
						Label = database.Name,
						Icon = Icons.Material.Filled.Storage,
						DatabaseInfo = database,
						Children = [tablesFolderNode],
						IsExpanded = true,
					};
					if (!tablesByDatabase.TryGetValue(database.Name, out var tables))
						tables = await Workspace.CurrentProject.QueryGenerator.GetTablesAsync(database.Name);
					foreach (var table in tables
						.OrderBy(table => table.Schema ?? string.Empty, StringComparer.OrdinalIgnoreCase)
						.ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase))
						tablesFolderNode.Children.Add(new SchemaTreeNode
						{
							NodeType = SchemaTreeNodeType.Table,
							Label = table.FullName,
							Icon = Icons.Material.Filled.TableRows,
							TableName = table,
							IsExpanded = _expandedNodeKeys.Contains($"table:{table.DatabaseName}:{table.FullName}"),
						});
					connectionNode.Children.Add(databaseNode);
					_tablesFolderNodes[database.Name] = tablesFolderNode;
				}
			}

			Logger.LogInformation("Loaded {DatabaseCount} databases from server.", databases.Count);
			_tableDetailsCache.Clear();
			EnsureSelectedNodeExists();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load database tables.");
			_loadError = SharedResource.DatabaseTreeView_LoadError;
			await ErrorHandlingService.HandleErrorAsync(
				ex, SharedResource.DatabaseTreeView_LoadErrorDialog);
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private async Task OnTableExpandedChanged(SchemaTreeNode tableNode, bool expanded)
	{
		tableNode.IsExpanded = expanded;
		if (expanded)
			_expandedNodeKeys.Add(tableNode.Key);
		else
			_expandedNodeKeys.Remove(tableNode.Key);

		if (!expanded || tableNode.TableName == null)
			return;

		if (_tableDetailsCache.ContainsKey(tableNode.Key))
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
		tableNode.LoadError = null;
		_loadingTables.Add(tableNode.Key);
		StateHasChanged();

		try
		{
			var tableDetail = string.IsNullOrWhiteSpace(tableNode.TableName.DatabaseName)
				? await Workspace.CurrentProject.QueryGenerator.GetTableAsync(tableNode.TableName.FullName)
				: await Workspace.CurrentProject.QueryGenerator.GetTableAsync(tableNode.TableName);
			_tableDetailsCache[tableNode.Key] = tableDetail;

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
					ParentKey = tableNode.Key,
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
			tableNode.LoadError = SharedResource.DatabaseTreeView_LoadColumnsError;
			await ErrorHandlingService.HandleErrorAsync(
				ex, SharedResource.DatabaseTreeView_LoadColumnsErrorDialog);
		}
		finally
		{
			if (tableNode.TableName != null)
				_loadingTables.Remove(tableNode.Key);
			tableNode.IsLoading = false;
			StateHasChanged();
		}
	}

	private void PopulateColumnsFromCache(SchemaTreeNode tableNode)
	{
		if (tableNode.TableName == null
			|| !_tableDetailsCache.TryGetValue(tableNode.Key, out var detail))
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
				ParentKey = tableNode.Key,
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
		SelectNode(node);
		_contextMenuNode = node;
		_contextMenuX = e.ClientX;
		_contextMenuY = e.ClientY;
	}

	private void CloseContextMenu() => _contextMenuNode = null;

	private async Task OpenCustomRelationshipsAsync(SchemaTreeNode databaseNode)
	{
		CloseContextMenu();
		if (Workspace.CurrentProject is null || databaseNode.DatabaseInfo is null)
			return;

		var parameters = new DialogParameters<CustomRelationshipsDialog>
		{
			{ x => x.Project, Workspace.CurrentProject },
			{ x => x.DatabaseName, databaseNode.DatabaseInfo.Name }
		};
		var options = new DialogOptions
		{
			CloseOnEscapeKey = true,
			MaxWidth = MaxWidth.Large,
			FullWidth = true,
			FullScreen = true,
		};
		var dialog = await DialogService.ShowAsync<CustomRelationshipsDialog>(
			SharedResource.CustomRelationships_Title, parameters, options);
		var result = await dialog.Result;
		if (result is not null && !result.Canceled && result.Data is Project project)
			Workspace.Update(project);
	}

	private IEnumerable<SchemaTreeNode> GetVisibleTableNodes()
	{
		var tables = _rootNodes
			.SelectMany(connection => connection.Children)
			.SelectMany(database => database.Children)
			.Where(node => node.NodeType == SchemaTreeNodeType.TablesFolder)
			.SelectMany(folder => folder.Children);
		if (string.IsNullOrWhiteSpace(_filterText))
			return tables;

		return tables.Where(table =>
			table.Label.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
	}

	private IEnumerable<SchemaTreeNode> GetVisibleDatabaseNodes()
		=> _rootNodes.SelectMany(connection => connection.Children).Where(database =>
			string.IsNullOrWhiteSpace(_filterText)
			|| database.Label.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
			|| database.Children.SelectMany(folder => folder.Children)
				.Any(table => table.Label.Contains(_filterText, StringComparison.OrdinalIgnoreCase)))
			?? [];

	private IEnumerable<SchemaTreeNode> GetVisibleTableNodes(SchemaTreeNode databaseNode)
		=> databaseNode.Children
			.Where(node => node.NodeType == SchemaTreeNodeType.TablesFolder)
			.SelectMany(folder => folder.Children)
			.Where(table =>
			string.IsNullOrWhiteSpace(_filterText)
			|| table.Label.Contains(_filterText, StringComparison.OrdinalIgnoreCase));

	private static string GetTablesFolderLabel(SchemaTreeNode tablesFolderNode)
		=> $"{tablesFolderNode.Label} ({tablesFolderNode.Children.Count})";

	private string GetColumnTitle(SchemaTreeNode columnNode)
		=> string.IsNullOrWhiteSpace(columnNode.ColumnTypeDisplay)
			? columnNode.Label
			: $"{columnNode.Label} · {columnNode.ColumnTypeDisplay}";

	private string GetNodeClass(SchemaTreeNode node)
		=> node.Key == _selectedNodeKey
			? "database-explorer-node database-explorer-node-selected"
			: "database-explorer-node";

	private void SelectNode(SchemaTreeNode node)
	{
		_selectedNodeKey = node.Key;
		StateHasChanged();
	}

	private void OnNodeExpandedChanged(SchemaTreeNode node, bool expanded)
	{
		node.IsExpanded = expanded;
		if (expanded)
			_expandedNodeKeys.Add(node.Key);
		else
			_expandedNodeKeys.Remove(node.Key);
	}

	private void EnsureSelectedNodeExists()
	{
		if (_selectedNodeKey == null)
			return;

		if (!GetVisibleTableNodes().Any(table => table.Key == _selectedNodeKey))
			_selectedNodeKey = null;
	}

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

	private Task HandleTableSelectTop1000Async(SchemaTreeNode tableNode)
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject == null || tableNode.TableName == null)
			return Task.CompletedTask;

		CloseContextMenu();
		var entitySetName = ToPascalCase(tableNode.TableName.Name);
		var contextParameterName = CodeGenerationNaming.GetDbContextParameterName(
			tableNode.TableName.DatabaseName ?? "Database");
		var queryText = $"// Write your EF Core query here as a one-liner:\r\n{contextParameterName}.{entitySetName}.Take(1000)";
		var queryId = Workspace.Queries.CreateNewQuery(
			$"Select top 1000 - {tableNode.TableName.Name}",
			queryText,
			executeOnOpen: true);

		Logger.LogInformation(
			"Created select-top query {QueryId} for table '{TableName}'.",
			queryId, tableNode.TableName.FullName);
		NavigationManager.NavigateTo($"/editor/{queryId}");
		return Task.CompletedTask;
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

	private static string ToPascalCase(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;

		var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
		return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
	}

	// ── Context menu actions ──────────────────────────────────────────────────

	/// <summary>
	/// Clears the tables folder children + cache and re-fetches all tables from the database.
	/// Called from the "Refresh" context menu on the Tables folder node.
	/// </summary>
	internal async Task RefreshTablesFolderAsync()
	{
		if (_tablesFolderNodes.Count == 0)
			return;

		foreach (var tablesFolderNode in _tablesFolderNodes.Values)
			tablesFolderNode.Children.Clear();
		_tableDetailsCache.Clear();
		_loadingTables.Clear();
		_tablesLoaded = false;

		await LoadTablesAsync();
	}

	/// <summary>
	/// Clears the table node's cached columns and re-fetches them from the database.
	/// Called from the "Refresh" context menu on a Table node.
	/// </summary>
	internal async Task RefreshTableNodeAsync(SchemaTreeNode tableNode)
	{
		if (tableNode.NodeType == SchemaTreeNodeType.Database)
		{
			foreach (var tablesFolderNode in tableNode.Children)
				foreach (var childTable in tablesFolderNode.Children)
					await RefreshTableNodeAsync(childTable);
			return;
		}

		if (tableNode.TableName == null)
			return;

		tableNode.Children.Clear();
		_tableDetailsCache.Remove(tableNode.Key);
		_loadingTables.Remove(tableNode.Key);
		tableNode.LoadError = null;

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
